using System.Text;

namespace JobBoardScraper.Models;

/// <summary>
/// Статистика работы скрапера
/// </summary>
public class ScraperStatistics
{
    // Приватные поля для потокобезопасных операций
    private int _totalProcessed;
    private int _totalSuccess;
    private int _totalFailed;
    private int _totalSkipped;
    private int _totalFound;
    private int _totalNotFound;
    private int _totalItemsCollected;
    private int _activeRequests;
    
    // Публичные свойства для чтения
    public string ScraperName { get; set; } = string.Empty;
    public int TotalProcessed => _totalProcessed;
    public int TotalSuccess => _totalSuccess;
    public int TotalFailed => _totalFailed;
    public int TotalSkipped => _totalSkipped;
    public int TotalFound => _totalFound;
    public int TotalNotFound => _totalNotFound;
    public int TotalItemsCollected => _totalItemsCollected;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan ElapsedTime => (EndTime ?? DateTime.Now) - StartTime;
    public double AverageRequestTime { get; set; }
    public int ActiveRequests => _activeRequests;
    
    public ScraperStatistics(string scraperName)
    {
        ScraperName = scraperName;
        StartTime = DateTime.Now;
    }
    
    // Методы для потокобезопасного обновления счетчиков
    public void IncrementProcessed() => Interlocked.Increment(ref _totalProcessed);
    public void IncrementSuccess() => Interlocked.Increment(ref _totalSuccess);
    public void IncrementFailed() => Interlocked.Increment(ref _totalFailed);
    public void IncrementSkipped() => Interlocked.Increment(ref _totalSkipped);
    public void IncrementFound() => Interlocked.Increment(ref _totalFound);
    public void IncrementNotFound() => Interlocked.Increment(ref _totalNotFound);
    public void IncrementItemsCollected() => Interlocked.Increment(ref _totalItemsCollected);
    public void AddItemsCollected(int count) => Interlocked.Add(ref _totalItemsCollected, count);
    public void UpdateActiveRequests(int count) => Interlocked.Exchange(ref _activeRequests, count);
    
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"[{ScraperName}] Обработано: {TotalProcessed}");
        
        if (TotalSuccess > 0)
            sb.Append($", Успешно: {TotalSuccess}");
        
        if (TotalFailed > 0)
            sb.Append($", Ошибок: {TotalFailed}");
        
        if (TotalSkipped > 0)
            sb.Append($", Пропущено: {TotalSkipped}");
        
        if (TotalFound > 0)
            sb.Append($", Найдено: {TotalFound}");
        
        if (TotalNotFound > 0)
            sb.Append($", Не найдено: {TotalNotFound}");
        
        if (TotalItemsCollected > 0)
            sb.Append($", Собрано элементов: {TotalItemsCollected}");
        
        sb.Append($", Время: {ElapsedTime.TotalMinutes:F2} мин");
        
        if (ActiveRequests > 0)
            sb.Append($", Активных: {ActiveRequests}");
        
        return sb.ToString();
    }
}
