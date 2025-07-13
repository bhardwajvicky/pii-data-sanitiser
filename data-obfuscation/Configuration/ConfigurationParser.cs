using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DataObfuscation.Configuration;

public interface IConfigurationParser
{
    Task<ObfuscationConfiguration> LoadConfigurationAsync(string configFilePath);
    Task ValidateConfigurationAsync(ObfuscationConfiguration config);
}

public class ConfigurationParser : IConfigurationParser
{
    private readonly ILogger<ConfigurationParser> _logger;
    private readonly IValidator<ObfuscationConfiguration> _validator;

    public ConfigurationParser(ILogger<ConfigurationParser> logger)
    {
        _logger = logger;
        _validator = new ObfuscationConfigurationValidator();
    }

    public async Task<ObfuscationConfiguration> LoadConfigurationAsync(string configFilePath)
    {
        _logger.LogInformation("Loading configuration from: {ConfigFilePath}", configFilePath);

        if (!File.Exists(configFilePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configFilePath}");
        }

        var jsonContent = await File.ReadAllTextAsync(configFilePath);
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var config = JsonSerializer.Deserialize<ObfuscationConfiguration>(jsonContent, options);
        
        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize configuration file");
        }

        await ValidateConfigurationAsync(config);
        
        _logger.LogInformation("Configuration loaded successfully");
        _logger.LogInformation("Tables to process: {TableCount}", config.Tables.Count);
        _logger.LogInformation("Custom data types: {DataTypeCount}", config.DataTypes.Count);
        
        return config;
    }

    public async Task ValidateConfigurationAsync(ObfuscationConfiguration config)
    {
        _logger.LogDebug("Validating configuration");

        var validationResult = await _validator.ValidateAsync(config);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(Environment.NewLine, validationResult.Errors.Select(e => e.ErrorMessage));
            _logger.LogError("Configuration validation failed: {Errors}", errors);
            throw new ValidationException($"Configuration validation failed:{Environment.NewLine}{errors}");
        }

        _logger.LogDebug("Configuration validation completed successfully");
    }
}

public class ObfuscationConfigurationValidator : AbstractValidator<ObfuscationConfiguration>
{
    public ObfuscationConfigurationValidator()
    {
        RuleFor(x => x.Global)
            .NotNull()
            .SetValidator(new GlobalConfigurationValidator());

        RuleFor(x => x.Tables)
            .NotEmpty()
            .WithMessage("At least one table must be configured for obfuscation");

        RuleForEach(x => x.Tables)
            .SetValidator(new TableConfigurationValidator());

        RuleFor(x => x.ReferentialIntegrity)
            .SetValidator(new ReferentialIntegrityValidator())
            .When(x => x.ReferentialIntegrity.Enabled);

        RuleForEach(x => x.DataTypes.Values)
            .SetValidator(new CustomDataTypeValidator());
    }
}

public class GlobalConfigurationValidator : AbstractValidator<GlobalConfiguration>
{
    public GlobalConfigurationValidator()
    {
        RuleFor(x => x.ConnectionString)
            .NotEmpty()
            .WithMessage("Connection string is required");

        RuleFor(x => x.GlobalSeed)
            .NotEmpty()
            .WithMessage("Global seed is required");

        RuleFor(x => x.BatchSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100000)
            .WithMessage("Batch size must be between 1 and 100,000");

        RuleFor(x => x.ParallelThreads)
            .GreaterThan(0)
            .LessThanOrEqualTo(32)
            .WithMessage("Parallel threads must be between 1 and 32");

        RuleFor(x => x.MaxCacheSize)
            .GreaterThan(0)
            .WithMessage("Max cache size must be greater than 0");

        RuleFor(x => x.CommandTimeoutSeconds)
            .GreaterThan(0)
            .LessThanOrEqualTo(3600)
            .WithMessage("Command timeout must be between 1 and 3600 seconds");
    }
}

public class TableConfigurationValidator : AbstractValidator<TableConfiguration>
{
    public TableConfigurationValidator()
    {
        RuleFor(x => x.TableName)
            .NotEmpty()
            .WithMessage("Table name is required");

        RuleFor(x => x.Priority)
            .GreaterThan(0)
            .WithMessage("Priority must be greater than 0");

        RuleFor(x => x.Columns)
            .NotEmpty()
            .WithMessage("At least one column must be configured for obfuscation");

        RuleForEach(x => x.Columns)
            .SetValidator(new ColumnConfigurationValidator());

        RuleFor(x => x.CustomBatchSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100000)
            .When(x => x.CustomBatchSize.HasValue)
            .WithMessage("Custom batch size must be between 1 and 100,000");
    }
}

public class ColumnConfigurationValidator : AbstractValidator<ColumnConfiguration>
{
    public ColumnConfigurationValidator()
    {
        RuleFor(x => x.ColumnName)
            .NotEmpty()
            .WithMessage("Column name is required");

        RuleFor(x => x.DataType)
            .NotEmpty()
            .WithMessage("Data type is required");
    }
}

public class ReferentialIntegrityValidator : AbstractValidator<ReferentialIntegrityConfiguration>
{
    public ReferentialIntegrityValidator()
    {
        RuleForEach(x => x.Relationships)
            .SetValidator(new RelationshipConfigurationValidator());
    }
}

public class RelationshipConfigurationValidator : AbstractValidator<RelationshipConfiguration>
{
    public RelationshipConfigurationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Relationship name is required");

        RuleFor(x => x.PrimaryTable)
            .NotEmpty()
            .WithMessage("Primary table is required");

        RuleFor(x => x.PrimaryColumn)
            .NotEmpty()
            .WithMessage("Primary column is required");

        RuleFor(x => x.RelatedMappings)
            .NotEmpty()
            .WithMessage("At least one related mapping is required");
    }
}

public class CustomDataTypeValidator : AbstractValidator<CustomDataType>
{
    public CustomDataTypeValidator()
    {
        RuleFor(x => x.BaseType)
            .NotEmpty()
            .WithMessage("Base type is required for custom data types");
    }
}