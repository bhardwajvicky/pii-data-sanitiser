using Microsoft.Extensions.Logging;
using AutoMappingGenerator.Models;
using Common.DataTypes;

namespace AutoMappingGenerator.Services;

public interface IEnhancedPIIDetectionService
{
    Task<PIIAnalysisResult> IdentifyPIIColumnsAsync(DatabaseSchema schema);
}

public class EnhancedPIIDetectionService : IEnhancedPIIDetectionService
{
    private readonly ILLMService _llmService;
    private readonly ISchemaAnalysisService _fallbackPIIService;
    private readonly ILogger<EnhancedPIIDetectionService> _logger;

    public EnhancedPIIDetectionService(
        ILLMService llmService,
        ISchemaAnalysisService fallbackPIIService,
        ILogger<EnhancedPIIDetectionService> logger)
    {
        _llmService = llmService;
        _fallbackPIIService = fallbackPIIService;
        _logger = logger;
    }

    public async Task<PIIAnalysisResult> IdentifyPIIColumnsAsync(DatabaseSchema schema)
    {
        _logger.LogInformation("Starting enhanced PII detection with {Provider} (schema-only)", _llmService.ProviderName);

        // Step 1: Use LLM API to analyze schema structure ONLY
        var llmPIIColumns = await _llmService.AnalyzeSchemaPIIAsync(schema);

        // Step 2: If LLM API didn't return results, fall back to pattern-based detection
        if (!llmPIIColumns.Any())
        {
            _logger.LogWarning("{Provider} returned no results, falling back to pattern-based detection", _llmService.ProviderName);
            var fallbackResult = await _fallbackPIIService.IdentifyPIIColumnsAsync(schema);
            
            // Convert pattern-based results to LLM-compatible format
            llmPIIColumns = ConvertToLLMFormat(fallbackResult.TablesWithPII);
        }

        // Step 3: Enhance LLM results with schema metadata (no data fetching)
        var enhancedPIIColumns = EnhanceWithSchemaMetadata(llmPIIColumns, schema);

        // Step 4: Apply data type validation and refinement based on schema only
        var finalPIIColumns = RefineBasedOnSchemaOnly(enhancedPIIColumns);

        _logger.LogInformation("Enhanced PII detection completed. Found {Count} PII columns", finalPIIColumns.Count);

        // Convert to PIIAnalysisResult format
        return ConvertToPIIAnalysisResult(finalPIIColumns, schema);
    }

