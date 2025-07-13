using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Text.Json;
using DataObfuscation.Core;
using DataObfuscation.Configuration;
using DataObfuscation.Data;

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
                Console.WriteLine("Usage: DataObfuscation.exe <config-file.json> [--dry-run] [--validate-only]");
                Console.WriteLine("   or: DataObfuscation.exe <mapping-file.json> <config-file.json> [--dry-run] [--validate-only]");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  DataObfuscation.exe production-config.json");
                Console.WriteLine("  DataObfuscation.exe AdventureWorks2019-mapping.json AdventureWorks2019-config.json");
                Console.WriteLine("  DataObfuscation.exe test-config.json --dry-run");
                Console.WriteLine("  DataObfuscation.exe config.json --validate-only");
                return 1;
            }

            var dryRun = args.Contains("--dry-run");
            var validateOnly = args.Contains("--validate-only");
            var nonFlagArgs = args.Where(arg => !arg.StartsWith("--")).ToArray();

            var host = CreateHostBuilder(args).Build();
            
            using var scope = host.Services.CreateScope();
            var configParser = scope.ServiceProvider.GetRequiredService<IConfigurationParser>();
            var configValidator = scope.ServiceProvider.GetRequiredService<IConfigurationValidator>();
            var obfuscationEngine = scope.ServiceProvider.GetRequiredService<IObfuscationEngine>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Starting Data Obfuscation Tool");
            
            ObfuscationConfiguration config;
            
            if (nonFlagArgs.Length == 1)
            {
                // Single file mode (legacy)
                var configFile = nonFlagArgs[0];
                logger.LogInformation("Configuration file: {ConfigFile}", configFile);
                config = await configParser.LoadConfigurationAsync(configFile);
            }
            else if (nonFlagArgs.Length == 2)
            {
                // Two file mode (mapping + config)
                var mappingFile = nonFlagArgs[0];
                var configFile = nonFlagArgs[1];
                logger.LogInformation("Mapping file: {MappingFile}, Configuration file: {ConfigFile}", mappingFile, configFile);
                config = await configParser.LoadConfigurationAsync(mappingFile, configFile);
            }
            else
            {
                Console.WriteLine("Invalid number of arguments. Expected 1 or 2 file paths.");
                return 1;
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

            var result = await obfuscationEngine.ExecuteAsync(config);
            
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
                services.AddScoped<IConfigurationParser, ConfigurationParser>();
                services.AddScoped<IConfigurationValidator, ConfigurationValidator>();
                services.AddScoped<IObfuscationEngine, ObfuscationEngine>();
                services.AddScoped<IDeterministicAustralianProvider, DeterministicAustralianProvider>();
                services.AddScoped<IReferentialIntegrityManager, ReferentialIntegrityManager>();
                services.AddScoped<IDataRepository, SqlServerRepository>();
                services.AddScoped<IProgressTracker, ProgressTracker>();
            });
}