using System.ComponentModel.DataAnnotations;

namespace Contracts.DTOs;

public class ProductDto
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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public class CreateProductDto
{
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
}

public class UpdateProductDto
{
    [MaxLength(255)]
    public string? Name { get; set; }
    
    public string? Description { get; set; }
    
    public string? ConnectionString { get; set; }
    
    [MaxLength(50)]
    public string? DatabaseTechnology { get; set; }
    
    [MaxLength(255)]
    public string? GlobalSeed { get; set; }
    
    public int? BatchSize { get; set; }
    public int? SqlBatchSize { get; set; }
    public int? ParallelThreads { get; set; }
    public int? MaxCacheSize { get; set; }
    public int? CommandTimeoutSeconds { get; set; }
    
    [MaxLength(500)]
    public string? MappingCacheDirectory { get; set; }
    
    public bool? IsActive { get; set; }
}
