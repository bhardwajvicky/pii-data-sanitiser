namespace Common.Models;

/// <summary>
/// Configuration settings for the obfuscation process
/// This file contains all technical configuration without table/column details
/// </summary>
public class ObfuscationConfiguration
{
    public ConfigurationMetadata Metadata { get; set; } = new();
    public GlobalConfiguration Global { get; set; } = new();
    public Dictionary<string, CustomDataType> DataTypes { get; set; } = new();
    public ReferentialIntegrityConfiguration ReferentialIntegrity { get; set; } = new();
    public PostProcessingConfiguration PostProcessing { get; set; } = new();
}

public class ConfigurationMetadata
{
    public string ConfigVersion { get; set; } = "2.1";
    public string Description { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = "SchemaAnalyzer";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public string? MappingFileVersion { get; set; }
    public string? DatabaseName { get; set; }
}

public class GlobalConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public string GlobalSeed { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 10000;
    public int SqlBatchSize { get; set; } = 100;
    public int ParallelThreads { get; set; } = 4;
    public int MaxCacheSize { get; set; } = 100000;
    public bool DryRun { get; set; } = true;
    public bool PersistMappings { get; set; } = true;
    public bool EnableValueCaching { get; set; } = true;
    public int CommandTimeoutSeconds { get; set; } = 300;
    public string MappingCacheDirectory { get; set; } = "mappings";
}

public class CustomDataType
{
    public string BaseType { get; set; } = string.Empty;
    public string? CustomSeed { get; set; }
    public bool PreserveLength { get; set; } = false;
    public ValidationConfiguration? Validation { get; set; }
    public FormattingConfiguration? Formatting { get; set; }
    public TransformationConfiguration? Transformation { get; set; }
    public string? Description { get; set; }
}

public class ValidationConfiguration
{
    public string? Regex { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public List<string>? AllowedValues { get; set; }
    public string? CustomValidator { get; set; }
}

public class FormattingConfiguration
{
    public string? Pattern { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public bool? ToUpper { get; set; }
    public bool? ToLower { get; set; }
    public string? DateFormat { get; set; }
}

public class TransformationConfiguration
{
    public string? Method { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public string? CustomTransformer { get; set; }
}

public class ReferentialIntegrityConfiguration
{
    public bool Enabled { get; set; } = false;
    public List<RelationshipConfiguration> Relationships { get; set; } = new();
    public bool StrictMode { get; set; } = false;
    public string OnViolation { get; set; } = "warn"; // warn, skip, fail
}

public class RelationshipConfiguration
{
    public string ParentTable { get; set; } = string.Empty;
    public string ParentColumn { get; set; } = string.Empty;
    public string ChildTable { get; set; } = string.Empty;
    public string ChildColumn { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = "foreign_key";
    public bool Enabled { get; set; } = true;
}

public class PostProcessingConfiguration
{
    public bool GenerateReport { get; set; } = true;
    public string ReportPath { get; set; } = "reports/obfuscation-{timestamp}.json";
    public bool ValidateResults { get; set; } = true;
    public bool BackupMappings { get; set; } = true;
}