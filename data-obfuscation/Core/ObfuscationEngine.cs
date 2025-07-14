using Microsoft.Extensions.Logging;
using DataObfuscation.Configuration;
using DataObfuscation.Data;
using DataObfuscation.Common.DataTypes;
using System.Diagnostics;

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

    public ObfuscationEngine(
        ILogger<ObfuscationEngine> logger,
        IDeterministicAustralianProvider dataProvider,
        IReferentialIntegrityManager integrityManager,
        IDataRepository dataRepository,
        IProgressTracker progressTracker)
    {
        _logger = logger;
        _dataProvider = dataProvider;
        _integrityManager = integrityManager;
        _dataRepository = dataRepository;
        _progressTracker = progressTracker;
    }

    public async Task<ObfuscationResult> ExecuteAsync(ObfuscationConfiguration config)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ObfuscationResult();

        try
        {
            _logger.LogInformation("Starting data obfuscation process");
            
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
            
            _logger.LogInformation("Table {TableName} has {TotalRows:N0} rows to process", tableConfig.TableName, totalRows);

            var enabledColumns = tableConfig.Columns.Where(c => c.Enabled).ToList();
            if (!enabledColumns.Any())
            {
                _logger.LogWarning("No enabled columns found for table: {TableName}", tableConfig.TableName);
                result.Success = true;
                return result;
            }

            var processedRows = 0;
            var offset = 0;

            while (offset < totalRows)
            {
                if (globalConfig.Global.DryRun)
                {
                    _logger.LogInformation("[DRY RUN] Would process batch {Offset}-{End} for table {TableName}", 
                        offset, Math.Min(offset + batchSize, totalRows), tableConfig.TableName);
                    
                    processedRows += (int)Math.Min(batchSize, totalRows - offset);
                    offset += batchSize;
                    continue;
                }

                var batch = await _dataRepository.GetBatchAsync(
                    tableConfig.TableName, 
                    enabledColumns.Select(c => c.ColumnName).ToList(),
                    tableConfig.PrimaryKey,
                    offset, 
                    batchSize, 
                    tableConfig.Conditions?.WhereClause);

                if (!batch.Any())
                {
                    break;
                }

                var obfuscatedBatch = await ObfuscateBatchAsync(batch, enabledColumns, globalConfig);
                
                await _dataRepository.UpdateBatchAsync(tableConfig.TableName, obfuscatedBatch, tableConfig.PrimaryKey);

                processedRows += batch.Count;
                offset += batchSize;

                _progressTracker.UpdateProgress(tableConfig.TableName, processedRows, totalRows);

                if (processedRows % (batchSize * 10) == 0)
                {
                    _logger.LogInformation("Processed {ProcessedRows:N0}/{TotalRows:N0} rows for table {TableName}", 
                        processedRows, totalRows, tableConfig.TableName);
                }
            }

            result.RowsProcessed = processedRows;
            result.Success = true;

            stopwatch.Stop();
            _logger.LogInformation("Completed table {TableName}: {ProcessedRows:N0} rows in {Duration}", 
                tableConfig.TableName, processedRows, stopwatch.Elapsed);

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

                    var originalValue = row[columnConfig.ColumnName]?.ToString();
                    
                    if (string.IsNullOrEmpty(originalValue))
                    {
                        if (columnConfig.Conditions?.OnlyIfNotNull == true)
                            continue;
                    }

                    try
                    {
                        var obfuscatedValue = GenerateObfuscatedValue(originalValue ?? "", columnConfig, globalConfig);
                        obfuscatedRow[columnConfig.ColumnName] = obfuscatedValue;
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

    private string GenerateObfuscatedValue(string originalValue, ColumnConfiguration columnConfig, ObfuscationConfiguration globalConfig)
    {
        var customDataType = globalConfig.DataTypes.GetValueOrDefault(columnConfig.DataType);
        var customSeed = customDataType?.CustomSeed;
        
        // Use BaseType if this is a custom data type, otherwise use the DataType directly
        var dataTypeToMatch = customDataType?.BaseType ?? columnConfig.DataType;

        var obfuscatedValue = dataTypeToMatch switch
        {
            // Core Personal Data Types
            SupportedDataTypes.FirstName => _dataProvider.GetFirstName(originalValue, customSeed),
            SupportedDataTypes.LastName => _dataProvider.GetLastName(originalValue, customSeed),
            SupportedDataTypes.FullName => _dataProvider.GetDriverName(originalValue, customSeed),
            
            // Contact Information
            SupportedDataTypes.Email => _dataProvider.GetContactEmail(originalValue, customSeed),
            SupportedDataTypes.Phone => _dataProvider.GetDriverPhone(originalValue, customSeed),
            
            // Address Components
            SupportedDataTypes.FullAddress => _dataProvider.GetFullAddress(originalValue, customSeed),
            SupportedDataTypes.AddressLine1 => _dataProvider.GetAddressLine1(originalValue, customSeed),
            SupportedDataTypes.AddressLine2 => _dataProvider.GetAddressLine2(originalValue, customSeed),
            SupportedDataTypes.City => _dataProvider.GetCity(originalValue, customSeed),
            SupportedDataTypes.Suburb => _dataProvider.GetSuburb(originalValue, customSeed),
            SupportedDataTypes.State => _dataProvider.GetState(originalValue, customSeed),
            SupportedDataTypes.StateAbbr => _dataProvider.GetStateAbbr(originalValue, customSeed),
            SupportedDataTypes.PostCode or SupportedDataTypes.ZipCode => _dataProvider.GetPostCode(originalValue, customSeed),
            SupportedDataTypes.Country => _dataProvider.GetCountry(originalValue, customSeed),
            SupportedDataTypes.Address => _dataProvider.GetAddress(originalValue, customSeed), // Legacy support
            
            // Financial Information
            SupportedDataTypes.CreditCard => _dataProvider.GetCreditCard(originalValue, customSeed),
            SupportedDataTypes.NINO or SupportedDataTypes.NationalInsuranceNumber => _dataProvider.GetDriverLicenseNumber(originalValue, customSeed), // Placeholder for now
            SupportedDataTypes.SortCode or SupportedDataTypes.BankSortCode => _dataProvider.GetRouteCode(originalValue, customSeed), // Placeholder for now
            
            // Identification & Licenses
            SupportedDataTypes.LicenseNumber => _dataProvider.GetDriverLicenseNumber(originalValue, customSeed),
            
            // Business Information
            SupportedDataTypes.CompanyName => _dataProvider.GetOperatorName(originalValue, customSeed),
            SupportedDataTypes.BusinessABN => _dataProvider.GetBusinessABN(originalValue, customSeed),
            SupportedDataTypes.BusinessACN => _dataProvider.GetBusinessACN(originalValue, customSeed),
            
            // Vehicle Information
            SupportedDataTypes.VehicleRegistration => _dataProvider.GetVehicleRegistration(originalValue, customSeed),
            SupportedDataTypes.VINNumber => _dataProvider.GetVINNumber(originalValue, customSeed),
            SupportedDataTypes.VehicleMakeModel => _dataProvider.GetVehicleMakeModel(originalValue, customSeed),
            SupportedDataTypes.EngineNumber => _dataProvider.GetEngineNumber(originalValue, customSeed),
            
            // Location & Geographic
            SupportedDataTypes.GPSCoordinate => _dataProvider.GetGPSCoordinate(originalValue, customSeed),
            SupportedDataTypes.RouteCode => _dataProvider.GetRouteCode(originalValue, customSeed),
            SupportedDataTypes.DepotLocation => _dataProvider.GetDepotLocation(originalValue, customSeed),
            
            // UK-Specific Types
            SupportedDataTypes.UKPostcode => _dataProvider.GetPostCode(originalValue, customSeed), // Placeholder for now
            
            _ => throw new NotSupportedException($"Data type '{dataTypeToMatch}' is not supported. Supported types: {SupportedDataTypes.GetAllSupportedTypesString()}")
        };

        // Handle length constraints
        if (columnConfig.PreserveLength && obfuscatedValue.Length != originalValue.Length)
        {
            obfuscatedValue = obfuscatedValue.Length > originalValue.Length 
                ? obfuscatedValue[..originalValue.Length]
                : obfuscatedValue.PadRight(originalValue.Length, 'X');
        }
        else if (columnConfig.MaxLength.HasValue && obfuscatedValue.Length > columnConfig.MaxLength.Value)
        {
            // Enforce MaxLength constraint even when PreserveLength is false
            obfuscatedValue = obfuscatedValue[..columnConfig.MaxLength.Value];
            _logger.LogDebug("Truncated value for column {Column} from {OriginalLength} to {MaxLength}", 
                columnConfig.ColumnName, obfuscatedValue.Length, columnConfig.MaxLength.Value);
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