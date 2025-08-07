using Npgsql;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text;

namespace DataObfuscation.Data;

public class PostgreSQLRepository : IDataRepository
{
    private readonly ILogger<PostgreSQLRepository> _logger;
    private string _connectionString = string.Empty;

    public PostgreSQLRepository(ILogger<PostgreSQLRepository> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(string connectionString)
    {
        _connectionString = connectionString;
        _logger.LogInformation("PostgreSQL repository initialized");
        return Task.CompletedTask;
    }

    public async Task<long> GetRowCountAsync(string tableName, string? whereClause = null)
    {
        var sql = $"SELECT COUNT(*) FROM {FormatTableName(tableName)}";
        if (!string.IsNullOrEmpty(whereClause))
        {
            sql += $" WHERE {whereClause}";
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = new NpgsqlCommand(sql, connection);
            command.CommandTimeout = 600; // 10 minutes
            var result = await command.ExecuteScalarAsync();
            
            return Convert.ToInt64(result);
        }
        catch (PostgresException ex) when (ex.SqlState == "08000" || ex.SqlState == "08003" || ex.SqlState == "08006")
        {
            _logger.LogError(ex, "Database connection error getting row count for table {TableName}. SQL State: {SqlState}", tableName, ex.SqlState);
            throw;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            _logger.LogError(ex, "Table '{TableName}' does not exist or is not accessible", tableName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting row count for table {TableName}", tableName);
            throw;
        }
    }

    public async Task<List<Dictionary<string, object?>>> GetBatchAsync(
        string tableName, 
        List<string> columns, 
        List<string> primaryKeyColumns,
        int offset, 
        int batchSize, 
        string? whereClause = null)
    {
        var columnList = string.Join(", ", columns.Concat(primaryKeyColumns).Distinct().Select(c => $"\"{c}\""));
        var orderBy = string.Join(", ", primaryKeyColumns.Select(pk => $"\"{pk}\""));
        
        var sql = new StringBuilder();
        sql.AppendLine($"SELECT {columnList}");
        sql.AppendLine($"FROM {FormatTableName(tableName)}");
        
        if (!string.IsNullOrEmpty(whereClause))
        {
            sql.AppendLine($"WHERE {whereClause}");
        }
        
        sql.AppendLine($"ORDER BY {orderBy}");
        sql.AppendLine($"LIMIT {batchSize} OFFSET {offset}");

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = new NpgsqlCommand(sql.ToString(), connection);
            command.CommandTimeout = 600; // 10 minutes
            
            using var reader = await command.ExecuteReaderAsync();
            
            var batch = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columnName] = value;
                }
                batch.Add(row);
            }
            
