using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using DataObfuscation.Core;
using DataObfuscation.Configuration;
using DataObfuscation.Data;
using DataObfuscation.Services;

namespace DataObfuscation;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/obfuscation-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: DataObfuscation.exe <unified-mapping-file.json> [--dry-run] [--validate-only]");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  DataObfuscation.exe adv2-mapping.json");
                Console.WriteLine("  DataObfuscation.exe adv2-mapping.json --dry-run");
                Console.WriteLine("  DataObfuscation.exe adv2-mapping.json --validate-only");
                return 1;
            }

            var dryRun = args.Contains("--dry-run");
            var validateOnly = args.Contains("--validate-only");
            var nonFlagArgs = args.Where(arg => !arg.StartsWith("--")).ToArray();

            if (nonFlagArgs.Length != 1)
            {
                Console.WriteLine("Error: Expected exactly one mapping file path.");
                Console.WriteLine("Usage: DataObfuscation.exe <unified-mapping-file.json> [--dry-run] [--validate-only]");
                return 1;
            }

            var host = CreateHostBuilder(args).Build();
            
            using var scope = host.Services.CreateScope();
            var unifiedConfigParser = scope.ServiceProvider.GetRequiredService<IUnifiedConfigurationParser>();
            var configValidator = scope.ServiceProvider.GetRequiredService<IConfigurationValidator>();
            var obfuscationEngine = scope.ServiceProvider.GetRequiredService<IObfuscationEngine>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Starting Data Obfuscation Tool");
            
            var mappingFilePath = nonFlagArgs[0];
            logger.LogInformation("Unified mapping file: {File}", mappingFilePath);
            
            // Load unified configuration
            var config = await unifiedConfigParser.LoadUnifiedConfigurationAsync(mappingFilePath);
            
            // Detect database technology if not specified
            if (string.IsNullOrEmpty(config.Global.DatabaseTechnology))
            {
                config.Global.DatabaseTechnology = DatabaseTechnologyHelper.DetectDatabaseTechnology(config.Global.ConnectionString);
                logger.LogInformation("Auto-detected database technology: {Technology}", config.Global.DatabaseTechnology);
            }
            else
            {
                logger.LogInformation("Using specified database technology: {Technology}", config.Global.DatabaseTechnology);
            }
            
            // Always validate configuration
            var validationResult = configValidator.ValidateConfiguration(config);
            
            if (!validationResult.IsValid)
            {
                logger.LogError("Configuration validation failed:");
                foreach (var error in validationResult.Errors)
                {
                    logger.LogError("  ❌ {Error}", error);
                }
                foreach (var warning in validationResult.Warnings)
                {
                    logger.LogWarning("  ⚠️  {Warning}", warning);
                }
                return 1;
            }
            
            if (validationResult.Warnings.Any())
            {
                logger.LogWarning("Configuration validation passed with warnings:");
                foreach (var warning in validationResult.Warnings)
                {
                    logger.LogWarning("  ⚠️  {Warning}", warning);
                }
            }
            else
            {
                logger.LogInformation("Configuration validation passed successfully");
            }
            
            if (dryRun)
            {
                config.Global.DryRun = true;
                logger.LogInformation("Running in DRY RUN mode - no data will be modified");
            }

            if (validateOnly)
            {
                logger.LogInformation("Configuration validation completed - exiting as requested");
                return 0;
            }

            var result = await obfuscationEngine.ExecuteAsync(config, mappingFilePath, mappingFilePath, resumeIfPossible: true);
            
            if (result.Success)
            {
                logger.LogInformation("Data obfuscation completed successfully");
                logger.LogInformation("Tables processed: {TablesProcessed}", result.TablesProcessed);
                logger.LogInformation("Rows processed: {RowsProcessed:N0}", result.RowsProcessed);
                logger.LogInformation("Duration: {Duration}", result.Duration);
                return 0;
            }
            else
            {
                logger.LogError("Data obfuscation failed: {Error}", result.ErrorMessage);
                return 1;
            }
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
                services.AddScoped<IUnifiedConfigurationParser, UnifiedConfigurationParser>();
                services.AddScoped<IConfigurationValidator, ConfigurationValidator>();
                services.AddScoped<IObfuscationEngine, ObfuscationEngine>();
                services.AddScoped<IDeterministicAustralianProvider, DeterministicAustralianProvider>();
                services.AddScoped<IReferentialIntegrityManager, ReferentialIntegrityManager>();
                services.AddScoped<IDatabaseRepositoryFactory, DatabaseRepositoryFactory>();
                services.AddScoped<IDataRepository>(provider =>
                {
                    var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                    var factory = provider.GetRequiredService<IDatabaseRepositoryFactory>();
                    
                    // Default to SQL Server - will be overridden when config is loaded
                    return factory.CreateRepository("SqlServer", loggerFactory);
                });
                services.AddScoped<IProgressTracker, ProgressTracker>();
                services.AddScoped<IFailureLogger, FailureLogger>();
                services.AddScoped<ICheckpointService, CheckpointService>();
            });
}