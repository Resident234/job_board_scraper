using JobBoardScraper.Domain.Models;
using JobBoardScraper.Infrastructure.Utils;

namespace JobBoardScraper.Infrastructure.Logging;

/// <summary>
/// Вспомогательный класс для логирования прогресса параллельных скраперов
/// </summary>
public static class ParallelScraperLogger
{
    public static void LogProgress(
        ConsoleLogger? logger,
        string scraperName,
        string url,
        double elapsedSeconds,
        int statusCode,
        int completed,
        int total,
        int activeCount)
    {
        double percent = completed * 100.0 / total;
        var messageBody = $"HTTP запрос {url}: {elapsedSeconds:F3} сек. " +
                         $"Код ответа {statusCode}. " +
                         $"Обработано: {completed}/{total} ({percent:F2}%). " +
                         $"Параллельных процессов: {activeCount}.";
        
        if (logger != null)
            logger.WriteLine(messageBody);
        else
            Console.WriteLine($"[{scraperName}] {messageBody}");
    }
    
    public static void LogProgress(
        ConsoleLogger? logger,
        ScraperStatistics statistics,
        string url,
        double elapsedSeconds,
        int statusCode,
        int total)
    {
        double percent = statistics.TotalProcessed * 100.0 / total;
        var messageBody = $"HTTP запрос {url}: {elapsedSeconds:F3} сек. " +
                         $"Код ответа {statusCode}. " +
                         $"Обработано: {statistics.TotalProcessed}/{total} ({percent:F2}%). " +
                         $"Параллельных процессов: {statistics.ActiveRequests}.";
        
        if (logger != null)
            logger.WriteLine(messageBody);
        else
            Console.WriteLine($"[{statistics.ScraperName}] {messageBody}");
    }
    
    public static void LogProgress(
        ConsoleLogger? logger,
        string scraperName,
        string url,
        double elapsedSeconds,
        int statusCode,
        ProgressTracker progress,
        int activeCount)
    {
        var messageBody = $"HTTP {url}: {elapsedSeconds:F3} сек. " +
                         $"Код: {statusCode}. Прогресс: {progress}. Параллельных: {activeCount}.";
        
        if (logger != null)
            logger.WriteLine(messageBody);
        else
            Console.WriteLine($"[{scraperName}] {messageBody}");
    }
    
    public static void LogProgress(
        ConsoleLogger? logger,
        ScraperStatistics statistics,
        string url,
        double elapsedSeconds,
        int statusCode,
        ProgressTracker progress)
    {
        var messageBody = $"{url}: {elapsedSeconds:F3} сек. " +
                         $"Код: {statusCode}. Прогресс: {progress}. Параллельных: {statistics.ActiveRequests}.";
        
        if (logger != null)
            logger.WriteLine(messageBody);
        else
            Console.WriteLine($"[{statistics.ScraperName}] {messageBody}");
    }
}
