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
    Task<ObfuscationResult> ExecuteAsync(ObfuscationConfiguration config, string configPath, string mappingPath, bool resumeIfPossible = true);
}

public class ObfuscationEngine : IObfuscationEngine
{
    private readonly ILogger<ObfuscationEngine> _logger;
    private readonly IDeterministicAustralianProvider _dataProvider;
    private readonly IReferentialIntegrityManager _integrityManager;
    private readonly IDataRepository _dataRepository;
    private readonly IProgressTracker _progressTracker;
    private readonly IFailureLogger _failureLogger;
    private readonly ICheckpointService _checkpointService;
    private CheckpointState? _currentCheckpoint;
    private readonly SemaphoreSlim _checkpointLock = new SemaphoreSlim(1, 1);

    public ObfuscationEngine(
        ILogger<ObfuscationEngine> logger,
        IDeterministicAustralianProvider dataProvider,
        IReferentialIntegrityManager integrityManager,
        IDataRepository dataRepository,
        IProgressTracker progressTracker,
        IFailureLogger failureLogger,
        ICheckpointService checkpointService)
    {
        _logger = logger;
        _dataProvider = dataProvider;
        _integrityManager = integrityManager;
        _dataRepository = dataRepository;
        _progressTracker = progressTracker;
        _failureLogger = failureLogger;
        _checkpointService = checkpointService;
    }

    public async Task<ObfuscationResult> ExecuteAsync(ObfuscationConfiguration config)
    {
        // Call the overloaded method without checkpoint support
        return await ExecuteAsync(config, string.Empty, string.Empty, false);
    }

    public async Task<ObfuscationResult> ExecuteAsync(ObfuscationConfiguration config, string configPath, string mappingPath, bool resumeIfPossible = true)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ObfuscationResult();

