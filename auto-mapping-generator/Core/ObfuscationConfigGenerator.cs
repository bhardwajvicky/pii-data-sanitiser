using Microsoft.Extensions.Logging;
using AutoMappingGenerator.Models;
using Common.Models;
using Common.DataTypes;
using System.Text.Json.Serialization;

namespace AutoMappingGenerator.Core;

public interface IObfuscationConfigGenerator
{
    UnifiedObfuscationMapping GenerateUnifiedObfuscationFile(PIIAnalysisResult piiAnalysis, string connectionString);
}

public class ObfuscationConfigGenerator : IObfuscationConfigGenerator
{
    private readonly ILogger<ObfuscationConfigGenerator> _logger;

    public ObfuscationConfigGenerator(ILogger<ObfuscationConfigGenerator> logger)
    {
        _logger = logger;
    }

    public UnifiedObfuscationMapping GenerateUnifiedObfuscationFile(PIIAnalysisResult piiAnalysis, string connectionString)
    {
        _logger.LogInformation("Generating unified obfuscation mapping file for {DatabaseName}", piiAnalysis.DatabaseName);

        var unifiedMapping = new UnifiedObfuscationMapping
        {
            Global = new GlobalConfiguration
            {
                ConnectionString = connectionString,
                GlobalSeed = "PII-Sanitizer-2024-CrossDB-Deterministic-AU-v3.7.2",
                BatchSize = DetermineBatchSize(piiAnalysis),
                SqlBatchSize = 500,
                ParallelThreads = 8,
                MaxCacheSize = DetermineCacheSize(piiAnalysis),
                DryRun = false,
                PersistMappings = true,
                EnableValueCaching = true,
                CommandTimeoutSeconds = 600,
                MappingCacheDirectory = $"mappings/{piiAnalysis.DatabaseName.ToLower()}"
            },
            DataTypes = GenerateCustomDataTypes(piiAnalysis),
            ReferentialIntegrity = new ReferentialIntegrityConfiguration
            {
                Enabled = false,
                Relationships = new List<RelationshipConfiguration>(),
                StrictMode = false,
                OnViolation = "warn"
            },
            PostProcessing = new PostProcessingConfiguration
            {
                GenerateReport = true,
                ReportPath = $"reports/{piiAnalysis.DatabaseName.ToLower()}-obfuscation-{{timestamp}}.json",
                ValidateResults = true,
                BackupMappings = true
            },
            Tables = GenerateTableMappings(piiAnalysis)
        };

        _logger.LogInformation("Generated unified mapping with {TableCount} tables and {DataTypeCount} custom data types",
            unifiedMapping.Tables.Count, unifiedMapping.DataTypes.Count);

        return unifiedMapping;
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
            "DriverName" => SupportedDataTypes.FullName,
            "AustralianFullName" => SupportedDataTypes.FullName,
            "ContactEmail" => SupportedDataTypes.Email,
            "Email" => SupportedDataTypes.Email,
            "EmailAddress" => SupportedDataTypes.Email,
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

    private Dictionary<string, CustomDataType> GenerateCustomDataTypes(PIIAnalysisResult piiAnalysis)
    {
        var dataTypes = new Dictionary<string, CustomDataType>();
        var dataTypeUsage = new Dictionary<string, int>();

        // Count usage of each data type
        foreach (var table in piiAnalysis.TablesWithPII)
        {
            foreach (var column in table.PIIColumns)
            {
                var standardDataType = MapToStandardDataType(column.DataType);
                dataTypeUsage[standardDataType] = dataTypeUsage.GetValueOrDefault(standardDataType, 0) + 1;
            }
        }

        // Only create custom data types for data types that are actually used
        // and only when they add meaningful value beyond just a custom seed
        foreach (var usage in dataTypeUsage)
        {
            var standardDataType = usage.Key;
            var usageCount = usage.Value;
            
            // Only create custom data types for frequently used types (2+ occurrences)
            // and only for types that benefit from custom configuration
            if (usageCount >= 2 && ShouldCreateCustomDataType(standardDataType))
            {
                // Check if this custom type would add meaningful value
                var validation = GenerateValidation(standardDataType);
                var shouldPreserveLength = ShouldPreserveLength(standardDataType);
                
                // Only create custom type if it adds validation, preserve length, or other meaningful config
                if (validation != null || shouldPreserveLength || HasSpecialRequirements(standardDataType))
                {
                    dataTypes[standardDataType] = new CustomDataType
                    {
                        BaseType = standardDataType,
                        CustomSeed = "PII-Sanitizer-2024-CrossDB-Deterministic-AU-v3.7.2",
                        PreserveLength = shouldPreserveLength,
                        Validation = validation,
                        Description = $"{standardDataType} with {piiAnalysis.DatabaseName}-specific configuration"
                    };
                }
                // If no special requirements, don't create a custom type - use the base type directly
            }
        }

        return dataTypes;
    }

    private bool ShouldCreateCustomDataType(string dataType)
    {
        // Only create custom data types for types that benefit from custom seeding
        // and are commonly used across multiple tables
        return dataType switch
        {
            SupportedDataTypes.FirstName => true,
            SupportedDataTypes.LastName => true,
            SupportedDataTypes.FullName => true,
            SupportedDataTypes.Email => true,
            SupportedDataTypes.Phone => true,
            SupportedDataTypes.AddressLine1 => true,
            SupportedDataTypes.City => true,
            SupportedDataTypes.State => true,
            SupportedDataTypes.PostCode => true,
            SupportedDataTypes.CompanyName => true,
            SupportedDataTypes.CreditCard => true,
            SupportedDataTypes.LicenseNumber => true,
            _ => false
        };
    }

    private int DetermineBatchSize(PIIAnalysisResult piiAnalysis)
    {
        // Conservative batch size of 2000 to avoid SQL parameter limits while maintaining performance
        return 2000;
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

    private ValidationConfiguration? GenerateValidation(string dataType)
    {
        return dataType switch
        {
            SupportedDataTypes.Email => new ValidationConfiguration
            {
                Regex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
                MinLength = 5,
                MaxLength = 100
            },
            SupportedDataTypes.Phone => new ValidationConfiguration
            {
                Regex = @"^(\+61|0)[2-478]\d{8}$",
                MinLength = 10,
                MaxLength = 15
            },
            SupportedDataTypes.FirstName => new ValidationConfiguration
            {
                MinLength = 2,
                MaxLength = 50
            },
            SupportedDataTypes.LastName => new ValidationConfiguration
            {
                MinLength = 2,
                MaxLength = 50
            },
            _ => null
        };
    }

    private bool HasSpecialRequirements(string dataType)
    {
        // Only create custom types for data types that have special requirements
        // beyond just a custom seed
        return dataType switch
        {
            SupportedDataTypes.CreditCard => true,  // Has validation and preserve length
            SupportedDataTypes.Phone => true,       // Has validation and preserve length
            SupportedDataTypes.LicenseNumber => true, // Has preserve length
            SupportedDataTypes.Email => true,       // Has validation
            SupportedDataTypes.FirstName => true,   // Has validation
            SupportedDataTypes.LastName => true,    // Has validation and preserve length
            _ => false
        };
    }
}