using System.ComponentModel.DataAnnotations;

namespace Contracts.Models;

public class ObfuscationConfiguration
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public string ConfigurationJson { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    
    // Navigation property
    public virtual Product? Product { get; set; }
}
