using Microsoft.Extensions.Logging;
using SchemaAnalyzer.Models;
using System.Text.Json.Serialization;

namespace SchemaAnalyzer.Core;

public interface IObfuscationConfigGenerator
{
    ObfuscationConfiguration GenerateObfuscationConfiguration(PIIAnalysisResult piiAnalysis, string connectionString);
}

public class ObfuscationConfigGenerator : IObfuscationConfigGenerator
{
    private readonly ILogger<ObfuscationConfigGenerator> _logger;

    public ObfuscationConfigGenerator(ILogger<ObfuscationConfigGenerator> logger)
    {
        _logger = logger;
    }

    public ObfuscationConfiguration GenerateObfuscationConfiguration(PIIAnalysisResult piiAnalysis, string connectionString)
    {
        _logger.LogInformation("Generating obfuscation configuration for {DatabaseName}", piiAnalysis.DatabaseName);

        var config = new ObfuscationConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                ConfigVersion = "2.1",
                Description = $"Auto-generated obfuscation configuration for {piiAnalysis.DatabaseName}",
                CreatedBy = "SchemaAnalyzer",
                CreatedDate = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            },
            Global = new GlobalConfiguration
            {
                ConnectionString = connectionString,
                GlobalSeed = $"{piiAnalysis.DatabaseName}Seed{DateTime.UtcNow:yyyyMMdd}",
                BatchSize = DetermineBatchSize(piiAnalysis),
                ParallelThreads = Environment.ProcessorCount,
                MaxCacheSize = DetermineCacheSize(piiAnalysis),
                DryRun = true, // Start with dry run for safety
                PersistMappings = true,
                EnableValueCaching = true,
                CommandTimeoutSeconds = 600,
                MappingCacheDirectory = $"mappings/{piiAnalysis.DatabaseName.ToLower()}"
            },
            DataTypes = GenerateCustomDataTypes(piiAnalysis),
            ReferentialIntegrity = GenerateReferentialIntegrity(piiAnalysis),
            Tables = GenerateTableConfigurations(piiAnalysis),
            PostProcessing = new PostProcessingConfiguration
            {
                GenerateReport = true,
                ReportPath = $"reports/{piiAnalysis.DatabaseName.ToLower()}-obfuscation-{{timestamp}}.json",
                ValidateResults = true,
                BackupMappings = true
            }
        };

        _logger.LogInformation("Generated configuration with {TableCount} tables and {DataTypeCount} custom data types",
            config.Tables.Count, config.DataTypes.Count);

        return config;
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
                dataTypeUsage[column.DataType] = dataTypeUsage.GetValueOrDefault(column.DataType, 0) + 1;
            }
        }

        // Create custom data types for frequently used types
        foreach (var usage in dataTypeUsage.Where(u => u.Value >= 2))
        {
            var customTypeName = $"{piiAnalysis.DatabaseName}{usage.Key}";
            dataTypes[customTypeName] = new CustomDataType
            {
                BaseType = usage.Key,
                CustomSeed = $"{customTypeName}Seed{DateTime.UtcNow:yyyyMMdd}",
                PreserveLength = ShouldPreserveLength(usage.Key),
                Validation = GenerateValidation(usage.Key)
            };
        }

        return dataTypes;
    }

    private ReferentialIntegrityConfiguration GenerateReferentialIntegrity(PIIAnalysisResult piiAnalysis)
    {
        var relationships = new List<RelationshipConfiguration>();

        // Group tables by common PII types to identify potential relationships
        var nameColumns = piiAnalysis.TablesWithPII
            .SelectMany(t => t.PIIColumns.Where(c => c.DataType == "DriverName")
                .Select(c => new { Table = t, Column = c }))
            .ToList();

        var emailColumns = piiAnalysis.TablesWithPII
            .SelectMany(t => t.PIIColumns.Where(c => c.DataType == "ContactEmail")
                .Select(c => new { Table = t, Column = c }))
            .ToList();

        // Create relationships for name consistency
        if (nameColumns.Count > 1)
        {
            var primaryTable = nameColumns
                .OrderBy(nc => nc.Table.Priority)
                .ThenByDescending(nc => nc.Table.RowCount)
                .First();

            var relatedMappings = nameColumns
                .Where(nc => nc.Table.FullName != primaryTable.Table.FullName)
                .Select(nc => new RelatedMapping
                {
                    Table = nc.Table.FullName,
                    Column = nc.Column.ColumnName,
                    Relationship = "exact"
                })
                .ToList();

            if (relatedMappings.Any())
            {
                relationships.Add(new RelationshipConfiguration
                {
                    Name = "PersonNameConsistency",
                    PrimaryTable = primaryTable.Table.FullName,
                    PrimaryColumn = primaryTable.Column.ColumnName,
                    RelatedMappings = relatedMappings
                });
            }
        }

        // Create relationships for email consistency
        if (emailColumns.Count > 1)
        {
            var primaryTable = emailColumns
                .OrderBy(ec => ec.Table.Priority)
                .ThenByDescending(ec => ec.Table.RowCount)
                .First();

            var relatedMappings = emailColumns
                .Where(ec => ec.Table.FullName != primaryTable.Table.FullName)
                .Select(ec => new RelatedMapping
                {
                    Table = ec.Table.FullName,
                    Column = ec.Column.ColumnName,
                    Relationship = "exact"
                })
                .ToList();

            if (relatedMappings.Any())
            {
                relationships.Add(new RelationshipConfiguration
                {
                    Name = "EmailConsistency",
                    PrimaryTable = primaryTable.Table.FullName,
                    PrimaryColumn = primaryTable.Column.ColumnName,
                    RelatedMappings = relatedMappings
                });
            }
        }

        return new ReferentialIntegrityConfiguration
        {
            Enabled = relationships.Any(),
            Relationships = relationships
        };
    }

    private List<TableConfiguration> GenerateTableConfigurations(PIIAnalysisResult piiAnalysis)
    {
        var tables = new List<TableConfiguration>();

        foreach (var tableWithPII in piiAnalysis.TablesWithPII.OrderBy(t => t.Priority))
        {
            var tableConfig = new TableConfiguration
            {
                TableName = tableWithPII.FullName,
                Priority = tableWithPII.Priority,
                PrimaryKey = tableWithPII.PrimaryKeyColumns,
                CustomBatchSize = DetermineTableBatchSize(tableWithPII),
                Conditions = GenerateTableConditions(tableWithPII),
                Columns = GenerateColumnConfigurations(tableWithPII)
            };

            tables.Add(tableConfig);
        }

        return tables;
    }

    private List<ColumnConfiguration> GenerateColumnConfigurations(TableWithPII table)
    {
        var columns = new List<ColumnConfiguration>();

        foreach (var piiColumn in table.PIIColumns)
        {
            var columnConfig = new ColumnConfiguration
            {
                ColumnName = piiColumn.ColumnName,
                DataType = piiColumn.DataType,
                Enabled = true,
                PreserveLength = piiColumn.PreserveLength,
                Conditions = piiColumn.IsNullable ? new ConditionsConfiguration { OnlyIfNotNull = true } : null,
                Fallback = new FallbackConfiguration
                {
                    OnError = DetermineFallbackStrategy(piiColumn),
                    DefaultValue = GenerateDefaultValue(piiColumn)
                },
                Validation = GenerateColumnValidation(piiColumn)
            };

            columns.Add(columnConfig);
        }

        return columns;
    }

    private ConditionsConfiguration? GenerateTableConditions(TableWithPII table)
    {
        // For very large tables, add row limits for initial testing
        if (table.RowCount > 1000000)
        {
            return new ConditionsConfiguration
            {
                MaxRows = 100000, // Limit for initial testing
                WhereClause = null // Can be customized later
            };
        }

        return null;
    }

    private int DetermineBatchSize(PIIAnalysisResult piiAnalysis)
    {
        var totalRows = piiAnalysis.TablesWithPII.Sum(t => t.RowCount);
        var avgRowsPerTable = totalRows / Math.Max(piiAnalysis.TablesWithPII.Count, 1);

        return avgRowsPerTable switch
        {
            > 1000000 => 25000,
            > 100000 => 15000,
            > 10000 => 10000,
            _ => 5000
        };
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
            "DriverLicenseNumber" => true,
            "BusinessABN" => true,
            "BusinessACN" => true,
            "DriverPhone" => true,
            _ => false
        };
    }

    private ValidationConfiguration? GenerateValidation(string dataType)
    {
        return dataType switch
        {
            "ContactEmail" => new ValidationConfiguration
            {
                Regex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
                MinLength = 5,
                MaxLength = 100
            },
            "DriverPhone" => new ValidationConfiguration
            {
                Regex = @"^(\+61|0)[2-478]\d{8}$",
                MinLength = 10,
                MaxLength = 15
            },
            "DriverName" => new ValidationConfiguration
            {
                MinLength = 2,
                MaxLength = 100
            },
            _ => null
        };
    }

    private ValidationConfiguration? GenerateColumnValidation(PIIColumn column)
    {
        var validation = GenerateValidation(column.DataType);
        
        if (validation != null && column.MaxLength.HasValue)
        {
            validation.MaxLength = Math.Min(validation.MaxLength ?? int.MaxValue, column.MaxLength.Value);
        }

        return validation;
    }

    private string DetermineFallbackStrategy(PIIColumn column)
    {
        return column.DataType switch
        {
            "DriverName" => "useDefault",
            "ContactEmail" => "useDefault", 
            "DriverPhone" => "useDefault",
            _ => "useOriginal"
        };
    }

    private string? GenerateDefaultValue(PIIColumn column)
    {
        return column.DataType switch
        {
            "DriverName" => "REDACTED_NAME",
            "ContactEmail" => "redacted@privacy.local",
            "DriverPhone" => "0400000000",
            "Address" => "REDACTED ADDRESS",
            _ => null
        };
    }
}

