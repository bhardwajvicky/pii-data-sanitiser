# Two-Step Workflow for Auto Mapping Generator

## Overview

The Auto Mapping Generator now supports a two-step workflow that separates database access from LLM processing:

1. **Schema Extraction** (`-schema`): Requires database connection, extracts schema only, no LLM calls
2. **Mapping Generation** (`-mapping`): No database connection needed, uses saved schema files, makes LLM calls

## Benefits

- **Security**: Database credentials only needed for schema extraction
- **Flexibility**: Run LLM analysis on a different machine or at a different time
- **Cost Efficiency**: Re-run LLM analysis without re-querying the database
- **Performance**: Process tables one at a time to avoid token limits
- **Debugging**: Review extracted schema before running expensive LLM calls

## Usage

### Step 1: Extract Schema (Default Mode)

```bash
# Extract schema only (no LLM calls)
AutoMappingGenerator -schema

# Or simply (schema is the default mode)
AutoMappingGenerator
```

**Requirements:**
- Database connection string in `appsettings.json`
- Database access credentials
- No LLM API keys needed

**Output:**
- Schema files saved to `../JSON/schema/{DatabaseName}/`
- One JSON file per table with full column metadata
- Database summary file with table list

### Step 2: Generate Mappings

```bash
# Generate PII mappings from saved schema
AutoMappingGenerator -mapping
```

**Requirements:**
- Previously extracted schema files
- LLM API key (Claude or Azure OpenAI) in `appsettings.json`
- No database connection needed

**Output:**
- `{DatabaseName}-mapping.json` - Table/column mapping
- `{DatabaseName}-config.json` - Obfuscation configuration
- `{DatabaseName}_analysis_summary.json` - Analysis report

## LLM Processing Changes

### Previous Behavior
- Sent ALL tables to LLM in a single API call
- Could hit token limits for large databases
- Single point of failure

### New Behavior
- Processes ONE table at a time
- Separate LLM call for each table
- 500ms delay between calls to avoid rate limiting
- Continues processing if individual table fails

## Configuration

### appsettings.json Example

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AdventureWorks;Trusted_Connection=true;"
  },
  "ClaudeApiKey": "your-claude-key-here",
  "AzureOpenAiApiKey": "your-azure-key-here",
  "LLMProvider": "Claude",
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com",
    "DeploymentName": "gpt-4"
  },
  "OutputOptions": {
    "OutputDirectory": "../JSON"
  }
}
```

## Schema Storage Structure

```
../JSON/
└── schema/
    └── AdventureWorks/
        ├── database-schema.json     # Database summary
        ├── dbo/
        │   ├── Person.json         # Table schema
        │   ├── Address.json        # Table schema
        │   └── ...
        └── Sales/
            ├── Customer.json       # Table schema
            └── ...
```

## Example Workflow

```bash
# 1. On database server (with DB access)
AutoMappingGenerator -schema
# Output: Schema extracted to ../JSON/schema/AdventureWorks/

# 2. Copy schema files to analysis machine

# 3. On analysis machine (with LLM access)
AutoMappingGenerator -mapping
# Output: Processing table 1/71: dbo.Person
#         Found 5 PII columns in dbo.Person
#         Processing table 2/71: dbo.Address
#         Found 4 PII columns in dbo.Address
#         ...
#         Mapping generation completed successfully

# 4. Results available in:
#    - AdventureWorks-mapping.json
#    - AdventureWorks-config.json
```

## Error Handling

- If a table fails during LLM analysis, it logs the error and continues
- Failed tables won't appear in the final mapping
- Check logs for details on any failures

## Performance Considerations

- Each table requires a separate LLM API call
- 500ms delay between calls prevents rate limiting
- Large databases may take several minutes to process
- Token usage is more predictable (per-table basis)