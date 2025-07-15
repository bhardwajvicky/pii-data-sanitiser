using Microsoft.Extensions.Logging;
using Common.DataTypes;
using System.Text.Json;
using System.Text;
using AutoMappingGenerator.Models;

namespace AutoMappingGenerator.Services;

public interface IClaudeApiService : ILLMService
{
    Task<List<PIIDataTypeRecommendation>> AnalyzeSampleDataAsync(string tableName, string columnName, List<object?> sampleData);
}

public class ClaudeApiService : IClaudeApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClaudeApiService> _logger;
    private readonly string _apiKey;
    private const string CLAUDE_API_URL = "https://api.anthropic.com/v1/messages";

    public string ProviderName => "Claude";
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

    public ClaudeApiService(HttpClient httpClient, ILogger<ClaudeApiService> logger, string apiKey)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey;
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }
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
        // Build comprehensive data type list from common library
        var dataTypeDescriptions = new[]
        {
            $"- {SupportedDataTypes.FirstName}: First names, given names only (NOT full names)",
            $"- {SupportedDataTypes.LastName}: Last names, surnames only (NOT full names)",
            $"- {SupportedDataTypes.FullName}: Full names, display names, complete personal names",
            $"- {SupportedDataTypes.Email}: Email addresses",
            $"- {SupportedDataTypes.Phone}: Phone numbers, contact numbers",
            $"- {SupportedDataTypes.FullAddress}: Complete address (all components combined)",
            $"- {SupportedDataTypes.AddressLine1}: Primary address line (street number + name)",
            $"- {SupportedDataTypes.AddressLine2}: Secondary address line (apartment, unit, suite)",
            $"- {SupportedDataTypes.City}: City/Town names",
            $"- {SupportedDataTypes.Suburb}: Suburb names (Australian term for City)",
            $"- {SupportedDataTypes.State}: State/Province/County names",
            $"- {SupportedDataTypes.StateAbbr}: State abbreviations (NSW, VIC, etc.)",
            $"- {SupportedDataTypes.PostCode}: Postal codes/ZIP codes",
            $"- {SupportedDataTypes.Country}: Country names",
            $"- {SupportedDataTypes.CreditCard}: Credit card numbers",
            $"- {SupportedDataTypes.NINO}: UK National Insurance Numbers",
            $"- {SupportedDataTypes.SortCode}: UK bank sort codes",
            $"- {SupportedDataTypes.LicenseNumber}: License numbers, permit IDs",
            $"- {SupportedDataTypes.CompanyName}: Company/operator names",
            $"- {SupportedDataTypes.BusinessABN}: Australian Business Numbers",
            $"- {SupportedDataTypes.BusinessACN}: Australian Company Numbers",
            $"- {SupportedDataTypes.VehicleRegistration}: Vehicle registration plates",
            $"- {SupportedDataTypes.VINNumber}: Vehicle identification numbers",
            $"- {SupportedDataTypes.VehicleMakeModel}: Vehicle make and model",
            $"- {SupportedDataTypes.GPSCoordinate}: GPS coordinates, location data",
            $"- {SupportedDataTypes.RouteCode}: Route identifiers",
            $"- {SupportedDataTypes.Date}: Personal dates ONLY (anniversary, hire date, termination date)",
            $"- {SupportedDataTypes.DateOfBirth}: Date of birth, birthdate ONLY"
        };

        return "You are an expert data privacy consultant analyzing a database schema to identify columns that likely contain Personally Identifiable Information (PII).\n\n" +
               "Please analyze the following database schema to identify columns that likely contain PII:\n\n" +
               schemaDescription + "\n\n" +
               "For each column you identify as containing PII, classify it into one of these categories:\n" +
               string.Join("\n", dataTypeDescriptions) + "\n\n" +
               "IMPORTANT ADDRESS MAPPING GUIDELINES:\n" +
               $"- Use {SupportedDataTypes.AddressLine1} for: Address, Address1, StreetAddress, Street\n" +
               $"- Use {SupportedDataTypes.AddressLine2} for: Address2, Unit, Apt, Suite, Level\n" +
               $"- Use {SupportedDataTypes.City} for: City, Town, Municipality\n" +
               $"- Use {SupportedDataTypes.Suburb} for: Suburb (Australian context)\n" +
               $"- Use {SupportedDataTypes.State} for: State, Province, Region\n" +
               $"- Use {SupportedDataTypes.PostCode} for: PostCode, ZipCode, PostalCode\n" +
               $"- Use {SupportedDataTypes.FullAddress} for: FullAddress, CompleteAddress\n\n" +
               "IMPORTANT NAME MAPPING GUIDELINES:\n" +
               $"- Use {SupportedDataTypes.FirstName} ONLY for: FirstName, GivenName, FName\n" +
               $"- Use {SupportedDataTypes.LastName} ONLY for: LastName, Surname, FamilyName, LName\n" +
               $"- Use {SupportedDataTypes.FullName} for: Name, DisplayName, PersonName, CustomerName\n\n" +
               "IMPORTANT DATE MAPPING GUIDELINES:\n" +
               $"- Use {SupportedDataTypes.DateOfBirth} for: DOB, DateOfBirth, BirthDate, Birthday, Born\n" +
               $"- Use {SupportedDataTypes.Date} ONLY for personal dates: Anniversary, HireDate, TerminationDate\n" +
               "- NEVER use Date for: ModifiedDate, CreatedDate, OrderDate, ShipDate, DueDate, etc.\n\n" +
               "Please respond in JSON format with an array of objects containing:\n" +
               "- tableName: Full table name (schema.table)\n" +
               "- columnName: Column name\n" +
               "- piiType: One of the categories above (exact match required)\n" +
               "- confidence: Confidence level (0.0-1.0)\n" +
               "- reasoning: Brief explanation why this column contains PII\n\n" +
               "CRITICAL EXCLUSION RULES:\n" +
               "- DO NOT identify as PII: Sales figures, amounts, quantities, metrics, statistics\n" +
               "- DO NOT identify as PII: System dates like ModifiedDate, CreatedDate, UpdatedDate\n" +
               "- DO NOT identify as PII: Business metrics like SalesLastYear, Revenue, Count\n" +
               "- DO NOT identify as PII: IDs that are just numbers (PersonID, CustomerID)\n" +
               "- DO NOT identify as PII: Status fields, flags, types, categories\n" +
               "- DO NOT identify as PII: Technical fields like versions, checksums, hashes\n" +
               "- DO NOT identify as PII: Columns ending with ID, YTD, LastYear, Count, Amount, Total\n" +
               "- DO NOT identify as PII: Geographic regions/territories that aren't personal addresses\n\n" +
               "IMPORTANT DATA TYPE VALIDATION:\n" +
               "- Names (FirstName, LastName, FullName) MUST be text types (varchar, nvarchar, char)\n" +
               "- Phone numbers MUST be text types, NOT numeric types\n" +
               "- Credit card numbers MUST be text types (varchar), NOT int or bigint\n" +
               "- Money, decimal, numeric, float types are NEVER names or PII\n" +
               "- Int/bigint columns ending with 'ID' are foreign keys, NOT actual data\n\n" +
               "Only include columns where confidence >= 0.7. Focus on actual PII data that identifies or contacts individuals.\n\n" +
               "Example response:\n" +
               "[\n" +
               "  {\n" +
               $"    \"tableName\": \"Person.Person\",\n" +
               $"    \"columnName\": \"FirstName\",\n" +
               $"    \"piiType\": \"{SupportedDataTypes.FirstName}\",\n" +
               $"    \"confidence\": 0.95,\n" +
               $"    \"reasoning\": \"Column clearly contains personal first names\"\n" +
               "  }\n" +
               "]";
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