using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DataObfuscation.Services;

public interface ICheckpointService
{
    Task<CheckpointState?> LoadCheckpointAsync(string configHash);
    Task SaveCheckpointAsync(CheckpointState state);
    Task ClearCheckpointAsync(string configHash);
    string ComputeConfigHash(string configPath, string mappingPath);
}

public class CheckpointService : ICheckpointService
{
    private readonly ILogger<CheckpointService> _logger;
    private readonly string _checkpointDirectory;

    public CheckpointService(ILogger<CheckpointService> logger)
    {
        _logger = logger;
        _checkpointDirectory = Path.Combine(Directory.GetCurrentDirectory(), "checkpoints");
        
        if (!Directory.Exists(_checkpointDirectory))
        {
            Directory.CreateDirectory(_checkpointDirectory);
        }
    }

    public async Task<CheckpointState?> LoadCheckpointAsync(string configHash)
    {
        var checkpointPath = GetCheckpointPath(configHash);
        
        if (!File.Exists(checkpointPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(checkpointPath);
            var state = JsonSerializer.Deserialize<CheckpointState>(json);
            
            _logger.LogInformation("Loaded checkpoint for config hash: {ConfigHash}", configHash);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load checkpoint for config hash: {ConfigHash}", configHash);
            return null;
        }
    }

    public async Task SaveCheckpointAsync(CheckpointState state)
    {
        var checkpointPath = GetCheckpointPath(state.ConfigHash);
        
        try
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(checkpointPath, json);
            _logger.LogDebug("Saved checkpoint for config hash: {ConfigHash}", state.ConfigHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save checkpoint for config hash: {ConfigHash}", state.ConfigHash);
            throw;
        }
    }

    public Task ClearCheckpointAsync(string configHash)
    {
        var checkpointPath = GetCheckpointPath(configHash);
        
        if (File.Exists(checkpointPath))
        {
            try
            {
                File.Delete(checkpointPath);
                _logger.LogInformation("Cleared checkpoint for config hash: {ConfigHash}", configHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear checkpoint for config hash: {ConfigHash}", configHash);
            }
        }
        
        return Task.CompletedTask;
    }

    public string ComputeConfigHash(string configPath, string mappingPath)
    {
        try
        {
            var configContent = File.ReadAllText(configPath);
            var mappingContent = File.ReadAllText(mappingPath);
            
            // Combine contents and compute hash
            var combined = $"{configContent}|{mappingContent}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
            return Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-")[..16];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute config hash");
            throw;
        }
    }

    private string GetCheckpointPath(string configHash)
    {
        return Path.Combine(_checkpointDirectory, $"checkpoint_{configHash}.json");
    }
}

public class CheckpointState
{
    public string ConfigHash { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public string Status { get; set; } = "InProgress"; // InProgress, Completed, Failed
    public List<CheckpointTableProgress> Tables { get; set; } = new();
    public long TotalRowsProcessed { get; set; }
    public string ConfigPath { get; set; } = string.Empty;
    public string MappingPath { get; set; } = string.Empty;
}

public class CheckpointTableProgress
{
    public string TableName { get; set; } = string.Empty;
    public string Status { get; set; } = "NotStarted"; // NotStarted, InProgress, Completed, Failed
    public List<CheckpointBatchProgress> Batches { get; set; } = new();
    public long TotalRows { get; set; }
    public long ProcessedRows { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class CheckpointBatchProgress
{
    public int BatchNumber { get; set; }
    public int Offset { get; set; }
    public int Size { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RowsProcessed { get; set; }
    public string? ErrorMessage { get; set; }
}