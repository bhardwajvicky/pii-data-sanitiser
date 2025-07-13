namespace DataObfuscation.Common.Models;

/// <summary>
/// Clean, user-friendly view of table and column mappings for obfuscation
/// This file shows only what tables/columns will be processed and their PII types
/// </summary>
public class TableColumnMapping
{
    public MappingMetadata Metadata { get; set; } = new();
    public List<TableMapping> Tables { get; set; } = new();
}

public class MappingMetadata
{
    public string ConfigVersion { get; set; } = "2.1";
    public string Description { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = "SchemaAnalyzer";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public string DatabaseName { get; set; } = string.Empty;
    public int TotalTables { get; set; }
    public int TotalColumns { get; set; }
    public int TotalPiiColumns { get; set; }
}

public class TableMapping
{
    public string TableName { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string FullTableName { get; set; } = string.Empty;
    public List<string> PrimaryKey { get; set; } = new();
    public List<ColumnMapping> Columns { get; set; } = new();
    public int TotalRows { get; set; } = 0;
    public bool Enabled { get; set; } = true;
    public string? Notes { get; set; }
}

public class ColumnMapping
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string SqlDataType { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public double Confidence { get; set; } = 1.0;
    public string? Reasoning { get; set; }
    public bool PreserveLength { get; set; } = false;
    public string? Notes { get; set; }
}

/// <summary>
/// Summary statistics for the mapping file
/// </summary>
public class MappingSummary
{
    public int TotalTables { get; set; }
    public int EnabledTables { get; set; }
    public int TotalColumns { get; set; }
    public int EnabledColumns { get; set; }
    public Dictionary<string, int> DataTypeCount { get; set; } = new();
    public Dictionary<string, int> TablesBySchema { get; set; } = new();
    public List<string> RequiredDataTypes { get; set; } = new();
}