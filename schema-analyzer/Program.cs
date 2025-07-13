using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using SchemaAnalyzer.Core;
using SchemaAnalyzer.Services;
using SchemaAnalyzer.Models;

namespace SchemaAnalyzer;

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
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            var connectionString = configuration.GetConnectionString("DefaultConnection") ?? 
                                 configuration["ConnectionString"] ?? 
                                 "Server=localhost;Database=AdventureWorks2019;User Id=sa;Password=Count123#;TrustServerCertificate=true;";
            var databaseName = "AdventureWorks2019";
            var outputDirectory = "../JSON";

            logger.LogInformation("Starting Enhanced Schema Analysis for database: {DatabaseName}", databaseName);

            // Analyze database schema
            var schemaInfo = await schemaService.AnalyzeDatabaseSchemaAsync(connectionString);
            
            logger.LogInformation("Found {TableCount} tables with {ColumnCount} columns total", 
                schemaInfo.Tables.Count, schemaInfo.Tables.Sum(t => t.Columns.Count));

            // Use enhanced PII detection with Claude API
            var piiColumns = await enhancedPIIService.IdentifyPIIColumnsAsync(schemaInfo, connectionString);

            // Create PII analysis result
            var piiAnalysis = CreatePIIAnalysisResult(schemaInfo, piiColumns);
            
            logger.LogInformation("Identified {PIITableCount} tables with PII data containing {PIIColumnCount} PII columns",
                piiAnalysis.TablesWithPII.Count, piiAnalysis.TablesWithPII.Sum(t => t.PIIColumns.Count));

            // Generate obfuscation configuration files
            var (mapping, config) = configGenerator.GenerateObfuscationFiles(piiAnalysis, connectionString);

            // Ensure output directory exists
            Directory.CreateDirectory(outputDirectory);
            
            // Save mapping file
            var mappingPath = Path.Combine(outputDirectory, $"{databaseName}-mapping.json");
            await File.WriteAllTextAsync(mappingPath, 
                System.Text.Json.JsonSerializer.Serialize(mapping, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }));

            // Save configuration file
            var configPath = Path.Combine(outputDirectory, $"{databaseName}-config.json");
            await File.WriteAllTextAsync(configPath, 
                System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }));

            logger.LogInformation("Table/column mapping saved to: {MappingPath}", mappingPath);
            logger.LogInformation("Obfuscation configuration saved to: {ConfigPath}", configPath);

            // Generate summary report
            var summaryPath = Path.Combine(outputDirectory, $"{databaseName}_analysis_summary.json");
            var summary = new
            {
                DatabaseName = databaseName,
                AnalysisTimestamp = DateTime.UtcNow,
                TotalTables = schemaInfo.Tables.Count,
                TotalColumns = schemaInfo.Tables.Sum(t => t.Columns.Count),
                TablesWithPII = piiAnalysis.TablesWithPII.Count,
                PIIColumns = piiAnalysis.TablesWithPII.Sum(t => t.PIIColumns.Count),
                PIIColumnsByType = piiAnalysis.TablesWithPII
                    .SelectMany(t => t.PIIColumns)
                    .GroupBy(c => c.DataType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                TablesAnalyzed = piiAnalysis.TablesWithPII.Select(t => new
                {
                    TableName = t.TableName,
                    Schema = t.Schema,
                    PIIColumnCount = t.PIIColumns.Count,
                    PIIColumns = t.PIIColumns.Select(c => new
                    {
                        c.ColumnName,
                        c.DataType,
                        c.SqlDataType,
                        c.MaxLength,
                        c.IsNullable
                    })
                })
            };

            await File.WriteAllTextAsync(summaryPath,
                System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }));

            logger.LogInformation("Analysis summary saved to: {SummaryPath}", summaryPath);
            logger.LogInformation("Schema analysis completed successfully");

            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Schema analysis failed with exception");
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
                var claudeApiKey = context.Configuration["ClaudeApiKey"] ?? "your-claude-api-key-here";
                
                services.AddHttpClient();
                services.AddScoped<ISchemaAnalysisService, SchemaAnalysisService>();
                services.AddScoped<IObfuscationConfigGenerator, ObfuscationConfigGenerator>();
                services.AddScoped<IPIIDetectionService, PIIDetectionService>();
                services.AddScoped<IClaudeApiService>(provider => 
                    new ClaudeApiService(
                        provider.GetRequiredService<HttpClient>(),
                        provider.GetRequiredService<ILogger<ClaudeApiService>>(),
                        claudeApiKey));
                services.AddScoped<IEnhancedPIIDetectionService>(provider =>
                    new EnhancedPIIDetectionService(
                        provider.GetRequiredService<IClaudeApiService>(),
                        provider.GetRequiredService<ISchemaAnalysisService>(),
                        provider.GetRequiredService<ILogger<EnhancedPIIDetectionService>>()));
            });

    private static PIIAnalysisResult CreatePIIAnalysisResult(DatabaseSchema schema, List<PIIColumn> piiColumns)
    {
        var tablesWithPII = new List<TableWithPII>();
        
        // Group PII columns by table
        var piiColumnsByTable = piiColumns.GroupBy(c => c.TableName);
        
        foreach (var group in piiColumnsByTable)
        {
            var tableName = group.Key;
            var parts = tableName.Split('.');
            var schemaName = parts.Length > 1 ? parts[0] : "dbo";
            var tableNameOnly = parts.Length > 1 ? parts[1] : tableName;
            
            // Find the original table info
            var originalTable = schema.Tables.FirstOrDefault(t => 
                t.Schema.Equals(schemaName, StringComparison.OrdinalIgnoreCase) &&
                t.TableName.Equals(tableNameOnly, StringComparison.OrdinalIgnoreCase));
            
            if (originalTable != null)
            {
                var tableWithPII = new TableWithPII
                {
                    TableName = tableNameOnly,
                    Schema = schemaName,
                    PIIColumns = group.ToList(),
                    RowCount = originalTable.RowCount,
                    Priority = DeterminePriority(originalTable, group.ToList()),
                    PrimaryKeyColumns = originalTable.Columns
                        .Where(c => c.IsPrimaryKey)
                        .Select(c => c.ColumnName)
                        .ToList()
                };
                
                tablesWithPII.Add(tableWithPII);
            }
        }
        
        return new PIIAnalysisResult
        {
            DatabaseName = schema.DatabaseName,
            TablesWithPII = tablesWithPII
        };
    }

    private static int DeterminePriority(TableInfo table, List<PIIColumn> piiColumns)
    {
        // Determine priority based on table characteristics
        var highPriorityPatterns = new[] { "person", "customer", "employee", "user", "contact" };
        var tableName = table.TableName.ToLower();
        
        if (highPriorityPatterns.Any(pattern => tableName.Contains(pattern)))
            return 1;
        
        if (piiColumns.Count >= 5)
            return 1;
        
        if (piiColumns.Count >= 3)
            return 3;
        
        return 5;
    }
}