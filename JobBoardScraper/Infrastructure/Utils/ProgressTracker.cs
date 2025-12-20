namespace JobBoardScraper.Infrastructure.Utils;

/// <summary>
/// Потокобезопасный счётчик прогресса для отслеживания выполнения задач.
/// </summary>
public sealed class ProgressTracker
{
    private int _processed;
    private int _total;
    private readonly string _taskName;
    
    public int Processed => _processed;
    public int Total => _total;
    public string TaskName => _taskName;
    public double Percent => _total > 0 ? _processed * 100.0 / _total : 0;
    public bool IsComplete => _processed >= _total;
    
    public ProgressTracker(int total, string? taskName = null)
    {
        _total = total;
        _taskName = taskName ?? string.Empty;
    }
    
    public int Increment() => Interlocked.Increment(ref _processed);
    
    public void Reset(int newTotal)
    {
        Interlocked.Exchange(ref _processed, 0);
        Interlocked.Exchange(ref _total, newTotal);
    }
    
    public override string ToString()
    {
        var prefix = string.IsNullOrEmpty(_taskName) ? "" : $"[{_taskName}] ";
        return $"{prefix}{_processed}/{_total} ({Percent:F1}%)";
    }
}
