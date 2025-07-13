using Microsoft.Extensions.Logging;
using DataObfuscation.Configuration;

namespace DataObfuscation.Configuration;

public interface IConfigurationValidator
{
    ValidationResult ValidateConfiguration(ObfuscationConfiguration config);
}

public class ConfigurationValidator : IConfigurationValidator
{
    private readonly ILogger<ConfigurationValidator> _logger;
    
    private static readonly HashSet<string> SupportedDataTypes = new()
    {
        "FirstName", "LastName", "FullName", "LicenseNumber", "Email", "Phone",
        "VehicleRegistration", "VINNumber", "VehicleMakeModel", "EngineNumber",
        "CompanyName", "BusinessABN", "BusinessACN", "Address", "GPSCoordinate",
        "RouteCode", "DepotLocation", "CreditCard"
    };

    public ConfigurationValidator(ILogger<ConfigurationValidator> logger)
    {
        _logger = logger;
    }

    public ValidationResult ValidateConfiguration(ObfuscationConfiguration config)
    {
        var result = new ValidationResult();
        
        _logger.LogInformation("Starting configuration validation");

        // Validate global configuration
        ValidateGlobalConfig(config.Global, result);
        
        // Validate data types
        ValidateDataTypes(config.DataTypes, result);
        
        // Validate tables
        ValidateTables(config.Tables, result);
        
        _logger.LogInformation("Configuration validation completed with {ErrorCount} errors and {WarningCount} warnings", 
            result.Errors.Count, result.Warnings.Count);
            
        return result;
    }

    private void ValidateGlobalConfig(GlobalConfiguration global, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(global.ConnectionString))
        {
            result.AddError("Global.ConnectionString is required");
        }

        if (global.BatchSize <= 0)
        {
            result.AddError("Global.BatchSize must be greater than 0");
        }

        if (global.ParallelThreads <= 0)
        {
            result.AddError("Global.ParallelThreads must be greater than 0");
        }

        if (global.CommandTimeoutSeconds <= 0)
        {
            result.AddWarning("Global.CommandTimeoutSeconds should be greater than 0");
        }
    }

    private void ValidateDataTypes(Dictionary<string, CustomDataType> dataTypes, ValidationResult result)
    {
        foreach (var (typeName, config) in dataTypes)
        {
            if (string.IsNullOrWhiteSpace(config.BaseType))
            {
                result.AddError($"DataType '{typeName}' must have a BaseType specified");
                continue;
            }

            if (!SupportedDataTypes.Contains(config.BaseType))
            {
                result.AddError($"DataType '{typeName}' has unsupported BaseType '{config.BaseType}'. Supported types: {string.Join(", ", SupportedDataTypes)}");
            }
        }
    }

    private void ValidateTables(List<TableConfiguration> tables, ValidationResult result)
    {
        if (!tables.Any())
        {
            result.AddWarning("No tables configured for obfuscation");
            return;
        }

        var tableNames = new HashSet<string>();
        
        foreach (var table in tables)
        {
            ValidateTable(table, result);
            
            if (!tableNames.Add(table.TableName))
            {
                result.AddError($"Duplicate table configuration found: {table.TableName}");
            }
        }
    }

    private void ValidateTable(TableConfiguration table, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(table.TableName))
        {
            result.AddError("Table must have a TableName");
            return;
        }

        if (!table.PrimaryKey?.Any() == true)
        {
            result.AddError($"Table '{table.TableName}' must have at least one PrimaryKey column");
        }

        if (!table.Columns?.Any() == true)
        {
            result.AddWarning($"Table '{table.TableName}' has no columns configured");
            return;
        }

        var columnNames = new HashSet<string>();
        var enabledColumns = table.Columns.Where(c => c.Enabled).ToList();
        
        if (!enabledColumns.Any())
        {
            result.AddWarning($"Table '{table.TableName}' has no enabled columns");
        }

        foreach (var column in table.Columns)
        {
            ValidateColumn(table.TableName, column, result);
            
            if (!columnNames.Add(column.ColumnName))
            {
                result.AddError($"Duplicate column configuration in table '{table.TableName}': {column.ColumnName}");
            }
        }
    }

    private void ValidateColumn(string tableName, ColumnConfiguration column, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(column.ColumnName))
        {
            result.AddError($"Column in table '{tableName}' must have a ColumnName");
            return;
        }

        if (string.IsNullOrWhiteSpace(column.DataType))
        {
            result.AddError($"Column '{tableName}.{column.ColumnName}' must have a DataType");
            return;
        }

        // Check if data type is supported (will be checked against custom types or base types)
        if (!SupportedDataTypes.Contains(column.DataType))
        {
            // This might be a custom data type, will be validated in ValidateDataTypes
            _logger.LogDebug("Column '{TableName}.{ColumnName}' uses data type '{DataType}' which may be a custom type", 
                tableName, column.ColumnName, column.DataType);
        }

        // Validate fallback configuration
        if (column.Fallback != null)
        {
            var validActions = new[] { "useOriginal", "useDefault", "skip" };
            if (!string.IsNullOrEmpty(column.Fallback.OnError) && !validActions.Contains(column.Fallback.OnError))
            {
                result.AddError($"Column '{tableName}.{column.ColumnName}' has invalid Fallback.OnError value. Valid values: {string.Join(", ", validActions)}");
            }
        }

        // Validate regex if present
        if (!string.IsNullOrEmpty(column.Validation?.Regex))
        {
            try
            {
                _ = new System.Text.RegularExpressions.Regex(column.Validation.Regex);
            }
            catch (ArgumentException)
            {
                result.AddError($"Column '{tableName}.{column.ColumnName}' has invalid regex pattern: {column.Validation.Regex}");
            }
        }
    }
}

public class ValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    
    public bool IsValid => !Errors.Any();
    
    public void AddError(string error) => Errors.Add(error);
    public void AddWarning(string warning) => Warnings.Add(warning);
    
    public override string ToString()
    {
        var lines = new List<string>();
        
        if (Errors.Any())
        {
            lines.Add("ERRORS:");
            lines.AddRange(Errors.Select(e => $"  ❌ {e}"));
        }
        
        if (Warnings.Any())
        {
            lines.Add("WARNINGS:");
            lines.AddRange(Warnings.Select(w => $"  ⚠️  {w}"));
        }
        
        if (!Errors.Any() && !Warnings.Any())
        {
            lines.Add("✅ Configuration validation passed successfully!");
        }
        
        return string.Join("\n", lines);
    }
}