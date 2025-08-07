using Microsoft.Extensions.Logging;

namespace DataObfuscation.Data;

public static class DatabaseTechnologyHelper
{
    public static string DetectDatabaseTechnology(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "SqlServer"; // Default

        var lowerConnStr = connectionString.ToLowerInvariant();
        
        if (lowerConnStr.Contains("server=") && lowerConnStr.Contains("database=") && 
            (lowerConnStr.Contains("encrypt=") || lowerConnStr.Contains("trusted_connection=")))
            return "SqlServer";
        else if (lowerConnStr.Contains("host=") && lowerConnStr.Contains("database="))
            return "PostgreSQL";
        else if (lowerConnStr.Contains("server=") && lowerConnStr.Contains("uid="))
            return "MySQL";
        else if (lowerConnStr.Contains("data source=") && lowerConnStr.Contains("user id="))
            return "Oracle";
        else if (lowerConnStr.Contains("data source=") && lowerConnStr.Contains(".db"))
            return "SQLite";
        
        return "SqlServer"; // Default fallback
    }

    public static string GetProviderName(string technology)
    {
        return technology.ToLowerInvariant() switch
        {
            "sqlserver" => "Microsoft.Data.SqlClient",
            "postgresql" => "Npgsql",
            "mysql" => "MySqlConnector",
            "oracle" => "Oracle.ManagedDataAccess",
            "sqlite" => "Microsoft.Data.Sqlite",
            _ => "Microsoft.Data.SqlClient"
        };
    }

    public static int GetDefaultBatchSize(string technology)
    {
        return technology.ToLowerInvariant() switch
        {
            "sqlserver" => 500,
            "postgresql" => 1000,
            "mysql" => 2000,
            "oracle" => 1000,
            "sqlite" => 500,
            _ => 500
        };
    }

    public static int GetDefaultSqlBatchSize(string technology)
    {
        return technology.ToLowerInvariant() switch
        {
            "sqlserver" => 100,
            "postgresql" => 500,
            "mysql" => 1000,
            "oracle" => 500,
            "sqlite" => 100,
            _ => 100
        };
    }

    public static int GetMaxParametersPerQuery(string technology)
    {
        return technology.ToLowerInvariant() switch
        {
            "sqlserver" => 2100,
            "postgresql" => 32767,
            "mysql" => 65535,
            "oracle" => 1000,
            "sqlite" => 999,
            _ => 2100
        };
    }
} 