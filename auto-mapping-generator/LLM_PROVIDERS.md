# LLM Provider Support

## Overview
The auto-mapping-generator now supports multiple Large Language Model (LLM) providers for enhanced PII detection:
- **Claude** (Anthropic) - Original implementation
- **Azure OpenAI** - New support added

## Configuration

### Option 1: Automatic Detection
The system will automatically detect which provider to use based on available API keys:
1. If `ClaudeApiKey` is present → Uses Claude
2. If `AzureOpenAiApiKey` is present → Uses Azure OpenAI
3. If both are present → Defaults to Claude

### Option 2: Explicit Configuration
You can explicitly set the provider in `appsettings.json`:

```json
{
  "LLMProvider": "AzureOpenAI", // or "Claude"
  "ClaudeApiKey": "sk-ant-api...",
  "AzureOpenAiApiKey": "your-azure-key",
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com",
    "DeploymentName": "gpt-4"
  }
}
```

## Provider-Specific Configuration

### Claude Configuration
```json
{
  "ClaudeApiKey": "sk-ant-api..."
}
```
- Uses Anthropic's Claude API
- Endpoint: https://api.anthropic.com/v1/messages
- Model: Claude (version managed by API)

### Azure OpenAI Configuration
```json
{
  "AzureOpenAiApiKey": "your-azure-key",
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com",
    "DeploymentName": "gpt-4"
  }
}
```
- Uses your Azure OpenAI deployment
- Requires Azure resource endpoint
- Specify your deployment name (e.g., "gpt-4", "gpt-35-turbo")

## Environment Variables

Both providers support environment variables:

```bash
# For Claude
export CLAUDE_API_KEY="sk-ant-api..."

# For Azure OpenAI
export AZURE_OPENAI_API_KEY="your-azure-key"
```

## Usage Examples

### Example 1: Using Claude (Default)
```json
{
  "ConnectionString": "Server=localhost;Database=MyDB;...",
  "ClaudeApiKey": "sk-ant-api..."
}
```

### Example 2: Using Azure OpenAI
```json
{
  "ConnectionString": "Server=localhost;Database=MyDB;...",
  "LLMProvider": "AzureOpenAI",
  "AzureOpenAiApiKey": "your-key",
  "AzureOpenAI": {
    "Endpoint": "https://my-openai.openai.azure.com",
    "DeploymentName": "gpt-4"
  }
}
```

### Example 3: Multiple Keys with Explicit Selection
```json
{
  "ConnectionString": "Server=localhost;Database=MyDB;...",
  "LLMProvider": "AzureOpenAI", // Explicitly choose Azure even though Claude key exists
  "ClaudeApiKey": "sk-ant-api...",
  "AzureOpenAiApiKey": "your-key",
  "AzureOpenAI": {
    "Endpoint": "https://my-openai.openai.azure.com",
    "DeploymentName": "gpt-4"
  }
}
```

## How It Works

1. **LLM Provider Factory**: Determines which provider to use based on configuration
2. **Common Interface**: Both providers implement `ILLMService` interface
3. **Schema-Only Analysis**: Both providers analyze only schema metadata, never actual data
4. **Fallback Support**: If LLM analysis fails, falls back to pattern-based detection

## Provider Comparison

| Feature | Claude | Azure OpenAI |
|---------|--------|--------------|
| API Key Required | Yes | Yes |
| Additional Config | No | Endpoint & Deployment |
| Response Format | JSON | JSON |
| Schema Analysis | ✓ | ✓ |
| Data Sampling | ✗ | ✗ |
| Cost Model | Per API call | Azure consumption |

## Logging

The application logs which provider is being used:
```
[INFO] Using LLM Provider: AzureOpenAI
[INFO] Starting enhanced PII detection with Azure OpenAI (schema-only)
```

## Error Handling

If the selected provider is not properly configured:
```
[ERROR] Azure OpenAI service is not properly configured
InvalidOperationException: Azure OpenAI API key is missing or invalid
```

The system will fail fast with clear error messages about missing configuration.

## Adding New Providers

To add support for a new LLM provider:

1. Create a new service implementing `ILLMService`
2. Add configuration properties in `appsettings.json`
3. Register the service in `Program.cs`
4. Update `LLMProviderFactory` to handle the new provider
5. Add the provider to the `LLMProvider` enum

## Security Considerations

- Never commit API keys to source control
- Use environment variables or secure key vaults in production
- API keys are only used for schema analysis, not data access
- All LLM calls contain only table/column metadata, no actual data