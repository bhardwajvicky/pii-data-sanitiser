using System.ComponentModel.DataAnnotations;

namespace Contracts.Models;

public class DatabaseSchema
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
    
    public string? PrimaryKeyColumns { get; set; } // JSON array
    public long RowCount { get; set; }
    public bool IsAnalyzed { get; set; }
    public DateTime? AnalyzedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public virtual Product? Product { get; set; }
    public virtual ICollection<TableColumn> TableColumns { get; set; } = new List<TableColumn>();
}
