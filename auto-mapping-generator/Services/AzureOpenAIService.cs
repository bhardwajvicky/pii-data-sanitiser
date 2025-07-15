using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using AutoMappingGenerator.Models;
using Common.DataTypes;

namespace AutoMappingGenerator.Services;

public class AzureOpenAIService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _deploymentName;
    
    // Azure OpenAI API endpoint format
    private const string API_VERSION = "2024-02-01";
    
    public string ProviderName => "Azure OpenAI";
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_endpoint);

    public AzureOpenAIService(
        HttpClient httpClient, 
        ILogger<AzureOpenAIService> logger, 
        string apiKey,
        string endpoint = "https://your-resource.openai.azure.com",
        string deploymentName = "gpt-4")
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey;
        _endpoint = endpoint;
        _deploymentName = deploymentName;
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
        }
    }

    public async Task<List<PIIColumn>> AnalyzeSchemaPIIAsync(DatabaseSchema schema)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Azure OpenAI service is not properly configured");
            return new List<PIIColumn>();
        }

        _logger.LogInformation("Analyzing schema for PII using Azure OpenAI");

        var schemaDescription = BuildSchemaDescription(schema);
        var prompt = BuildSchemaPIIPrompt(schemaDescription);

        try
        {
            var response = await CallAzureOpenAIAsync(prompt);
            var piiColumns = ParsePIIResponse(response);

            _logger.LogInformation("Azure OpenAI identified {Count} potential PII columns", piiColumns.Count);
            return piiColumns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze schema using Azure OpenAI");
            return new List<PIIColumn>();
        }
    }
    

    private async Task<string> CallAzureOpenAIAsync(string prompt)
    {
        var requestUrl = $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version={API_VERSION}";
        
        var requestBody = new
        {
            messages = new[]
            {
                new { role = "system", content = "You are an expert data privacy consultant specializing in identifying PII in database schemas." },
                new { role = "user", content = prompt }
            },
            temperature = 0.3,
            max_tokens = 4000,
            response_format = new { type = "json_object" }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(requestUrl, content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);

        // Extract the assistant's response
        var assistantMessage = responseData
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        _logger.LogDebug("Azure OpenAI response: {Response}", assistantMessage);

        return assistantMessage ?? string.Empty;
    }

    private string BuildSchemaDescription(DatabaseSchema schema)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Database: {schema.DatabaseName}");
        sb.AppendLine($"Total Tables: {schema.Tables.Count}");
        sb.AppendLine();

        foreach (var table in schema.Tables.Take(100)) // Limit to prevent token overflow
        {
            sb.AppendLine($"Table: {table.Schema}.{table.TableName}");
            sb.AppendLine("Columns:");
            
            foreach (var column in table.Columns)
            {
                var columnInfo = $"  - {column.ColumnName} ({column.SqlDataType})";
                if (!column.IsNullable) columnInfo += " NOT NULL";
                if (column.MaxLength.HasValue) columnInfo += $" MAX:{column.MaxLength}";
                if (column.IsPrimaryKey) columnInfo += " PRIMARY KEY";
                if (column.IsForeignKey) columnInfo += " FOREIGN KEY";
                if (column.IsIdentity) columnInfo += " IDENTITY";
                
                sb.AppendLine(columnInfo);
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string BuildSchemaPIIPrompt(string schemaDescription)
    {
        var dataTypeDescriptions = new[]
        {
            $"- {SupportedDataTypes.FirstName}: First names only",
            $"- {SupportedDataTypes.LastName}: Last names only", 
            $"- {SupportedDataTypes.FullName}: Full names, display names",
            $"- {SupportedDataTypes.Email}: Email addresses",
            $"- {SupportedDataTypes.Phone}: Phone numbers",
            $"- {SupportedDataTypes.AddressLine1}: Street addresses",
            $"- {SupportedDataTypes.AddressLine2}: Apartment/unit numbers",
            $"- {SupportedDataTypes.City}: City names",
            $"- {SupportedDataTypes.State}: State/Province names",
            $"- {SupportedDataTypes.PostCode}: Postal/ZIP codes",
            $"- {SupportedDataTypes.CreditCard}: Credit card numbers",
            $"- {SupportedDataTypes.LicenseNumber}: License/permit numbers",
            $"- {SupportedDataTypes.CompanyName}: Company names",
            $"- {SupportedDataTypes.BusinessABN}: Business numbers",
            $"- {SupportedDataTypes.VehicleRegistration}: Vehicle registrations",
            $"- {SupportedDataTypes.Date}: Personal dates ONLY (anniversary, hire date)",
            $"- {SupportedDataTypes.DateOfBirth}: Date of birth ONLY"
        };

        return $@"Analyze the following database schema to identify columns containing PII.

{schemaDescription}

Classify each PII column into one of these categories:
{string.Join("\n", dataTypeDescriptions)}

IMPORTANT ADDRESS MAPPING GUIDELINES:
- Use {SupportedDataTypes.AddressLine1} for: Address, Address1, StreetAddress, Street
- Use {SupportedDataTypes.AddressLine2} for: Address2, Unit, Apt, Suite, Level
- Use {SupportedDataTypes.City} for: City, Town, Municipality
- Use {SupportedDataTypes.State} for: State, Province, Region
- Use {SupportedDataTypes.PostCode} for: PostCode, ZipCode, PostalCode

IMPORTANT NAME MAPPING GUIDELINES:
- Use {SupportedDataTypes.FirstName} ONLY for: FirstName, GivenName, FName
- Use {SupportedDataTypes.LastName} ONLY for: LastName, Surname, FamilyName, LName
- Use {SupportedDataTypes.FullName} for: Name, DisplayName, PersonName, CustomerName

IMPORTANT DATE MAPPING GUIDELINES:
- Use {SupportedDataTypes.DateOfBirth} for: DOB, DateOfBirth, BirthDate, Birthday, Born
- Use {SupportedDataTypes.Date} ONLY for personal dates: Anniversary, HireDate, TerminationDate
- NEVER use Date for: ModifiedDate, CreatedDate, OrderDate, ShipDate, DueDate, etc.

CRITICAL EXCLUSION RULES:
- DO NOT identify as PII: Sales figures, amounts, quantities, metrics, statistics
- DO NOT identify as PII: System dates like ModifiedDate, CreatedDate, UpdatedDate
- DO NOT identify as PII: Business metrics like SalesLastYear, Revenue, Count
- DO NOT identify as PII: IDs that are just numbers (PersonID, CustomerID)
- DO NOT identify as PII: Status fields, flags, types, categories
- DO NOT identify as PII: Technical fields like versions, checksums, hashes
- DO NOT identify as PII: Columns ending with ID, YTD, LastYear, Count, Amount, Total
- DO NOT identify as PII: Geographic regions/territories that aren't personal addresses

IMPORTANT DATA TYPE VALIDATION:
- Names (FirstName, LastName, FullName) MUST be text types (varchar, nvarchar, char)
- Phone numbers MUST be text types, NOT numeric types
- Credit card numbers MUST be text types (varchar), NOT int or bigint
- Money, decimal, numeric, float types are NEVER names or PII
- Int/bigint columns ending with 'ID' are foreign keys, NOT actual data

IMPORTANT: Return ONLY a valid JSON object with this structure:
{{
  ""columns"": [
    {{
      ""tableName"": ""Schema.TableName"",
      ""columnName"": ""ColumnName"",
      ""piiType"": ""ExactTypeFromList"",
      ""confidence"": 0.95,
      ""reasoning"": ""Brief explanation""
    }}
  ]
}}

Only include columns with confidence >= 0.7. Focus on actual PII data that identifies or contacts individuals.";
    }

    private List<PIIColumn> ParsePIIResponse(string response)
    {
        var piiColumns = new List<PIIColumn>();

        try
        {
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(response);
            
            if (jsonResponse.TryGetProperty("columns", out var columnsElement))
            {
                foreach (var element in columnsElement.EnumerateArray())
                {
                    try
                    {
                        var tableName = element.GetProperty("tableName").GetString() ?? "";
                        var columnName = element.GetProperty("columnName").GetString() ?? "";
                        var piiType = element.GetProperty("piiType").GetString() ?? "";
                        var confidence = element.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.8;
                        var reasoning = element.TryGetProperty("reasoning", out var reason) ? reason.GetString() : "";

                        // Map Azure OpenAI PII types to our data types
                        var mappedType = MapPIIType(piiType);
                        
                        if (!string.IsNullOrEmpty(mappedType))
                        {
                            piiColumns.Add(new PIIColumn
                            {
                                TableName = tableName,
                                ColumnName = columnName,
                                DataType = mappedType,
                                ConfidenceScore = confidence,
                                Confidence = confidence,
                                DetectionReasons = new List<string> { reasoning ?? $"Detected by {ProviderName}" },
                                PreserveLength = ShouldPreserveLength(mappedType)
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse PII column element");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Azure OpenAI response");
        }

        return piiColumns;
    }

    private string MapPIIType(string piiType)
    {
        // Handle common variations and exact matches
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Exact matches for our supported types
            [SupportedDataTypes.FirstName] = SupportedDataTypes.FirstName,
            [SupportedDataTypes.LastName] = SupportedDataTypes.LastName,
            [SupportedDataTypes.FullName] = SupportedDataTypes.FullName,
            [SupportedDataTypes.Email] = SupportedDataTypes.Email,
            [SupportedDataTypes.Phone] = SupportedDataTypes.Phone,
            [SupportedDataTypes.AddressLine1] = SupportedDataTypes.AddressLine1,
            [SupportedDataTypes.AddressLine2] = SupportedDataTypes.AddressLine2,
            [SupportedDataTypes.City] = SupportedDataTypes.City,
            [SupportedDataTypes.State] = SupportedDataTypes.State,
            [SupportedDataTypes.PostCode] = SupportedDataTypes.PostCode,
            [SupportedDataTypes.CreditCard] = SupportedDataTypes.CreditCard,
            [SupportedDataTypes.LicenseNumber] = SupportedDataTypes.LicenseNumber,
            [SupportedDataTypes.CompanyName] = SupportedDataTypes.CompanyName,
            [SupportedDataTypes.BusinessABN] = SupportedDataTypes.BusinessABN,
            [SupportedDataTypes.VehicleRegistration] = SupportedDataTypes.VehicleRegistration,
            [SupportedDataTypes.Date] = SupportedDataTypes.Date,
            [SupportedDataTypes.DateOfBirth] = SupportedDataTypes.DateOfBirth,
            
            // Common variations
            ["Email"] = SupportedDataTypes.Email,
            ["EmailAddress"] = SupportedDataTypes.Email,
            ["Phone"] = SupportedDataTypes.Phone,
            ["PhoneNumber"] = SupportedDataTypes.Phone,
            ["Mobile"] = SupportedDataTypes.Phone,
            ["Address"] = SupportedDataTypes.AddressLine1,
            ["StreetAddress"] = SupportedDataTypes.AddressLine1,
            ["PostalCode"] = SupportedDataTypes.PostCode,
            ["ZipCode"] = SupportedDataTypes.PostCode,
            ["CreditCardNumber"] = SupportedDataTypes.CreditCard,
            ["DriverLicense"] = SupportedDataTypes.LicenseNumber,
            ["Name"] = SupportedDataTypes.FullName,
            ["PersonName"] = SupportedDataTypes.FullName,
            ["DOB"] = SupportedDataTypes.DateOfBirth,
            ["BirthDate"] = SupportedDataTypes.DateOfBirth,
            ["Birthday"] = SupportedDataTypes.DateOfBirth
        };

        return mapping.TryGetValue(piiType, out var mapped) ? mapped : piiType;
    }

    private bool ShouldPreserveLength(string dataType)
    {
        var preserveLengthTypes = new[]
        {
            SupportedDataTypes.Phone,
            SupportedDataTypes.PostCode,
            SupportedDataTypes.CreditCard,
            SupportedDataTypes.LicenseNumber,
            SupportedDataTypes.BusinessABN,
            SupportedDataTypes.BusinessACN,
            SupportedDataTypes.VehicleRegistration
        };

        return preserveLengthTypes.Contains(dataType);
    }
}