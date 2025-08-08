using System.ComponentModel.DataAnnotations;

namespace Contracts.DTOs;

public class TableColumnDto
{
    public Guid Id { get; set; }
    public Guid DatabaseSchemaId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string ColumnName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string SqlDataType { get; set; } = string.Empty;
    
    public int? MaxLength { get; set; }
    public bool IsNullable { get; set; } = true;
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
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public DatabaseSchemaDto? DatabaseSchema { get; set; }
    public ColumnObfuscationMappingDto? ColumnObfuscationMapping { get; set; }
}

public class CreateTableColumnDto
{
    [Required]
    [MaxLength(255)]
    public string ColumnName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string SqlDataType { get; set; } = string.Empty;
    
    public int? MaxLength { get; set; }
    public bool IsNullable { get; set; } = true;
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

public class UpdateTableColumnDto
{
    [MaxLength(255)]
    public string? ColumnName { get; set; }
    
    [MaxLength(100)]
    public string? SqlDataType { get; set; }
    
    public int? MaxLength { get; set; }
    public bool? IsNullable { get; set; }
    public bool? IsPrimaryKey { get; set; }
    public bool? IsForeignKey { get; set; }
    public bool? IsIdentity { get; set; }
    public bool? IsComputed { get; set; }
    public string? DefaultValue { get; set; }
    public int? OrdinalPosition { get; set; }
    
    // Extended properties
    public byte? NumericPrecision { get; set; }
    public byte? NumericScale { get; set; }
    public string? CharacterSet { get; set; }
    public string? Collation { get; set; }
    public bool? IsRowGuid { get; set; }
    public bool? IsFileStream { get; set; }
    public bool? IsSparse { get; set; }
    public bool? IsXmlDocument { get; set; }
}
