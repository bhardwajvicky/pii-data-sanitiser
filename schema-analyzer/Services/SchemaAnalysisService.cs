using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SchemaAnalyzer.Models;

namespace SchemaAnalyzer.Services;

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
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.CHARACTER_MAXIMUM_LENGTH,
                CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IsNullable,
                c.COLUMN_DEFAULT,
                c.ORDINAL_POSITION
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_SCHEMA = @SchemaName 
                AND c.TABLE_NAME = @TableName
            ORDER BY c.ORDINAL_POSITION";

        var columns = new List<ColumnInfo>();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                ColumnName = reader.GetString(0),
                SqlDataType = reader.GetString(1),
                MaxLength = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                IsNullable = reader.GetInt32(3) == 1,
                DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                OrdinalPosition = reader.GetInt32(5)
            });
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