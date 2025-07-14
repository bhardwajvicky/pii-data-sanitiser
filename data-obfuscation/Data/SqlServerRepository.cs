using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text;

namespace DataObfuscation.Data;

public interface IDataRepository
{
    Task InitializeAsync(string connectionString);
    Task<long> GetRowCountAsync(string tableName, string? whereClause = null);
    Task<List<Dictionary<string, object?>>> GetBatchAsync(
        string tableName, 
        List<string> columns, 
        List<string> primaryKeyColumns,
        int offset, 
        int batchSize, 
        string? whereClause = null);
    Task<UpdateBatchResult> UpdateBatchAsync(
        string tableName, 
        List<Dictionary<string, object?>> batch, 
        List<string> primaryKeyColumns);
    Task<bool> TableExistsAsync(string tableName);
    Task<List<string>> GetTableColumnsAsync(string tableName);
}

public class SqlServerRepository : IDataRepository
{
    private readonly ILogger<SqlServerRepository> _logger;
    private string _connectionString = string.Empty;

    public SqlServerRepository(ILogger<SqlServerRepository> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(string connectionString)
    {
        _connectionString = connectionString;
        _logger.LogInformation("SQL Server repository initialized");
        return Task.CompletedTask;
    }

    public async Task<long> GetRowCountAsync(string tableName, string? whereClause = null)
    {
        var sql = $"SELECT COUNT(*) FROM {FormatTableName(tableName)}";
        if (!string.IsNullOrEmpty(whereClause))
        {
            sql += $" WHERE {whereClause}";
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var command = new SqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        
        return Convert.ToInt64(result);
    }

    public async Task<List<Dictionary<string, object?>>> GetBatchAsync(
        string tableName, 
        List<string> columns, 
        List<string> primaryKeyColumns,
        int offset, 
        int batchSize, 
        string? whereClause = null)
    {
        var columnList = string.Join(", ", columns.Concat(primaryKeyColumns).Distinct().Select(c => $"[{c}]"));
        var orderBy = string.Join(", ", primaryKeyColumns.Select(pk => $"[{pk}]"));
        
        var sql = new StringBuilder();
        sql.AppendLine($"SELECT {columnList}");
        sql.AppendLine($"FROM {FormatTableName(tableName)}");
        
        if (!string.IsNullOrEmpty(whereClause))
        {
            sql.AppendLine($"WHERE {whereClause}");
        }
        
        sql.AppendLine($"ORDER BY {orderBy}");
        sql.AppendLine($"OFFSET {offset} ROWS");
        sql.AppendLine($"FETCH NEXT {batchSize} ROWS ONLY");

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var command = new SqlCommand(sql.ToString(), connection);
        command.CommandTimeout = 600; // 10 minutes
        
        using var reader = await command.ExecuteReaderAsync();
        
        var results = new List<Dictionary<string, object?>>();
        
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[columnName] = value;
            }
            
            results.Add(row);
        }
        
        return results;
    }

    public async Task<UpdateBatchResult> UpdateBatchAsync(
        string tableName, 
        List<Dictionary<string, object?>> batch, 
        List<string> primaryKeyColumns)
    {
        var result = new UpdateBatchResult();
        if (!batch.Any())
            return result;

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        
        try
        {
            foreach (var row in batch)
            {
                try
                {
                    await UpdateRowAsync(tableName, row, primaryKeyColumns, connection, transaction);
                    result.SuccessfulRows++;
                }
                catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) // Unique constraint violations
                {
                    result.FailedRows.Add(new FailedRow
                    {
                        TableName = tableName,
                        PrimaryKeyValues = primaryKeyColumns.ToDictionary(pk => pk, pk => row[pk]),
                        UpdatedValues = row.Where(kvp => !primaryKeyColumns.Contains(kvp.Key))
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        ErrorMessage = ex.Message,
                        SqlErrorNumber = ex.Number
                    });
                    result.SkippedRows++;
                    
                    _logger.LogWarning("Skipped duplicate row in table {TableName}: {ErrorMessage}", 
                        tableName, ex.Message);
                    // Continue with next row instead of throwing
                }
            }
            
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            result.HasCriticalError = true;
            result.CriticalErrorMessage = ex.Message;
            throw;
        }
        