            return batch;
        }
        catch (PostgresException ex) when (ex.SqlState == "08000" || ex.SqlState == "08003" || ex.SqlState == "08006")
        {
            _logger.LogError(ex, "Database connection error getting batch for table {TableName}. SQL State: {SqlState}", tableName, ex.SqlState);
            throw;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            _logger.LogError(ex, "Table '{TableName}' does not exist or is not accessible", tableName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting batch for table {TableName}", tableName);
            throw;
        }
    }

    public async Task<UpdateBatchResult> UpdateBatchAsync(
        string tableName, 
        List<Dictionary<string, object?>> batch, 
        List<string> primaryKeyColumns)
    {
        return await UpdateBatchAsync(tableName, batch, primaryKeyColumns, 100);
    }

    public async Task<UpdateBatchResult> UpdateBatchAsync(
        string tableName, 
        List<Dictionary<string, object?>> batch, 
        List<string> primaryKeyColumns,
        int sqlBatchSize)
    {
        if (batch.Count == 0)
        {
            return new UpdateBatchResult { SuccessfulRows = 0 };
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var transaction = await connection.BeginTransactionAsync();
            
            try
            {
                var result = await UpdateSqlBatchAsync(tableName, batch, primaryKeyColumns);
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "08000" || ex.SqlState == "08003" || ex.SqlState == "08006")
        {
            _logger.LogError(ex, "Database connection error updating batch for table {TableName}. SQL State: {SqlState}", tableName, ex.SqlState);
            throw;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            _logger.LogError(ex, "Table '{TableName}' does not exist or is not accessible", tableName);
            throw;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            _logger.LogError(ex, "Unique constraint violation updating table {TableName}", tableName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating batch for table {TableName}", tableName);
            throw;
        }
    }

    private async Task<UpdateBatchResult> UpdateSqlBatchAsync(
        string tableName, 
        List<Dictionary<string, object?>> batch, 
        List<string> primaryKeyColumns)
    {
        if (batch.Count == 0)
        {
            return new UpdateBatchResult { SuccessfulRows = 0 };
        }

        var sampleRow = batch[0];
        var dataColumns = sampleRow.Keys.Where(k => !primaryKeyColumns.Contains(k)).ToList();
        
        if (dataColumns.Count == 0)
        {
            _logger.LogWarning("No data columns found for table {TableName}, skipping update", tableName);
            return new UpdateBatchResult { SuccessfulRows = 0 };
        }

        var tempTableName = $"temp_{tableName.Replace(".", "_").Replace("\"", "")}_{Guid.NewGuid():N}";
        
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var transaction = await connection.BeginTransactionAsync();
            
            try
            {
                // Create temporary table
                await CreateTempTableAsync(connection, transaction, tempTableName, sampleRow, primaryKeyColumns, dataColumns);
                
                // Bulk insert data into temp table using COPY
                await BulkInsertToTempTableAsync(connection, transaction, tempTableName, batch, primaryKeyColumns, dataColumns);
                
                // Update main table from temp table
                var updatedRows = await BulkUpdateFromTempTableAsync(connection, transaction, tableName, tempTableName, primaryKeyColumns, dataColumns);
                
                await transaction.CommitAsync();
                
                return new UpdateBatchResult { SuccessfulRows = updatedRows };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk update operation for table {TableName}", tableName);
            throw;
        }
    }

    private async Task CreateTempTableAsync(
        NpgsqlConnection connection, 
        NpgsqlTransaction transaction, 
        string tempTableName, 
        Dictionary<string, object?> sampleRow,
        List<string> primaryKeyColumns,
        List<string> dataColumns)
    {
        var allColumns = primaryKeyColumns.Concat(dataColumns).ToList();
        var columnDefinitions = allColumns.Select(col => $"\"{col}\" {GetPostgreSQLTypeString(sampleRow[col])}").ToList();
        
        var createTableSql = $"CREATE TEMP TABLE \"{tempTableName}\" ({string.Join(", ", columnDefinitions)})";
        
        using var command = new NpgsqlCommand(createTableSql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private async Task BulkInsertToTempTableAsync(
        NpgsqlConnection connection, 
        NpgsqlTransaction transaction, 
        string tempTableName, 
        List<Dictionary<string, object?>> batch,
        List<string> primaryKeyColumns,
        List<string> dataColumns)
    {
        var allColumns = primaryKeyColumns.Concat(dataColumns).ToList();
        var columnList = string.Join(", ", allColumns.Select(c => $"\"{c}\""));
        
        // Use COPY command for bulk insert (PostgreSQL's equivalent to SqlBulkCopy)
        using var writer = connection.BeginBinaryImport($"COPY \"{tempTableName}\" ({columnList}) FROM STDIN");
        
        foreach (var row in batch)
        {
            var values = allColumns.Select(col => row.ContainsKey(col) ? row[col] : null).ToArray();
            writer.StartRow();
            
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null || values[i] == DBNull.Value)
                {
                    writer.WriteNull();
                }
                else
                {
                    writer.Write(values[i]);
                }
            }
        }
        
        writer.Complete();
    }

    private async Task<int> BulkUpdateFromTempTableAsync(
        NpgsqlConnection connection, 
        NpgsqlTransaction transaction, 
        string tableName, 
        string tempTableName,
        List<string> primaryKeyColumns,
        List<string> dataColumns)
    {
        // PostgreSQL uses UPDATE with FROM clause instead of MERGE
        var setClause = string.Join(", ", dataColumns.Select(col => $"\"{col}\" = temp.\"{col}\""));
        var joinCondition = string.Join(" AND ", primaryKeyColumns.Select(pk => $"t.\"{pk}\" = temp.\"{pk}\""));
        
        var updateSql = $@"
            UPDATE {FormatTableName(tableName)} AS t 
            SET {setClause}
            FROM ""{tempTableName}"" AS temp
            WHERE {joinCondition}";
        
        using var command = new NpgsqlCommand(updateSql, connection, transaction);
        var rowsAffected = await command.ExecuteNonQueryAsync();
        
        return rowsAffected;
    }

    public async Task<bool> TableExistsAsync(string tableName)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM information_schema.tables 
            WHERE table_schema = @Schema AND table_name = @TableName";

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = new NpgsqlCommand(sql, connection);
            
            var parts = tableName.Split('.');
            if (parts.Length == 2)
            {
                command.Parameters.AddWithValue("@Schema", parts[0]);
                command.Parameters.AddWithValue("@TableName", parts[1]);
            }
            else
            {
                command.Parameters.AddWithValue("@Schema", "public");
                command.Parameters.AddWithValue("@TableName", tableName);
            }
            
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if table {TableName} exists", tableName);
            return false;
        }
    }

    public async Task<List<string>> GetTableColumnsAsync(string tableName)
    {
        const string sql = @"
            SELECT column_name 
            FROM information_schema.columns 
            WHERE table_schema = @Schema AND table_name = @TableName 
            ORDER BY ordinal_position";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var command = new NpgsqlCommand(sql, connection);
        
        var parts = tableName.Split('.');
        if (parts.Length == 2)
        {
            command.Parameters.AddWithValue("@Schema", parts[0]);
            command.Parameters.AddWithValue("@TableName", parts[1]);
        }
        else
        {
            command.Parameters.AddWithValue("@Schema", "public");
            command.Parameters.AddWithValue("@TableName", tableName);
        }
        
        using var reader = await command.ExecuteReaderAsync();
        
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString("column_name"));
        }
        
        return columns;
    }

    private static string FormatTableName(string tableName)
    {
        // Handle schema.table format correctly
        if (tableName.Contains('.') && !tableName.StartsWith('"'))
        {
            var parts = tableName.Split('.');
            if (parts.Length == 2)
            {
                return $"\"{parts[0]}\".\"{parts[1]}\"";
            }
        }
        
        // Handle simple table names
        return tableName.StartsWith('"') ? tableName : $"\"{tableName}\"";
    }

    private static string GetPostgreSQLTypeString(object? value)
    {
        return value switch
        {
            null => "TEXT",
            string stringValue => $"VARCHAR({Math.Max(stringValue.Length + 50, 100)})",
            int => "INTEGER",
            long => "BIGINT",
            short => "SMALLINT",
            byte => "SMALLINT",
            decimal => "NUMERIC(18,2)",
            double => "DOUBLE PRECISION",
            float => "REAL",
            bool => "BOOLEAN",
            DateTime => "TIMESTAMP",
            DateTimeOffset => "TIMESTAMPTZ",
            Guid => "UUID",
            byte[] => "BYTEA",
            _ => "TEXT"
        };
    }
} 