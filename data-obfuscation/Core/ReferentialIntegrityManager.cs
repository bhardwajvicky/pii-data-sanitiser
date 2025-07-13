using Microsoft.Extensions.Logging;
using DataObfuscation.Configuration;
using System.Collections.Concurrent;

namespace DataObfuscation.Core;

public interface IReferentialIntegrityManager
{
    Task InitializeAsync(ReferentialIntegrityConfiguration config);
    string GetConsistentValue(string tableName, string columnName, string originalValue);
    void RegisterMapping(string tableName, string columnName, string originalValue, string obfuscatedValue);
    Dictionary<string, Dictionary<string, Dictionary<string, string>>> GetAllMappings();
}

public class ReferentialIntegrityManager : IReferentialIntegrityManager
{
    private readonly ILogger<ReferentialIntegrityManager> _logger;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, string>>> _mappings;
    private ReferentialIntegrityConfiguration _config = new();

    public ReferentialIntegrityManager(ILogger<ReferentialIntegrityManager> logger)
    {
        _logger = logger;
        _mappings = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, string>>>();
    }

    public Task InitializeAsync(ReferentialIntegrityConfiguration config)
    {
        _config = config;
        _logger.LogInformation("Referential integrity manager initialized with {RelationshipCount} relationships", 
            config.Relationships.Count);

        foreach (var relationship in config.Relationships)
        {
            _logger.LogDebug("Registered relationship: {RelationshipName} - {PrimaryTable}.{PrimaryColumn} -> {RelatedCount} related mappings",
                relationship.Name, relationship.PrimaryTable, relationship.PrimaryColumn, relationship.RelatedMappings.Count);
        }

        return Task.CompletedTask;
    }

    public string GetConsistentValue(string tableName, string columnName, string originalValue)
    {
        var tableKey = GetTableKey(tableName, columnName);
        
        if (tableKey == null)
        {
            return originalValue;
        }

        var tableMappings = _mappings.GetOrAdd(tableKey.PrimaryTable, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>());
        var columnMappings = tableMappings.GetOrAdd(tableKey.PrimaryColumn, _ => new ConcurrentDictionary<string, string>());

        return columnMappings.GetValueOrDefault(originalValue, originalValue);
    }

    public void RegisterMapping(string tableName, string columnName, string originalValue, string obfuscatedValue)
    {
        var tableKey = GetTableKey(tableName, columnName);
        
        if (tableKey != null)
        {
            var tableMappings = _mappings.GetOrAdd(tableKey.PrimaryTable, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>());
            var columnMappings = tableMappings.GetOrAdd(tableKey.PrimaryColumn, _ => new ConcurrentDictionary<string, string>());
            
            columnMappings.TryAdd(originalValue, obfuscatedValue);
            
            _logger.LogDebug("Registered mapping for {Table}.{Column}: {Original} -> {Obfuscated}",
                tableName, columnName, originalValue, obfuscatedValue);
        }

        var relationshipKey = GetRelationshipKey(tableName, columnName);
        
        if (relationshipKey != null)
        {
            var primaryTableMappings = _mappings.GetOrAdd(relationshipKey.PrimaryTable, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>());
            var primaryColumnMappings = primaryTableMappings.GetOrAdd(relationshipKey.PrimaryColumn, _ => new ConcurrentDictionary<string, string>());
            
            primaryColumnMappings.TryAdd(originalValue, obfuscatedValue);
            
            _logger.LogDebug("Registered cross-reference mapping for {PrimaryTable}.{PrimaryColumn}: {Original} -> {Obfuscated}",
                relationshipKey.PrimaryTable, relationshipKey.PrimaryColumn, originalValue, obfuscatedValue);
        }
    }

    private RelationshipKey? GetTableKey(string tableName, string columnName)
    {
        var relationship = _config.Relationships.FirstOrDefault(r => 
            string.Equals(r.PrimaryTable, tableName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.PrimaryColumn, columnName, StringComparison.OrdinalIgnoreCase));

        return relationship != null ? new RelationshipKey(relationship.PrimaryTable, relationship.PrimaryColumn) : null;
    }

    private RelationshipKey? GetRelationshipKey(string tableName, string columnName)
    {
        foreach (var relationship in _config.Relationships)
        {
            var relatedMapping = relationship.RelatedMappings.FirstOrDefault(rm =>
                string.Equals(rm.Table, tableName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(rm.Column, columnName, StringComparison.OrdinalIgnoreCase));

            if (relatedMapping != null)
            {
                return new RelationshipKey(relationship.PrimaryTable, relationship.PrimaryColumn);
            }
        }

        return null;
    }

    public Dictionary<string, Dictionary<string, Dictionary<string, string>>> GetAllMappings()
    {
        var result = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

        foreach (var tableKvp in _mappings)
        {
            var tableDict = new Dictionary<string, Dictionary<string, string>>();
            
            foreach (var columnKvp in tableKvp.Value)
            {
                tableDict[columnKvp.Key] = new Dictionary<string, string>(columnKvp.Value);
            }
            
            result[tableKvp.Key] = tableDict;
        }

        return result;
    }

    private record RelationshipKey(string PrimaryTable, string PrimaryColumn);
}