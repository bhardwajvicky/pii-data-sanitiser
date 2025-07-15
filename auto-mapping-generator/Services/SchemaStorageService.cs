using System.Text.Json;
using Microsoft.Extensions.Logging;
using AutoMappingGenerator.Models;

namespace AutoMappingGenerator.Services;

public interface ISchemaStorageService
{
    Task SaveSchemaAsync(DatabaseSchema schema, string outputDirectory);
    Task SaveTableSchemaAsync(TableInfo table, string outputDirectory);
    Task<DatabaseSchema?> LoadSchemaAsync(string schemaDirectory);
}

public class SchemaStorageService : ISchemaStorageService
{
    private readonly ILogger<SchemaStorageService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SchemaStorageService(ILogger<SchemaStorageService> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task SaveSchemaAsync(DatabaseSchema schema, string outputDirectory)
    {
        var schemaDir = Path.Combine(outputDirectory, "schema", schema.DatabaseName);
        Directory.CreateDirectory(schemaDir);

        // Save database-level schema summary
        var summaryPath = Path.Combine(schemaDir, "database-schema.json");
        var summary = new
        {
            DatabaseName = schema.DatabaseName,
            CapturedAt = DateTime.UtcNow,
            TableCount = schema.Tables.Count,
            TotalColumnCount = schema.Tables.Sum(t => t.Columns.Count),
            Tables = schema.Tables.Select(t => new
            {
                Schema = t.Schema,
                TableName = t.TableName,
                FullName = t.FullName,
                ColumnCount = t.Columns.Count,
                PrimaryKeyColumns = t.PrimaryKeyColumns,
                RowCount = t.RowCount
            })
        };

        await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(summary, _jsonOptions));
        _logger.LogInformation("Saved database schema summary to {Path}", summaryPath);

        // Save detailed schema for each table
        foreach (var table in schema.Tables)
        {
            await SaveTableSchemaAsync(table, schemaDir);
        }
    }

    public async Task SaveTableSchemaAsync(TableInfo table, string schemaDirectory)
    {
        // schemaDirectory is already the database schema directory
        var schemaDir = Path.Combine(schemaDirectory, table.Schema);
        Directory.CreateDirectory(schemaDir);

        var fileName = $"{table.TableName}.json";
        var filePath = Path.Combine(schemaDir, fileName);

        var tableSchema = new
        {
            Schema = table.Schema,
            TableName = table.TableName,
            FullName = table.FullName,
            PrimaryKeyColumns = table.PrimaryKeyColumns,
            RowCount = table.RowCount,
            CapturedAt = DateTime.UtcNow,
            Columns = table.Columns.Select(c => new
            {
                c.ColumnName,
                c.SqlDataType,
                c.MaxLength,
                c.IsNullable,
                c.IsPrimaryKey,
                c.IsForeignKey,
                c.IsIdentity,
                c.IsComputed,
                c.DefaultValue,
                c.OrdinalPosition,
                ExtendedProperties = new
                {
                    c.NumericPrecision,
                    c.NumericScale,
                    c.CharacterSet,
                    c.Collation,
                    c.IsRowGuid,
                    c.IsFileStream,
                    c.IsSparse,
                    c.IsXmlDocument
                }
            })
        };

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(tableSchema, _jsonOptions));
        _logger.LogDebug("Saved schema for table {TableName} to {Path}", table.FullName, filePath);
    }

    public async Task<DatabaseSchema?> LoadSchemaAsync(string schemaDirectory)
    {
        // schemaDirectory is the full path to the database schema directory
        if (!Directory.Exists(schemaDirectory))
        {
            _logger.LogWarning("Schema directory not found: {Directory}", schemaDirectory);
            return null;
        }

        var summaryPath = Path.Combine(schemaDirectory, "database-schema.json");
        if (!File.Exists(summaryPath))
        {
            _logger.LogWarning("Database schema summary not found: {Path}", summaryPath);
            return null;
        }

        try
        {
            var databaseName = Path.GetFileName(schemaDirectory);
            var schema = new DatabaseSchema { DatabaseName = databaseName };

            // Load table schemas
            foreach (var schemaDir in Directory.GetDirectories(schemaDirectory))
            {
                var schemaName = Path.GetFileName(schemaDir);
                foreach (var tableFile in Directory.GetFiles(schemaDir, "*.json"))
                {
                    var tableJson = await File.ReadAllTextAsync(tableFile);
                    var tableData = JsonSerializer.Deserialize<JsonElement>(tableJson, _jsonOptions);
                    
                    var table = new TableInfo
                    {
                        Schema = tableData.GetProperty("schema").GetString() ?? schemaName,
                        TableName = tableData.GetProperty("tableName").GetString() ?? Path.GetFileNameWithoutExtension(tableFile),
                        RowCount = tableData.GetProperty("rowCount").GetInt64(),
                        PrimaryKeyColumns = tableData.GetProperty("primaryKeyColumns").EnumerateArray()
                            .Select(e => e.GetString() ?? "").ToList(),
                        Columns = new List<ColumnInfo>()
                    };

                    foreach (var col in tableData.GetProperty("columns").EnumerateArray())
                    {
                        var column = new ColumnInfo
                        {
                            ColumnName = col.GetProperty("columnName").GetString() ?? "",
                            SqlDataType = col.GetProperty("sqlDataType").GetString() ?? "",
                            MaxLength = col.TryGetProperty("maxLength", out var ml) && ml.ValueKind != JsonValueKind.Null ? ml.GetInt32() : null,
                            IsNullable = col.GetProperty("isNullable").GetBoolean(),
                            IsPrimaryKey = col.GetProperty("isPrimaryKey").GetBoolean(),
                            OrdinalPosition = col.GetProperty("ordinalPosition").GetInt32(),
                            DefaultValue = col.TryGetProperty("defaultValue", out var dv) && dv.ValueKind != JsonValueKind.Null ? dv.GetString() : null
                        };

                        table.Columns.Add(column);
                    }

                    schema.Tables.Add(table);
                }
            }

            _logger.LogInformation("Loaded schema for database {Database} with {TableCount} tables", 
                databaseName, schema.Tables.Count);
            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load schema from directory {Directory}", schemaDirectory);
            return null;
        }
    }
}