using System.ComponentModel.DataAnnotations;

namespace Contracts.Models;

public class TableColumn
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
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual DatabaseSchema? DatabaseSchema { get; set; }
    public virtual ColumnObfuscationMapping? ColumnObfuscationMapping { get; set; }
}
