# Automatic Checkpoint Cleanup

## Overview
When a data obfuscation process completes successfully, the checkpoint file is automatically removed to ensure future runs start fresh. This prevents confusion and ensures clean state management.

## Behavior

### Successful Completion
```
1. Start obfuscation → Checkpoint file created
2. Process tables/batches → Checkpoint updated
3. Complete successfully → Checkpoint file automatically deleted
4. Future run → Starts fresh (no old checkpoint data)
```

### Failed Completion
```
1. Start obfuscation → Checkpoint file created
2. Process tables/batches → Checkpoint updated
3. Fail with errors → Checkpoint preserved with "Failed" status
4. Future run → User can choose to resume or start fresh
```

## Code Implementation

### ObfuscationEngine.cs
```csharp
if (result.Success)
{
    _logger.LogInformation("Data obfuscation completed successfully in {Duration}", result.Duration);
    _logger.LogInformation("Tables processed: {TablesProcessed}, Rows processed: {RowsProcessed:N0}", 
        result.TablesProcessed, result.RowsProcessed);
    
    // Clear checkpoint on successful completion to ensure future runs start fresh
    if (_currentCheckpoint != null)
    {
        _logger.LogInformation("Clearing checkpoint file after successful completion");
        await _checkpointService.ClearCheckpointAsync(_currentCheckpoint.ConfigHash);
        _currentCheckpoint = null;
    }
}
```

## Benefits

1. **Clean State Management**: No accumulation of old checkpoint files
2. **User Experience**: Future runs start fresh without confusion
3. **Storage Efficiency**: Automatic cleanup prevents disk space waste
4. **Reliability**: Ensures consistent behavior across runs

## Example Scenarios

### Scenario 1: Successful Run
```
$ ./DataObfuscation.exe mapping.json
[INFO] Starting data obfuscation process
[INFO] Processing table: Users (1,000,000 rows)
[INFO] Processing table: Orders (5,000,000 rows)
[INFO] Data obfuscation completed successfully in 00:15:30
[INFO] Tables processed: 2, Rows processed: 6,000,000
[INFO] Clearing checkpoint file after successful completion
```

**Result**: Checkpoint file is deleted, next run starts fresh.

### Scenario 2: Failed Run
```
$ ./DataObfuscation.exe mapping.json
[INFO] Starting data obfuscation process
[INFO] Processing table: Users (1,000,000 rows)
[ERROR] Database connection lost during batch processing
[ERROR] Data obfuscation completed with errors: Database connection lost
```

**Result**: Checkpoint file preserved with "Failed" status for potential resume.

### Scenario 3: Resume from Failure
```
$ ./DataObfuscation.exe mapping.json
[INFO] Found incomplete run started at 2024-01-15 10:30:00, last updated at 2024-01-15 10:45:00
[INFO] Processed 500,000 rows across 1 tables

An incomplete obfuscation run was detected:
  Started: 2024-01-15 10:30:00
  Last Update: 2024-01-15 10:45:00
  Progress: 500,000 rows processed

Do you want to resume from where it stopped? (Y/N): Y
[INFO] Resuming from checkpoint
[INFO] Processing table: Orders (5,000,000 rows) - starting from batch 51
```

## Checkpoint File Structure

### During Processing
```json
{
  "configHash": "abc123def456",
  "databaseName": "MyDatabase",
  "startedAt": "2024-01-15T10:30:00Z",
  "lastUpdatedAt": "2024-01-15T10:45:00Z",
  "status": "InProgress",
  "tables": [
    {
      "tableName": "Users",
      "status": "Completed",
      "totalRows": 1000000,
      "processedRows": 1000000,
      "batches": [...]
    },
    {
      "tableName": "Orders", 
      "status": "InProgress",
      "totalRows": 5000000,
      "processedRows": 500000,
      "batches": [...]
    }
  ],
  "totalRowsProcessed": 1500000
}
```

### After Failure
```json
{
  "configHash": "abc123def456",
  "databaseName": "MyDatabase", 
  "startedAt": "2024-01-15T10:30:00Z",
  "lastUpdatedAt": "2024-01-15T10:45:00Z",
  "status": "Failed",
  "tables": [...],
  "totalRowsProcessed": 1500000
}
```

### After Success
**File is deleted** - no checkpoint file exists.

## Configuration Options

The automatic cleanup behavior is built-in and cannot be disabled. This ensures consistent behavior across all runs.

If you need to preserve checkpoint data for debugging or audit purposes, consider:
1. Implementing a separate logging mechanism
2. Creating backup copies before cleanup
3. Adding configuration options for checkpoint retention (future enhancement)

## Testing

To verify the automatic cleanup works:

1. Run a successful obfuscation process
2. Check that the checkpoint file is removed from `checkpoints/` directory
3. Run the same process again - it should start fresh without any checkpoint prompts

```bash
# First run
./DataObfuscation.exe mapping.json
# Check: ls checkpoints/ (should be empty after success)

# Second run  
./DataObfuscation.exe mapping.json
# Should start fresh without checkpoint prompts
```
