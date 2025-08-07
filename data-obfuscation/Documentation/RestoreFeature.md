# Restore Feature Documentation

## Overview
The data obfuscation tool now supports checkpoint-based restore functionality to handle interrupted runs. This ensures that rows are never obfuscated twice, maintaining deterministic obfuscation integrity.

## How It Works

### Checkpoint Creation
- When an obfuscation run starts, a checkpoint file is created in the `checkpoints/` directory
- The checkpoint filename includes a hash of the configuration and mapping files
- Progress is saved after each batch is processed

### Checkpoint Structure
- **Config Hash**: Unique identifier based on config + mapping file contents
- **Database Name**: The database being processed
- **Start Time**: When the run started
- **Last Update**: When the checkpoint was last saved
- **Status**: InProgress, Completed, or Failed
- **Table Progress**: Status and batch progress for each table
- **Batch Progress**: Detailed tracking of each processed batch

### Resume Detection
When the application starts:
1. Checks if a checkpoint exists for the current config/mapping combination
2. If an incomplete run is found, prompts the user:
   ```
   An incomplete obfuscation run was detected:
     Started: 2025-07-29 10:30:15
     Last Update: 2025-07-29 10:45:32
     Progress: 125,000 rows processed
   
   Do you want to resume from where it stopped? (Y/N):
   ```

### Resume Behavior
If the user chooses to resume:
- Already completed tables are skipped
- Already processed batches within incomplete tables are skipped
- Processing continues from the next unprocessed batch
- All deterministic values remain consistent

## Benefits

1. **No Double Obfuscation**: Ensures rows are never obfuscated twice
2. **Deterministic Consistency**: Maintains the same obfuscated values across runs
3. **Time Savings**: Avoids reprocessing already completed work
4. **Fault Tolerance**: Handles interruptions gracefully

## File Locations
- Checkpoints: `/checkpoints/checkpoint_<hash>.json`
- Each checkpoint is tied to specific config + mapping file combination

## Future Enhancements
- Database-based checkpoint storage (planned)
- Multi-instance coordination
- Checkpoint compression for large databases

## Example Checkpoint File
```json
{
  "ConfigHash": "abc123def456...",
  "DatabaseName": "AdventureWorks",
  "StartedAt": "2025-07-29T10:30:15Z",
  "LastUpdatedAt": "2025-07-29T10:45:32Z",
  "Status": "InProgress",
  "Tables": [
    {
      "TableName": "Person.Address",
      "Status": "Completed",
      "TotalRows": 19614,
      "ProcessedRows": 19614,
      "Batches": [...]
    },
    {
      "TableName": "Person.EmailAddress",
      "Status": "InProgress",
      "TotalRows": 19972,
      "ProcessedRows": 5000,
      "Batches": [...]
    }
  ],
  "TotalRowsProcessed": 24614
}
```