// Configuration classes that match the data-obfuscation project structure
public class ObfuscationConfiguration
{
    public MetadataConfiguration? Metadata { get; set; }
    public GlobalConfiguration Global { get; set; } = new();
    public Dictionary<string, CustomDataType> DataTypes { get; set; } = new();
    public ReferentialIntegrityConfiguration ReferentialIntegrity { get; set; } = new();
    public List<TableConfiguration> Tables { get; set; } = new();
    public PostProcessingConfiguration PostProcessing { get; set; } = new();
}

public class GlobalConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public string GlobalSeed { get; set; } = "DefaultSeed2024";
    public int BatchSize { get; set; } = 15000;
    public int ParallelThreads { get; set; } = Environment.ProcessorCount;
    public int MaxCacheSize { get; set; } = 1000000;
    public bool DryRun { get; set; } = false;
    public bool PersistMappings { get; set; } = true;
    public bool EnableValueCaching { get; set; } = true;
    public int CommandTimeoutSeconds { get; set; } = 600;
    public string? MappingCacheDirectory { get; set; } = "mappings";
}

public class CustomDataType
{
    public string BaseType { get; set; } = string.Empty;
    public string? CustomSeed { get; set; }
    public bool PreserveLength { get; set; } = false;
    public ValidationConfiguration? Validation { get; set; }
    public FormattingConfiguration? Formatting { get; set; }
    public TransformationConfiguration? Transformation { get; set; }
}