    private List<PIIColumn> EnhanceWithSchemaMetadata(List<PIIColumn> claudeColumns, DatabaseSchema schema)
    {
        _logger.LogInformation("Enhancing LLM results with schema metadata");

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
                
                // Add extended properties to detection reasons
                var extendedInfo = new List<string>();
                if (actualColumn.IsIdentity) extendedInfo.Add("Identity column");
                if (actualColumn.IsComputed) extendedInfo.Add("Computed column");
                if (actualColumn.IsForeignKey) extendedInfo.Add("Foreign key");
                if (actualColumn.IsPrimaryKey) extendedInfo.Add("Primary key");
                if (actualColumn.DefaultValue != null) extendedInfo.Add($"Has default: {actualColumn.DefaultValue}");
                
                if (extendedInfo.Any())
                {
                    claudeColumn.DetectionReasons.Add($"Column properties: {string.Join(", ", extendedInfo)}");
                }

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

    private List<PIIColumn> RefineBasedOnSchemaOnly(List<PIIColumn> piiColumns)
    {
        _logger.LogInformation("Refining PII detection based on schema analysis only");

        var refinedColumns = new List<PIIColumn>();

        foreach (var column in piiColumns)
        {
            // Apply schema-based refinements
            var refined = ApplySchemaBasedRefinements(column);
            
            // Adjust confidence based on schema characteristics
            refined.ConfidenceScore = CalculateSchemaBasedConfidence(refined);
            refined.Confidence = refined.ConfidenceScore; // Keep both for compatibility

            if (refined.ConfidenceScore >= 0.5) // Minimum threshold
            {
                refinedColumns.Add(refined);
                _logger.LogDebug("Refined {Table}.{Column} to {PIIType} (confidence: {Confidence})",
                    refined.TableName, refined.ColumnName, refined.DataType, refined.ConfidenceScore);
            }
        }

        return refinedColumns;
    }

    private PIIColumn ApplySchemaBasedRefinements(PIIColumn column)
    {
        // Refine based on column characteristics without looking at data
        var columnNameLower = column.ColumnName.ToLower();
        
        // Email refinement
        if (column.DataType == "Email" || columnNameLower.Contains("email"))
        {
            if (column.MaxLength >= 50 && column.MaxLength <= 320)
            {
                column.DataType = "Email";
                column.DetectionReasons.Add("Column length appropriate for email (50-320 chars)");
            }
        }

        // Phone number refinement
        if (column.DataType == "PhoneNumber" || columnNameLower.Contains("phone") || columnNameLower.Contains("mobile"))
        {
            if (column.MaxLength >= 10 && column.MaxLength <= 20)
            {
                column.DataType = "PhoneNumber";
                column.PreserveLength = true;
                column.DetectionReasons.Add("Column length appropriate for phone (10-20 chars)");
            }
        }

        // Address component refinement
        if (columnNameLower.Contains("address") || columnNameLower.Contains("street"))
        {
            column.DataType = "AddressLine1";
            column.DetectionReasons.Add("Column name indicates street address");
        }
        else if (columnNameLower.Contains("city") || columnNameLower.Contains("suburb"))
        {
            column.DataType = "City";
            column.DetectionReasons.Add("Column name indicates city/suburb");
        }
        else if (columnNameLower.Contains("state") || columnNameLower.Contains("province"))
        {
            column.DataType = "State";
            column.DetectionReasons.Add("Column name indicates state/province");
        }
        else if (columnNameLower.Contains("postcode") || columnNameLower.Contains("zip"))
        {
            column.DataType = "PostCode";
            column.PreserveLength = true;
            column.DetectionReasons.Add("Column name indicates postal code");
        }

        // Name refinement
        if (columnNameLower.Contains("firstname") || columnNameLower.Contains("first_name"))
        {
            column.DataType = "FirstName";
            column.DetectionReasons.Add("Column name indicates first name");
        }
        else if (columnNameLower.Contains("lastname") || columnNameLower.Contains("last_name"))
        {
            column.DataType = "LastName";
            column.DetectionReasons.Add("Column name indicates last name");
        }
        else if (columnNameLower.Contains("fullname") || columnNameLower.Contains("full_name"))
        {
            column.DataType = "FullName";
            column.DetectionReasons.Add("Column name indicates full name");
        }

        return column;
    }

    private double CalculateSchemaBasedConfidence(PIIColumn column)
    {
        var confidence = column.ConfidenceScore;

        // Boost confidence for exact matches
        var columnNameLower = column.ColumnName.ToLower();
        
        if (columnNameLower == "email" || columnNameLower == "emailaddress")
            confidence = Math.Min(confidence + 0.2, 1.0);
        
        if (columnNameLower == "phone" || columnNameLower == "phonenumber" || columnNameLower == "mobile")
            confidence = Math.Min(confidence + 0.2, 1.0);
        
        if (columnNameLower == "firstname" || columnNameLower == "lastname" || columnNameLower == "fullname")
            confidence = Math.Min(confidence + 0.2, 1.0);

        // Reduce confidence for generic columns
        if (columnNameLower.Contains("description") || columnNameLower.Contains("comment") || columnNameLower.Contains("note"))
            confidence *= 0.7;

        // Note: PIIColumn doesn't have IsIdentity or IsPrimaryKey properties
        // These would need to be checked from the actual ColumnInfo in the schema

        return confidence;
    }

    private ColumnInfo? FindColumnInSchema(string tableName, string columnName, DatabaseSchema schema)
    {
        // Handle table name with or without schema prefix
        var parts = tableName.Split('.');
        string schemaName, tableNameOnly;
        
        if (parts.Length == 2)
        {
            schemaName = parts[0];
            tableNameOnly = parts[1];
        }
        else
        {
            schemaName = "dbo"; // Default schema
            tableNameOnly = tableName;
        }

        var table = schema.Tables.FirstOrDefault(t => 
            t.Schema.Equals(schemaName, StringComparison.OrdinalIgnoreCase) && 
            t.TableName.Equals(tableNameOnly, StringComparison.OrdinalIgnoreCase));

        if (table == null)
        {
            // Try without schema
            table = schema.Tables.FirstOrDefault(t => 
                t.TableName.Equals(tableNameOnly, StringComparison.OrdinalIgnoreCase));
        }

        return table?.Columns.FirstOrDefault(c => 
            c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsValidPIITypeForSqlType(string piiType, string sqlType)
    {
        var sqlTypeLower = sqlType.ToLower();
        
        // Text-based PII types require string SQL types
        var textPiiTypes = new[] { "FirstName", "LastName", "FullName", "Email", "PhoneNumber", 
            "AddressLine1", "AddressLine2", "City", "State", "PostCode", "CompanyName" };
        
        if (textPiiTypes.Contains(piiType))
        {
            return sqlTypeLower.Contains("char") || sqlTypeLower.Contains("text");
        }

        // Numeric PII types
        if (piiType == "CreditCardNumber" || piiType == "DriverLicenseNumber")
        {
            return sqlTypeLower.Contains("char") || sqlTypeLower.Contains("text") || 
                   sqlTypeLower.Contains("numeric") || sqlTypeLower.Contains("bigint");
        }

        // Date-based PII types
        if (piiType == "DateOfBirth" || piiType == "Date")
        {
            return sqlTypeLower.Contains("date") || sqlTypeLower.Contains("datetime");
        }

        return true; // Allow by default
    }

    private List<PIIColumn> ConvertToLLMFormat(List<TableWithPII> tablesWithPII)
    {
        var claudeColumns = new List<PIIColumn>();
        
        foreach (var table in tablesWithPII)
        {
            foreach (var piiColumn in table.PIIColumns)
            {
                piiColumn.TableName = table.FullName;
                claudeColumns.Add(piiColumn);
            }
        }
        
        return claudeColumns;
    }
    
    private PIIAnalysisResult ConvertToPIIAnalysisResult(List<PIIColumn> piiColumns, DatabaseSchema schema)
    {
        var tablesWithPII = new List<TableWithPII>();
        
        // Group PII columns by table
        var piiColumnsByTable = piiColumns.GroupBy(c => c.TableName);
        
        foreach (var group in piiColumnsByTable)
        {
            var tableName = group.Key;
            var parts = tableName.Split('.');
            var schemaName = parts.Length > 1 ? parts[0] : "dbo";
            var tableNameOnly = parts.Length > 1 ? parts[1] : tableName;
            
            // Find the original table info
            var originalTable = schema.Tables.FirstOrDefault(t => 
                t.Schema.Equals(schemaName, StringComparison.OrdinalIgnoreCase) &&
                t.TableName.Equals(tableNameOnly, StringComparison.OrdinalIgnoreCase));
            
            if (originalTable != null)
            {
                var tableWithPII = new TableWithPII
                {
                    TableName = tableNameOnly,
                    Schema = schemaName,
                    PIIColumns = group.ToList(),
                    RowCount = originalTable.RowCount,
                    Priority = DeterminePriority(originalTable, group.ToList()),
                    PrimaryKeyColumns = originalTable.Columns
                        .Where(c => c.IsPrimaryKey)
                        .Select(c => c.ColumnName)
                        .ToList()
                };
                
                tablesWithPII.Add(tableWithPII);
            }
        }
        
        return new PIIAnalysisResult
        {
            DatabaseName = schema.DatabaseName,
            TablesWithPII = tablesWithPII
        };
    }
    
    private int DeterminePriority(TableInfo table, List<PIIColumn> piiColumns)
    {
        // Determine priority based on table characteristics
        var highPriorityPatterns = new[] { "person", "customer", "employee", "user", "contact" };
        var tableName = table.TableName.ToLower();
        
        if (highPriorityPatterns.Any(pattern => tableName.Contains(pattern)))
            return 1;
        
        if (piiColumns.Count >= 5)
            return 1;
        
        if (piiColumns.Count >= 3)
            return 3;
        
        return 5;
    }
}