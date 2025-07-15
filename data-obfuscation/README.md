# Fleet Management Data Obfuscation Tool

A comprehensive, production-ready data obfuscation solution for Fleet Management databases, designed to generate realistic Australian fleet industry data while maintaining deterministic consistency and enterprise-scale performance.

## 🚀 Key Features

- **Deterministic Obfuscation**: Same input always generates same output using SHA-256 seeding
- **Australian Fleet Data**: Realistic fleet industry data (driver licenses, vehicle registrations, ABN/ACN, etc.)
- **Enterprise Scale**: Handle 1TB+ databases with 100,000+ rows/minute throughput
- **External Configuration**: Complete JSON-based configuration without code changes
- **Referential Integrity**: Maintain relationships across tables (driver-vehicle, operator-fleet)
- **Parallel Processing**: Configurable multi-threading with batch processing
- **Performance Optimized**: Memory management, caching, and database load balancing

## 🏗️ Architecture Overview

```
┌─────────────────────┐    ┌──────────────────────┐    ┌─────────────────────┐
│  Configuration      │    │  Obfuscation Engine  │    │  Australian Data    │
│  Parser & Validator │◄──►│  with Progress       │◄──►│  Provider (Bogus)   │
└─────────────────────┘    │  Tracking            │    └─────────────────────┘
                           └──────────┬───────────┘
                                      │
┌─────────────────────┐    ┌──────────▼───────────┐    ┌─────────────────────┐
│  Referential        │◄──►│  SQL Server          │◄──►│  Performance        │
│  Integrity Manager  │    │  Repository          │    │  Monitor            │
└─────────────────────┘    └──────────────────────┘    └─────────────────────┘
```

## 📋 Prerequisites

- **.NET 8.0** or later
- **SQL Server** (2016 or later)
- **Windows/Linux/macOS** (cross-platform)

## 🚀 Quick Start

### 1. Build the Application
```bash
dotnet build DataObfuscation.csproj
dotnet publish -c Release -o publish
```

### 2. Basic Usage
```bash
# Test run (dry mode)
DataObfuscation.exe configs/test-config.json --dry-run

# Validate configuration only
DataObfuscation.exe configs/production-config.json --validate-only

# Production obfuscation
DataObfuscation.exe configs/production-config.json
```

### 3. Sample Configuration
```json
{
  "global": {
    "connectionString": "Server=localhost;Database=FleetDB;Trusted_Connection=true;",
    "globalSeed": "FleetSeed2024",
    "batchSize": 15000,
    "parallelThreads": 8,
    "dryRun": false
  },
  "tables": [
    {
      "tableName": "Drivers",
      "primaryKey": ["DriverID"],
      "columns": [
        {"columnName": "DriverName", "dataType": "DriverName", "enabled": true},
        {"columnName": "LicenseNumber", "dataType": "DriverLicenseNumber", "enabled": true}
      ]
    }
  ]
}
```

## 🇦🇺 Australian Fleet Data Types

### Driver & Personnel
- **`DriverName`**: Australian names with cultural diversity
- **`DriverLicenseNumber`**: Format: `{State}-{8digits}` (e.g., NSW-12345678)
- **`ContactEmail`**: Fleet industry domains (.transport.com.au, .logistics.net.au)
- **`DriverPhone`**: Australian mobile format: 04XX XXX XXX

### Vehicle Data
- **`VehicleRegistration`**: State-specific formats (ABC123, 123ABC, 1ABC123)
- **`VINNumber`**: 17-character vehicle identification
- **`VehicleMakeModel`**: Realistic fleet vehicles (Toyota Hiace, Ford Transit)
- **`EngineNumber`**: Engine identification numbers

### Fleet Business
- **`OperatorName`**: Companies ending with "Transport", "Logistics", "Fleet Services"
- **`BusinessABN`**: Valid 11-digit Australian Business Number format
- **`BusinessACN`**: Valid 9-digit Australian Company Number format
- **`Address`**: Commercial Australian addresses with correct state/postcode

### Location & Route
- **`GPSCoordinate`**: Coordinates within Australian bounds
- **`RouteCode`**: Route identifiers (R001, SYD-MEL-001)
- **`DepotLocation`**: Australian commercial addresses

### Date & Time
- **`Date`**: General date fields (anniversaries, hire dates, expiry dates)
- **`DateOfBirth`**: Date of birth for individuals aged 18-80

## ⚙️ Configuration Examples

### Production Environment
```json
{
  "global": {
    "connectionString": "Server=prod-db;Database=FleetDB;Trusted_Connection=true;",
    "globalSeed": "FleetProdSeed2024_v3",
    "batchSize": 25000,
    "parallelThreads": 16,
    "maxCacheSize": 5000000,
    "dryRun": false
  },
  "dataTypes": {
    "ExecutiveDriverName": {
      "baseType": "DriverName",
      "customSeed": "ExecutiveSeed2024",
      "preserveLength": true
    }
  },
  "referentialIntegrity": {
    "enabled": true,
    "relationships": [
      {
        "name": "DriverConsistency",
        "primaryTable": "Drivers",
        "primaryColumn": "DriverName",
        "relatedMappings": [
          {"table": "VehicleAssignments", "column": "DriverName", "relationship": "exact"}
        ]
      }
    ]
  }
}
```

### Development/Testing
```json
{
  "global": {
    "connectionString": "Server=localhost;Database=TestDB;Trusted_Connection=true;",
    "globalSeed": "DevSeed2024",
    "batchSize": 1000,
    "dryRun": true
  },
  "tables": [
    {
      "tableName": "Drivers",
      "conditions": {"maxRows": 100},
      "columns": [
        {"columnName": "DriverName", "dataType": "DriverName", "enabled": true}
      ]
    }
  ]
}
```

