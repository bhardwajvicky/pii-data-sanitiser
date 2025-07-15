# Auto Mapping Generator Configuration Guide

## Overview
The Auto Mapping Generator uses appsettings.json files for configuration. This guide explains all available configuration options.

## Configuration Files

### Base Configuration
- `appsettings.json` - Base configuration (required)
- `appsettings.template.json` - Template with all options

### Environment-Specific
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides
- `appsettings.{Environment}.json` - Custom environment

## Configuration Sections

### Connection String
```json
"ConnectionString": "Server=server;Database=db;User Id=user;Password=pass;"
```
- **Required**: Yes
- **Environment Variable**: `AUTO_MAPPING_CONNECTION_STRING`
- **Description**: SQL Server connection string for the database to analyze

### Claude API Key
```json
"ClaudeApiKey": "sk-ant-api..."
```
- **Required**: No (only for enhanced PII detection)
- **Environment Variable**: `CLAUDE_API_KEY`
- **Description**: API key for Claude-based analysis

### Logging
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "AutoMappingGenerator": "Debug"
  }
}
```
- **Levels**: Trace, Debug, Information, Warning, Error, Critical
- **Namespaces**: Configure per namespace

### Analysis Options
```json
"AnalysisOptions": {
  "EnableEnhancedPIIDetection": true,
  "EnableClaudeApiAnalysis": false,
  "MaxTablesPerAnalysis": 100,
  "MaxColumnsPerTable": 500,
  "TimeoutSeconds": 300,
  "SkipSystemTables": true,
  "IncludeViews": false
}
```

| Option | Description | Default |
|--------|-------------|---------|
| EnableEnhancedPIIDetection | Use advanced pattern matching | true |
| EnableClaudeApiAnalysis | Use Claude for analysis | false |
| MaxTablesPerAnalysis | Maximum tables to process | 100 |
| MaxColumnsPerTable | Maximum columns per table | 500 |
| TimeoutSeconds | Analysis timeout | 300 |
| SkipSystemTables | Skip system tables | true |
| IncludeViews | Analyze views | false |

### Output Options
```json
"OutputOptions": {
  "OutputDirectory": "../JSON",
  "GenerateSummaryReport": true,
  "GenerateMappingFile": true,
  "GenerateConfigFile": true,
  "PrettyPrintJson": true,
  "OverwriteExistingFiles": false
}
```

| Option | Description | Default |
|--------|-------------|---------|
| OutputDirectory | Where to save files | ../JSON |
| GenerateSummaryReport | Create analysis summary | true |
| GenerateMappingFile | Create mapping JSON | true |
| GenerateConfigFile | Create config JSON | true |
| PrettyPrintJson | Format JSON output | true |
| OverwriteExistingFiles | Replace existing files | false |

### PII Detection Options
```json
"PIIDetectionOptions": {
  "MinConfidenceScore": 0.7,
  "EnablePatternMatching": true,
  "EnableNameAnalysis": true,
  "EnableDataSampling": true,
  "SampleSize": 100,
  "CustomPatterns": {
    "PatternName": "RegexPattern"
  }
}
```

| Option | Description | Default |
|--------|-------------|---------|
| MinConfidenceScore | Minimum confidence (0-1) | 0.7 |
| EnablePatternMatching | Use regex patterns | true |
| EnableNameAnalysis | Analyze column names | true |
| EnableDataSampling | Sample data for detection | true |
| SampleSize | Rows to sample | 100 |
| CustomPatterns | Additional patterns | {} |

### Default Obfuscation Settings
```json
"DefaultObfuscationSettings": {
  "GlobalSeed": "SecretSeed2024",
  "BatchSize": 1000,
  "ParallelThreads": 8,
  "EnableValueCaching": true,
  "MaxCacheSize": 500000,
  "CommandTimeoutSeconds": 300,
  "DryRun": false
}
```

These settings are applied to the generated configuration file.

## Environment Variables

Sensitive values should use environment variables:

| Variable | Description |
|----------|-------------|
| AUTO_MAPPING_CONNECTION_STRING | Database connection |
| CLAUDE_API_KEY | Claude API key |
| OBFUSCATION_GLOBAL_SEED | Global seed value |
| ASPNETCORE_ENVIRONMENT | Environment name |

## Usage Examples

### Basic Usage
```bash
dotnet run "Server=localhost;Database=MyDB;Integrated Security=true;"
```

### With Environment Variables
```bash
export AUTO_MAPPING_CONNECTION_STRING="Server=..."
export CLAUDE_API_KEY="sk-ant-..."
dotnet run
```

### With Custom Config
```bash
export ASPNETCORE_ENVIRONMENT=Staging
dotnet run
```

## Security Best Practices

1. **Never commit sensitive values**
   - Use templates for examples
   - Add appsettings.json to .gitignore

2. **Use environment variables in production**
   ```bash
   export AUTO_MAPPING_CONNECTION_STRING="..."
   export CLAUDE_API_KEY="..."
   ```

3. **Use secure key vaults when available**
   - Azure Key Vault
   - AWS Secrets Manager
   - HashiCorp Vault

4. **Restrict file permissions**
   ```bash
   chmod 600 appsettings.json
   ```

5. **Enable audit logging in production**
   ```json
   "Security": {
     "EnableAuditLogging": true
   }
   ```

## Troubleshooting

### Missing Configuration
- Error: "ConnectionString is required"
- Solution: Set connection string in appsettings.json or environment

### Invalid JSON
- Error: "Failed to load configuration"
- Solution: Validate JSON syntax, check for trailing commas

### Permission Issues
- Error: "Access denied to output directory"
- Solution: Ensure write permissions to OutputDirectory

### API Key Issues
- Error: "Invalid Claude API key"
- Solution: Verify API key format and validity