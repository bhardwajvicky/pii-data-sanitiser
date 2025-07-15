using AutoMappingGenerator.Models;

namespace AutoMappingGenerator.Services;

/// <summary>
/// Common interface for LLM services (Claude, Azure OpenAI, etc.)
/// </summary>
public interface ILLMService
{
    /// <summary>
    /// Analyzes database schema to identify PII columns (all tables at once)
    /// </summary>
    Task<List<PIIColumn>> AnalyzeSchemaPIIAsync(DatabaseSchema schema);
    
    /// <summary>
    /// Gets the name of the LLM provider
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Checks if the service is properly configured
    /// </summary>
    bool IsConfigured { get; }
}