## 🔧 Advanced Features

### Custom Data Types
```json
{
  "dataTypes": {
    "SeniorDriverName": {
      "baseType": "DriverName",
      "customSeed": "SeniorDriverSeed2024",
      "validation": {"minLength": 10, "maxLength": 50},
      "formatting": {"addPrefix": "SR_"}
    }
  }
}
```

### Conditional Processing
```json
{
  "columns": [
    {
      "columnName": "SensitiveData",
      "dataType": "DriverName",
      "conditions": {
        "conditionalExpression": "DataClassification = 'SENSITIVE'",
        "onlyIfNotNull": true
      },
      "fallback": {
        "onError": "useDefault",
        "defaultValue": "REDACTED"
      }
    }
  ]
}
```

### Performance Tuning
```json
{
  "global": {
    "batchSize": 25000,
    "parallelThreads": 16,
    "maxCacheSize": 5000000,
    "enableValueCaching": true
  },
  "tables": [
    {
      "tableName": "LargeTable",
      "customBatchSize": 50000,
      "priority": 1
    }
  ]
}
```

## 📊 Performance Expectations

| Database Size | Processing Time | Throughput | Resource Usage |
|---------------|----------------|------------|----------------|
| 100GB | 1-3 hours | 150K rows/min | 4-8GB RAM |
| 500GB | 4-12 hours | 200K rows/min | 8-16GB RAM |
| 1TB+ | 8-24 hours | 250K rows/min | 16-32GB RAM |

### Optimization Guidelines
- **Single Server**: 100,000-250,000 rows/minute
- **Multiple Servers**: 3x-5x throughput improvement
- **Memory**: 2-4GB RAM for optimal caching
- **CPU**: 16+ cores recommended for large datasets

## 🔍 Monitoring & Logging

### Real-time Progress
```
[12:30:45] Started processing table: Drivers
[12:31:15] Table Drivers: 25,000/100,000 (25.0%) - 833 rows/sec
[12:32:45] Completed table Drivers: 100,000 rows in 00:02:00 (833 rows/sec)
[12:32:46] Overall progress: 3/10 tables (30.0%) - 2 completed, 0 failed
```

### Generated Reports
```json
{
  "timestamp": "2024-07-13T12:35:00Z",
  "configuration": {
    "globalSeed": "FleetProdSeed2024_v3",
    "batchSize": 25000,
    "parallelThreads": 16,
    "dryRun": false
  },
  "tablesProcessed": [
    {
      "tableName": "Drivers",
      "priority": 1,
      "columnsObfuscated": 5
    }
  ],
  "mappingStatistics": {
    "totalMappings": 1250000
  }
}
```

## 🚀 Deployment Guide

### Pre-deployment Checklist
```bash
# 1. Validate configuration
DataObfuscation.exe config.json --validate-only

# 2. Test with dry run
DataObfuscation.exe config.json --dry-run

# 3. Backup database
sqlcmd -S server -Q "BACKUP DATABASE MyDB TO DISK='backup.bak'"

# 4. Execute obfuscation
DataObfuscation.exe config.json

# 5. Verify results
DataObfuscation.exe config.json --verify-mappings
```

### Environment Management
```
environments/
├── production.json     # Full production settings
├── staging.json       # Staging environment  
├── development.json   # Dev environment with limits
└── testing.json       # Unit test configurations
```

### Docker Support
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
COPY publish/ /app/
WORKDIR /app
ENTRYPOINT ["dotnet", "DataObfuscation.dll"]
```

## 🔒 Security & Compliance

### Data Protection
- **No Hardcoded Values**: All sensitive mappings externalized to JSON
- **Deterministic Generation**: Same input always produces same output
- **Secure Hashing**: SHA-256 for seed generation
- **Access Control**: Database-level security maintained

### Audit Trail
- **Mapping Persistence**: Optional saving of all value mappings
- **Detailed Logging**: Complete audit trail of all operations
- **Error Handling**: Comprehensive fallback strategies
- **Dry Run Mode**: Safe testing without data modification

## 🛠️ Troubleshooting

### Common Issues

#### Performance Issues
```json
{
  "solutions": [
    "Increase batchSize (15000-50000)",
    "Optimize parallelThreads (CPU core count)",
    "Enable maxCacheSize (1M-10M mappings)",
    "Temporarily disable database indexes"
  ]
}
```

#### Memory Issues
```json
{
  "solutions": [
    "Reduce maxCacheSize",
    "Lower parallelThreads",
    "Smaller customBatchSize per table",
    "Process tables sequentially (parallelThreads=1)"
  ]
}
```

#### Database Connection Issues
```json
{
  "solutions": [
    "Increase commandTimeoutSeconds (600-3600)",
    "Optimize connection string",
    "Check database server load",
    "Verify network connectivity"
  ]
}
```

### Error Recovery
```json
{
  "fallback": {
    "onError": "useOriginal|useDefault|skip",
    "defaultValue": "REDACTED_VALUE"
  }
}
```

## 📚 API Reference

### Core Interfaces
- `IObfuscationEngine`: Main processing engine
- `IDeterministicAustralianProvider`: Data generation
- `IReferentialIntegrityManager`: Relationship management
- `IDataRepository`: Database operations
- `IProgressTracker`: Monitoring and reporting

### Configuration Schema
Complete JSON schema validation ensures configuration correctness and prevents runtime errors.

## 🤝 Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Open Pull Request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Support

For support and questions:
- Create an issue in the repository
- Check the troubleshooting section
- Review configuration examples
- Enable detailed logging for diagnostics