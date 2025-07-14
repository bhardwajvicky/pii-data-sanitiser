using Microsoft.Extensions.Logging;
using SchemaAnalyzer.Models;
using DataObfuscation.Common.Models;
using DataObfuscation.Common.DataTypes;
using System.Text.Json.Serialization;

namespace SchemaAnalyzer.Core;

public interface IObfuscationConfigGenerator
{
    (TableColumnMapping mapping, ObfuscationConfiguration config) GenerateObfuscationFiles(PIIAnalysisResult piiAnalysis, string connectionString);
}

public class ObfuscationConfigGenerator : IObfuscationConfigGenerator
{
    private readonly ILogger<ObfuscationConfigGenerator> _logger;

    public ObfuscationConfigGenerator(ILogger<ObfuscationConfigGenerator> logger)
    {
        _logger = logger;
    }

    public (TableColumnMapping mapping, ObfuscationConfiguration config) GenerateObfuscationFiles(PIIAnalysisResult piiAnalysis, string connectionString)
    {
        _logger.LogInformation("Generating obfuscation configuration files for {DatabaseName}", piiAnalysis.DatabaseName);

        // Generate table/column mapping file
        var mapping = GenerateTableColumnMapping(piiAnalysis);
        
        // Generate configuration file
        var config = GenerateObfuscationConfiguration(piiAnalysis, connectionString);

        _logger.LogInformation("Generated mapping with {TableCount} tables and configuration with {DataTypeCount} custom data types",
            mapping.Tables.Count, config.DataTypes.Count);

        return (mapping, config);
    }

    private TableColumnMapping GenerateTableColumnMapping(PIIAnalysisResult piiAnalysis)
    {
        var totalPiiColumns = piiAnalysis.TablesWithPII.Sum(t => t.PIIColumns.Count);
        var totalColumns = piiAnalysis.TablesWithPII.Sum(t => t.PIIColumns.Count); // Only PII columns in this mapping
        
        var mapping = new TableColumnMapping
        {
            Metadata = new MappingMetadata
            {
                ConfigVersion = "2.1",
                Description = $"Table and column mappings for {piiAnalysis.DatabaseName} database",
                CreatedBy = "SchemaAnalyzer",
                CreatedDate = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                DatabaseName = piiAnalysis.DatabaseName,
                TotalTables = piiAnalysis.TablesWithPII.Count,
                TotalColumns = (int)totalColumns,
                TotalPiiColumns = totalPiiColumns
            },
            Tables = GenerateTableMappings(piiAnalysis)
        };

        return mapping;
    }

    private ObfuscationConfiguration GenerateObfuscationConfiguration(PIIAnalysisResult piiAnalysis, string connectionString)
    {
        var config = new ObfuscationConfiguration
        {
            Metadata = new ConfigurationMetadata
            {
                ConfigVersion = "2.1",
                Description = $"Obfuscation configuration for {piiAnalysis.DatabaseName} database",
                CreatedBy = "SchemaAnalyzer",
                CreatedDate = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                MappingFileVersion = "2.1",
                DatabaseName = piiAnalysis.DatabaseName
            },
            Global = new DataObfuscation.Common.Models.GlobalConfiguration
            {
                ConnectionString = connectionString,
                GlobalSeed = "DataObfuscation2024-CrossDB-Deterministic-AU-v3.7.2",
                BatchSize = DetermineBatchSize(piiAnalysis),
                ParallelThreads = 8,
                MaxCacheSize = DetermineCacheSize(piiAnalysis),
                DryRun = false,
                PersistMappings = true,
                EnableValueCaching = true,
                CommandTimeoutSeconds = 600,
                MappingCacheDirectory = $"mappings/{piiAnalysis.DatabaseName.ToLower()}",
                LogLevel = "Information",
                EnableProgressTracking = true
            },
            DataTypes = GenerateCustomDataTypes(piiAnalysis),
            ReferentialIntegrity = new DataObfuscation.Common.Models.ReferentialIntegrityConfiguration
            {
                Enabled = false,
                Relationships = new List<DataObfuscation.Common.Models.RelationshipConfiguration>(),
                StrictMode = false,
                OnViolation = "warn"
            },
            PostProcessing = new DataObfuscation.Common.Models.PostProcessingConfiguration
            {
                GenerateReport = true,
                ReportPath = $"reports/{piiAnalysis.DatabaseName.ToLower()}-obfuscation-{{timestamp}}.json",
                ValidateResults = true,
                BackupMappings = true,
                CompressMappings = false,
                GenerateSummary = true,
                NotificationEndpoints = new List<string>()
            },
            Performance = new DataObfuscation.Common.Models.PerformanceConfiguration
            {
                MaxMemoryUsageMB = 4096,
                BufferSize = 8192,
                EnableParallelProcessing = true,
                MaxDegreeOfParallelism = 4,
                OptimizeForThroughput = true,
                ConnectionPoolSize = 20
            },
            Security = new DataObfuscation.Common.Models.SecurityConfiguration
            {
                EncryptMappings = false,
                EncryptionKey = null,
                HashSensitiveData = false,
                AuditEnabled = true,
                AuditLogPath = $"audit/{piiAnalysis.DatabaseName.ToLower()}-audit-{{timestamp}}.log",
                SensitiveConfigKeys = new List<string> { "ConnectionString", "EncryptionKey" }
            }
        };

        return config;
    }

