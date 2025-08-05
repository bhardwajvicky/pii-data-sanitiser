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

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = 600; // 10 minutes
            var result = await command.ExecuteScalarAsync();
            
            return Convert.ToInt64(result);
        }
        catch (SqlException ex) when (ex.Number == -2 || ex.Number == 53 || ex.Number == 10060 || ex.Number == 10061)
        {
            _logger.LogError(ex, "Database connection error getting row count for table {TableName}. Error Number: {ErrorNumber}", tableName, ex.Number);
            throw;
        }
        catch (SqlException ex) when (ex.Number == 208)
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

        try
        {
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
        catch (SqlException ex) when (ex.Number == -2 || ex.Number == 53 || ex.Number == 10060 || ex.Number == 10061)
        {
            _logger.LogError(ex, "Database connection error getting batch for table {TableName}. Error Number: {ErrorNumber}", tableName, ex.Number);
            throw;
        }
        catch (SqlException ex) when (ex.Number == 208)
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

            // Perform bulk update using the optimized approach (handles temp table creation and data loading)
            var updatedRows = await BulkUpdateWithMergeAsync(connection, transaction, tableName, batch, primaryKeyColumns, dataColumns);
            
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
        catch (SqlException ex) when (ex.Number == -2 || ex.Number == 53 || ex.Number == 10060 || ex.Number == 10061)
        {
            // Network-related errors (timeout, connection refused, etc.)
            await transaction.RollbackAsync();
            result.HasCriticalError = true;
            result.CriticalErrorMessage = $"Database connection error: {ex.Message}";
            _logger.LogError(ex, "Database connection error during batch update for table {TableName}. Error Number: {ErrorNumber}", tableName, ex.Number);
            throw;
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            // Deadlock
            await transaction.RollbackAsync();
            result.HasCriticalError = true;
            result.CriticalErrorMessage = $"Deadlock detected: {ex.Message}";
            _logger.LogError(ex, "Deadlock detected during batch update for table {TableName}", tableName);
            throw;
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



    private async Task<int> BulkUpdateWithMergeAsync(
        SqlConnection connection, 
        SqlTransaction transaction, 
        string tableName, 
        List<Dictionary<string, object?>> batch,
        List<string> primaryKeyColumns,
        List<string> dataColumns)
    {
        // Create a DataTable for bulk copy
        var dataTable = new DataTable();
        var allColumns = primaryKeyColumns.Concat(dataColumns).ToList();
        
        // Add columns to DataTable
        foreach (var column in allColumns)
        {
            var sampleValue = batch.First()[column];
            var dataType = GetClrType(sampleValue);
            dataTable.Columns.Add(column, dataType);
        }
        
        // Add rows to DataTable
        foreach (var row in batch)
        {
            var dataRow = dataTable.NewRow();
            foreach (var column in allColumns)
            {
                dataRow[column] = ConvertValueForDatabase(row[column]) ?? DBNull.Value;
            }
            dataTable.Rows.Add(dataRow);
        }
        
        // Create temp table name
        var tempTableName = $"#TempUpdate_{Guid.NewGuid():N}";
        
        // Create temp table with same structure as target
        await CreateTempTableAsync(connection, transaction, tempTableName, batch.First(), primaryKeyColumns, dataColumns);
        
        // Use SqlBulkCopy to load data into temp table (much faster than parameterized INSERT)
        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction);
        bulkCopy.DestinationTableName = tempTableName;
        bulkCopy.BatchSize = 1000; // Optimize for large datasets
        bulkCopy.NotifyAfter = 1000;
        
        // Map columns
        foreach (var column in allColumns)
        {
            bulkCopy.ColumnMappings.Add(column, column);
        }
        
        await bulkCopy.WriteToServerAsync(dataTable);
        
        // Use MERGE statement for better performance than JOIN UPDATE
        var setClause = string.Join(", ", dataColumns.Select(col => $"t.[{col}] = s.[{col}]"));
        var matchClause = string.Join(" AND ", primaryKeyColumns.Select(pk => $"t.[{pk}] = s.[{pk}]"));
        
        var mergeSql = $@"
            MERGE {FormatTableName(tableName)} AS t
            USING {tempTableName} AS s
            ON {matchClause}
            WHEN MATCHED THEN
                UPDATE SET {setClause};";
        
        using var command = new SqlCommand(mergeSql, connection, transaction);
        command.CommandTimeout = 600;
        
        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected;
    }
    
    private static Type GetClrType(object? value)
    {
        if (value == null) return typeof(string);
        
        return value switch
        {
            int => typeof(int),
            long => typeof(long),
            short => typeof(short),
            byte => typeof(byte),
            decimal => typeof(decimal),
            double => typeof(double),
            float => typeof(float),
            DateTime => typeof(DateTime),
            DateTimeOffset => typeof(DateTimeOffset),
            bool => typeof(bool),
            Guid => typeof(Guid),
            byte[] => typeof(byte[]),
            _ => typeof(string)
        };
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
            // IMPORTANT: Do NOT convert empty strings to NULL
            // Empty strings are valid values and should not be converted to NULL
            // This was causing issues where obfuscated values that were empty strings
            // were being inserted as NULL into the database
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