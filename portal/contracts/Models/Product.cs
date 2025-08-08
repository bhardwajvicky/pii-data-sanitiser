using System.ComponentModel.DataAnnotations;

namespace Contracts.Models;

public class Product
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string DatabaseTechnology { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? GlobalSeed { get; set; }
    
    public int BatchSize { get; set; } = 2000;
    public int SqlBatchSize { get; set; } = 500;
    public int ParallelThreads { get; set; } = 8;
    public int MaxCacheSize { get; set; } = 500000;
    public int CommandTimeoutSeconds { get; set; } = 600;
    
    [MaxLength(500)]
    public string? MappingCacheDirectory { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    
    // Navigation properties
    public virtual ICollection<DatabaseSchema> DatabaseSchemas { get; set; } = new List<DatabaseSchema>();
    public virtual ICollection<ObfuscationConfiguration> ObfuscationConfigurations { get; set; } = new List<ObfuscationConfiguration>();
    public virtual ICollection<ColumnObfuscationMapping> ColumnObfuscationMappings { get; set; } = new List<ColumnObfuscationMapping>();
}
