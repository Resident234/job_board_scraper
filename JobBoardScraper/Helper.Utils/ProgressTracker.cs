namespace JobBoardScraper.Helper.Utils;

/// <summary>
/// Потокобезопасный счётчик прогресса для отслеживания выполнения задач.
/// Используется для отображения прогресса в формате "X/Y (Z%)"
/// </summary>
public sealed class ProgressTracker
{
    private int _processed;
    private readonly int _total;
    private readonly string _taskName;
    
    /// <summary>
    /// Количество обработанных элементов
    /// </summary>
    public int Processed => _processed;
    
    /// <summary>
    /// Общее количество элементов
    /// </summary>
    public int Total => _total;
    
    /// <summary>
    /// Название задачи
    /// </summary>
    public string TaskName => _taskName;
    
    /// <summary>
    /// Процент выполнения (0-100)
    /// </summary>
    public double Percent => _total > 0 ? _processed * 100.0 / _total : 0;
    
    /// <summary>
    /// Создаёт новый счётчик прогресса
    /// </summary>
    /// <param name="total">Общее количество элементов для обработки</param>
    /// <param name="taskName">Название задачи (опционально)</param>
    public ProgressTracker(int total, string? taskName = null)
    {
        _total = total;
        _taskName = taskName ?? string.Empty;
        _processed = 0;
    }
    
    /// <summary>
    /// Увеличивает счётчик обработанных элементов на 1
    /// </summary>
    /// <returns>Новое значение счётчика</returns>
    public int Increment()
    {
        return Interlocked.Increment(ref _processed);
    }
    
    /// <summary>
    /// Увеличивает счётчик обработанных элементов на указанное значение
    /// </summary>
    /// <param name="count">Количество для добавления</param>
    /// <returns>Новое значение счётчика</returns>
    public int Add(int count)
    {
        return Interlocked.Add(ref _processed, count);
    }
    
    /// <summary>
    /// Сбрасывает счётчик в 0
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _processed, 0);
    }
    
    /// <summary>
    /// Возвращает строку прогресса в формате "X/Y (Z%)"
    /// </summary>
    public string GetProgressString()
    {
        return $"{_processed}/{_total} ({Percent:F2}%)";
    }
    
    /// <summary>
    /// Возвращает строку прогресса с названием задачи в формате "[TaskName] X/Y (Z%)"
    /// </summary>
    public string GetProgressStringWithName()
    {
        if (string.IsNullOrEmpty(_taskName))
            return GetProgressString();
        
        return $"[{_taskName}] {GetProgressString()}";
    }
    
    /// <summary>
    /// Проверяет, завершена ли обработка всех элементов
    /// </summary>
    public bool IsComplete => _processed >= _total;
    
    public override string ToString() => GetProgressString();
}