    private List<TableMapping> GenerateTableMappings(PIIAnalysisResult piiAnalysis)
    {
        var tables = new List<TableMapping>();

        foreach (var tableWithPII in piiAnalysis.TablesWithPII.OrderBy(t => t.Priority))
        {
            var tableMapping = new TableMapping
            {
                TableName = tableWithPII.TableName,
                Schema = tableWithPII.Schema,
                FullTableName = tableWithPII.FullName,
                PrimaryKey = tableWithPII.PrimaryKeyColumns,
                TotalRows = (int)tableWithPII.RowCount,
                Enabled = true,
                Columns = GenerateColumnMappings(tableWithPII)
            };

            tables.Add(tableMapping);
        }

        return tables;
    }

    private List<ColumnMapping> GenerateColumnMappings(TableWithPII table)
    {
        var columns = new List<ColumnMapping>();

        foreach (var piiColumn in table.PIIColumns)
        {
            var columnMapping = new ColumnMapping
            {
                ColumnName = piiColumn.ColumnName,
                DataType = MapToStandardDataType(piiColumn.DataType),
                Enabled = true,
                IsNullable = piiColumn.IsNullable,
                PreserveLength = ShouldPreserveLength(piiColumn.DataType)
            };

            columns.Add(columnMapping);
        }

        return columns;
    }

    private string MapToStandardDataType(string originalDataType)
    {
        return originalDataType switch
        {
            "DriverName" => SupportedDataTypes.FirstName,
            "AustralianFullName" => SupportedDataTypes.FullName,
            "ContactEmail" => SupportedDataTypes.Email,
            "DriverPhone" => SupportedDataTypes.Phone,
            "Address" => SupportedDataTypes.AddressLine1,
            "OperatorName" => SupportedDataTypes.CompanyName,
            "StoreName" => SupportedDataTypes.CompanyName,
            "VendorName" => SupportedDataTypes.CompanyName,
            "PostalCode" => SupportedDataTypes.PostCode,
            "PostCode" => SupportedDataTypes.PostCode,
            _ => originalDataType
        };
    }


    private Dictionary<string, DataObfuscation.Common.Models.CustomDataType> GenerateCustomDataTypes(PIIAnalysisResult piiAnalysis)
    {
        var dataTypes = new Dictionary<string, DataObfuscation.Common.Models.CustomDataType>();
        var dataTypeUsage = new Dictionary<string, int>();

        // Count usage of each data type
        foreach (var table in piiAnalysis.TablesWithPII)
        {
            foreach (var column in table.PIIColumns)
            {
                dataTypeUsage[column.DataType] = dataTypeUsage.GetValueOrDefault(column.DataType, 0) + 1;
            }
        }

        // Create custom data types for frequently used types
        foreach (var usage in dataTypeUsage.Where(u => u.Value >= 2))
        {
            var customTypeName = $"{piiAnalysis.DatabaseName}{MapToStandardDataType(usage.Key)}";
            dataTypes[customTypeName] = new DataObfuscation.Common.Models.CustomDataType
            {
                BaseType = MapToStandardDataType(usage.Key),
                CustomSeed = "DataObfuscation2024-CrossDB-Deterministic-AU-v3.7.2",
                PreserveLength = ShouldPreserveLength(usage.Key),
                Validation = GenerateValidation(MapToStandardDataType(usage.Key)),
                Description = $"{MapToStandardDataType(usage.Key)} with {piiAnalysis.DatabaseName}-specific seeding"
            };
        }

        return dataTypes;
    }





    private int DetermineBatchSize(PIIAnalysisResult piiAnalysis)
    {
        // Fixed batch size of 1000 for all tables
        return 1000;
    }

    private int? DetermineTableBatchSize(TableWithPII table)
    {
        return table.RowCount switch
        {
            > 5000000 => 50000,
            > 1000000 => 25000,
            > 100000 => 15000,
            > 10000 => 10000,
            < 1000 => 500,
            _ => null // Use global default
        };
    }

    private int DetermineCacheSize(PIIAnalysisResult piiAnalysis)
    {
        var totalPIIColumns = piiAnalysis.TablesWithPII.Sum(t => t.PIIColumns.Count);
        var estimatedMappings = totalPIIColumns * 1000; // Rough estimate

        return estimatedMappings switch
        {
            > 5000000 => 10000000,
            > 1000000 => 5000000,
            > 100000 => 1000000,
            _ => 500000
        };
    }

    private bool ShouldPreserveLength(string dataType)
    {
        return dataType switch
        {
            SupportedDataTypes.LicenseNumber => true,
            SupportedDataTypes.CreditCard => true,
            SupportedDataTypes.Phone => true,
            SupportedDataTypes.LastName => true,
            _ => false
        };
    }

    private DataObfuscation.Common.Models.ValidationConfiguration? GenerateValidation(string dataType)
    {
        return dataType switch
        {
            SupportedDataTypes.Email => new DataObfuscation.Common.Models.ValidationConfiguration
            {
                Regex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
                MinLength = 5,
                MaxLength = 100
            },
            SupportedDataTypes.Phone => new DataObfuscation.Common.Models.ValidationConfiguration
            {
                Regex = @"^(\+61|0)[2-478]\d{8}$",
                MinLength = 10,
                MaxLength = 15
            },
            SupportedDataTypes.FirstName => new DataObfuscation.Common.Models.ValidationConfiguration
            {
                MinLength = 2,
                MaxLength = 50
            },
            SupportedDataTypes.LastName => new DataObfuscation.Common.Models.ValidationConfiguration
            {
                MinLength = 2,
                MaxLength = 50
            },
            _ => null
        };
    }



}