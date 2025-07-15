using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutoMappingGenerator.Services;

public enum LLMProvider
{
    Claude,
    AzureOpenAI
}

public interface ILLMProviderFactory
{
    ILLMService CreateLLMService();
    LLMProvider GetConfiguredProvider();
}

public class LLMProviderFactory : ILLMProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LLMProviderFactory> _logger;

    public LLMProviderFactory(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<LLMProviderFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public ILLMService CreateLLMService()
    {
        var provider = GetConfiguredProvider();
        
        switch (provider)
        {
            case LLMProvider.Claude:
                var claudeService = _serviceProvider.GetService<IClaudeApiService>();
                if (claudeService == null || !claudeService.IsConfigured)
                {
                    _logger.LogError("Claude API service is not properly configured");
                    throw new InvalidOperationException("Claude API key is missing or invalid");
                }
                return claudeService;
                
            case LLMProvider.AzureOpenAI:
                var azureService = _serviceProvider.GetService<AzureOpenAIService>();
                if (azureService == null || !azureService.IsConfigured)
                {
                    _logger.LogError("Azure OpenAI service is not properly configured");
                    throw new InvalidOperationException("Azure OpenAI API key is missing or invalid");
                }
                return azureService;
                
            default:
                throw new NotSupportedException($"LLM provider {provider} is not supported");
        }
    }

    public LLMProvider GetConfiguredProvider()
    {
        // Check configuration for explicit provider setting
        var providerSetting = _configuration["LLMProvider"];
        if (!string.IsNullOrEmpty(providerSetting))
        {
            if (Enum.TryParse<LLMProvider>(providerSetting, true, out var explicitProvider))
            {
                _logger.LogInformation("Using explicitly configured LLM provider: {Provider}", explicitProvider);
                return explicitProvider;
            }
        }

        // Auto-detect based on available API keys
        var claudeApiKey = _configuration["ClaudeApiKey"];
        var azureOpenAiApiKey = _configuration["AzureOpenAiApiKey"];

        // Prefer Claude if both are configured
        if (!string.IsNullOrEmpty(claudeApiKey))
        {
            _logger.LogInformation("Auto-detected Claude API key, using Claude as LLM provider");
            return LLMProvider.Claude;
        }

        if (!string.IsNullOrEmpty(azureOpenAiApiKey))
        {
            _logger.LogInformation("Auto-detected Azure OpenAI API key, using Azure OpenAI as LLM provider");
            return LLMProvider.AzureOpenAI;
        }

        _logger.LogWarning("No LLM API keys found in configuration. Defaulting to Claude.");
        return LLMProvider.Claude;
    }
}