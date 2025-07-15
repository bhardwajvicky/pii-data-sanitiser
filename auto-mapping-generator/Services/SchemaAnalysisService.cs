using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using AutoMappingGenerator.Models;

namespace AutoMappingGenerator.Services;

public interface ISchemaAnalysisService
{
    Task<DatabaseSchema> AnalyzeDatabaseSchemaAsync(string connectionString);
    Task<PIIAnalysisResult> IdentifyPIIColumnsAsync(DatabaseSchema schema);
}

public class SchemaAnalysisService : ISchemaAnalysisService
{
    private readonly ILogger<SchemaAnalysisService> _logger;
    private readonly IPIIDetectionService _piiDetectionService;

    public SchemaAnalysisService(ILogger<SchemaAnalysisService> logger, IPIIDetectionService piiDetectionService)
    {
        _logger = logger;
        _piiDetectionService = piiDetectionService;
    }

    public async Task<DatabaseSchema> AnalyzeDatabaseSchemaAsync(string connectionString)
    {
        _logger.LogInformation("Starting database schema analysis");

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var databaseName = connection.Database;
        var schema = new DatabaseSchema { DatabaseName = databaseName };

        // Get all tables
        var tables = await GetTablesAsync(connection);
        _logger.LogInformation("Found {TableCount} tables", tables.Count);

        foreach (var table in tables)
        {
            _logger.LogDebug("Analyzing table: {TableName}", table.FullName);

            // Get columns for each table
            table.Columns = await GetColumnsAsync(connection, table.Schema, table.TableName);
            
            // Get primary key information
            table.PrimaryKeyColumns = await GetPrimaryKeyColumnsAsync(connection, table.Schema, table.TableName);
            
            // Mark primary key columns
            foreach (var pkColumn in table.PrimaryKeyColumns)
            {
                var column = table.Columns.FirstOrDefault(c => c.ColumnName == pkColumn);
                if (column != null)
                {
                    column.IsPrimaryKey = true;
                }
            }

            // Get row count
            table.RowCount = await GetTableRowCountAsync(connection, table.Schema, table.TableName);

            schema.Tables.Add(table);
        }

        _logger.LogInformation("Schema analysis completed. Found {TableCount} tables with {ColumnCount} total columns",
            schema.Tables.Count, schema.Tables.Sum(t => t.Columns.Count));

        return schema;
    }

    public Task<PIIAnalysisResult> IdentifyPIIColumnsAsync(DatabaseSchema schema)
    {
        _logger.LogInformation("Starting PII identification for {TableCount} tables", schema.Tables.Count);

        var result = new PIIAnalysisResult { DatabaseName = schema.DatabaseName };

        foreach (var table in schema.Tables)
        {
            var piiColumns = new List<PIIColumn>();

            foreach (var column in table.Columns)
            {
                var piiResult = _piiDetectionService.AnalyzeColumn(table, column);
                if (piiResult != null)
                {
                    piiColumns.Add(piiResult);
                }
            }

            if (piiColumns.Any())
            {
                var tableWithPII = new TableWithPII
                {
                    Schema = table.Schema,
                    TableName = table.TableName,
                    PrimaryKeyColumns = table.PrimaryKeyColumns,
                    RowCount = table.RowCount,
                    PIIColumns = piiColumns,
                    Priority = DeterminePriority(table, piiColumns)
                };

                result.TablesWithPII.Add(tableWithPII);
                _logger.LogInformation("Table {TableName} contains {PIIColumnCount} PII columns",
                    table.FullName, piiColumns.Count);
            }
        }

        _logger.LogInformation("PII identification completed. Found {PIITableCount} tables with PII",
            result.TablesWithPII.Count);

        return Task.FromResult(result);
    }

