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
    
    public override string ToString()
    {
        return $"[{ScraperName}] Обработано: {TotalProcessed}, Успешно: {TotalSuccess}, " +
               $"Ошибок: {TotalFailed}, Пропущено: {TotalSkipped}, " +
               $"Время: {ElapsedTime.TotalMinutes:F2} мин, " +
               $"Активных: {ActiveRequests}";
    }
}
