using Microsoft.Extensions.Logging;
using AutoMappingGenerator.Models;
using System.Text.RegularExpressions;

namespace AutoMappingGenerator.Services;

public interface IPIIDetectionService
{
    PIIColumn? AnalyzeColumn(TableInfo table, ColumnInfo column);
    List<PIIDetectionRule> GetDetectionRules();
}

public class PIIDetectionService : IPIIDetectionService
{
    private readonly ILogger<PIIDetectionService> _logger;
    private readonly List<PIIDetectionRule> _detectionRules;

    public PIIDetectionService(ILogger<PIIDetectionService> logger)
    {
        _logger = logger;
        _detectionRules = InitializeDetectionRules();
    }

    public PIIColumn? AnalyzeColumn(TableInfo table, ColumnInfo column)
    {
        var detectionReasons = new List<string>();
        double maxConfidence = 0;
        PIIDetectionRule? bestRule = null;

        foreach (var rule in _detectionRules)
        {
            var confidence = CalculateConfidence(table, column, rule, detectionReasons);
            if (confidence > maxConfidence)
            {
                maxConfidence = confidence;
                bestRule = rule;
            }
        }

        // Minimum confidence threshold for PII detection
        const double MinConfidenceThreshold = 0.6;

        if (maxConfidence >= MinConfidenceThreshold && bestRule != null)
        {
            return new PIIColumn
            {
                ColumnName = column.ColumnName,
                DataType = bestRule.ObfuscationDataType,
                SqlDataType = column.SqlDataType,
                MaxLength = column.MaxLength,
                IsNullable = column.IsNullable,
                ConfidenceScore = maxConfidence,
                DetectionReasons = detectionReasons,
                PreserveLength = bestRule.PreserveLength && column.MaxLength.HasValue
            };
        }

        return null;
    }

    public List<PIIDetectionRule> GetDetectionRules()
    {
        return _detectionRules.ToList();
    }

    private double CalculateConfidence(TableInfo table, ColumnInfo column, PIIDetectionRule rule, List<string> reasons)
    {
        double confidence = 0;
        var columnNameLower = column.ColumnName.ToLower();
        var tableNameLower = table.TableName.ToLower();

        // Check column name patterns
        foreach (var pattern in rule.ColumnNamePatterns)
        {
            if (IsPatternMatch(columnNameLower, pattern.ToLower()))
            {
                confidence += 0.8;
                reasons.Add($"Column name matches pattern: {pattern}");
                break;
            }
        }

        // Check SQL data type compatibility
        if (rule.SqlDataTypes.Contains(column.SqlDataType.ToLower()))
        {
            confidence += 0.3;
            reasons.Add($"SQL data type matches: {column.SqlDataType}");
        }

        // Check table name context
        foreach (var tablePattern in rule.TableNamePatterns)
        {
            if (IsPatternMatch(tableNameLower, tablePattern.ToLower()))
            {
                confidence += 0.2;
                reasons.Add($"Table name matches pattern: {tablePattern}");
                break;
            }
        }

        // Additional heuristics based on context
        confidence += ApplyContextualHeuristics(table, column, rule, reasons);

        return Math.Min(confidence, 1.0); // Cap at 1.0
    }

