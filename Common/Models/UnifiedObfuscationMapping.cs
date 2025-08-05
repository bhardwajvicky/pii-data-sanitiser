using System.Text.Json.Serialization;

namespace Common.Models;

/// <summary>
/// Unified obfuscation mapping that combines configuration and table/column mappings
/// This replaces the need for separate config and mapping files
/// </summary>
public class UnifiedObfuscationMapping
{
    public GlobalConfiguration Global { get; set; } = new();
    public Dictionary<string, CustomDataType> DataTypes { get; set; } = new();
    public ReferentialIntegrityConfiguration ReferentialIntegrity { get; set; } = new();
    public PostProcessingConfiguration PostProcessing { get; set; } = new();
    public List<TableMapping> Tables { get; set; } = new();
} 