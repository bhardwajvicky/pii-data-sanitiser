# Data Obfuscation Tool - Usage Examples

This document provides comprehensive usage examples for the Fleet Management Data Obfuscation Tool, covering various scenarios from development testing to production deployment.

## 📋 Table of Contents

1. [Basic Usage Examples](#basic-usage-examples)
2. [Configuration Scenarios](#configuration-scenarios)
3. [Performance Optimization](#performance-optimization)
4. [Error Handling & Recovery](#error-handling--recovery)
5. [Production Deployment](#production-deployment)
6. [Monitoring & Troubleshooting](#monitoring--troubleshooting)

## 🚀 Basic Usage Examples

### Example 1: First-Time Setup (Development)
```bash
# Step 1: Validate your configuration
DataObfuscation.exe configs/development-config.json --validate-only

# Step 2: Run in dry-run mode to test
DataObfuscation.exe configs/development-config.json --dry-run

# Step 3: Process a small subset of data
DataObfuscation.exe configs/development-config.json
```

**Expected Output:**
```
[INFO] Loading configuration from: configs/development-config.json
[INFO] Configuration loaded successfully
[INFO] Tables to process: 2
[INFO] Running in DRY RUN mode - no data will be modified
[INFO] Started processing table: Drivers
[INFO] [DRY RUN] Would process batch 0-1000 for table Drivers
[INFO] Completed table Drivers: 100 rows in 00:00:02
[INFO] Overall progress: 2/2 tables (100.0%) - 2 completed, 0 failed
[INFO] Data obfuscation completed successfully
```

### Example 2: Production Database (Large Scale)
```bash
# Step 1: Backup database first
sqlcmd -S prod-server -Q "BACKUP DATABASE FleetDB TO DISK='C:\Backups\FleetDB_before_obfuscation.bak'"

# Step 2: Execute production obfuscation
DataObfuscation.exe configs/production-config.json

# Step 3: Verify results
DataObfuscation.exe configs/production-config.json --verify-mappings
```

**Expected Output:**
```
[INFO] Loading configuration from: configs/production-config.json
[INFO] Configuration loaded successfully
[INFO] Tables to process: 10
[INFO] Started processing table: Drivers
[INFO] Table Drivers: 25,000/100,000 (25.0%) - 1,250 rows/sec
[INFO] Table Drivers: 50,000/100,000 (50.0%) - 1,389 rows/sec
[INFO] Completed table Drivers: 100,000 rows in 00:01:15 (1,333 rows/sec)
[INFO] Overall progress: 10/10 tables (100.0%) - 10 completed, 0 failed
[INFO] Tables processed: 10, Rows processed: 2,547,891
[INFO] Duration: 01:23:45
```

## ⚙️ Configuration Scenarios

### Scenario 1: Customer Data Migration
**Use Case**: Migrating customer data from legacy system while obfuscating PII

```json
{
  "global": {
    "connectionString": "Server=migration-server;Database=CustomerMigrationDB;Trusted_Connection=true;",
    "globalSeed": "CustomerMigrationSeed2024",
    "batchSize": 10000,
    "parallelThreads": 8,
    "dryRun": false
  },
  "dataTypes": {
    "LegacyCustomerName": {
      "baseType": "DriverName",
      "customSeed": "LegacyCustomerSeed",
      "preserveLength": true,
      "validation": {
        "minLength": 2,
        "maxLength": 100
      }
    },
    "LegacyEmail": {
      "baseType": "ContactEmail",
      "customSeed": "LegacyEmailSeed",
      "formatting": {
        "addSuffix": ".migrated"
      }
    }
  },
  "tables": [
    {
      "tableName": "LegacyCustomers",
      "priority": 1,
      "conditions": {
        "whereClause": "MigrationStatus = 'PENDING' AND DataClassification = 'SENSITIVE'"
      },
      "primaryKey": ["CustomerID"],
      "columns": [
        {
          "columnName": "CustomerName",
          "dataType": "LegacyCustomerName",
          "enabled": true,
          "fallback": {
            "onError": "useDefault",
            "defaultValue": "MIGRATION_ERROR"
          }
        },
        {
          "columnName": "EmailAddress",
          "dataType": "LegacyEmail",
          "enabled": true,
          "conditions": {
            "onlyIfNotNull": true
          }
        }
      ]
    }
  ]
}
```

### Scenario 2: Multi-Environment Processing
**Use Case**: Different obfuscation rules for different environments

**configs/environments/staging.json:**
```json
{
  "global": {
    "connectionString": "Server=staging-db;Database=FleetStagingDB;Trusted_Connection=true;",
    "globalSeed": "StagingSeed2024",
    "batchSize": 15000,
    "dryRun": false
  },
  "dataTypes": {
    "StagingDriverName": {
      "baseType": "DriverName",
      "customSeed": "StagingDriverSeed",
      "formatting": {
        "addPrefix": "STG_"
      }
    }
  },
  "tables": [
    {
      "tableName": "Drivers",
      "conditions": {
        "whereClause": "TestAccount = 0 AND Environment = 'STAGING'"
      },
      "columns": [
        {
          "columnName": "DriverName",
          "dataType": "StagingDriverName",
          "enabled": true
        }
      ]
    }
  ]
}
```

**Usage:**
```bash
# Process different environments
DataObfuscation.exe configs/environments/development.json
DataObfuscation.exe configs/environments/staging.json
DataObfuscation.exe configs/environments/production.json
```

### Scenario 3: Selective Column Obfuscation
**Use Case**: Obfuscate only specific sensitive columns while preserving others

```json
{
  "tables": [
    {
      "tableName": "EmployeeRecords",
      "primaryKey": ["EmployeeID"],
      "columns": [
        {
          "columnName": "FullName",
          "dataType": "DriverName",
          "enabled": true,
          "conditions": {
            "conditionalExpression": "SecurityClearance IN ('PUBLIC', 'RESTRICTED')"
          }
        },
        {
          "columnName": "PersonalEmail",
          "dataType": "ContactEmail",
          "enabled": true
        },
        {
          "columnName": "WorkEmail",
          "dataType": "ContactEmail",
          "enabled": false
        },
        {
          "columnName": "EmergencyContact",
          "dataType": "DriverPhone",
          "enabled": true,
          "conditions": {
            "onlyIfNotNull": true
          },
          "fallback": {
            "onError": "skip"
          }
        }
      ]
    }
  ]
}
```

## 🚀 Performance Optimization

### Example 1: High-Throughput Configuration
**Use Case**: Processing large databases (500GB+) with maximum performance

```json
{
  "global": {
    "connectionString": "Server=high-perf-db;Database=LargeFleetDB;Trusted_Connection=true;Connection Timeout=60;Command Timeout=1800;",
    "globalSeed": "HighPerfSeed2024",
    "batchSize": 50000,
    "parallelThreads": 32,
    "maxCacheSize": 10000000,
    "enableValueCaching": true,
    "commandTimeoutSeconds": 1800
  },
  "tables": [
    {
      "tableName": "LargeDriverTable",
      "priority": 1,
      "customBatchSize": 75000,
      "conditions": {
        "whereClause": "ProcessingStatus = 'READY'"
      },
      "columns": [
        {
          "columnName": "DriverName",
          "dataType": "DriverName",
          "enabled": true
        }
      ]
    },
    {
      "tableName": "SmallLookupTable",
      "priority": 10,
      "customBatchSize": 5000,
      "columns": [
        {
          "columnName": "LookupValue",
          "dataType": "OperatorName",
          "enabled": true
        }
      ]
    }
  ]
}
```

**Usage with Monitoring:**
```bash
# Start with performance monitoring
DataObfuscation.exe configs/high-performance-config.json > performance.log 2>&1 &

# Monitor progress in real-time
tail -f performance.log

# Monitor system resources
htop  # or Task Manager on Windows
```

### Example 2: Memory-Constrained Environment
**Use Case**: Processing on servers with limited RAM (8GB or less)

```json
{
  "global": {
    "batchSize": 5000,
    "parallelThreads": 4,
    "maxCacheSize": 500000,
    "enableValueCaching": true
  },
  "tables": [
    {
      "tableName": "Drivers",
      "customBatchSize": 2500,
      "columns": [
        {
          "columnName": "DriverName",
          "dataType": "DriverName",
          "enabled": true
        }
      ]
    }
  ]
}
```

## 🛠️ Error Handling & Recovery

### Example 1: Graceful Error Recovery
**Configuration with comprehensive fallback strategies:**

```json
{
  "tables": [
    {
      "tableName": "CriticalDriverData",
      "columns": [
        {
          "columnName": "DriverName",
          "dataType": "DriverName",
          "enabled": true,
          "fallback": {
            "onError": "useDefault",
            "defaultValue": "OBFUSCATION_FAILED"
          },
          "validation": {
            "minLength": 2,
            "maxLength": 100
          }
        },
        {
          "columnName": "OptionalNotes",
          "dataType": "DriverName",
          "enabled": true,
          "fallback": {
            "onError": "skip"
          }
        },
        {
          "columnName": "BackupEmail",
          "dataType": "ContactEmail",
          "enabled": true,
          "fallback": {
            "onError": "useOriginal"
          }
        }
      ]
    }
  ]
}
```

### Example 2: Resume After Failure
**Use Case**: Resuming processing after system failure

```bash
# Step 1: Check which tables were completed
grep "Completed table" logs/obfuscation.log

# Step 2: Create resume configuration (exclude completed tables)
# Edit config to remove already processed tables

# Step 3: Resume processing
DataObfuscation.exe configs/resume-config.json
```

**Resume Configuration Example:**
```json
{
  "metadata": {
    "description": "Resume configuration - excludes already processed tables"
  },
  "global": {
    "globalSeed": "SAME_SEED_AS_ORIGINAL",
    "persistMappings": true,
    "mappingCacheDirectory": "mappings/original"
  },
  "tables": [
    {
      "tableName": "UnprocessedTable1",
      "columns": []
    },
    {
      "tableName": "UnprocessedTable2", 
      "columns": []
    }
  ]
}
```

## 🏗️ Production Deployment

### Example 1: Blue-Green Deployment
**Use Case**: Zero-downtime obfuscation using blue-green deployment

```bash
#!/bin/bash
# Blue-Green Obfuscation Script

# Step 1: Create green database (copy of blue)
sqlcmd -S prod-server -Q "
    BACKUP DATABASE FleetDB_Blue TO DISK = 'C:\Backups\FleetDB_Blue.bak'
    RESTORE DATABASE FleetDB_Green FROM DISK = 'C:\Backups\FleetDB_Blue.bak'
"

# Step 2: Obfuscate green database
DataObfuscation.exe configs/production-green-config.json

# Step 3: Validate green database
DataObfuscation.exe configs/production-green-config.json --verify-mappings

# Step 4: Switch applications to green database
# (Update connection strings in applications)

# Step 5: Cleanup blue database after validation
# sqlcmd -S prod-server -Q "DROP DATABASE FleetDB_Blue"
```

### Example 2: Staged Rollout
**Use Case**: Processing database in stages to minimize risk

**Stage 1 - Non-Critical Tables:**
```json
{
  "metadata": {
    "description": "Stage 1: Non-critical tables"
  },
  "tables": [
    {"tableName": "AuditLogs", "priority": 1},
    {"tableName": "SystemConfiguration", "priority": 2},
    {"tableName": "ReportingData", "priority": 3}
  ]
}
```

**Stage 2 - Supporting Tables:**
```json
{
  "metadata": {
    "description": "Stage 2: Supporting tables"
  },
  "tables": [
    {"tableName": "VehicleTypes", "priority": 1},
    {"tableName": "RouteDefinitions", "priority": 2},
    {"tableName": "MaintenanceCategories", "priority": 3}
  ]
}
```

**Stage 3 - Core Tables:**
```json
{
  "metadata": {
    "description": "Stage 3: Core business tables"
  },
  "tables": [
    {"tableName": "Drivers", "priority": 1},
    {"tableName": "Vehicles", "priority": 2},
    {"tableName": "FleetOperators", "priority": 3}
  ]
}
```

**Deployment Script:**
```bash
#!/bin/bash
# Staged deployment script

echo "Starting Stage 1: Non-critical tables"
DataObfuscation.exe configs/stage1-config.json
if [ $? -ne 0 ]; then
    echo "Stage 1 failed, aborting deployment"
    exit 1
fi

echo "Starting Stage 2: Supporting tables"
DataObfuscation.exe configs/stage2-config.json
if [ $? -ne 0 ]; then
    echo "Stage 2 failed, aborting deployment"
    exit 1
fi

echo "Starting Stage 3: Core tables"
DataObfuscation.exe configs/stage3-config.json
if [ $? -ne 0 ]; then
    echo "Stage 3 failed, aborting deployment"
    exit 1
fi

echo "All stages completed successfully"
```

## 📊 Monitoring & Troubleshooting

### Example 1: Real-time Monitoring Setup
**PowerShell monitoring script:**

```powershell
# monitor-obfuscation.ps1
$logFile = "logs/obfuscation.log"
$processName = "DataObfuscation"

Write-Host "Starting obfuscation monitoring..."

# Start the obfuscation process
Start-Process -FilePath "DataObfuscation.exe" -ArgumentList "configs/production-config.json" -NoNewWindow

# Monitor the log file
Get-Content $logFile -Wait | ForEach-Object {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] $_"
    
    # Check for specific patterns
    if ($_ -match "ERROR|FAILED") {
        Write-Host "ERROR DETECTED: $_" -ForegroundColor Red
        # Send alert notification
        # Send-MailMessage -To "admin@company.com" -Subject "Obfuscation Error" -Body $_
    }
    
    if ($_ -match "Completed table.*(\d+) rows") {
        $rows = $matches[1]
        Write-Host "Table completed with $rows rows" -ForegroundColor Green
    }
}
```

### Example 2: Performance Analysis
**SQL queries for performance analysis:**

```sql
-- Monitor database performance during obfuscation
SELECT 
    db_name() as DatabaseName,
    (SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id = DB_ID()) as ActiveConnections,
    (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id = DB_ID()) as TotalSessions,
    (SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_database_id = DB_ID()) as ActiveLocks

-- Monitor I/O statistics
SELECT 
    DB_NAME(database_id) as DatabaseName,
    file_id,
    num_of_reads,
    num_of_writes,
    io_stall_read_ms,
    io_stall_write_ms,
    io_stall_read_ms + io_stall_write_ms as total_io_stall_ms
FROM sys.dm_io_virtual_file_stats(DB_ID(), NULL)

-- Check for blocking processes
SELECT 
    blocking_session_id,
    session_id,
    wait_type,
    wait_time,
    wait_resource
FROM sys.dm_exec_requests 
WHERE blocking_session_id <> 0
```

### Example 3: Troubleshooting Common Issues

**Issue: Memory exhaustion**
```json
{
  "solution": {
    "configuration": {
      "global": {
        "batchSize": 5000,
        "parallelThreads": 2,
        "maxCacheSize": 100000
      }
    },
    "monitoring": "tasklist /fi \"imagename eq DataObfuscation.exe\" /fo table"
  }
}
```

**Issue: Database timeout**
```json
{
  "solution": {
    "configuration": {
      "global": {
        "commandTimeoutSeconds": 1800,
        "batchSize": 10000
      }
    },
    "database": "ALTER DATABASE MyDB SET READ_COMMITTED_SNAPSHOT ON"
  }
}
```

**Issue: Referential integrity violations**
```json
{
  "solution": {
    "configuration": {
      "referentialIntegrity": {
        "enabled": true,
        "relationships": [
          {
            "name": "FixBrokenRelationship",
            "primaryTable": "ParentTable",
            "primaryColumn": "ParentID",
            "relatedMappings": [
              {"table": "ChildTable", "column": "ParentID", "relationship": "exact"}
            ]
          }
        ]
      }
    }
  }
}
```

## 🔧 Advanced Scenarios

### Scenario 1: Custom Data Type Creation
**Use Case**: Creating industry-specific data types

```json
{
  "dataTypes": {
    "HeavyVehicleLicense": {
      "baseType": "DriverLicenseNumber",
      "customSeed": "HeavyVehicleSeed2024",
      "formatting": {
        "addPrefix": "HV-"
      },
      "validation": {
        "regex": "^HV-[A-Z]{3}-\\d{8}$"
      }
    },
    "DangerousGoodsLicense": {
      "baseType": "DriverLicenseNumber", 
      "customSeed": "DangerousGoodsSeed2024",
      "formatting": {
        "addPrefix": "DG-"
      }
    },
    "CommercialOperatorABN": {
      "baseType": "BusinessABN",
      "customSeed": "CommercialOperatorSeed2024",
      "validation": {
        "regex": "^\\d{2} \\d{3} \\d{3} \\d{3}$"
      }
    }
  }
}
```

This comprehensive set of examples covers the most common usage scenarios for the Data Obfuscation Tool, providing practical guidance for implementation in various environments and use cases.