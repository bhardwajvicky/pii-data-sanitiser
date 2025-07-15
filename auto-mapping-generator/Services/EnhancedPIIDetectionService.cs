using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using AutoMappingGenerator.Models;
using Common.DataTypes;

namespace AutoMappingGenerator.Services;

public interface IEnhancedPIIDetectionService
{
    Task<List<PIIColumn>> IdentifyPIIColumnsAsync(DatabaseSchema schema, string connectionString);
}

public class EnhancedPIIDetectionService : IEnhancedPIIDetectionService
{
    private readonly IClaudeApiService _claudeApiService;
    private readonly ISchemaAnalysisService _fallbackPIIService;
    private readonly ILogger<EnhancedPIIDetectionService> _logger;
    private readonly HashSet<string> _clrDependentTables = new();

    public EnhancedPIIDetectionService(
        IClaudeApiService claudeApiService,
        ISchemaAnalysisService fallbackPIIService,
        ILogger<EnhancedPIIDetectionService> logger)
    {
        _claudeApiService = claudeApiService;
        _fallbackPIIService = fallbackPIIService;
        _logger = logger;
    }

    public async Task<List<PIIColumn>> IdentifyPIIColumnsAsync(DatabaseSchema schema, string connectionString)
    {
        _logger.LogInformation("Starting enhanced PII detection with Claude API integration");

        // Step 1: Use Claude API to analyze schema structure
        var claudePIIColumns = await _claudeApiService.AnalyzeSchemaPIIAsync(schema);

        // Step 2: If Claude API didn't return results, fall back to pattern-based detection
        if (!claudePIIColumns.Any())
        {
            _logger.LogWarning("Claude API returned no results, falling back to pattern-based detection");
            var fallbackResult = await _fallbackPIIService.IdentifyPIIColumnsAsync(schema);
            
            // Convert pattern-based results to Claude-compatible format
            claudePIIColumns = ConvertToClaudeFormat(fallbackResult.TablesWithPII);
        }

        // Step 3: Enhance Claude results with actual database metadata
        var enhancedPIIColumns = await EnhanceWithDatabaseMetadataAsync(claudePIIColumns, schema, connectionString);

        // Step 4: Analyze sample data for refined PII type detection and filter out CLR-dependent tables
        var finalPIIColumns = await AnalyzeSampleDataAsync(enhancedPIIColumns, connectionString);

        // Step 5: Filter out CLR-dependent tables that cannot be processed by the obfuscation engine
        var processablePIIColumns = FilterOutCLRDependentTables(finalPIIColumns);

        _logger.LogInformation("Enhanced PII detection completed. Found {Count} PII columns ({FilteredCount} after CLR filtering)", 
            finalPIIColumns.Count, processablePIIColumns.Count);

        return processablePIIColumns;
    }

    private async Task<List<PIIColumn>> EnhanceWithDatabaseMetadataAsync(
        List<PIIColumn> claudeColumns, 
        DatabaseSchema schema, 
        string connectionString)
    {
        _logger.LogInformation("Enhancing Claude results with database metadata");

        var enhancedColumns = new List<PIIColumn>();

        foreach (var claudeColumn in claudeColumns)
        {
            // Find the actual column in the schema
            var actualColumn = FindColumnInSchema(claudeColumn.TableName, claudeColumn.ColumnName, schema);
            
            if (actualColumn != null)
            {
                claudeColumn.SqlDataType = actualColumn.SqlDataType;
                claudeColumn.MaxLength = actualColumn.MaxLength;
                claudeColumn.IsNullable = actualColumn.IsNullable;
                
                // Validate that the PII type makes sense for the SQL data type
                if (IsValidPIITypeForSqlType(claudeColumn.DataType, actualColumn.SqlDataType))
                {
                    enhancedColumns.Add(claudeColumn);
                    _logger.LogDebug("Enhanced {Table}.{Column} as {PIIType}", 
                        claudeColumn.TableName, claudeColumn.ColumnName, claudeColumn.DataType);
                }
                else
                {
                    _logger.LogWarning("Skipping {Table}.{Column} - PII type {PIIType} incompatible with SQL type {SqlType}",
                        claudeColumn.TableName, claudeColumn.ColumnName, claudeColumn.DataType, actualColumn.SqlDataType);
                }
            }
            else
            {
                _logger.LogWarning("Column {Table}.{Column} not found in schema", 
                    claudeColumn.TableName, claudeColumn.ColumnName);
            }
        }

        return enhancedColumns;
    }

