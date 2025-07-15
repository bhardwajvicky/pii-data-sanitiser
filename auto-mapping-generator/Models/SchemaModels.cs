namespace AutoMappingGenerator.Models;

public class DatabaseSchema
{
    public string DatabaseName { get; set; } = string.Empty;
    public List<TableInfo> Tables { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

public class TableInfo
{
    public string Schema { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string FullName => $"{Schema}.{TableName}";
    public List<ColumnInfo> Columns { get; set; } = new();
    public List<string> PrimaryKeyColumns { get; set; } = new();
    public long RowCount { get; set; }
}

public class ColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string SqlDataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsComputed { get; set; }
    public string? DefaultValue { get; set; }
    public int OrdinalPosition { get; set; }
    
    // Extended properties
    public byte? NumericPrecision { get; set; }
    public byte? NumericScale { get; set; }
    public string? CharacterSet { get; set; }
    public string? Collation { get; set; }
    public bool IsRowGuid { get; set; }
    public bool IsFileStream { get; set; }
    public bool IsSparse { get; set; }
    public bool IsXmlDocument { get; set; }
}

public class PIIAnalysisResult
{
    public string DatabaseName { get; set; } = string.Empty;
    public List<TableWithPII> TablesWithPII { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

public class TableWithPII
{
    public string Schema { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string FullName => $"{Schema}.{TableName}";
    public List<string> PrimaryKeyColumns { get; set; } = new();
    public long RowCount { get; set; }
    public List<PIIColumn> PIIColumns { get; set; } = new();
    public int Priority { get; set; } = 10;
}

public class PIIColumn
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string SqlDataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public bool IsNullable { get; set; }
    public double ConfidenceScore { get; set; }
    public double Confidence { get; set; } // Alias for compatibility
    public string TableName { get; set; } = string.Empty; // For Claude API compatibility
    public List<string> DetectionReasons { get; set; } = new();
    public bool PreserveLength { get; set; } = true;
}

public enum PIIDataType
{
    PersonName,
    EmailAddress,
    PhoneNumber,
    Address,
    PostalCode,
    CreditCardNumber,
    SSN,
    TaxFileNumber,
    BusinessNumber,
    LicenseNumber,
    BankAccount,
    IPAddress,
    URL,
    UserName,
    Password,
    Comments,
    Notes,
    Description,
    Date,
    DateOfBirth,
    Custom
}

public class PIIDetectionRule
{
    public PIIDataType DataType { get; set; }
    public string ObfuscationDataType { get; set; } = string.Empty;
    public List<string> ColumnNamePatterns { get; set; } = new();
    public List<string> SqlDataTypes { get; set; } = new();
    public List<string> TableNamePatterns { get; set; } = new();
    public double BaseConfidence { get; set; } = 0.5;
    public bool PreserveLength { get; set; } = true;
}