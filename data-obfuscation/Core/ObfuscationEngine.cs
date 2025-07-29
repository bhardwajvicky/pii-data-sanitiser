using Microsoft.Extensions.Logging;
using DataObfuscation.Configuration;
using DataObfuscation.Data;
using Common.DataTypes;
using DataObfuscation.Services;
using System.Diagnostics;
using System;

namespace DataObfuscation.Core;

public interface IObfuscationEngine
{
    Task<ObfuscationResult> ExecuteAsync(ObfuscationConfiguration config);
}

public class ObfuscationEngine : IObfuscationEngine
{
    private readonly ILogger<ObfuscationEngine> _logger;
    private readonly IDeterministicAustralianProvider _dataProvider;
    private readonly IReferentialIntegrityManager _integrityManager;
    private readonly IDataRepository _dataRepository;
    private readonly IProgressTracker _progressTracker;
    private readonly IFailureLogger _failureLogger;

    public ObfuscationEngine(
        ILogger<ObfuscationEngine> logger,
        IDeterministicAustralianProvider dataProvider,
        IReferentialIntegrityManager integrityManager,
        IDataRepository dataRepository,
        IProgressTracker progressTracker,
        IFailureLogger failureLogger)
    {
        _logger = logger;
        _dataProvider = dataProvider;
        _integrityManager = integrityManager;
        _dataRepository = dataRepository;
        _progressTracker = progressTracker;
        _failureLogger = failureLogger;
    }

    public async Task<ObfuscationResult> ExecuteAsync(ObfuscationConfiguration config)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ObfuscationResult();