    private async Task<List<PIIColumn>> AnalyzeSampleDataAsync(List<PIIColumn> piiColumns, string connectionString)
    {
        _logger.LogInformation("Analyzing sample data for {Count} PII columns", piiColumns.Count);

        var finalColumns = new List<PIIColumn>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var piiColumn in piiColumns)
        {
            try
            {
                var sampleData = await GetSampleDataAsync(connection, piiColumn.TableName, piiColumn.ColumnName);
                
                if (sampleData.Any())
                {
                    var recommendations = await _claudeApiService.AnalyzeSampleDataAsync(
                        piiColumn.TableName, piiColumn.ColumnName, sampleData);

                    if (recommendations.Any())
                    {
                        var bestRecommendation = recommendations.OrderByDescending(r => r.Confidence).First();
                        
                        // Update PII column with Claude's refined analysis
                        piiColumn.DataType = bestRecommendation.PIIType;
                        piiColumn.PreserveLength = bestRecommendation.PreserveLength;
                        piiColumn.Confidence = Math.Max(piiColumn.Confidence, bestRecommendation.Confidence);
                        
                        _logger.LogDebug("Refined {Table}.{Column} to {PIIType} (confidence: {Confidence})",
                            piiColumn.TableName, piiColumn.ColumnName, piiColumn.DataType, piiColumn.Confidence);
                    }
                }

                finalColumns.Add(piiColumn);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze sample data for {Table}.{Column}", 
                    piiColumn.TableName, piiColumn.ColumnName);
                
                // Keep the column but mark it with lower confidence
                piiColumn.Confidence *= 0.8;
                finalColumns.Add(piiColumn);
            }
        }

        return finalColumns;
    }

    private async Task<List<object?>> GetSampleDataAsync(SqlConnection connection, string tableName, string columnName)
    {
        var sampleData = new List<object?>();

        try
        {
            var sql = $@"
                SELECT TOP 10 [{columnName}]
                FROM {FormatTableName(tableName)}
                WHERE [{columnName}] IS NOT NULL
                ORDER BY NEWID()"; // Random sampling

            using var command = new SqlCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var value = reader.IsDBNull(0) ? null : reader.GetValue(0);
                sampleData.Add(value);
            }
        }
        catch (SqlException ex) when (ex.Number == 10347) // CLR not enabled
        {
            _logger.LogInformation("Table {Table} has CLR dependencies - marking as non-processable", tableName);
            
            // Mark this table as CLR-dependent so it can be filtered out later
            _clrDependentTables.Add(tableName);
            
            // For CLR tables, try a simpler query without ORDER BY NEWID() which can trigger CLR issues
            try
            {
                var simpleSql = $@"
                    SELECT TOP 10 [{columnName}]
                    FROM {FormatTableName(tableName)}
                    WHERE [{columnName}] IS NOT NULL";

                using var simpleCommand = new SqlCommand(simpleSql, connection);
                using var simpleReader = await simpleCommand.ExecuteReaderAsync();

                while (await simpleReader.ReadAsync())
                {
                    var value = simpleReader.IsDBNull(0) ? null : simpleReader.GetValue(0);
                    sampleData.Add(value);
                }
                
                _logger.LogDebug("Successfully retrieved sample data using simplified query for {Table}.{Column}", tableName, columnName);
            }
            catch (SqlException innerEx) when (innerEx.Number == 10347)
            {
                _logger.LogInformation("CLR table {Table} cannot be queried - will be excluded from configuration", tableName);
            }
            catch (Exception innerEx)
            {
                _logger.LogWarning("Unexpected error querying CLR table {Table} - will be excluded from configuration", tableName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get sample data for {Table}.{Column}", tableName, columnName);
        }

        return sampleData;
    }

    private static ColumnInfo? FindColumnInSchema(string tableName, string columnName, DatabaseSchema schema)
    {
        var parts = tableName.Split('.');
        if (parts.Length != 2) return null;

        var schemaName = parts[0];
        var tableNameOnly = parts[1];

        var table = schema.Tables.FirstOrDefault(t => 
            t.Schema.Equals(schemaName, StringComparison.OrdinalIgnoreCase) &&
            t.TableName.Equals(tableNameOnly, StringComparison.OrdinalIgnoreCase));

        return table?.Columns.FirstOrDefault(c => 
            c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidPIITypeForSqlType(string piiType, string sqlDataType)
    {
        // First validate that the PII type is supported
        if (!SupportedDataTypes.IsSupported(piiType))
        {
            return false;
        }

        var sqlTypeLower = sqlDataType.ToLower();

        return piiType switch
        {
            // String-based data types
            SupportedDataTypes.FirstName or SupportedDataTypes.LastName or SupportedDataTypes.FullName or 
            SupportedDataTypes.Email or SupportedDataTypes.Phone or
            SupportedDataTypes.FullAddress or SupportedDataTypes.AddressLine1 or SupportedDataTypes.AddressLine2 or
            SupportedDataTypes.City or SupportedDataTypes.Suburb or SupportedDataTypes.State or SupportedDataTypes.StateAbbr or
            SupportedDataTypes.PostCode or SupportedDataTypes.ZipCode or SupportedDataTypes.Country or
            SupportedDataTypes.CompanyName or SupportedDataTypes.LicenseNumber or
            SupportedDataTypes.VehicleRegistration or SupportedDataTypes.VINNumber or SupportedDataTypes.VehicleMakeModel or
            SupportedDataTypes.RouteCode or SupportedDataTypes.DepotLocation or
            SupportedDataTypes.NINO or SupportedDataTypes.NationalInsuranceNumber or SupportedDataTypes.SortCode or
            SupportedDataTypes.UKPostcode or SupportedDataTypes.Address => 
                sqlTypeLower.Contains("varchar") || sqlTypeLower.Contains("char") || sqlTypeLower.Contains("text"),
            
            // Credit card numbers - typically stored as varchar
            SupportedDataTypes.CreditCard => 
                sqlTypeLower.Contains("varchar") || sqlTypeLower.Contains("char"),
            
            // Business identifiers - typically varchar/char
            SupportedDataTypes.BusinessABN or SupportedDataTypes.BusinessACN or SupportedDataTypes.EngineNumber => 
                sqlTypeLower.Contains("varchar") || sqlTypeLower.Contains("char"),
            
            // GPS coordinates - can be various numeric or text formats
            SupportedDataTypes.GPSCoordinate => 
                sqlTypeLower.Contains("varchar") || sqlTypeLower.Contains("decimal") || 
                sqlTypeLower.Contains("float") || sqlTypeLower.Contains("geography") || sqlTypeLower.Contains("geometry"),
            
            _ => false // Reject unknown types
        };
    }

    private static string FormatTableName(string tableName)
    {
        if (tableName.Contains('.') && !tableName.StartsWith('['))
        {
            var parts = tableName.Split('.');
            if (parts.Length == 2)
            {
                return $"[{parts[0]}].[{parts[1]}]";
            }
        }
        
        return tableName.StartsWith('[') ? tableName : $"[{tableName}]";
    }

    private List<PIIColumn> ConvertToClaudeFormat(List<TableWithPII> tablesWithPII)
    {
        var claudeColumns = new List<PIIColumn>();

        foreach (var table in tablesWithPII)
        {
            foreach (var piiColumn in table.PIIColumns)
            {
                // Set the TableName for Claude compatibility
                piiColumn.TableName = table.FullName;
                piiColumn.Confidence = piiColumn.ConfidenceScore;
                claudeColumns.Add(piiColumn);
            }
        }

        return claudeColumns;
    }

    private List<PIIColumn> FilterOutCLRDependentTables(List<PIIColumn> piiColumns)
    {
        var filtered = piiColumns.Where(column => !_clrDependentTables.Contains(column.TableName)).ToList();
        
        var excludedCount = piiColumns.Count - filtered.Count;
        if (excludedCount > 0)
        {
            var excludedTables = _clrDependentTables.ToList();
            _logger.LogInformation("Excluded {Count} PII columns from {TableCount} CLR-dependent tables: {Tables}", 
                excludedCount, excludedTables.Count, string.Join(", ", excludedTables));
        }
        
        return filtered;
    }
}