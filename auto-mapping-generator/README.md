# Auto Mapping Generator Tool

A .NET 8 console application that analyzes SQL Server database schemas to automatically identify columns containing Personally Identifiable Information (PII) and generates configuration files for the data-obfuscation project.

## 🎯 Purpose

This tool automates the discovery of PII columns in SQL Server databases and creates ready-to-use JSON configuration files for the data obfuscation engine, eliminating the manual effort of identifying sensitive data.

## 🔍 Features

- **Automatic Schema Discovery**: Connects to SQL Server and analyzes complete database schema
- **Intelligent PII Detection**: Uses pattern matching and heuristics to identify PII columns
- **Confidence Scoring**: Assigns confidence scores to PII detection results
- **Auto-Configuration Generation**: Creates complete obfuscation configurations
- **Referential Integrity**: Automatically identifies relationships between tables
- **Comprehensive Reporting**: Generates detailed analysis summaries

## 📋 PII Detection Capabilities

### Supported PII Types
- **Person Names**: FirstName, LastName, FullName, DisplayName
- **Contact Information**: Email addresses, phone numbers, mobile numbers
- **Addresses**: Street addresses, cities, postal codes, states
- **Business Identifiers**: ABN, ACN, business numbers, tax numbers
- **License Numbers**: Driver licenses, permits, credentials
- **User Identifiers**: Usernames, login IDs, account names
- **Technical Data**: IP addresses, URLs
- **Free Text**: Comments, notes, descriptions (potential PII)

### Detection Algorithms
- **Column Name Pattern Matching**: Recognizes common naming conventions
- **Data Type Analysis**: Considers SQL data types for compatibility
- **Table Context**: Uses table names for additional context
- **Length Heuristics**: Validates column lengths against expected PII formats
- **Confidence Scoring**: Assigns scores based on multiple factors

## 🚀 Usage

### Two-Step Workflow

The tool now supports a two-step workflow:

1. **Schema Extraction** (requires database access):
```bash
# Extract schema only - no LLM calls
dotnet run -- -schema    # or just: dotnet run
```

2. **Mapping Generation** (no database needed):
```bash
# Generate PII mappings from saved schema - uses LLM
dotnet run -- -mapping
```

### Help
```bash
dotnet run -- -help
```

### Configuration

The application uses `appsettings.json` for configuration. Templates are provided:

- `appsettings.template.json` - Basic template with all options
- `appsettings.Development.template.json` - Development environment template  
- `appsettings.Production.template.json` - Production environment template

Copy the appropriate template and rename it to `appsettings.json`, then update with your values:

```bash
cp appsettings.template.json appsettings.json
# Edit appsettings.json with your connection string and settings
```

See [CONFIGURATION.md](CONFIGURATION.md) for detailed configuration documentation.

### Output Files
1. **AdventureWorks2019.json**: Complete obfuscation configuration
2. **AdventureWorks2019_analysis_summary.json**: Detailed analysis report

## 📊 Detection Rules

### Person Names
```
Column Patterns: *name*, *first*, *last*, *middle*, *full*, *display*
Table Context: *person*, *employee*, *customer*, *contact*
Confidence: High (0.7 base)
Obfuscation: DriverName
```

### Email Addresses
```
Column Patterns: *email*, *mail*, *e_mail*, *emailaddress*
Data Types: varchar, nvarchar, text
Confidence: Very High (0.8 base)
Obfuscation: ContactEmail
```

### Phone Numbers
```
Column Patterns: *phone*, *mobile*, *cell*, *tel*, *telephone*
Data Types: varchar, nvarchar, char
Confidence: Very High (0.8 base)
Obfuscation: DriverPhone
```

### Addresses
```
Column Patterns: *address*, *street*, *city*, *suburb*, *postal*
Table Context: *address*, *contact*, *location*
Confidence: High (0.7 base)
Obfuscation: Address
```

## 🔧 Configuration Generation

