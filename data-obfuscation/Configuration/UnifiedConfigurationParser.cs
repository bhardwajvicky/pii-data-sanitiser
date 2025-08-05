using Microsoft.Extensions.Logging;
using FluentValidation;
using DataObfuscation.Configuration;
using Common.Models;
using System.Text.Json;

namespace DataObfuscation.Configuration;

public interface IUnifiedConfigurationParser
{
    Task<ObfuscationConfiguration> LoadUnifiedConfigurationAsync(string mappingFilePath);
    Task ValidateConfigurationAsync(ObfuscationConfiguration config);
}

public class UnifiedConfigurationParser : IUnifiedConfigurationParser
{
    private readonly ILogger<UnifiedConfigurationParser> _logger;
    private readonly IConfigurationValidator _validator;

    public UnifiedConfigurationParser(ILogger<UnifiedConfigurationParser> logger, IConfigurationValidator validator)
    {
        _logger = logger;
        _validator = validator;
    }

    public async Task<ObfuscationConfiguration> LoadUnifiedConfigurationAsync(string mappingFilePath)
    {
        _logger.LogInformation("Loading unified configuration from: {MappingFilePath}", mappingFilePath);

        if (!File.Exists(mappingFilePath))
        {
            throw new FileNotFoundException($"Unified mapping file not found: {mappingFilePath}");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        // Load unified mapping file
        var mappingContent = await File.ReadAllTextAsync(mappingFilePath);
        var unifiedMapping = JsonSerializer.Deserialize<UnifiedObfuscationMapping>(mappingContent, options);
        
        if (unifiedMapping == null)
        {
            throw new InvalidOperationException("Failed to deserialize unified mapping file");
        }

        // Convert to the legacy format expected by the obfuscation engine
        var config = ConvertToLegacyFormat(unifiedMapping);

        await ValidateConfigurationAsync(config);
        
        _logger.LogInformation("Unified configuration loaded successfully");
        _logger.LogInformation("Tables to process: {TableCount}", config.Tables.Count);
        _logger.LogInformation("Custom data types: {DataTypeCount}", config.DataTypes.Count);
        
        return config;
    }

    private ObfuscationConfiguration ConvertToLegacyFormat(UnifiedObfuscationMapping unifiedMapping)
    {
        var config = new ObfuscationConfiguration
        {
            Global = new GlobalConfiguration
            {
                ConnectionString = unifiedMapping.Global.ConnectionString,
                GlobalSeed = unifiedMapping.Global.GlobalSeed,
                BatchSize = unifiedMapping.Global.BatchSize,
                SqlBatchSize = unifiedMapping.Global.SqlBatchSize,
                ParallelThreads = unifiedMapping.Global.ParallelThreads,
                MaxCacheSize = unifiedMapping.Global.MaxCacheSize,
                DryRun = unifiedMapping.Global.DryRun,
                PersistMappings = unifiedMapping.Global.PersistMappings,
                EnableValueCaching = unifiedMapping.Global.EnableValueCaching,
                CommandTimeoutSeconds = unifiedMapping.Global.CommandTimeoutSeconds,
                MappingCacheDirectory = unifiedMapping.Global.MappingCacheDirectory
            },
            DataTypes = ConvertCustomDataTypes(unifiedMapping.DataTypes),
            ReferentialIntegrity = ConvertReferentialIntegrity(unifiedMapping.ReferentialIntegrity),
            Tables = ConvertTableMappings(unifiedMapping.Tables),
            PostProcessing = new PostProcessingConfiguration
            {
                GenerateReport = unifiedMapping.PostProcessing.GenerateReport,
                ReportPath = unifiedMapping.PostProcessing.ReportPath,
                ValidateResults = unifiedMapping.PostProcessing.ValidateResults,
                BackupMappings = unifiedMapping.PostProcessing.BackupMappings
            }
        };

        return config;
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
            Enabled = source.Enabled
        };

        foreach (var relationship in source.Relationships)
        {
            result.Relationships.Add(new RelationshipConfiguration
            {
                Name = $"{relationship.ParentTable}.{relationship.ParentColumn} -> {relationship.ChildTable}.{relationship.ChildColumn}",
                PrimaryTable = relationship.ParentTable,
                PrimaryColumn = relationship.ParentColumn,
                RelatedMappings = new List<RelatedMapping>
                {
                    new RelatedMapping
                    {
                        Table = relationship.ChildTable,
                        Column = relationship.ChildColumn,
                        Relationship = relationship.RelationshipType
                    }
                }
            });
        }

        return result;
    }

    private List<TableConfiguration> ConvertTableMappings(List<TableMapping> mappings)
    {
        var result = new List<TableConfiguration>();

        foreach (var mapping in mappings)
        {
            var tableConfig = new TableConfiguration
            {
                TableName = mapping.FullTableName,
                Priority = 10, // Default priority
                PrimaryKey = mapping.PrimaryKey,
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
                Conditions = mapping.IsNullable ? new ConditionsConfiguration { OnlyIfNotNull = true } : null
            };

            result.Add(columnConfig);
        }

        return result;
    }

    public Task ValidateConfigurationAsync(ObfuscationConfiguration config)
    {
        var validationResult = _validator.ValidateConfiguration(config);
        
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors);
            throw new ValidationException($"Configuration validation failed: {errors}");
        }
        
        _logger.LogInformation("Configuration validation passed");
        return Task.CompletedTask;
    }
} 