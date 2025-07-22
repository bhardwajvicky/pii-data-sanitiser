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
    Task<UpdateBatchResult> UpdateBatchAsync(
        string tableName, 
        List<Dictionary<string, object?>> batch, 
        List<string> primaryKeyColumns,
        int sqlBatchSize);
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
        return await UpdateBatchAsync(tableName, batch, primaryKeyColumns, 100); // Default SQL batch size
    }

    public async Task<UpdateBatchResult> UpdateBatchAsync(
        string tableName, 
        List<Dictionary<string, object?>> batch, 
        List<string> primaryKeyColumns,
        int sqlBatchSize)
    {
        var result = new UpdateBatchResult();
        if (!batch.Any())
            return result;

        // Process the batch in smaller SQL batches for optimal performance
        for (int i = 0; i < batch.Count; i += sqlBatchSize)
        {
            var sqlBatch = batch.Skip(i).Take(sqlBatchSize).ToList();
            var batchResult = await UpdateSqlBatchAsync(tableName, sqlBatch, primaryKeyColumns);
            
            result.SuccessfulRows += batchResult.SuccessfulRows;
            result.SkippedRows += batchResult.SkippedRows;
            result.FailedRows.AddRange(batchResult.FailedRows);
            
            if (batchResult.HasCriticalError)
            {
                result.HasCriticalError = true;
                result.CriticalErrorMessage = batchResult.CriticalErrorMessage;
                break;
            }
        }

        return result;
    }

    private async Task<UpdateBatchResult> UpdateSqlBatchAsync(
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
            // Create a temporary table to hold the update data
            var tempTableName = $"#TempUpdate_{Guid.NewGuid():N}";
            var dataColumns = batch.First().Keys.Except(primaryKeyColumns).ToList();
            
            if (!dataColumns.Any())
            {
                result.SuccessfulRows = batch.Count;
                return result;
            }

            // Create temporary table
            await CreateTempTableAsync(connection, transaction, tempTableName, batch.First(), primaryKeyColumns, dataColumns);
            
            // Insert data into temporary table using bulk insert
            await BulkInsertToTempTableAsync(connection, transaction, tempTableName, batch, primaryKeyColumns, dataColumns);
            
            // Perform bulk update using the temporary table
            var updatedRows = await BulkUpdateFromTempTableAsync(connection, transaction, tableName, tempTableName, primaryKeyColumns, dataColumns);
            
            result.SuccessfulRows = updatedRows;
            
            // Handle cases where some rows weren't updated (might not exist)
            var expectedRows = batch.Count;
            if (updatedRows < expectedRows)
            {
                _logger.LogWarning("Expected to update {ExpectedRows} rows but only updated {UpdatedRows} in table {TableName}", 
                    expectedRows, updatedRows, tableName);
                result.SkippedRows = expectedRows - updatedRows;
            }
            
            await transaction.CommitAsync();
        }
        catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) // Unique constraint violations
        {
            await transaction.RollbackAsync();
            _logger.LogWarning("Unique constraint violation in batch update for table {TableName}, falling back to row-by-row: {ErrorMessage}", 
                tableName, ex.Message);
            
            // Fall back to row-by-row for this batch to handle unique constraints
            result = await UpdateBatchRowByRowAsync(tableName, batch, primaryKeyColumns);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            result.HasCriticalError = true;
            result.CriticalErrorMessage = ex.Message;
            _logger.LogError(ex, "Critical error during batch update for table {TableName}", tableName);
            throw;
        }
        
        return result;
    }

    private async Task CreateTempTableAsync(
        SqlConnection connection, 
        SqlTransaction transaction, 
        string tempTableName, 
        Dictionary<string, object?> sampleRow,
        List<string> primaryKeyColumns,
        List<string> dataColumns)
    {
        var allColumns = primaryKeyColumns.Concat(dataColumns);
        var columnDefinitions = allColumns.Select(col => 
        {
            var sampleValue = sampleRow[col];
            var sqlType = GetSqlTypeString(sampleValue);
            return $"[{col}] {sqlType}";
        });

        var createTableSql = $"CREATE TABLE {tempTableName} ({string.Join(", ", columnDefinitions)})";
        
        using var command = new SqlCommand(createTableSql, connection, transaction);
        command.CommandTimeout = 600;
        await command.ExecuteNonQueryAsync();
    }

    private async Task BulkInsertToTempTableAsync(
        SqlConnection connection, 
        SqlTransaction transaction, 
        string tempTableName, 
        List<Dictionary<string, object?>> batch,
        List<string> primaryKeyColumns,
        List<string> dataColumns)
    {
        var allColumns = primaryKeyColumns.Concat(dataColumns).ToList();
        var parameterSets = new List<string>();
        
        using var command = new SqlCommand();
        command.Connection = connection;
        command.Transaction = transaction;
        command.CommandTimeout = 600;

        for (int i = 0; i < batch.Count; i++)
        {
            var row = batch[i];
            var parameters = allColumns.Select(col => $"@{col}_{i}").ToList();
            parameterSets.Add($"({string.Join(", ", parameters)})");
            
            foreach (var col in allColumns)
            {
                var parameter = command.Parameters.Add($"@{col}_{i}", GetSqlDbType(row[col]));
                parameter.Value = ConvertValueForDatabase(row[col]);
                
                if (row[col] is string stringValue && parameter.SqlDbType == SqlDbType.NVarChar)
                {
                    parameter.Size = Math.Max(stringValue.Length, 1);
                }
            }
        }

        var insertSql = $"INSERT INTO {tempTableName} ([{string.Join("], [", allColumns)}]) VALUES {string.Join(", ", parameterSets)}";
        command.CommandText = insertSql;
        
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> BulkUpdateFromTempTableAsync(
        SqlConnection connection, 
        SqlTransaction transaction, 
        string tableName, 
        string tempTableName,
        List<string> primaryKeyColumns,
        List<string> dataColumns)
    {
        var setClause = string.Join(", ", dataColumns.Select(col => $"t.[{col}] = temp.[{col}]"));
        var joinClause = string.Join(" AND ", primaryKeyColumns.Select(pk => $"t.[{pk}] = temp.[{pk}]"));
        
        var updateSql = $@"
            UPDATE t 
            SET {setClause}
            FROM {FormatTableName(tableName)} t
            INNER JOIN {tempTableName} temp ON {joinClause}";
        
        using var command = new SqlCommand(updateSql, connection, transaction);
        command.CommandTimeout = 600;
        
        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected;
    }

    private async Task<UpdateBatchResult> UpdateBatchRowByRowAsync(
        string tableName, 
        List<Dictionary<string, object?>> batch, 
        List<string> primaryKeyColumns)
    {
        var result = new UpdateBatchResult();
        
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

    private static string GetSqlTypeString(object? value)
    {
        return value switch
        {
            null => "NVARCHAR(MAX)",
            string stringValue => $"NVARCHAR({Math.Max(stringValue.Length + 50, 100)})", // Add buffer for obfuscated values
            int => "INT",
            long => "BIGINT",
            short => "SMALLINT",
            byte => "TINYINT",
            decimal => "DECIMAL(18,2)",
            double => "FLOAT",
            float => "REAL",
            bool => "BIT",
            DateTime => "DATETIME",
            DateTimeOffset => "DATETIMEOFFSET",
            Guid => "UNIQUEIDENTIFIER",
            byte[] => "VARBINARY(MAX)",
            _ => "NVARCHAR(MAX)"
        };
    }
}