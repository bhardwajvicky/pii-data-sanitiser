using System.ComponentModel.DataAnnotations;

namespace Contracts.DTOs;

public class DatabaseSchemaDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string SchemaName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string TableName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string FullTableName { get; set; } = string.Empty;
    
    public string? PrimaryKeyColumns { get; set; }
    public long RowCount { get; set; }
    public bool IsAnalyzed { get; set; }
    public DateTime? AnalyzedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public ProductDto? Product { get; set; }
    public List<TableColumnDto> TableColumns { get; set; } = new();
}

public class CreateDatabaseSchemaDto
{
    [Required]
    [MaxLength(255)]
    public string SchemaName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string TableName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string FullTableName { get; set; } = string.Empty;
    
    public string? PrimaryKeyColumns { get; set; }
    public long RowCount { get; set; }
    public bool IsAnalyzed { get; set; }
    public DateTime? AnalyzedAt { get; set; }
}

public class UpdateDatabaseSchemaDto
{
    [MaxLength(255)]
    public string? SchemaName { get; set; }
    
    [MaxLength(255)]
    public string? TableName { get; set; }
    
    [MaxLength(500)]
    public string? FullTableName { get; set; }
    
    public string? PrimaryKeyColumns { get; set; }
    public long? RowCount { get; set; }
    public bool? IsAnalyzed { get; set; }
    public DateTime? AnalyzedAt { get; set; }
}