public class ValidationConfiguration
{
    public string? Regex { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public List<string>? AllowedValues { get; set; }
}

public class FormattingConfiguration
{
    public string? AddPrefix { get; set; }
    public string? AddSuffix { get; set; }
    public string? Pattern { get; set; }
}

public class TransformationConfiguration
{
    public List<string> PreProcess { get; set; } = new();
    public List<string> PostProcess { get; set; } = new();
}

public class ReferentialIntegrityConfiguration
{
    public bool Enabled { get; set; } = true;
    public List<RelationshipConfiguration> Relationships { get; set; } = new();
}

public class RelationshipConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string PrimaryTable { get; set; } = string.Empty;
    public string PrimaryColumn { get; set; } = string.Empty;
    public List<RelatedMapping> RelatedMappings { get; set; } = new();
}

public class RelatedMapping
{
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public string Relationship { get; set; } = "exact";
}

public class TableConfiguration
{
    public string TableName { get; set; } = string.Empty;
    public int Priority { get; set; } = 10;
    public ConditionsConfiguration? Conditions { get; set; }
    public int? CustomBatchSize { get; set; }
    public List<string> PrimaryKey { get; set; } = new();
    public List<ColumnConfiguration> Columns { get; set; } = new();
}

public class ConditionsConfiguration
{
    public string? WhereClause { get; set; }
    public int? MaxRows { get; set; }
    public bool OnlyIfNotNull { get; set; } = false;
    public string? ConditionalExpression { get; set; }
}

public class ColumnConfiguration
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool PreserveLength { get; set; } = false;
    public ConditionsConfiguration? Conditions { get; set; }
    public FallbackConfiguration? Fallback { get; set; }
    public ValidationConfiguration? Validation { get; set; }
    public TransformationConfiguration? Transformation { get; set; }
}

public class FallbackConfiguration
{
    public string OnError { get; set; } = "useOriginal";
    public string? DefaultValue { get; set; }
}

public class PostProcessingConfiguration
{
    public bool GenerateReport { get; set; } = true;
    public string ReportPath { get; set; } = "reports/obfuscation-{timestamp}.json";
    public bool ValidateResults { get; set; } = true;
    public bool BackupMappings { get; set; } = true;
}

public class MetadataConfiguration
{
    public string ConfigVersion { get; set; } = "1.0";
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastModified { get; set; }
}