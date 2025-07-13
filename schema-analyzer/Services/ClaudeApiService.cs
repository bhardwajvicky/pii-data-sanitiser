using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;
using SchemaAnalyzer.Models;

namespace SchemaAnalyzer.Services;

public interface IClaudeApiService
{
    Task<List<PIIColumn>> AnalyzeSchemaPIIAsync(DatabaseSchema schema);
    Task<List<PIIDataTypeRecommendation>> AnalyzeSampleDataAsync(string tableName, string columnName, List<object?> sampleData);
}

public class ClaudeApiService : IClaudeApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClaudeApiService> _logger;
    private readonly string _apiKey;
    private const string CLAUDE_API_URL = "https://api.anthropic.com/v1/messages";

    public ClaudeApiService(HttpClient httpClient, ILogger<ClaudeApiService> logger, string apiKey)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey;
        
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<List<PIIColumn>> AnalyzeSchemaPIIAsync(DatabaseSchema schema)
    {
        _logger.LogInformation("Analyzing schema for PII using Claude API");

        var schemaDescription = BuildSchemaDescription(schema);
        var prompt = BuildSchemaPIIPrompt(schemaDescription);

        var response = await CallClaudeApiAsync(prompt);
        var piiColumns = ParseSchemaPIIResponse(response);

        _logger.LogInformation("Claude API identified {Count} potential PII columns", piiColumns.Count);
        return piiColumns;
    }

    public async Task<List<PIIDataTypeRecommendation>> AnalyzeSampleDataAsync(string tableName, string columnName, List<object?> sampleData)
    {
        _logger.LogInformation("Analyzing sample data for {Table}.{Column} using Claude API", tableName, columnName);

        var prompt = BuildSampleDataPrompt(tableName, columnName, sampleData);
        var response = await CallClaudeApiAsync(prompt);
        var recommendations = ParseSampleDataResponse(response);

        _logger.LogInformation("Claude API provided {Count} recommendations for {Table}.{Column}", recommendations.Count, tableName, columnName);
        return recommendations;
    }

    private string BuildSchemaDescription(DatabaseSchema schema)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Database: {schema.DatabaseName}");
        sb.AppendLine($"Total Tables: {schema.Tables.Count}");
        sb.AppendLine();

        foreach (var table in schema.Tables)
        {
            sb.AppendLine($"Table: {table.Schema}.{table.TableName}");
            sb.AppendLine("Columns:");
            
            foreach (var column in table.Columns)
            {
                sb.AppendLine($"  - {column.ColumnName} ({column.SqlDataType}) {(column.IsNullable ? "NULL" : "NOT NULL")} {(column.MaxLength.HasValue ? $"MAX:{column.MaxLength}" : "")}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string BuildSchemaPIIPrompt(string schemaDescription)
    {
        return $@"You are an expert data privacy consultant analyzing a database schema to identify columns that likely contain Personally Identifiable Information (PII).

Please analyze the following database schema and identify columns that likely contain PII data:

{schemaDescription}

For each column you identify as containing PII, classify it into one of these categories:
- FirstName: First names, given names only (NOT full names)
- LastName: Last names, surnames only (NOT full names)
- DriverName: Full names, display names, complete personal names
- Address: Physical addresses, location data, postal codes
- DriverPhone: Phone numbers, contact numbers
- ContactEmail: Email addresses
- DriverLicenseNumber: License numbers, permit IDs
- BusinessABN: Business numbers, tax IDs
- VehicleRegistration: Vehicle plates, registration numbers
- VINNumber: Vehicle identification numbers
- GPSCoordinate: Coordinates, location data
- VehicleMakeModel: Vehicle information
- OperatorName: Company/operator names
- RouteCode: Route identifiers

IMPORTANT: Distinguish carefully between:
- FirstName: Use ONLY for columns named "FirstName", "GivenName", "FName" etc.
- LastName: Use ONLY for columns named "LastName", "Surname", "FamilyName", "LName" etc.
- DriverName: Use for columns containing full names or general "Name" fields

Please respond in JSON format with an array of objects containing:
- tableName: Full table name (schema.table)
- columnName: Column name
- piiType: One of the categories above
- confidence: Confidence level (0.0-1.0)
- reasoning: Brief explanation why this column contains PII

Only include columns where confidence >= 0.7. Focus on actual PII data, not technical IDs or system fields.

Example response:
[
  {{
    ""tableName"": ""Person.Person"",
    ""columnName"": ""FirstName"",
    ""piiType"": ""DriverName"",
    ""confidence"": 0.95,
    ""reasoning"": ""Column clearly contains personal first names""
  }}
]";
    }

    private string BuildSampleDataPrompt(string tableName, string columnName, List<object?> sampleData)
    {
        var sampleDataStr = string.Join(", ", sampleData.Take(10).Select(d => d?.ToString() ?? "NULL"));
        
        return $@"You are analyzing sample data from a database column to determine what type of PII data it contains and what appropriate replacement data should be generated.

Table: {tableName}
Column: {columnName}
Sample Data: {sampleDataStr}

Based on this sample data, please:
1. Identify what type of PII this column contains
2. Recommend appropriate Australian fleet industry replacement data
3. Suggest data generation parameters

Available PII types:
- FirstName: Australian first names only (NOT full names)
- LastName: Australian last names only (NOT full names)  
- DriverName: Full Australian names (first + last combined)
- Address: Australian addresses
- DriverPhone: Australian phone numbers
- ContactEmail: Fleet industry emails
- DriverLicenseNumber: Australian license formats
- BusinessABN: Australian Business Numbers
- VehicleRegistration: Australian vehicle plates
- VINNumber: Vehicle identification numbers
- GPSCoordinate: Australian coordinates
- VehicleMakeModel: Fleet vehicles
- OperatorName: Transport company names
- RouteCode: Route identifiers

Please respond in JSON format:
{{
  ""piiType"": ""DriverName"",
  ""confidence"": 0.95,
  ""reasoning"": ""Sample data shows personal names"",
  ""replacementStrategy"": ""Generate realistic Australian names"",
  ""preserveLength"": true,
  ""sampleReplacements"": [""John Smith"", ""Sarah Wilson"", ""Michael Chen""]
}}

If the data doesn't appear to be PII, set confidence to 0.0.";
    }

    private async Task<string> CallClaudeApiAsync(string prompt)
    {
        if (string.IsNullOrEmpty(_apiKey) || _apiKey == "your-claude-api-key-here")
        {
            _logger.LogWarning("Claude API key not configured, using fallback analysis");
            return "{}"; // Return empty JSON for fallback
        }

        try
        {
            var requestBody = new
            {
                model = "claude-3-5-sonnet-20241022",
                max_tokens = 4000,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(CLAUDE_API_URL, content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                
                if (responseObj.TryGetProperty("content", out var contentArray) && 
                    contentArray.GetArrayLength() > 0 &&
                    contentArray[0].TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString() ?? "{}";
                }
            }
            else
            {
                _logger.LogError("Claude API request failed with status: {StatusCode}", response.StatusCode);
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error details: {Error}", errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Claude API");
        }

        return "{}"; // Fallback to empty response
    }

    private List<PIIColumn> ParseSchemaPIIResponse(string response)
    {
        var piiColumns = new List<PIIColumn>();

        try
        {
            if (string.IsNullOrEmpty(response) || response == "{}")
            {
                _logger.LogInformation("No Claude API response, falling back to pattern-based detection");
                return piiColumns;
            }

            _logger.LogDebug("Claude API schema response: {Response}", response);

            // Try to parse as JSON array first, then as single object if that fails
            JsonElement[] jsonArray;
            try
            {
                jsonArray = JsonSerializer.Deserialize<JsonElement[]>(response);
            }
            catch (JsonException)
            {
                // If not an array, try parsing the response content to extract JSON
                var jsonStartIndex = response.IndexOf('[');
                var jsonEndIndex = response.LastIndexOf(']');
                
                if (jsonStartIndex >= 0 && jsonEndIndex > jsonStartIndex)
                {
                    var jsonContent = response.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                    // Clean the JSON content (but preserve array brackets)
                    jsonContent = CleanJsonContent(jsonContent);
                    _logger.LogDebug("Extracted and cleaned JSON content: {JsonContent}", jsonContent);
                    
                    try
                    {
                        jsonArray = JsonSerializer.Deserialize<JsonElement[]>(jsonContent);
                    }
                    catch (JsonException parseEx)
                    {
                        _logger.LogWarning(parseEx, "Failed to parse cleaned JSON content: {Content}", jsonContent);
                        return piiColumns;
                    }
                }
                else
                {
                    _logger.LogWarning("Could not extract JSON array from Claude response");
                    return piiColumns;
                }
            }
            
            foreach (var item in jsonArray)
            {
                if (item.TryGetProperty("tableName", out var tableNameElement) &&
                    item.TryGetProperty("columnName", out var columnNameElement) &&
                    item.TryGetProperty("piiType", out var piiTypeElement) &&
                    item.TryGetProperty("confidence", out var confidenceElement))
                {
                    var tableName = tableNameElement.GetString();
                    var columnName = columnNameElement.GetString();
                    var piiType = piiTypeElement.GetString();
                    var confidence = confidenceElement.GetDouble();

                    if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(columnName) && 
                        !string.IsNullOrEmpty(piiType) && confidence >= 0.7)
                    {
                        piiColumns.Add(new PIIColumn
                        {
                            TableName = tableName,
                            ColumnName = columnName,
                            DataType = piiType,
                            Confidence = confidence,
                            SqlDataType = "nvarchar", // Will be updated later
                            MaxLength = null,
                            IsNullable = true,
                            PreserveLength = false
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Claude API schema response");
        }

        return piiColumns;
    }

    private List<PIIDataTypeRecommendation> ParseSampleDataResponse(string response)
    {
        var recommendations = new List<PIIDataTypeRecommendation>();

        try
        {
            if (string.IsNullOrEmpty(response) || response == "{}")
            {
                return recommendations;
            }

            _logger.LogDebug("Claude API sample data response: {Response}", response);

            // Try to extract and clean JSON from response
            string jsonContent = CleanJsonResponse(response);
            _logger.LogDebug("Cleaned JSON content: {JsonContent}", jsonContent);

            JsonElement jsonObj;
            try
            {
                jsonObj = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            }
            catch (JsonException parseEx)
            {
                _logger.LogWarning(parseEx, "Failed to parse Claude API response as JSON: {Content}", jsonContent);
                return recommendations;
            }
            
            if (jsonObj.TryGetProperty("piiType", out var piiTypeElement) &&
                jsonObj.TryGetProperty("confidence", out var confidenceElement))
            {
                var piiType = piiTypeElement.GetString();
                var confidence = confidenceElement.GetDouble();

                if (!string.IsNullOrEmpty(piiType) && confidence >= 0.7)
                {
                    var recommendation = new PIIDataTypeRecommendation
                    {
                        PIIType = piiType,
                        Confidence = confidence,
                        PreserveLength = false,
                        SampleReplacements = new List<string>()
                    };

                    if (jsonObj.TryGetProperty("preserveLength", out var preserveLengthElement))
                    {
                        recommendation.PreserveLength = preserveLengthElement.GetBoolean();
                    }

                    if (jsonObj.TryGetProperty("sampleReplacements", out var samplesElement))
                    {
                        foreach (var sample in samplesElement.EnumerateArray())
                        {
                            var sampleStr = sample.GetString();
                            if (!string.IsNullOrEmpty(sampleStr))
                            {
                                recommendation.SampleReplacements.Add(sampleStr);
                            }
                        }
                    }

                    recommendations.Add(recommendation);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Claude API sample data response");
        }

        return recommendations;
    }

    private static string CleanJsonResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return "{}";

        // Extract JSON from response if it's wrapped in text
        var jsonStartIndex = response.IndexOf('{');
        var jsonEndIndex = response.LastIndexOf('}');
        
        if (jsonStartIndex >= 0 && jsonEndIndex > jsonStartIndex)
        {
            var jsonContent = response.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
            
            // Clean common JSON formatting issues that Claude sometimes produces
            jsonContent = jsonContent
                .Replace("\\n", "\n")           // Fix escaped newlines
                .Replace("\\/", "/")            // Fix escaped slashes
                .Replace("\\\"", "\"")          // Fix escaped quotes
                .Replace("\n", " ")             // Replace newlines with spaces
                .Replace("\r", " ")             // Replace carriage returns with spaces
                .Replace("\t", " ");            // Replace tabs with spaces
            
            // Remove multiple spaces
            while (jsonContent.Contains("  "))
            {
                jsonContent = jsonContent.Replace("  ", " ");
            }
            
            return jsonContent.Trim();
        }
        
        return "{}";
    }

    private static string CleanJsonContent(string jsonContent)
    {
        if (string.IsNullOrEmpty(jsonContent))
            return jsonContent;

        // Clean common JSON formatting issues that Claude sometimes produces
        return jsonContent
            .Replace("\\n", "\n")           // Fix escaped newlines
            .Replace("\\/", "/")            // Fix escaped slashes
            .Replace("\\\"", "\"")          // Fix escaped quotes
            .Replace("\n", " ")             // Replace newlines with spaces
            .Replace("\r", " ")             // Replace carriage returns with spaces
            .Replace("\t", " ")             // Replace tabs with spaces
            .Replace("  ", " ")             // Remove double spaces
            .Replace("  ", " ")             // Remove double spaces again
            .Replace("  ", " ")             // Remove double spaces once more
            .Trim();
    }
}

public class PIIDataTypeRecommendation
{
    public string PIIType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public bool PreserveLength { get; set; }
    public List<string> SampleReplacements { get; set; } = new();
}