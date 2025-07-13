using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DataObfuscation.Core;

public interface IProgressTracker
{
    void Initialize(int totalTables);
    void StartTable(string tableName);
    void UpdateProgress(string tableName, long processedRows, long totalRows);
    void CompleteTable(string tableName);
    void FailTable(string tableName, string errorMessage);
    ProgressSummary GetSummary();
}

public class ProgressTracker : IProgressTracker
{
    private readonly ILogger<ProgressTracker> _logger;
    private readonly ConcurrentDictionary<string, TableProgress> _tableProgress;
    private readonly Stopwatch _overallStopwatch;
    private int _totalTables;
    private int _completedTables;
    private int _failedTables;

    public ProgressTracker(ILogger<ProgressTracker> logger)
    {
        _logger = logger;
        _tableProgress = new ConcurrentDictionary<string, TableProgress>();
        _overallStopwatch = new Stopwatch();
    }

    public void Initialize(int totalTables)
    {
        _totalTables = totalTables;
        _completedTables = 0;
        _failedTables = 0;
        _tableProgress.Clear();
        
        _overallStopwatch.Start();
        
        _logger.LogInformation("Progress tracking initialized for {TotalTables} tables", totalTables);
    }

    public void StartTable(string tableName)
    {
        var progress = new TableProgress
        {
            TableName = tableName,
            Status = TableStatus.InProgress,
            StartTime = DateTime.UtcNow
        };

        _tableProgress.TryAdd(tableName, progress);
        
        _logger.LogInformation("Started processing table: {TableName}", tableName);
    }

    public void UpdateProgress(string tableName, long processedRows, long totalRows)
    {
        if (_tableProgress.TryGetValue(tableName, out var progress))
        {
            progress.ProcessedRows = processedRows;
            progress.TotalRows = totalRows;
            progress.LastUpdateTime = DateTime.UtcNow;

            var percentage = totalRows > 0 ? (double)processedRows / totalRows * 100 : 0;
            
            if (processedRows % 50000 == 0 || percentage % 10 < 0.1)
            {
                var elapsed = DateTime.UtcNow - progress.StartTime;
                var rowsPerSecond = elapsed.TotalSeconds > 0 ? processedRows / elapsed.TotalSeconds : 0;
                
                _logger.LogInformation("Table {TableName}: {ProcessedRows:N0}/{TotalRows:N0} ({Percentage:F1}%) - {RowsPerSecond:F0} rows/sec",
                    tableName, processedRows, totalRows, percentage, rowsPerSecond);
            }
        }
    }

    public void CompleteTable(string tableName)
    {
        if (_tableProgress.TryGetValue(tableName, out var progress))
        {
            progress.Status = TableStatus.Completed;
            progress.EndTime = DateTime.UtcNow;
            progress.Duration = progress.EndTime.Value - progress.StartTime;
            
            Interlocked.Increment(ref _completedTables);
            
            var rowsPerSecond = progress.Duration.TotalSeconds > 0 
                ? progress.ProcessedRows / progress.Duration.TotalSeconds 
                : 0;
            
            _logger.LogInformation("Completed table {TableName}: {ProcessedRows:N0} rows in {Duration} ({RowsPerSecond:F0} rows/sec)",
                tableName, progress.ProcessedRows, progress.Duration, rowsPerSecond);
            
            LogOverallProgress();
        }
    }

    public void FailTable(string tableName, string errorMessage)
    {
        if (_tableProgress.TryGetValue(tableName, out var progress))
        {
            progress.Status = TableStatus.Failed;
            progress.EndTime = DateTime.UtcNow;
            progress.Duration = progress.EndTime.Value - progress.StartTime;
            progress.ErrorMessage = errorMessage;
            
            Interlocked.Increment(ref _failedTables);
            
            _logger.LogError("Failed table {TableName} after {Duration}: {ErrorMessage}",
                tableName, progress.Duration, errorMessage);
            
            LogOverallProgress();
        }
    }

    private void LogOverallProgress()
    {
        var totalCompleted = _completedTables + _failedTables;
        var overallPercentage = _totalTables > 0 ? (double)totalCompleted / _totalTables * 100 : 0;
        
        _logger.LogInformation("Overall progress: {Completed}/{Total} tables ({Percentage:F1}%) - {CompletedCount} completed, {FailedCount} failed",
            totalCompleted, _totalTables, overallPercentage, _completedTables, _failedTables);
    }

    public ProgressSummary GetSummary()
    {
        var summary = new ProgressSummary
        {
            TotalTables = _totalTables,
            CompletedTables = _completedTables,
            FailedTables = _failedTables,
            OverallDuration = _overallStopwatch.Elapsed,
            TableDetails = _tableProgress.Values.ToList()
        };

        summary.TotalRowsProcessed = summary.TableDetails.Sum(t => t.ProcessedRows);
        summary.OverallRowsPerSecond = summary.OverallDuration.TotalSeconds > 0 
            ? summary.TotalRowsProcessed / summary.OverallDuration.TotalSeconds 
            : 0;

        return summary;
    }
}

public class TableProgress
{
    public string TableName { get; set; } = string.Empty;
    public TableStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public long ProcessedRows { get; set; }
    public long TotalRows { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public string? ErrorMessage { get; set; }

    public double ProgressPercentage => TotalRows > 0 ? (double)ProcessedRows / TotalRows * 100 : 0;
    
    public double RowsPerSecond => Duration.TotalSeconds > 0 ? ProcessedRows / Duration.TotalSeconds : 0;
}

public enum TableStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public class ProgressSummary
{
    public int TotalTables { get; set; }
    public int CompletedTables { get; set; }
    public int FailedTables { get; set; }
    public long TotalRowsProcessed { get; set; }
    public TimeSpan OverallDuration { get; set; }
    public double OverallRowsPerSecond { get; set; }
    public List<TableProgress> TableDetails { get; set; } = new();

    public double OverallPercentage => TotalTables > 0 ? (double)(CompletedTables + FailedTables) / TotalTables * 100 : 0;
}