### Automatic Features
- **Priority Assignment**: Critical tables (Person, Employee, Customer) get highest priority
- **Batch Size Optimization**: Determined by table row counts
- **Cache Size Calculation**: Based on estimated PII volume
- **Referential Integrity**: Automatic relationship detection
- **Fallback Strategies**: Error handling for each data type

### Generated Structure
```json
{
  "metadata": {
    "configVersion": "2.1",
    "description": "Auto-generated obfuscation configuration",
    "createdBy": "AutoMappingGenerator"
  },
  "global": {
    "connectionString": "...",
    "globalSeed": "AdventureWorks2019Seed20240713",
    "batchSize": 15000,
    "dryRun": true
  },
  "tables": [
    {
      "tableName": "Person",
      "priority": 1,
      "columns": [
        {
          "columnName": "FirstName",
          "dataType": "DriverName",
          "enabled": true
        }
      ]
    }
  ]
}
```

## 📈 Analysis Report

The tool generates comprehensive analysis reports including:

```json
{
  "databaseName": "AdventureWorks2019",
  "totalTables": 71,
  "totalColumns": 503,
  "tablesWithPII": 12,
  "piiColumns": 47,
  "piiColumnsByType": {
    "DriverName": 15,
    "ContactEmail": 8,
    "DriverPhone": 6,
    "Address": 12
  },
  "tablesAnalyzed": [
    {
      "tableName": "Person",
      "schema": "Person",
      "piiColumnCount": 5,
      "piiColumns": [...]
    }
  ]
}
```

## 🎯 Integration with Data Obfuscation

The generated configuration files are ready for immediate use:

```bash
# Use generated configuration with data obfuscation tool
cd ../data-obfuscation
dotnet run ../JSON/AdventureWorks2019.json --dry-run
```

## 🔧 Customization

### Adding Custom Detection Rules
```csharp
// In PIIDetectionService.cs
new PIIDetectionRule
{
    DataType = PIIDataType.Custom,
    ObfuscationDataType = "CustomDataType",
    ColumnNamePatterns = new List<string> { "*custom*pattern*" },
    SqlDataTypes = new List<string> { "varchar", "nvarchar" },
    BaseConfidence = 0.8
}
```

### Adjusting Confidence Thresholds
```csharp
// In PIIDetectionService.cs
const double MinConfidenceThreshold = 0.6; // Adjust as needed
```

### Custom Connection Strings
```csharp
// In Program.cs
var connectionString = "Server=your-server;Database=your-db;...";
```

## 🛠️ Architecture

```
AutoMappingGenerator/
├── Models/
│   └── SchemaModels.cs          # Data models
├── Services/
│   ├── SchemaAnalysisService.cs # Database schema analysis
│   └── PIIDetectionService.cs   # PII identification logic
├── Core/
│   └── ObfuscationConfigGenerator.cs # Configuration generation
└── Program.cs                   # Main application
```

## 🚀 Performance

- **Schema Analysis**: ~2-5 seconds for typical databases
- **PII Detection**: ~1-3 seconds per 100 columns
- **Configuration Generation**: ~1 second
- **Memory Usage**: <100MB for most databases

## 🔍 Troubleshooting

### Connection Issues
```
Error: Cannot connect to database
Solution: Verify connection string, credentials, and network connectivity
```

### No PII Detected
```
Cause: Column naming doesn't match detection patterns
Solution: Review and customize detection rules in PIIDetectionService.cs
```

### Low Confidence Scores
```
Cause: Ambiguous column names or types
Solution: Lower MinConfidenceThreshold or add custom patterns
```

## 📚 Example Output

For AdventureWorks2019, the tool typically identifies:
- **Person tables**: Person.Person, Person.Contact, Person.EmailAddress
- **Employee data**: HumanResources.Employee
- **Customer info**: Sales.Customer
- **Address tables**: Person.Address, Person.StateProvince
- **Contact details**: Person.PersonPhone, Person.EmailAddress

This automated analysis saves hours of manual schema review and ensures no PII columns are missed during obfuscation planning.