using System.ComponentModel.DataAnnotations;

namespace Contracts.DTOs;

public class ColumnObfuscationMappingDto
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
    public string? DetectionReasons { get; set; }
    public bool IsManuallyConfigured { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    
    // Navigation properties
    public ProductDto? Product { get; set; }
    public TableColumnDto? TableColumn { get; set; }
}

public class CreateColumnObfuscationMappingDto
{
    [Required]
    [MaxLength(100)]
    public string ObfuscationDataType { get; set; } = string.Empty;
    
    public bool IsEnabled { get; set; } = true;
    public bool PreserveLength { get; set; } = true;
    public decimal ConfidenceScore { get; set; } = 0.0m;
    public string? DetectionReasons { get; set; }
    public bool IsManuallyConfigured { get; set; }
}

public class UpdateColumnObfuscationMappingDto
{
    [MaxLength(100)]
    public string? ObfuscationDataType { get; set; }
    
    public bool? IsEnabled { get; set; }
    public bool? PreserveLength { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string? DetectionReasons { get; set; }
    public bool? IsManuallyConfigured { get; set; }
}
