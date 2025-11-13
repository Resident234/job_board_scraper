using System.Text;

namespace JobBoardScraper.Models;

/// <summary>
/// Статистика работы скрапера
/// </summary>
public class ScraperStatistics
{
    public string ScraperName { get; set; } = string.Empty;
    public int TotalProcessed { get; set; }
    public int TotalSuccess { get; set; }
    public int TotalFailed { get; set; }
    public int TotalSkipped { get; set; }
    public int TotalFound { get; set; }
    public int TotalNotFound { get; set; }
    public int TotalItemsCollected { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan ElapsedTime => (EndTime ?? DateTime.Now) - StartTime;
    public double AverageRequestTime { get; set; }
    public int ActiveRequests { get; set; }
    
    public ScraperStatistics(string scraperName)
    {
        ScraperName = scraperName;
        StartTime = DateTime.Now;
    }
    
    // Методы для потокобезопасного обновления счетчиков
    public void IncrementProcessed() => Interlocked.Increment(ref TotalProcessed);
    public void IncrementSuccess() => Interlocked.Increment(ref TotalSuccess);
    public void IncrementFailed() => Interlocked.Increment(ref TotalFailed);
    public void IncrementSkipped() => Interlocked.Increment(ref TotalSkipped);
    public void IncrementFound() => Interlocked.Increment(ref TotalFound);
    public void IncrementNotFound() => Interlocked.Increment(ref TotalNotFound);
    public void IncrementItemsCollected() => Interlocked.Increment(ref TotalItemsCollected);
    public void AddItemsCollected(int count) => Interlocked.Add(ref TotalItemsCollected, count);
    public void UpdateActiveRequests(int count) => Interlocked.Exchange(ref ActiveRequests, count);
    
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
