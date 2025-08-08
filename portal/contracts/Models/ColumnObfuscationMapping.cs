using System.ComponentModel.DataAnnotations;

namespace Contracts.Models;

public class ColumnObfuscationMapping
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid TableColumnId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string ObfuscationDataType { get; set; } = string.Empty;
    
    public bool IsEnabled { get; set; } = true;
    public bool PreserveLength { get; set; } = true;
    public decimal ConfidenceScore { get; set; } = 0.0m;
    public string? DetectionReasons { get; set; } // JSON array
    public bool IsManuallyConfigured { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    
    // Navigation properties
    public virtual Product? Product { get; set; }
    public virtual TableColumn? TableColumn { get; set; }
}