    private double ApplyContextualHeuristics(TableInfo table, ColumnInfo column, PIIDetectionRule rule, List<string> reasons)
    {
        double additionalConfidence = 0;
        var columnNameLower = column.ColumnName.ToLower();

        // Email address specific heuristics
        if (rule.DataType == PIIDataType.EmailAddress)
        {
            if (columnNameLower.Contains("email") || columnNameLower.Contains("mail"))
            {
                additionalConfidence += 0.3;
                reasons.Add("Column name contains email-related terms");
            }
        }

        // Phone number specific heuristics
        if (rule.DataType == PIIDataType.PhoneNumber)
        {
            if (columnNameLower.Contains("phone") || columnNameLower.Contains("mobile") || 
                columnNameLower.Contains("tel") || columnNameLower.Contains("fax"))
            {
                additionalConfidence += 0.3;
                reasons.Add("Column name contains phone-related terms");
            }
        }

        // Address specific heuristics
        if (rule.DataType == PIIDataType.Address)
        {
            if (columnNameLower.Contains("address") || columnNameLower.Contains("street") ||
                columnNameLower.Contains("city") || columnNameLower.Contains("suburb"))
            {
                additionalConfidence += 0.3;
                reasons.Add("Column name contains address-related terms");
            }
        }

        // Name specific heuristics
        if (rule.DataType == PIIDataType.PersonName)
        {
            if (columnNameLower.Contains("name") && 
                (columnNameLower.Contains("first") || columnNameLower.Contains("last") || 
                 columnNameLower.Contains("full") || columnNameLower.Contains("display")))
            {
                additionalConfidence += 0.3;
                reasons.Add("Column name indicates personal name");
            }
        }

        // Length-based heuristics
        if (column.MaxLength.HasValue)
        {
            if (rule.DataType == PIIDataType.EmailAddress && column.MaxLength >= 50)
            {
                additionalConfidence += 0.1;
                reasons.Add("Column length appropriate for email addresses");
            }
            else if (rule.DataType == PIIDataType.PhoneNumber && column.MaxLength >= 10 && column.MaxLength <= 20)
            {
                additionalConfidence += 0.1;
                reasons.Add("Column length appropriate for phone numbers");
            }
            else if (rule.DataType == PIIDataType.PersonName && column.MaxLength >= 20)
            {
                additionalConfidence += 0.1;
                reasons.Add("Column length appropriate for person names");
            }
        }

        return additionalConfidence;
    }

    private static bool IsPatternMatch(string input, string pattern)
    {
        // Simple pattern matching - can be enhanced with regex if needed
        if (pattern.Contains("*"))
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
        }
        
