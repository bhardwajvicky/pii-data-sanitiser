using System.Text.Json.Serialization;

namespace DataObfuscation.Configuration;

public class ObfuscationConfiguration
{
    public GlobalConfiguration Global { get; set; } = new();
    public Dictionary<string, CustomDataType> DataTypes { get; set; } = new();
    public ReferentialIntegrityConfiguration ReferentialIntegrity { get; set; } = new();
    public List<TableConfiguration> Tables { get; set; } = new();
    public PostProcessingConfiguration PostProcessing { get; set; } = new();
    public MetadataConfiguration? Metadata { get; set; }
}

public class GlobalConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseTechnology { get; set; } = "SqlServer";
    public string GlobalSeed { get; set; } = "DefaultSeed2024";
    public int BatchSize { get; set; } = 15000;
    public int SqlBatchSize { get; set; } = 100;
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