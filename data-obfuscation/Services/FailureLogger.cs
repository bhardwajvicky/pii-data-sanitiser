using System.Text.Json;
using DataObfuscation.Data;

namespace DataObfuscation.Services;

public interface IFailureLogger
{
    Task LogFailedRowAsync(FailedRow failedRow);
    Task InitializeAsync(string databaseName);
    Task FinalizeAsync();
}

public class FailureLogger : IFailureLogger, IDisposable
{
    private StreamWriter? _writer;
    private readonly object _lock = new();
    private string _logPath = string.Empty;
    
    public async Task InitializeAsync(string databaseName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var directory = Path.Combine("logs", "failures");
        Directory.CreateDirectory(directory);
        
        _logPath = Path.Combine(directory, $"{databaseName}_failures_{timestamp}.log");
        _writer = new StreamWriter(_logPath, append: false) { AutoFlush = true };
        
        // Write header
        await _writer.WriteLineAsync($"# Data Obfuscation Failure Log");
        await _writer.WriteLineAsync($"# Database: {databaseName}");
        await _writer.WriteLineAsync($"# Started: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        await _writer.WriteLineAsync($"# Format: Timestamp | Table | PrimaryKeys | OriginalValues | ObfuscatedValues | Error");
        await _writer.WriteLineAsync(new string('-', 120));
    }
    
    public Task LogFailedRowAsync(FailedRow failedRow)
    {
        if (_writer == null) return Task.CompletedTask;
        
        lock (_lock)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var primaryKeys = JsonSerializer.Serialize(failedRow.PrimaryKeyValues);
            
            // Separate original and obfuscated values
            var originalValues = new Dictionary<string, object?>();
            var obfuscatedValues = new Dictionary<string, object?>();
            
            foreach (var kvp in failedRow.UpdatedValues)
            {
                if (kvp.Key.Contains("_original", StringComparison.OrdinalIgnoreCase))
                {
                    originalValues[kvp.Key] = kvp.Value;
                }
                else
                {
                    obfuscatedValues[kvp.Key] = kvp.Value;
                }
            }
            
            var logEntry = $"{timestamp} | " +
                          $"{failedRow.TableName} | " +
                          $"PK: {primaryKeys} | " +
                          $"Original: {JsonSerializer.Serialize(originalValues)} | " +
                          $"Obfuscated: {JsonSerializer.Serialize(obfuscatedValues)} | " +
                          $"Error: {failedRow.ErrorMessage}";
            
            _writer.WriteLine(logEntry);
        }
        
        return Task.CompletedTask;
    }
    
    public async Task FinalizeAsync()
    {
        if (_writer != null)
        {
            await _writer.WriteLineAsync(new string('-', 120));
            await _writer.WriteLineAsync($"# Completed: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            await _writer.FlushAsync();
        }
    }
    
    public void Dispose()
    {
        _writer?.Dispose();
    }
}