        return input.Contains(pattern);
    }

    private List<PIIDetectionRule> InitializeDetectionRules()
    {
        return new List<PIIDetectionRule>
        {
            // Person Names
            new PIIDetectionRule
            {
                DataType = PIIDataType.PersonName,
                ObfuscationDataType = "DriverName",
                ColumnNamePatterns = new List<string>
                {
                    "*name*", "*first*", "*last*", "*middle*", "*full*", "*display*",
                    "*fname*", "*lname*", "*firstname*", "*lastname*", "*fullname*",
                    "*contact*name*", "*person*", "*employee*name*", "*customer*name*"
                },
                SqlDataTypes = new List<string> { "varchar", "nvarchar", "char", "nchar", "text", "ntext" },
                TableNamePatterns = new List<string> { "*person*", "*employee*", "*customer*", "*contact*", "*user*" },
                BaseConfidence = 0.7,
                PreserveLength = true
            },

            // Email Addresses
            new PIIDetectionRule
            {
                DataType = PIIDataType.EmailAddress,
                ObfuscationDataType = "ContactEmail",
                ColumnNamePatterns = new List<string>
                {
                    "*email*", "*mail*", "*e_mail*", "*emailaddress*", "*email_address*",
                    "*contact*email*", "*business*email*", "*work*email*", "*personal*email*"
                },
                SqlDataTypes = new List<string> { "varchar", "nvarchar", "char", "nchar", "text", "ntext" },
                TableNamePatterns = new List<string> { "*contact*", "*customer*", "*employee*", "*user*", "*person*" },
                BaseConfidence = 0.8,
                PreserveLength = false
            },

            // Phone Numbers
            new PIIDetectionRule
            {
                DataType = PIIDataType.PhoneNumber,
                ObfuscationDataType = "DriverPhone",
                ColumnNamePatterns = new List<string>
                {
                    "*phone*", "*mobile*", "*cell*", "*tel*", "*telephone*", "*fax*",
                    "*contact*number*", "*home*phone*", "*work*phone*", "*business*phone*"
                },
                SqlDataTypes = new List<string> { "varchar", "nvarchar", "char", "nchar" },
                TableNamePatterns = new List<string> { "*contact*", "*customer*", "*employee*", "*person*" },
                BaseConfidence = 0.8,
                PreserveLength = true
            },

            // Addresses
            new PIIDetectionRule
            {
                DataType = PIIDataType.Address,
                ObfuscationDataType = "Address",
                ColumnNamePatterns = new List<string>
                {
                    "*address*", "*street*", "*addr*", "*location*", "*city*", "*suburb*",
                    "*postal*", "*zip*", "*state*", "*country*", "*home*address*", "*work*address*"
                },
                SqlDataTypes = new List<string> { "varchar", "nvarchar", "char", "nchar", "text", "ntext" },
                TableNamePatterns = new List<string> { "*address*", "*contact*", "*customer*", "*employee*", "*location*" },
                BaseConfidence = 0.7,
                PreserveLength = false
            },

            // Business Numbers (ABN/ACN equivalent)
            new PIIDetectionRule
            {
                DataType = PIIDataType.BusinessNumber,
                ObfuscationDataType = "BusinessABN",
                ColumnNamePatterns = new List<string>
                {
                    "*abn*", "*acn*", "*business*number*", "*company*number*", "*tax*number*",
                    "*employer*number*", "*federal*id*", "*ein*"
                },
                SqlDataTypes = new List<string> { "varchar", "nvarchar", "char", "nchar" },
                TableNamePatterns = new List<string> { "*business*", "*company*", "*vendor*", "*supplier*" },
                BaseConfidence = 0.9,
                PreserveLength = true
            },

            // License Numbers
            new PIIDetectionRule
            {
                DataType = PIIDataType.LicenseNumber,
                ObfuscationDataType = "DriverLicenseNumber",
                ColumnNamePatterns = new List<string>
                {
                    "*license*", "*licence*", "*permit*", "*registration*", "*credential*",
                    "*driver*license*", "*dl*number*"
                },
                SqlDataTypes = new List<string> { "varchar", "nvarchar", "char", "nchar" },
                TableNamePatterns = new List<string> { "*driver*", "*license*", "*permit*", "*credential*" },
                BaseConfidence = 0.8,
                PreserveLength = true
            },

            // Comments and Notes (potentially containing PII)
            new PIIDetectionRule
            {
                DataType = PIIDataType.Comments,
                ObfuscationDataType = "DriverName",
                ColumnNamePatterns = new List<string>
                {
                    "*comment*", "*note*", "*description*", "*remark*", "*memo*",
                    "*detail*", "*observation*", "*feedback*"
                },
                SqlDataTypes = new List<string> { "varchar", "nvarchar", "text", "ntext" },
                TableNamePatterns = new List<string> { "*", }, // Any table
                BaseConfidence = 0.4, // Lower confidence as these may not always contain PII
                PreserveLength = false
            },

            // UserNames
            new PIIDetectionRule
            {
                DataType = PIIDataType.UserName,
                ObfuscationDataType = "DriverName",
                ColumnNamePatterns = new List<string>
                {
                    "*username*", "*user*name*", "*login*", "*userid*", "*user*id*",
                    "*account*name*", "*loginid*", "*login*id*"
                },
                SqlDataTypes = new List<string> { "varchar", "nvarchar", "char", "nchar" },
                TableNamePatterns = new List<string> { "*user*", "*account*", "*login*", "*authentication*" },
                BaseConfidence = 0.8,
                PreserveLength = true
            },

            // IP Addresses
            new PIIDetectionRule
            {
                DataType = PIIDataType.IPAddress,
                ObfuscationDataType = "GPSCoordinate", // Reuse coordinate obfuscation
                ColumnNamePatterns = new List<string>
                {
                    "*ip*", "*ipaddress*", "*ip_address*", "*host*", "*client*ip*"
                },
                SqlDataTypes = new List<string> { "varchar", "nvarchar", "char", "nchar" },
                TableNamePatterns = new List<string> { "*log*", "*audit*", "*session*", "*connection*" },
                BaseConfidence = 0.9,
                PreserveLength = true
            }
        };
    }
}