    private async Task<List<TableInfo>> GetTablesAsync(SqlConnection connection)
    {
        const string sql = @"
            SELECT 
                s.name AS SchemaName,
                t.name AS TableName
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE t.type = 'U'  -- User tables only
            ORDER BY s.name, t.name";

        var tables = new List<TableInfo>();

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo
            {
                Schema = reader.GetString(0),
                TableName = reader.GetString(1)
            });
        }

        return tables;
    }

    private async Task<List<ColumnInfo>> GetColumnsAsync(SqlConnection connection, string schemaName, string tableName)
    {
        const string sql = @"
            SELECT 
                c.name AS ColumnName,
                t.name AS DataType,
                c.max_length,
                c.is_nullable,
                dc.definition AS DefaultValue,
                c.column_id AS OrdinalPosition,
                c.is_identity,
                c.is_computed,
                c.precision,
                c.scale,
                c.is_rowguidcol,
                c.is_filestream,
                c.is_sparse,
                c.is_xml_document,
                c.collation_name,
                CASE 
                    WHEN fk.parent_column_id IS NOT NULL THEN 1 
                    ELSE 0 
                END AS IsForeignKey
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            INNER JOIN sys.tables tb ON c.object_id = tb.object_id
            INNER JOIN sys.schemas s ON tb.schema_id = s.schema_id
            LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
            LEFT JOIN sys.foreign_key_columns fk ON c.object_id = fk.parent_object_id AND c.column_id = fk.parent_column_id
            WHERE s.name = @SchemaName 
                AND tb.name = @TableName
            ORDER BY c.column_id";

        var columns = new List<ColumnInfo>();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var column = new ColumnInfo
            {
                ColumnName = reader.GetString(0),
                SqlDataType = reader.GetString(1),
                MaxLength = reader.GetInt16(2),
                IsNullable = reader.GetBoolean(3),
                DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                OrdinalPosition = reader.GetInt32(5),
                IsIdentity = reader.GetBoolean(6),
                IsComputed = reader.GetBoolean(7),
                NumericPrecision = reader.IsDBNull(8) ? null : reader.GetByte(8),
                NumericScale = reader.IsDBNull(9) ? null : reader.GetByte(9),
                IsRowGuid = reader.GetBoolean(10),
                IsFileStream = reader.GetBoolean(11),
                IsSparse = reader.GetBoolean(12),
                IsXmlDocument = reader.GetBoolean(13),
                Collation = reader.IsDBNull(14) ? null : reader.GetString(14),
                IsForeignKey = reader.GetInt32(15) == 1
            };

            // Adjust max length for nvarchar/nchar types (stored as byte count in sys.columns)
            if (column.SqlDataType.StartsWith("n") && column.MaxLength > 0)
            {
                column.MaxLength = column.MaxLength / 2;
            }

            // Handle max length for varchar(max), nvarchar(max), etc.
            if (column.MaxLength == -1)
            {
                column.MaxLength = null; // Represents MAX
            }

            columns.Add(column);
        }

        return columns;
    }

    private async Task<List<string>> GetPrimaryKeyColumnsAsync(SqlConnection connection, string schemaName, string tableName)
    {
        const string sql = @"
            SELECT c.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            INNER JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu 
                ON tc.CONSTRAINT_NAME = ccu.CONSTRAINT_NAME 
                AND tc.TABLE_SCHEMA = ccu.TABLE_SCHEMA
            INNER JOIN INFORMATION_SCHEMA.COLUMNS c 
                ON ccu.COLUMN_NAME = c.COLUMN_NAME 
                AND ccu.TABLE_NAME = c.TABLE_NAME 
                AND ccu.TABLE_SCHEMA = c.TABLE_SCHEMA
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                AND tc.TABLE_SCHEMA = @SchemaName
                AND tc.TABLE_NAME = @TableName
            ORDER BY c.ORDINAL_POSITION";

        var primaryKeyColumns = new List<string>();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            primaryKeyColumns.Add(reader.GetString(0));
        }

        return primaryKeyColumns;
    }

    private async Task<long> GetTableRowCountAsync(SqlConnection connection, string schemaName, string tableName)
    {
        try
        {
            var sql = $"SELECT COUNT(*) FROM [{schemaName}].[{tableName}]";
            using var command = new SqlCommand(sql, connection);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }
        catch (SqlException ex) when (ex.Number == 10347) // CLR not enabled
        {
            _logger.LogInformation("Table {SchemaName}.{TableName} has CLR dependencies - using default row count estimation", schemaName, tableName);
            return 10000; // Default estimate for batch sizing
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get row count for table {SchemaName}.{TableName}", schemaName, tableName);
            return 0;
        }
    }

    private static int DeterminePriority(TableInfo table, List<PIIColumn> piiColumns)
    {
        // Assign priority based on table importance and PII sensitivity
        var tableName = table.TableName.ToLower();
        var piiCount = piiColumns.Count;
        var hasHighSensitivityPII = piiColumns.Any(c => 
            c.DataType == "PersonName" || 
            c.DataType == "SSN" || 
            c.DataType == "CreditCardNumber");

        // High priority tables (process first)
        if (tableName.Contains("employee") || tableName.Contains("person") || tableName.Contains("customer"))
            return 1;

        if (hasHighSensitivityPII)
            return 2;

        if (piiCount >= 5)
            return 3;

        if (piiCount >= 3)
            return 5;

        return 10; // Default priority
    }
}