        try
        {
            _logger.LogInformation("Starting data obfuscation process");
            
            // Initialize failure logger
            var databaseName = ExtractDatabaseName(config.Global.ConnectionString);
            await _failureLogger.InitializeAsync(databaseName);
            
            await InitializeAsync(config);
            
            var sortedTables = config.Tables
                .OrderBy(t => t.Priority)
                .ThenBy(t => t.TableName)
                .ToList();

            _progressTracker.Initialize(sortedTables.Count);

            var semaphore = new SemaphoreSlim(config.Global.ParallelThreads, config.Global.ParallelThreads);
            var tasks = new List<Task<TableProcessingResult>>();

            foreach (var tableConfig in sortedTables)
            {
                tasks.Add(ProcessTableAsync(tableConfig, config, semaphore));
            }

            var tableResults = await Task.WhenAll(tasks);

            foreach (var tableResult in tableResults)
            {
                result.TablesProcessed++;
                result.RowsProcessed += tableResult.RowsProcessed;
                
                if (!tableResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage += $"Table {tableResult.TableName}: {tableResult.ErrorMessage}\n";
                }
            }

            await FinalizeAsync(config);
            await _failureLogger.FinalizeAsync();

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            if (result.Success)
            {
                _logger.LogInformation("Data obfuscation completed successfully in {Duration}", result.Duration);
                _logger.LogInformation("Tables processed: {TablesProcessed}, Rows processed: {RowsProcessed:N0}", 
                    result.TablesProcessed, result.RowsProcessed);
            }
            else
            {
                _logger.LogError("Data obfuscation completed with errors: {ErrorMessage}", result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;
            
            _logger.LogError(ex, "Data obfuscation failed with exception");
            
            return result;
        }
    }

    private async Task InitializeAsync(ObfuscationConfiguration config)
    {
        _logger.LogInformation("Initializing obfuscation engine");

        await _dataRepository.InitializeAsync(config.Global.ConnectionString);

        if (config.Global.PersistMappings && !string.IsNullOrEmpty(config.Global.MappingCacheDirectory))
        {
            await _dataProvider.LoadMappingsAsync(config.Global.MappingCacheDirectory);
        }

        if (config.ReferentialIntegrity.Enabled)
        {
            await _integrityManager.InitializeAsync(config.ReferentialIntegrity);
        }

        _logger.LogInformation("Engine initialization completed");
    }

    private async Task<TableProcessingResult> ProcessTableAsync(
        TableConfiguration tableConfig, 
        ObfuscationConfiguration globalConfig,
        SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        
        try
        {
            return await ProcessTableInternalAsync(tableConfig, globalConfig);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<TableProcessingResult> ProcessTableInternalAsync(
        TableConfiguration tableConfig,
        ObfuscationConfiguration globalConfig)
    {
        var result = new TableProcessingResult { TableName = tableConfig.TableName };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Processing table: {TableName}", tableConfig.TableName);
            _progressTracker.StartTable(tableConfig.TableName);

            var batchSize = tableConfig.CustomBatchSize ?? globalConfig.Global.BatchSize;
            var totalRows = await _dataRepository.GetRowCountAsync(tableConfig.TableName, tableConfig.Conditions?.WhereClause);
            
            _logger.LogInformation("Table {TableName} has {TotalRows:N0} rows to process with batch size {BatchSize} and SQL batch size {SqlBatchSize}", 
                tableConfig.TableName, totalRows, batchSize, globalConfig.Global.SqlBatchSize);

            var enabledColumns = tableConfig.Columns.Where(c => c.Enabled).ToList();
            if (!enabledColumns.Any())
            {
                _logger.LogWarning("No enabled columns found for table: {TableName}", tableConfig.TableName);
                result.Success = true;
                return result;
            }

            if (globalConfig.Global.DryRun)
            {
                _logger.LogInformation("[DRY RUN] Would process {TotalRows:N0} rows for table {TableName}", totalRows, tableConfig.TableName);
                result.RowsProcessed = (int)totalRows;
                result.Success = true;
                return result;
            }

            // Create batch ranges for parallel processing
            var batchRanges = CreateBatchRanges(totalRows, batchSize);
            _logger.LogInformation("Created {BatchCount} batches for parallel processing of table {TableName}", 
                batchRanges.Count, tableConfig.TableName);

            // Process batches in parallel with controlled concurrency
            var semaphore = new SemaphoreSlim(globalConfig.Global.ParallelThreads, globalConfig.Global.ParallelThreads);
            var batchTasks = new List<Task<BatchProcessingResult>>();

            foreach (var (offset, size) in batchRanges)
            {
                var task = ProcessBatchAsync(
                    tableConfig, 
                    globalConfig, 
                    enabledColumns, 
                    offset, 
                    size, 
                    semaphore);
                batchTasks.Add(task);
            }

            // Wait for all batches to complete
            var batchResults = await Task.WhenAll(batchTasks);

            // Aggregate results
            foreach (var batchResult in batchResults)
            {
                result.RowsProcessed += batchResult.RowsProcessed;
                
                if (!batchResult.Success)
                {
                    result.Success = false;
                    _logger.LogError("Batch processing failed for table {TableName}: {ErrorMessage}", 
                        tableConfig.TableName, batchResult.ErrorMessage);
                }

                // Log failed rows from this batch
                foreach (var failedRow in batchResult.FailedRows)
                {
                    await _failureLogger.LogFailedRowAsync(failedRow);
                    _logger.LogError("Failed to update row: {FailedRowDetails}", failedRow.GetLogMessage());
                }
            }

            result.Success = result.Success && batchResults.All(br => br.Success);

            // Update final progress
            _progressTracker.UpdateProgress(tableConfig.TableName, result.RowsProcessed, totalRows);

            stopwatch.Stop();
            var rowsPerSecond = result.RowsProcessed / Math.Max(stopwatch.Elapsed.TotalSeconds, 1);
            _logger.LogInformation("✓ Completed table {TableName}: {ProcessedRows:N0} rows in {Duration} ({RowsPerSecond:N0} rows/sec)", 
                tableConfig.TableName, result.RowsProcessed, stopwatch.Elapsed, rowsPerSecond);

            _progressTracker.CompleteTable(tableConfig.TableName);

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            
            _logger.LogError(ex, "Failed to process table: {TableName}", tableConfig.TableName);
            _progressTracker.FailTable(tableConfig.TableName, ex.Message);
            
            return result;
        }
    }

    private static List<(int Offset, int Size)> CreateBatchRanges(long totalRows, int batchSize)
    {
        var ranges = new List<(int Offset, int Size)>();
        var offset = 0;
        
        while (offset < totalRows)
        {
            var size = (int)Math.Min(batchSize, totalRows - offset);
            ranges.Add((offset, size));
            offset += size;
        }
        
        return ranges;
    }

    private async Task<BatchProcessingResult> ProcessBatchAsync(
        TableConfiguration tableConfig,
        ObfuscationConfiguration globalConfig,
        List<ColumnConfiguration> enabledColumns,
        int offset,
        int batchSize,
        SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        
        try
        {
            var result = new BatchProcessingResult();
            var batchStartTime = DateTime.UtcNow;
            
            var totalRows = await _dataRepository.GetRowCountAsync(tableConfig.TableName, tableConfig.Conditions?.WhereClause);
            var batchNumber = (offset / batchSize) + 1;
            var totalBatches = (int)Math.Ceiling((double)totalRows / batchSize);
            
            var progressPercentage = (batchNumber * 100) / totalBatches;
            var endRow = Math.Min(offset + batchSize, (int)totalRows);
            _logger.LogInformation("[{ProgressPercentage}%] Processing batch {BatchNumber}/{TotalBatches} (rows {Offset}-{End}) for table {TableName}", 
                progressPercentage, batchNumber, totalBatches, offset + 1, endRow, tableConfig.TableName);

            var batch = await _dataRepository.GetBatchAsync(
                tableConfig.TableName, 
                enabledColumns.Select(c => c.ColumnName).ToList(),
                tableConfig.PrimaryKey,
                offset, 
                batchSize, 
                tableConfig.Conditions?.WhereClause);

            if (!batch.Any())
            {
                result.Success = true;
                return result;
            }

            _logger.LogInformation("Obfuscating {RowCount} rows in batch {BatchNumber}/{TotalBatches}...", 
                batch.Count, batchNumber, totalBatches);
            
            var obfuscatedBatch = await ObfuscateBatchAsync(batch, enabledColumns, globalConfig);
            
            _logger.LogInformation("Writing {RowCount} obfuscated rows to database...", obfuscatedBatch.Count);
            
            var updateResult = await _dataRepository.UpdateBatchAsync(
                tableConfig.TableName, 
                obfuscatedBatch, 
                tableConfig.PrimaryKey, 
                globalConfig.Global.SqlBatchSize);

            result.RowsProcessed = updateResult.SuccessfulRows;
            result.Success = !updateResult.HasCriticalError;
            result.FailedRows = updateResult.FailedRows;
            
            // Log batch completion with timing
            var elapsedMs = (int)(DateTime.UtcNow - batchStartTime).TotalMilliseconds;
            _logger.LogInformation("Completed batch {BatchNumber}/{TotalBatches} for table {TableName}: {SuccessfulRows} rows written, {SkippedRows} skipped in {ElapsedMs}ms", 
                batchNumber, totalBatches, tableConfig.TableName, updateResult.SuccessfulRows, updateResult.SkippedRows, elapsedMs);
            
            if (updateResult.FailedRows.Any())
            {
                // Add original values to failed rows for better logging
                foreach (var failedRow in updateResult.FailedRows)
                {
                    var originalRow = batch.FirstOrDefault(b => 
                        failedRow.PrimaryKeyValues.All(pk => b.ContainsKey(pk.Key) && b[pk.Key]?.ToString() == pk.Value?.ToString()));
                    
                    if (originalRow != null)
                    {
                        foreach (var col in enabledColumns)
                        {
                            if (originalRow.ContainsKey(col.ColumnName))
                            {
                                failedRow.UpdatedValues[$"{col.ColumnName}_original"] = originalRow[col.ColumnName];
                            }
                        }
                    }
                }
                
                _logger.LogWarning("Batch {Offset}-{End} completed with {SuccessfulRows} successful and {SkippedRows} skipped rows", 
                    offset, offset + batchSize, updateResult.SuccessfulRows, updateResult.SkippedRows);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch {Offset}-{End} for table {TableName}", 
                offset, offset + batchSize, tableConfig.TableName);
            
            return new BatchProcessingResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                RowsProcessed = 0
            };
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<List<Dictionary<string, object?>>> ObfuscateBatchAsync(
        List<Dictionary<string, object?>> batch,
        List<ColumnConfiguration> columns,
        ObfuscationConfiguration globalConfig)
    {
        return await Task.Run(() =>
        {
            var obfuscatedBatch = new List<Dictionary<string, object?>>();

            foreach (var row in batch)
            {
                var obfuscatedRow = new Dictionary<string, object?>(row);

                foreach (var columnConfig in columns)
                {
                    if (!row.ContainsKey(columnConfig.ColumnName))
                        continue;

                    var originalValue = row[columnConfig.ColumnName];
                    
                    if (originalValue == null || originalValue == DBNull.Value)
                    {
                        // If the value is NULL, preserve it as NULL regardless of configuration
                        // This ensures nullable columns maintain their NULL values
                        obfuscatedRow[columnConfig.ColumnName] = originalValue;
                        continue;
                    }

                    try
                    {
                        var obfuscatedValue = GenerateObfuscatedValue(originalValue, columnConfig, globalConfig);
                        
                        // CRITICAL: Ensure we never generate NULL for non-NULL source values
                        if (obfuscatedValue == null || obfuscatedValue == DBNull.Value || 
                            (obfuscatedValue is string strValue && string.IsNullOrEmpty(strValue)))
                        {
                            _logger.LogError("CRITICAL: Obfuscation generated NULL/empty value for non-NULL source. Column: {ColumnName}, OriginalValue: {OriginalValue}", 
                                columnConfig.ColumnName, originalValue);
                            
                            // Use original value as fallback to prevent NULL insertion
                            obfuscatedRow[columnConfig.ColumnName] = originalValue;
                        }
                        else
                        {
                            obfuscatedRow[columnConfig.ColumnName] = obfuscatedValue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to obfuscate value for column {ColumnName}, using fallback", columnConfig.ColumnName);
                        
                        obfuscatedRow[columnConfig.ColumnName] = columnConfig.Fallback?.OnError switch
                        {
                            "useDefault" => columnConfig.Fallback.DefaultValue ?? originalValue,
                            "skip" => originalValue,
                            _ => originalValue
                        };
                    }
                }

                obfuscatedBatch.Add(obfuscatedRow);
            }

            return obfuscatedBatch;
        });
    }

    private object? GenerateObfuscatedValue(object? originalValue, ColumnConfiguration columnConfig, ObfuscationConfiguration globalConfig)
    {
        // NULL values are now handled before this method is called
        // This method should only be called with non-null values

        var customDataType = globalConfig.DataTypes.GetValueOrDefault(columnConfig.DataType);
        var customSeed = customDataType?.CustomSeed;
        
        // Use BaseType if this is a custom data type, otherwise use the DataType directly
        var dataTypeToMatch = customDataType?.BaseType ?? columnConfig.DataType;

        // Handle Date types
        if (dataTypeToMatch == SupportedDataTypes.Date || dataTypeToMatch == SupportedDataTypes.DateOfBirth)
        {
            DateTime dateValue;
            if (originalValue is DateTime dt)
            {
                dateValue = dt;
            }
            else if (DateTime.TryParse(originalValue.ToString(), out var parsedDate))
            {
                dateValue = parsedDate;
            }
            else
            {
                throw new InvalidOperationException($"Cannot convert value '{originalValue}' to DateTime for column {columnConfig.ColumnName}");
            }

            return dataTypeToMatch switch
            {
                SupportedDataTypes.Date => _dataProvider.GetDate(dateValue, customSeed),
                SupportedDataTypes.DateOfBirth => _dataProvider.GetDateOfBirth(dateValue, customSeed),
                _ => dateValue
            };
        }

        // For all other types, convert to string
        var originalStringValue = originalValue.ToString() ?? "";
        
        var obfuscatedValue = dataTypeToMatch switch
        {
            // Core Personal Data Types
            SupportedDataTypes.FirstName => _dataProvider.GetFirstName(originalStringValue, customSeed),
            SupportedDataTypes.LastName => _dataProvider.GetLastName(originalStringValue, customSeed),
            SupportedDataTypes.FullName => _dataProvider.GetDriverName(originalStringValue, customSeed),
            
            // Contact Information
            SupportedDataTypes.Email => _dataProvider.GetContactEmail(originalStringValue, customSeed),
            SupportedDataTypes.Phone => _dataProvider.GetDriverPhone(originalStringValue, customSeed),
            
            // Address Components
            SupportedDataTypes.FullAddress => _dataProvider.GetFullAddress(originalStringValue, customSeed),
            SupportedDataTypes.AddressLine1 => _dataProvider.GetAddressLine1(originalStringValue, customSeed),
            SupportedDataTypes.AddressLine2 => _dataProvider.GetAddressLine2(originalStringValue, customSeed),
            SupportedDataTypes.City => _dataProvider.GetCity(originalStringValue, customSeed),
            SupportedDataTypes.Suburb => _dataProvider.GetSuburb(originalStringValue, customSeed),
            SupportedDataTypes.State => _dataProvider.GetState(originalStringValue, customSeed),
            SupportedDataTypes.StateAbbr => _dataProvider.GetStateAbbr(originalStringValue, customSeed),
            SupportedDataTypes.PostCode or SupportedDataTypes.ZipCode => _dataProvider.GetPostCode(originalStringValue, customSeed),
            SupportedDataTypes.Country => _dataProvider.GetCountry(originalStringValue, customSeed),
            SupportedDataTypes.Address => _dataProvider.GetAddress(originalStringValue, customSeed), // Legacy support
            
            // Financial Information
            SupportedDataTypes.CreditCard => _dataProvider.GetCreditCard(originalStringValue, customSeed),
            SupportedDataTypes.NINO or SupportedDataTypes.NationalInsuranceNumber => _dataProvider.GetDriverLicenseNumber(originalStringValue, customSeed), // Placeholder for now
            SupportedDataTypes.SortCode or SupportedDataTypes.BankSortCode => _dataProvider.GetRouteCode(originalStringValue, customSeed), // Placeholder for now
            
            // Identification & Licenses
            SupportedDataTypes.LicenseNumber => _dataProvider.GetDriverLicenseNumber(originalStringValue, customSeed),
            
            // Business Information
            SupportedDataTypes.CompanyName => _dataProvider.GetOperatorName(originalStringValue, customSeed),
            SupportedDataTypes.BusinessABN => _dataProvider.GetBusinessABN(originalStringValue, customSeed),
            SupportedDataTypes.BusinessACN => _dataProvider.GetBusinessACN(originalStringValue, customSeed),
            
            // Vehicle Information
            SupportedDataTypes.VehicleRegistration => _dataProvider.GetVehicleRegistration(originalStringValue, customSeed),
            SupportedDataTypes.VINNumber => _dataProvider.GetVINNumber(originalStringValue, customSeed),
            SupportedDataTypes.VehicleMakeModel => _dataProvider.GetVehicleMakeModel(originalStringValue, customSeed),
            SupportedDataTypes.EngineNumber => _dataProvider.GetEngineNumber(originalStringValue, customSeed),
            
            // Location & Geographic
            SupportedDataTypes.GPSCoordinate => _dataProvider.GetGPSCoordinate(originalStringValue, customSeed),
            SupportedDataTypes.RouteCode => _dataProvider.GetRouteCode(originalStringValue, customSeed),
            SupportedDataTypes.DepotLocation => _dataProvider.GetDepotLocation(originalStringValue, customSeed),
            
            // UK-Specific Types
            SupportedDataTypes.UKPostcode => _dataProvider.GetPostCode(originalStringValue, customSeed), // Placeholder for now
            
            _ => throw new NotSupportedException($"Data type '{dataTypeToMatch}' is not supported. Supported types: {SupportedDataTypes.GetAllSupportedTypesString()}")
        };

        if (columnConfig.PreserveLength && obfuscatedValue.Length != originalStringValue.Length)
        {
            obfuscatedValue = obfuscatedValue.Length > originalStringValue.Length 
                ? obfuscatedValue[..originalStringValue.Length]
                : obfuscatedValue.PadRight(originalStringValue.Length, 'X');
        }

        return obfuscatedValue;
    }

    private async Task FinalizeAsync(ObfuscationConfiguration config)
    {
        _logger.LogInformation("Finalizing obfuscation process");

        if (config.Global.PersistMappings && !string.IsNullOrEmpty(config.Global.MappingCacheDirectory))
        {
            await _dataProvider.SaveMappingsAsync(config.Global.MappingCacheDirectory);
        }

        if (config.PostProcessing.GenerateReport)
        {
            await GenerateReportAsync(config);
        }

        _logger.LogInformation("Finalization completed");
    }

    private async Task GenerateReportAsync(ObfuscationConfiguration config)
    {
        try
        {
            var reportPath = config.PostProcessing.ReportPath
                .Replace("{timestamp}", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

            var reportDir = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(reportDir) && !Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }

            var report = new
            {
                Timestamp = DateTime.UtcNow,
                Configuration = new
                {
                    config.Global.GlobalSeed,
                    config.Global.BatchSize,
                    config.Global.ParallelThreads,
                    config.Global.DryRun
                },
                TablesProcessed = config.Tables.Select(t => new
                {
                    t.TableName,
                    t.Priority,
                    ColumnsObfuscated = t.Columns.Count(c => c.Enabled)
                }),
                MappingStatistics = new
                {
                    TotalMappings = _dataProvider.GetAllMappings().Count
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(reportPath, json);
            _logger.LogInformation("Report generated: {ReportPath}", reportPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate report");
        }
    }

    private string ExtractDatabaseName(string connectionString)
    {
        try
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;
            return string.IsNullOrEmpty(databaseName) ? "UnknownDatabase" : databaseName;
        }
        catch
        {
            return "UnknownDatabase";
        }
    }
}

public class ObfuscationResult
{
    public bool Success { get; set; } = true;
    public int TablesProcessed { get; set; }
    public long RowsProcessed { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TableProcessingResult
{
    public string TableName { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public long RowsProcessed { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BatchProcessingResult
{
    public bool Success { get; set; } = true;
    public int RowsProcessed { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<FailedRow> FailedRows { get; set; } = new();
}