        try
        {
            _logger.LogInformation("Starting data obfuscation process");
            
            // Initialize failure logger
            var databaseName = ExtractDatabaseName(config.Global.ConnectionString);
            await _failureLogger.InitializeAsync(databaseName);
            
            // Handle checkpoint/resume logic
            string configHash = string.Empty;
            
            if (resumeIfPossible && (!string.IsNullOrEmpty(configPath) || !string.IsNullOrEmpty(mappingPath)))
            {
                // For unified mapping file, use the mapping path as both config and mapping
                var effectiveConfigPath = !string.IsNullOrEmpty(configPath) ? configPath : mappingPath;
                var effectiveMappingPath = !string.IsNullOrEmpty(mappingPath) ? mappingPath : configPath;
                
                configHash = _checkpointService.ComputeConfigHash(effectiveConfigPath, effectiveMappingPath);
                _currentCheckpoint = await _checkpointService.LoadCheckpointAsync(configHash);
                
                if (_currentCheckpoint != null && _currentCheckpoint.Status == "InProgress")
                {
                    _logger.LogInformation("Found incomplete run started at {StartedAt:yyyy-MM-dd HH:mm:ss}, last updated at {LastUpdatedAt:yyyy-MM-dd HH:mm:ss}", 
                        _currentCheckpoint.StartedAt, _currentCheckpoint.LastUpdatedAt);
                    _logger.LogInformation("Processed {ProcessedRows:N0} rows across {ProcessedTables} tables", 
                        _currentCheckpoint.TotalRowsProcessed, 
                        _currentCheckpoint.Tables.Count(t => t.Status == "Completed"));
                    
                    // Ask user if they want to resume
                    Console.WriteLine("\nAn incomplete obfuscation run was detected:");
                    Console.WriteLine($"  Started: {_currentCheckpoint.StartedAt:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"  Last Update: {_currentCheckpoint.LastUpdatedAt:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"  Progress: {_currentCheckpoint.TotalRowsProcessed:N0} rows processed");
                    Console.WriteLine("\nDo you want to resume from where it stopped? (Y/N): ");
                    
                    var response = Console.ReadLine()?.Trim().ToUpperInvariant();
                    if (response != "Y")
                    {
                        _logger.LogInformation("User chose to start fresh. Clearing checkpoint.");
                        await _checkpointService.ClearCheckpointAsync(configHash);
                        _currentCheckpoint = null;
                    }
                    else
                    {
                        _logger.LogInformation("Resuming from checkpoint");
                    }
                }
            }
            
            // Initialize new checkpoint if not resuming
            if (_currentCheckpoint == null && !string.IsNullOrEmpty(configHash))
            {
                // For unified mapping file, use the mapping path as both config and mapping
                var effectiveConfigPath = !string.IsNullOrEmpty(configPath) ? configPath : mappingPath;
                var effectiveMappingPath = !string.IsNullOrEmpty(mappingPath) ? mappingPath : configPath;
                
                _currentCheckpoint = new CheckpointState
                {
                    ConfigHash = configHash,
                    DatabaseName = databaseName,
                    StartedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    Status = "InProgress",
                    ConfigPath = effectiveConfigPath,
                    MappingPath = effectiveMappingPath
                };
                await _checkpointService.SaveCheckpointAsync(_currentCheckpoint);
            }
            
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

            // Wait for all table processing tasks to complete with proper exception handling
            TableProcessingResult[] tableResults;
            try
            {
                tableResults = await Task.WhenAll(tasks);
            }
            catch (AggregateException ae)
            {
                _logger.LogError(ae, "One or more table processing tasks failed with exceptions");
                // Extract individual exceptions and log them
                foreach (var innerException in ae.InnerExceptions)
                {
                    _logger.LogError(innerException, "Table processing task exception");
                }
                throw; // Re-throw to be caught by the outer try-catch
            }

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
                
                // Mark checkpoint as completed
                if (_currentCheckpoint != null)
                {
                    _currentCheckpoint.Status = "Completed";
                    _currentCheckpoint.LastUpdatedAt = DateTime.UtcNow;
                    await _checkpointService.SaveCheckpointAsync(_currentCheckpoint);
                }
            }
            else
            {
                _logger.LogError("Data obfuscation completed with errors: {ErrorMessage}", result.ErrorMessage);
                
                // Mark checkpoint as failed
                if (_currentCheckpoint != null)
                {
                    _currentCheckpoint.Status = "Failed";
                    _currentCheckpoint.LastUpdatedAt = DateTime.UtcNow;
                    await _checkpointService.SaveCheckpointAsync(_currentCheckpoint);
                }
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
            // Check if table was already processed in checkpoint
            CheckpointTableProgress? tableProgress = null;
            if (_currentCheckpoint != null)
            {
                tableProgress = _currentCheckpoint.Tables.FirstOrDefault(t => t.TableName == tableConfig.TableName);
                if (tableProgress?.Status == "Completed")
                {
                    _logger.LogInformation("Table {TableName} already processed in previous run. Skipping.", tableConfig.TableName);
                    result.Success = true;
                    result.RowsProcessed = tableProgress.ProcessedRows;
                    return result;
                }
            }
            
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

            // Initialize or update table progress in checkpoint
            if (_currentCheckpoint != null)
            {
                if (tableProgress == null)
                {
                    tableProgress = new CheckpointTableProgress
                    {
                        TableName = tableConfig.TableName,
                        Status = "InProgress",
                        TotalRows = totalRows,
                        StartedAt = DateTime.UtcNow
                    };
                    _currentCheckpoint.Tables.Add(tableProgress);
                }
                else
                {
                    tableProgress.Status = "InProgress";
                }
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
                // Check if batch was already processed
                if (tableProgress != null && tableProgress.Batches.Any(b => b.Offset == offset && b.IsProcessed))
                {
                    _logger.LogInformation("Batch at offset {Offset} already processed. Skipping.", offset);
                    continue;
                }
                
                var task = ProcessBatchAsync(
                    tableConfig, 
                    globalConfig, 
                    enabledColumns, 
                    offset, 
                    size, 
                    semaphore,
                    tableProgress);
                batchTasks.Add(task);
            }

            // Wait for all batches to complete with proper exception handling
            BatchProcessingResult[] batchResults;
            try
            {
                batchResults = await Task.WhenAll(batchTasks);
            }
            catch (AggregateException ae)
            {
                _logger.LogError(ae, "One or more batch processing tasks failed for table {TableName}", tableConfig.TableName);
                // Extract individual exceptions and log them
                foreach (var innerException in ae.InnerExceptions)
                {
                    _logger.LogError(innerException, "Batch processing task exception for table {TableName}", tableConfig.TableName);
                }
                throw; // Re-throw to be caught by the outer try-catch
            }

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

            // Mark table as completed in checkpoint
            if (_currentCheckpoint != null && tableProgress != null)
            {
                tableProgress.Status = "Completed";
                tableProgress.CompletedAt = DateTime.UtcNow;
                _currentCheckpoint.LastUpdatedAt = DateTime.UtcNow;
                await _checkpointService.SaveCheckpointAsync(_currentCheckpoint);
            }

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
        SemaphoreSlim semaphore,
        CheckpointTableProgress? tableProgress)
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
            
            // Update checkpoint for this batch
            if (_currentCheckpoint != null && tableProgress != null)
            {
                await _checkpointLock.WaitAsync();
                try
                {
                    var batchProgress = new CheckpointBatchProgress
                    {
                        BatchNumber = batchNumber,
                        Offset = offset,
                        Size = batchSize,
                        IsProcessed = true,
                        ProcessedAt = DateTime.UtcNow,
                        RowsProcessed = updateResult.SuccessfulRows
                    };
                    
                    tableProgress.Batches.Add(batchProgress);
                    tableProgress.ProcessedRows += updateResult.SuccessfulRows;
                    
                    _currentCheckpoint.TotalRowsProcessed += updateResult.SuccessfulRows;
                    _currentCheckpoint.LastUpdatedAt = DateTime.UtcNow;
                    
                    // Save checkpoint after each batch
                    await _checkpointService.SaveCheckpointAsync(_currentCheckpoint);
                }
                finally
                {
                    _checkpointLock.Release();
                }
            }
            
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
                        // Check if this column has OnlyIfNotNull condition (meaning it's NOT nullable)
                        if (columnConfig.Conditions?.OnlyIfNotNull == true)
                        {
                            // For non-nullable columns, preserve NULL values as NULL
                            // This prevents trying to obfuscate NULL values for columns that shouldn't accept NULL
                            obfuscatedRow[columnConfig.ColumnName] = originalValue;
                            _logger.LogDebug("Preserving NULL value for non-nullable column: {ColumnName}", columnConfig.ColumnName);
                        }
                        else
                        {
                            // For nullable columns, we can try to obfuscate NULL values
                            // But for now, we'll preserve them to be safe
                            obfuscatedRow[columnConfig.ColumnName] = originalValue;
                            _logger.LogDebug("Preserving NULL value for nullable column: {ColumnName}", columnConfig.ColumnName);
                        }
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