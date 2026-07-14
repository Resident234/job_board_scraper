using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Infrastructure.Utils;

namespace JobBoardScraper.Infrastructure.Logging;

/// <summary>
/// Вспомогательный класс для логирования прогресса параллельных скраперов
/// </summary>
public static class ScraperParallelLogger
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

        WriteLine(logger, scraperName, messageBody);
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

        WriteLine(logger, statistics.ScraperName, messageBody);
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
                         $"Код: {statusCode}. Прогресс: {progress}. Параллельных процессов: {activeCount}.";

        WriteLine(logger, scraperName, messageBody);
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
                         $"Код: {statusCode}. Прогресс: {progress}. Параллельных процессов: {statistics.ActiveRequests}.";

        WriteLine(logger, statistics.ScraperName, messageBody);
    }

    /// <summary>
    /// Логирует HTTP-запрос для произвольной страницы пагинации (без обновления статистики).
    /// Используется, когда первая страница уже была залогирована через <see cref="LogProgress(ConsoleLogger, ScraperStatistics, string, double, int, ProgressTracker)"/>,
    /// а для последующих страниц нужно лишь сообщить URL/время/код ответа.
    /// </summary>
    public static void LogPage(
        ConsoleLogger? logger,
        string scraperName,
        string url,
        int page,
        double elapsedSeconds,
        int statusCode)
    {
        var messageBody = $"HTTP запрос {url} (страница {page}): {elapsedSeconds:F3} сек. Код ответа {statusCode}.";

        WriteLine(logger, scraperName, messageBody);
    }

    private static void WriteLine(ConsoleLogger? logger, string scraperName, string messageBody)
    {
        (logger ?? new ConsoleLogger(scraperName)).WriteLine(messageBody);
    }
}
