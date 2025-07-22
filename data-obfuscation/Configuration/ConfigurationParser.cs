using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Common.Models;

namespace DataObfuscation.Configuration;

public interface IConfigurationParser
{
    Task<ObfuscationConfiguration> LoadConfigurationAsync(string configFilePath);
    Task<ObfuscationConfiguration> LoadConfigurationAsync(string mappingFilePath, string configFilePath);
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
        _logger.LogInformation("Tables to process: {TableCount}", config.Tables?.Count ?? 0);
        _logger.LogInformation("Custom data types: {DataTypeCount}", config.DataTypes.Count);
        
        return config;
    }

    public async Task<ObfuscationConfiguration> LoadConfigurationAsync(string mappingFilePath, string configFilePath)
    {
        _logger.LogInformation("Loading configuration from mapping file: {MappingFilePath} and config file: {ConfigFilePath}", 
            mappingFilePath, configFilePath);

        if (!File.Exists(mappingFilePath))
        {
            throw new FileNotFoundException($"Mapping file not found: {mappingFilePath}");
        }
        
        if (!File.Exists(configFilePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configFilePath}");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        // Load mapping file
        var mappingContent = await File.ReadAllTextAsync(mappingFilePath);
        var mapping = JsonSerializer.Deserialize<TableColumnMapping>(mappingContent, options);
        
        if (mapping == null)
        {
            throw new InvalidOperationException("Failed to deserialize mapping file");
        }

        // Load configuration file
        var configContent = await File.ReadAllTextAsync(configFilePath);
        var config = JsonSerializer.Deserialize<Common.Models.ObfuscationConfiguration>(configContent, options);
        
        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize configuration file");
        }

        // Merge mapping and configuration into the legacy format
        var mergedConfig = MergeConfiguration(mapping, config);

        await ValidateConfigurationAsync(mergedConfig);
        
        _logger.LogInformation("Configuration loaded successfully from split files");
        _logger.LogInformation("Tables to process: {TableCount}", mapping.Tables.Count);
        _logger.LogInformation("Custom data types: {DataTypeCount}", config.DataTypes.Count);
        
        return mergedConfig;
    }

    private ObfuscationConfiguration MergeConfiguration(TableColumnMapping mapping, 
        Common.Models.ObfuscationConfiguration config)
    {
        var mergedConfig = new ObfuscationConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                ConfigVersion = config.Metadata.ConfigVersion,
                Description = config.Metadata.Description,
                CreatedBy = config.Metadata.CreatedBy,
                CreatedDate = config.Metadata.CreatedDate,
                LastModified = config.Metadata.LastModified
            },
            Global = new GlobalConfiguration
            {
                ConnectionString = config.Global.ConnectionString,
                GlobalSeed = config.Global.GlobalSeed,
                BatchSize = config.Global.BatchSize,
                SqlBatchSize = config.Global.SqlBatchSize,
                ParallelThreads = config.Global.ParallelThreads,
                MaxCacheSize = config.Global.MaxCacheSize,
                DryRun = config.Global.DryRun,
                PersistMappings = config.Global.PersistMappings,
                EnableValueCaching = config.Global.EnableValueCaching,
                CommandTimeoutSeconds = config.Global.CommandTimeoutSeconds,
                MappingCacheDirectory = config.Global.MappingCacheDirectory
            },
            DataTypes = ConvertCustomDataTypes(config.DataTypes),
            ReferentialIntegrity = ConvertReferentialIntegrity(config.ReferentialIntegrity),
            Tables = ConvertTableMappings(mapping.Tables),
            PostProcessing = new PostProcessingConfiguration
            {
                GenerateReport = config.PostProcessing.GenerateReport,
                ReportPath = config.PostProcessing.ReportPath,
                ValidateResults = config.PostProcessing.ValidateResults,
                BackupMappings = config.PostProcessing.BackupMappings
            }
        };

        return mergedConfig;
    }

    private Dictionary<string, CustomDataType> ConvertCustomDataTypes(
        Dictionary<string, Common.Models.CustomDataType> sourceDataTypes)
    {
        var result = new Dictionary<string, CustomDataType>();
        
        foreach (var kvp in sourceDataTypes)
        {
            result[kvp.Key] = new CustomDataType
            {
                BaseType = kvp.Value.BaseType,
                CustomSeed = kvp.Value.CustomSeed,
                PreserveLength = kvp.Value.PreserveLength,
                Validation = ConvertValidation(kvp.Value.Validation),
                Formatting = ConvertFormatting(kvp.Value.Formatting),
                Transformation = ConvertTransformation(kvp.Value.Transformation)
            };
        }
        
        return result;
    }

    private ValidationConfiguration? ConvertValidation(
        Common.Models.ValidationConfiguration? source)
    {
        if (source == null) return null;
        
        return new ValidationConfiguration
        {
            Regex = source.Regex,
            MinLength = source.MinLength,
            MaxLength = source.MaxLength,
            AllowedValues = source.AllowedValues
        };
    }

    private FormattingConfiguration? ConvertFormatting(
        Common.Models.FormattingConfiguration? source)
    {
        if (source == null) return null;
        
        return new FormattingConfiguration
        {
            AddPrefix = source.Prefix,
            AddSuffix = source.Suffix,
            Pattern = source.Pattern
        };
    }

    private TransformationConfiguration? ConvertTransformation(
        Common.Models.TransformationConfiguration? source)
    {
        if (source == null) return null;
        
        return new TransformationConfiguration
        {
            PreProcess = new List<string>(),
            PostProcess = new List<string>()
        };
    }

    private ReferentialIntegrityConfiguration ConvertReferentialIntegrity(
        Common.Models.ReferentialIntegrityConfiguration source)
    {
        var result = new ReferentialIntegrityConfiguration
        {
            Enabled = source.Enabled,
            Relationships = new List<RelationshipConfiguration>()
        };
        
        foreach (var rel in source.Relationships)
        {
            result.Relationships.Add(new RelationshipConfiguration
            {
                Name = $"{rel.ParentTable}_{rel.ParentColumn}",
                PrimaryTable = rel.ParentTable,
                PrimaryColumn = rel.ParentColumn,
                RelatedMappings = new List<RelatedMapping>
                {
                    new RelatedMapping
                    {
                        Table = rel.ChildTable,
                        Column = rel.ChildColumn,
                        Relationship = "exact"
                    }
                }
            });
        }
        
        return result;
    }

    private List<TableConfiguration> ConvertTableMappings(List<TableMapping> mappings)
    {
        var result = new List<TableConfiguration>();
        
        foreach (var mapping in mappings.Where(t => t.Enabled))
        {
            var tableConfig = new TableConfiguration
            {
                TableName = mapping.FullTableName,
                Priority = 1, // Default priority since mapping doesn't have this
                PrimaryKey = mapping.PrimaryKey,
                CustomBatchSize = null, // Will use global default
                Conditions = null,
                Columns = ConvertColumnMappings(mapping.Columns)
            };
            
            result.Add(tableConfig);
        }
        
        return result;
    }
    
    private List<ColumnConfiguration> ConvertColumnMappings(List<ColumnMapping> mappings)
    {
        var result = new List<ColumnConfiguration>();
        
        foreach (var mapping in mappings.Where(c => c.Enabled))
        {
            var columnConfig = new ColumnConfiguration
            {
                ColumnName = mapping.ColumnName,
                DataType = mapping.DataType,
                Enabled = mapping.Enabled,
                PreserveLength = mapping.PreserveLength,
                Conditions = mapping.IsNullable ? new ConditionsConfiguration { OnlyIfNotNull = true } : null,
                Fallback = new FallbackConfiguration
                {
                    OnError = "useOriginal",
                    DefaultValue = null
                },
                Validation = null,
                Transformation = null
            };
            
            result.Add(columnConfig);
        }
        
        return result;
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