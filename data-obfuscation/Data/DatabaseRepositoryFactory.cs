using Microsoft.Extensions.Logging;

namespace DataObfuscation.Data;

public interface IDatabaseRepositoryFactory
{
    IDataRepository CreateRepository(string technology, ILoggerFactory loggerFactory);
}

public class DatabaseRepositoryFactory : IDatabaseRepositoryFactory
{
    public IDataRepository CreateRepository(string technology, ILoggerFactory loggerFactory)
    {
        return technology.ToLowerInvariant() switch
        {
            "sqlserver" => new SqlServerRepository(loggerFactory.CreateLogger<SqlServerRepository>()),
            "postgresql" => new PostgreSQLRepository(loggerFactory.CreateLogger<PostgreSQLRepository>()),
            _ => new SqlServerRepository(loggerFactory.CreateLogger<SqlServerRepository>()) // Default fallback
        };
    }
} 