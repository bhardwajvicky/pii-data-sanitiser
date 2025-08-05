# PII Data Sanitizer

A high-performance, enterprise-grade solution for discovering and obfuscating personally identifiable information (PII) in SQL Server databases. Designed for cross-database deterministic obfuscation with a focus on Australian fleet management industry data.

## 🚀 Quick Start

```bash
# 1. Clone the repository
git clone https://github.com/bhardwajvicky/pii-data-sanitiser.git
cd pii-data-sanitiser

# 2. Build the solution
dotnet build

# 3. Analyze your database for PII
cd auto-mapping-generator
dotnet run "Server=localhost;Database=YourDB;Integrated Security=true;"

# 4. Obfuscate the data
cd ../data-obfuscation
dotnet run ../JSON/YourDB-mapping.json
```

## 📋 Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Architecture](#architecture)
- [Installation](#installation)
- [Usage Guide](#usage-guide)
- [Configuration](#configuration)
- [Generated Files & Logs](#generated-files--logs)
- [Caching Strategy](#caching-strategy)
- [Performance](#performance)
- [Examples](#examples)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)

## Overview

The PII Data Sanitizer provides a complete solution for:
- **Automatic PII Discovery**: Identifies sensitive data columns using pattern matching and naming conventions
- **Deterministic Obfuscation**: Ensures the same input always produces the same output across databases
- **Industry-Specific Data**: Generates realistic Australian fleet management data
- **High Performance**: Processes millions of records efficiently with parallel processing

## Features

### 🔍 Auto Mapping Generator
- Pattern-based PII detection with confidence scoring
- Automatic detection of 20+ data types (names, addresses, licenses, etc.)
- Referential integrity detection
- **Unified configuration generation** - Single file containing all settings
- Comprehensive analysis reports

### 🔐 Data Obfuscation Engine
- **Deterministic Processing**: Same input → same output across all databases
- **Australian Data Generation**: Realistic fleet industry data
- **Smart Caching**: Selective caching for optimal performance
- **Parallel Processing**: Up to 32 concurrent threads
- **Progress Tracking**: Real-time status updates
- **Dry Run Mode**: Test configurations without modifying data
- **Failure Recovery**: Detailed failure logs and skip capabilities
- **NULL Value Handling**: Respects database constraints for nullable/non-nullable columns
- **Performance Optimized**: Uses SqlBulkCopy and MERGE statements for large datasets

### 🔐 Data Obfuscation Engine
- **Deterministic Processing**: Same input → same output across all databases
- **Australian Data Generation**: Realistic fleet industry data
- **Smart Caching**: Selective caching for optimal performance
- **Parallel Processing**: Up to 32 concurrent threads
- **Progress Tracking**: Real-time status updates
- **Dry Run Mode**: Test configurations without modifying data
- **Failure Recovery**: Detailed failure logs and skip capabilities

### 📊 Supported Data Types
- **Personal**: FirstName, LastName, FullName
- **Contact**: Email, Phone, Mobile
- **Address**: AddressLine1, AddressLine2, City, State, PostCode
- **Vehicle**: Registration, VIN, Make/Model, EngineNumber
- **Business**: CompanyName, ABN, ACN
- **Financial**: CreditCard, BankAccount
- **Identifiers**: DriverLicense, EmployeeID, NationalID
- **Geographic**: GPSCoordinate, RouteCode, DepotLocation

## Architecture

```
pii-data-sanitiser/
├── auto-mapping-generator/          # PII discovery tool
├── data-obfuscation/         # Obfuscation engine
├── Common/                   # Shared libraries
├── JSON/                     # Generated configurations
├── logs/                     # Application logs
├── reports/                  # Processing reports
└── mappings/                 # Value mapping cache
```

## Installation

### Prerequisites
- .NET 8.0 SDK or Runtime
- SQL Server 2016+ (or Azure SQL Database)
- Windows, Linux, or macOS

### Build from Source
```bash
# Clone repository
git clone https://github.com/bhardwajvicky/pii-data-sanitiser.git
cd pii-data-sanitiser

# Build all projects
dotnet build

# Run tests (if available)
dotnet test
```

## Usage Guide

### Step 1: Analyze Your Database

```bash
cd auto-mapping-generator
dotnet run "Server=localhost;Database=AdventureWorks;Integrated Security=true;"
```

This generates:
- `JSON/{database}-mapping.json` - Unified configuration file containing all settings and mappings

### Step 2: Review & Customize Configuration

Edit the generated config files to:
- Enable/disable specific tables or columns
- Adjust batch sizes and parallelism
- Configure custom data types
- Set up referential integrity rules

### Step 3: Run Obfuscation

```bash
cd data-obfuscation

# Dry run (no changes)
dotnet run ../JSON/YourDB-mapping.json --dry-run

# Full obfuscation
dotnet run ../JSON/YourDB-mapping.json
```

## Configuration

### Unified File Approach

The system now uses a single unified configuration file that contains all settings and mappings. This simplifies deployment and reduces the number of files to manage.

#### File Structure
```json
{
  "Global": {
    "ConnectionString": "Server=...;Database=...;",
    "GlobalSeed": "YourSecretSeed2024",
    "BatchSize": 2000,
    "SqlBatchSize": 500,
    "ParallelThreads": 8,
    "MaxCacheSize": 500000,
    "DryRun": false,
    "EnableValueCaching": true
  },
  "DataTypes": {
    "customDataType": {
      "BaseType": "Email",
      "CustomSeed": "CustomSeed2024",
      "PreserveLength": false
    }
  },
  "ReferentialIntegrity": {
    "Enabled": false,
    "Relationships": []
  },
  "PostProcessing": {
    "GenerateReport": true,
    "ReportPath": "reports/{database}-obfuscation-{timestamp}.json"
  },
  "Tables": [
    {
      "TableName": "Person.Person",
      "Schema": "Person",
      "FullTableName": "Person.Person",
      "PrimaryKey": ["BusinessEntityID"],
      "Columns": [
        {
          "ColumnName": "FirstName",
          "DataType": "FirstName",
          "Enabled": true,
          "IsNullable": false,
          "PreserveLength": false
        }
      ]
    }
  ]
}
```

### Basic Configuration Structure

```json
{
  "Global": {
    "ConnectionString": "Server=...;Database=...;",
    "GlobalSeed": "YourSecretSeed2024",
    "BatchSize": 1000,
    "ParallelThreads": 8,
    "DryRun": false,
    "EnableValueCaching": true
  },
  "Tables": [
    {
      "TableName": "Person.Person",
      "Columns": [
        {
          "ColumnName": "FirstName",
          "DataType": "FirstName",
          "Enabled": true
        }
      ]
    }
  ]
}
```

### Key Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `GlobalSeed` | Master seed for deterministic generation | Required |
| `BatchSize` | Records per batch | 1000 |
| `ParallelThreads` | Concurrent processing threads | 4 |
| `MaxCacheSize` | Maximum cached mappings | 500000 |
| `CommandTimeoutSeconds` | SQL command timeout | 300 |
| `DryRun` | Test mode without changes | false |

### Advanced Features

#### Custom Data Types
```json
"DataTypes": {
  "CompanyEmail": {
    "BaseType": "Email",
    "CustomSeed": "CompanySeed2024",
    "Validation": {
      "Regex": "^[a-zA-Z0-9.]+@company\\.com$"
    }
  }
}
```

#### Conditional Processing
```json
"Tables": [{
  "TableName": "Sales.Orders",
  "WhereClause": "OrderDate >= '2024-01-01'",
  "ProcessingOrder": 1
}]
```

#### Referential Integrity
```json
"ReferentialIntegrity": {
  "Enabled": true,
  "Relationships": [{
    "ParentTable": "Person.Person",
    "ParentColumn": "BusinessEntityID",
    "ChildTable": "Person.EmailAddress",
    "ChildColumn": "BusinessEntityID"
  }]
}
```

## Generated Files & Logs

### 📁 Log Files

#### Application Logs
- **Location**: `logs/obfuscation-{date}.log`
- **Content**: Runtime information, warnings, errors
- **Rotation**: Daily
- **Example**:
```
[14:32:15 INF] Starting data obfuscation process
[14:32:15 INF] Processing table: Person.Person
[14:32:16 INF] Batch 1000-2000 completed in 1.2s
```

#### Failure Logs
- **Location**: `logs/failures/{database}_failures_{timestamp}.log`
- **Content**: Detailed failure information per row
- **Format**: Timestamp | Table | Keys | Error
- **Example**:
```
2024-07-15T14:32:16Z | Person.Address | AddressID=123 | Duplicate key violation
```

### 📊 Reports

#### Obfuscation Reports
- **Location**: `reports/{database}-obfuscation-{timestamp}.json`
- **Content**: Processing statistics and summary
```json
{
  "Database": "AdventureWorks",
  "StartTime": "2024-07-15T14:32:15Z",
  "Duration": "00:05:23",
  "TablesProcessed": 8,
  "TotalRows": 99046,
  "RowsPerSecond": 307.2,
  "Success": true
}
```

#### Schema Analysis Reports
- **Location**: `JSON/{database}-mapping.json` (embedded in unified file)
- **Content**: PII detection results and statistics

### 💾 Mapping Files

- **Location**: `mappings/{environment}/mappings_{timestamp}.json`
- **Purpose**: Cache deterministic mappings for consistency
- **Size**: Varies based on unique values (typically 1-100MB)

### 🔍 Audit Logs

- **Location**: `audit/{database}-audit-{timestamp}.log`
- **Content**: Security and compliance audit trail
- **Enable**: Set `"AuditEnabled": true` in config

## Caching Strategy

### Smart Selective Caching

The system implements intelligent caching to optimize performance while managing memory:

#### ✅ Cached (Low-Cardinality)
- **Names**: ~5,000 first names, ~50,000 last names
- **Geographic**: Cities, states, countries, postcodes
- **Categories**: Company names, departments, job titles
- **Benefits**: 100-1000x performance for repeated values

#### ❌ Not Cached (High-Cardinality)
- **Addresses**: Street addresses, GPS coordinates
- **Identifiers**: SSN, licenses, VINs, employee IDs
- **Contact**: Email addresses, phone numbers
- **Financial**: Credit cards, bank accounts
- **Benefits**: Prevents memory exhaustion on large datasets

### Cache Statistics Example
```
Cache statistics - Total entries: 3,568
  LastName: 1,206 entries
  FirstName: 1,018 entries
  PostCode: 661 entries
  City: 575 entries
```

### Performance Impact
- **1TB Database**: Only ~200MB cache vs potential 5GB+
- **Memory Savings**: 95%+ reduction in cache size
- **Speed**: Maintains high performance for common values

## Recent Improvements

### 🚀 Performance Enhancements
- **SqlBulkCopy Integration**: Replaced parameterized INSERT with bulk operations for 3-5x performance improvement
- **MERGE Statements**: Optimized UPDATE operations using SQL Server MERGE for better performance
- **Conservative Batch Sizes**: Default batch sizes optimized to avoid SQL Server parameter limits (2100 max)
- **Enhanced Parallel Processing**: Increased default parallel threads to 8 for better CPU utilization

### 🔒 NULL Value Handling
- **Constraint Respect**: Automatically preserves NULL values for non-nullable columns
- **Database Schema Compliance**: Uses `IsNullable` information from database schema
- **Safe Processing**: Prevents constraint violations during obfuscation
- **Debug Logging**: Detailed logging for NULL handling (debug level only)

### 📁 Unified Configuration
- **Single File Approach**: All settings and mappings in one JSON file
- **Simplified Deployment**: Reduced from 3 files to 1 file
- **Auto-Generation**: Auto-mapping-generator creates unified files automatically
- **Template Updates**: Template files updated with new format and placeholder credentials

## Performance

### Benchmarks

| Database Size | Tables | Total Rows | Time | Speed |
|--------------|--------|------------|------|-------|
| 100MB | 5 | 50K | 30s | 1,667 rows/sec |
| 1GB | 15 | 500K | 5 min | 1,667 rows/sec |
| 10GB | 25 | 5M | 45 min | 1,852 rows/sec |
| 100GB | 50 | 50M | 7 hours | 1,984 rows/sec |

### Optimization Tips

1. **Batch Size**: Larger batches (5000-10000) for simple tables
2. **Parallelism**: Match CPU cores (8-16 threads typical)
3. **Network**: Ensure low latency to SQL Server
4. **Indexing**: Maintain indexes on primary keys
5. **Cache**: Pre-warm cache with common values

## Examples

### Example 1: Basic Obfuscation
```bash
# Simple obfuscation with defaults
dotnet run config.json
```

### Example 2: Dry Run with Custom Batch Size
```bash
# Test configuration without changes
dotnet run config.json --dry-run
```

### Example 3: Unified Configuration
```bash
# Single unified configuration file
dotnet run YourDB-mapping.json
```

### Example 4: Parallel Processing
```json
{
  "Global": {
    "BatchSize": 5000,
    "ParallelThreads": 16
  }
}
```

## Troubleshooting

### Common Issues

#### 1. Duplicate Key Violations
**Problem**: "Cannot insert duplicate key row"
**Solution**: Ensure cache is cleared between runs:
```bash
rm -rf mappings/{environment}/*.json
```

#### 2. NULL Value Issues
**Problem**: NULL values being replaced with obfuscated data
**Solution**: The system now automatically preserves NULL values for non-nullable columns. Check that `IsNullable` is correctly set in your mapping file.

#### 3. Memory Issues
**Problem**: OutOfMemoryException
**Solution**: Reduce cache size or disable caching for large fields:
```json
"MaxCacheSize": 100000,
"EnableValueCaching": false
```

#### 4. Slow Performance
**Problem**: Processing < 1000 rows/sec
**Solution**: 
- Increase batch size and threads
- Check network latency
- Verify indexes exist

#### 5. Connection Timeouts
**Problem**: "Timeout expired"
**Solution**: Increase timeout:
```json
"CommandTimeoutSeconds": 600
```

### Debug Mode

Enable detailed logging:
```json
"LogLevel": "Debug"
```

Check logs at:
- Application: `logs/obfuscation-{date}.log`
- Failures: `logs/failures/{db}_failures_{timestamp}.log`

## Contributing

We welcome contributions! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

### Development Setup

```bash
# Clone your fork
git clone https://github.com/yourusername/pii-data-sanitiser.git

# Add upstream remote
git remote add upstream https://github.com/bhardwajvicky/pii-data-sanitiser.git

# Create feature branch
git checkout -b feature/your-feature
```

## License

This project is licensed under the MIT License - see LICENSE file for details.

## Support

- **Issues**: [GitHub Issues](https://github.com/bhardwajvicky/pii-data-sanitiser/issues)
- **Documentation**: See `/docs` folder
- **Examples**: See `/data-obfuscation/Examples/`

---

Built with ❤️ for data privacy and compliance