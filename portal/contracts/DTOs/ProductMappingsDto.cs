namespace Contracts.DTOs;

public class ProductMappingsDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DatabaseTechnology { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    // Global settings for export/config editing
    public string ConnectionString { get; set; } = string.Empty;
    public string GlobalSeed { get; set; } = string.Empty;
    public int BatchSize { get; set; }
    public int SqlBatchSize { get; set; }
    public int ParallelThreads { get; set; }
    public int MaxCacheSize { get; set; }
    public int CommandTimeoutSeconds { get; set; }
    public string MappingCacheDirectory { get; set; } = string.Empty;
    public List<TableMappingDto> Tables { get; set; } = new();
}

public class TableMappingDto
{
    public Guid Id { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string FullTableName { get; set; } = string.Empty;
    public string? PrimaryKeyColumns { get; set; }
    public long RowCount { get; set; }
    public bool IsAnalyzed { get; set; }
    public DateTime? AnalyzedAt { get; set; }
    public List<ColumnMappingDto> Columns { get; set; } = new();
}

public class ColumnMappingDto
{
    public Guid Id { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string SqlDataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public int OrdinalPosition { get; set; }
    public ObfuscationMappingDto? ObfuscationMapping { get; set; }
}

public class ObfuscationMappingDto
{
    public Guid Id { get; set; }
    public string ObfuscationDataType { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool PreserveLength { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string? DetectionReasons { get; set; }
    public bool IsManuallyConfigured { get; set; }
}