        return result;
    }

    private async Task UpdateRowAsync(
        string tableName,
        Dictionary<string, object?> row,
        List<string> primaryKeyColumns,
        SqlConnection connection,
        SqlTransaction transaction)
    {
        var dataColumns = row.Keys.Except(primaryKeyColumns).ToList();
        
        if (!dataColumns.Any())
            return;

        var setClause = string.Join(", ", dataColumns.Select(col => $"[{col}] = @{col}"));
        var whereClause = string.Join(" AND ", primaryKeyColumns.Select(pk => $"[{pk}] = @{pk}"));
        
        var sql = $"UPDATE {FormatTableName(tableName)} SET {setClause} WHERE {whereClause}";
        
        using var command = new SqlCommand(sql, connection, transaction);
        command.CommandTimeout = 600;
        
        foreach (var kvp in row)
        {
            var parameter = command.Parameters.Add($"@{kvp.Key}", GetSqlDbType(kvp.Value));
            parameter.Value = ConvertValueForDatabase(kvp.Value);
            
            if (kvp.Value is string stringValue && parameter.SqlDbType == SqlDbType.NVarChar)
            {
                parameter.Size = Math.Max(stringValue.Length, 1);
            }
        }
        
        var rowsAffected = await command.ExecuteNonQueryAsync();
        
        if (rowsAffected == 0)
        {
            var pkValues = string.Join(", ", primaryKeyColumns.Select(pk => $"{pk}={row[pk]}"));
            _logger.LogWarning("No rows updated for table {TableName} with PK: {PrimaryKey}", tableName, pkValues);
        }
    }

    public async Task<bool> TableExistsAsync(string tableName)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME = @TableName AND TABLE_TYPE = 'BASE TABLE'";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TableName", tableName);
        
        var result = await command.ExecuteScalarAsync();
        var count = Convert.ToInt32(result);
        return count > 0;
    }

    public async Task<List<string>> GetTableColumnsAsync(string tableName)
    {
        const string sql = @"
            SELECT COLUMN_NAME 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = @TableName 
            ORDER BY ORDINAL_POSITION";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TableName", tableName);
        
        using var reader = await command.ExecuteReaderAsync();
        
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString("COLUMN_NAME"));
        }
        
        return columns;
    }

    private static string FormatTableName(string tableName)
    {
        // Handle schema.table format correctly
        if (tableName.Contains('.') && !tableName.StartsWith('['))
        {
            var parts = tableName.Split('.');
            if (parts.Length == 2)
            {
                return $"[{parts[0]}].[{parts[1]}]";
            }
        }
        
        // Handle simple table names
        return tableName.StartsWith('[') ? tableName : $"[{tableName}]";
    }

    private static SqlDbType GetSqlDbType(object? value)
    {
        return value switch
        {
            null => SqlDbType.NVarChar,
            string => SqlDbType.NVarChar,
            int => SqlDbType.Int,
            long => SqlDbType.BigInt,
            short => SqlDbType.SmallInt,
            byte => SqlDbType.TinyInt,
            decimal => SqlDbType.Decimal,
            double => SqlDbType.Float,
            float => SqlDbType.Real,
            bool => SqlDbType.Bit,
            DateTime => SqlDbType.DateTime,
            DateTimeOffset => SqlDbType.DateTimeOffset,
            Guid => SqlDbType.UniqueIdentifier,
            byte[] => SqlDbType.VarBinary,
            _ => SqlDbType.NVarChar
        };
    }

    private static object? ConvertValueForDatabase(object? value)
    {
        if (value == null)
            return DBNull.Value;

        // Handle specific conversions
        return value switch
        {
            string stringValue when string.IsNullOrEmpty(stringValue) => DBNull.Value,
            _ => value
        };
    }
}