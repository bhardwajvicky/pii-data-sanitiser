namespace DataObfuscation.Data;

public class UpdateBatchResult
{
    public int SuccessfulRows { get; set; }
    public int SkippedRows { get; set; }
    public List<FailedRow> FailedRows { get; set; } = new();
    public bool HasCriticalError { get; set; }
    public string? CriticalErrorMessage { get; set; }
    
    public bool IsCompleteSuccess => SkippedRows == 0 && !HasCriticalError;
}

public class FailedRow
{
    public string TableName { get; set; } = string.Empty;
    public Dictionary<string, object?> PrimaryKeyValues { get; set; } = new();
    public Dictionary<string, object?> UpdatedValues { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
    public int SqlErrorNumber { get; set; }
    
    public string GetLogMessage()
    {
        var primaryKeys = string.Join(", ", PrimaryKeyValues.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        var updatedCols = string.Join(", ", UpdatedValues.Select(kvp => $"{kvp.Key}='{kvp.Value}'"));
        return $"Table: {TableName} | PrimaryKeys: [{primaryKeys}] | UpdatedValues: [{updatedCols}] | Error: {ErrorMessage}";
    }
}