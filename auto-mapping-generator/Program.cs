using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using AutoMappingGenerator.Core;
using AutoMappingGenerator.Services;
using AutoMappingGenerator.Models;

namespace AutoMappingGenerator;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            var host = CreateHostBuilder(args).Build();
            
            using var scope = host.Services.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var schemaService = scope.ServiceProvider.GetRequiredService<ISchemaAnalysisService>();
            var enhancedPIIService = scope.ServiceProvider.GetRequiredService<IEnhancedPIIDetectionService>();
            var configGenerator = scope.ServiceProvider.GetRequiredService<IObfuscationConfigGenerator>();
            var llmProviderFactory = scope.ServiceProvider.GetRequiredService<ILLMProviderFactory>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            var connectionString = configuration.GetConnectionString("DefaultConnection") ?? 
                                 configuration["ConnectionString"];
            
            if (string.IsNullOrEmpty(connectionString))
            {
                logger.LogError("Connection string not provided. Please set ConnectionString in appsettings.json or provide as argument.");
                return 1;
            }
            
            // Extract database name from connection string
            var databaseName = ExtractDatabaseName(connectionString);
            
            // Log which LLM provider is being used
            var llmProvider = llmProviderFactory.GetConfiguredProvider();
            logger.LogInformation("Using explicitly configured LLM provider: {Provider}", llmProvider);

            logger.LogInformation("Starting Schema Analysis for database: {DatabaseName}", databaseName);

            // Step 1: Analyze database schema (structure only, no data sampling)
            var schemaInfo = await schemaService.AnalyzeDatabaseSchemaAsync(connectionString);
            
            logger.LogInformation("Found {TableCount} tables with {ColumnCount} columns total", 
                schemaInfo.Tables.Count, schemaInfo.Tables.Sum(t => t.Columns.Count));

            // Step 2: Identify PII columns using enhanced detection with LLM API
            var piiAnalysis = await enhancedPIIService.IdentifyPIIColumnsAsync(schemaInfo);

            logger.LogInformation("Identified {PIITableCount} tables with PII data containing {PIIColumnCount} PII columns",
                piiAnalysis.TablesWithPII.Count, piiAnalysis.TablesWithPII.Sum(t => t.PIIColumns.Count));

            // Step 3: Generate unified obfuscation mapping file
            var unifiedMapping = configGenerator.GenerateUnifiedObfuscationFile(piiAnalysis, connectionString);

            // Step 4: Save output file
            var outputDirectory = configuration["OutputOptions:OutputDirectory"] ?? "../JSON";
            Directory.CreateDirectory(outputDirectory);
            
            // Save unified mapping file
            var mappingPath = Path.Combine(outputDirectory, $"{databaseName}-mapping.json");
            await File.WriteAllTextAsync(mappingPath, 
                System.Text.Json.JsonSerializer.Serialize(unifiedMapping, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }));

            logger.LogInformation("Unified obfuscation mapping saved to: {MappingPath}", mappingPath);
            logger.LogInformation("Analysis completed successfully");
            
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;
                var claudeApiKey = configuration["ClaudeApiKey"] ?? "";
                var azureOpenAiApiKey = configuration["AzureOpenAiApiKey"] ?? "";
                var azureEndpoint = configuration["AzureOpenAI:Endpoint"] ?? "https://your-resource.openai.azure.com";
                var azureDeployment = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4";
                
                services.AddHttpClient();
                services.AddScoped<ISchemaAnalysisService, SchemaAnalysisService>();
                services.AddScoped<ISchemaStorageService, SchemaStorageService>();
                services.AddScoped<IObfuscationConfigGenerator, ObfuscationConfigGenerator>();
                services.AddScoped<IPIIDetectionService, PIIDetectionService>();
                
                // Register Claude API Service
                services.AddScoped<IClaudeApiService>(provider => 
                    new ClaudeApiService(
                        provider.GetRequiredService<HttpClient>(),
                        provider.GetRequiredService<ILogger<ClaudeApiService>>(),
                        claudeApiKey));
                
                // Register Azure OpenAI Service
                services.AddScoped<AzureOpenAIService>(provider => 
                    new AzureOpenAIService(
                        provider.GetRequiredService<HttpClient>(),
                        provider.GetRequiredService<ILogger<AzureOpenAIService>>(),
                        azureOpenAiApiKey,
                        azureEndpoint,
                        azureDeployment));
                
                // Register LLM Provider Factory
                services.AddScoped<ILLMProviderFactory, LLMProviderFactory>();
                
                // Register Enhanced PII Detection Service with factory
                services.AddScoped<IEnhancedPIIDetectionService>(provider =>
                {
                    var factory = provider.GetRequiredService<ILLMProviderFactory>();
                    var llmService = factory.CreateLLMService();
                    
                    return new EnhancedPIIDetectionService(
                        llmService,
                        provider.GetRequiredService<ISchemaAnalysisService>(),
                        provider.GetRequiredService<ILogger<EnhancedPIIDetectionService>>());
                });
            });

    private static string ExtractDatabaseName(string connectionString)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        
        if (string.IsNullOrEmpty(databaseName))
        {
            // Try to find Database= or Initial Catalog= in the connection string
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var keyValue = part.Split('=', 2);
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim();
                    var value = keyValue[1].Trim();
                    
                    if (key.Equals("Database", StringComparison.OrdinalIgnoreCase) || 
                        key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
                    {
                        databaseName = value;
                        break;
                    }
                }
            }
        }
        
        return string.IsNullOrEmpty(databaseName) ? "UnknownDatabase" : databaseName;
    }
}