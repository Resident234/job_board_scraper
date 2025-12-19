using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Helper.Utils;
using JobBoardScraper.Models;

namespace JobBoardScraper.Helper.Logger;

/// <summary>
/// Вспомогательный класс для логирования прогресса параллельных скраперов
/// </summary>
public static class ParallelScraperLogger
{
    /// <summary>
    /// Логирует прогресс HTTP запроса в параллельном скрапере
    /// </summary>
    /// <param name="logger">Логгер (если null, используется Console.WriteLine)</param>
    /// <param name="scraperName">Название скрапера</param>
    /// <param name="url">URL запроса</param>
    /// <param name="elapsedSeconds">Время выполнения запроса в секундах</param>
    /// <param name="statusCode">HTTP код ответа</param>
    /// <param name="completed">Количество обработанных элементов</param>
    /// <param name="total">Общее количество элементов</param>
    /// <param name="activeCount">Количество активных параллельных процессов</param>
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
        // Формируем сообщение без префикса - ConsoleLogger добавит его сам
        var messageBody = $"HTTP запрос {url}: {elapsedSeconds:F3} сек. " +
                         $"Код ответа {statusCode}. " +
                         $"Обработано: {completed}/{total} ({percent:F2}%). " +
                         $"Параллельных процессов: {activeCount}.";
        
        if (logger != null)
        {
            // ConsoleLogger уже добавляет [ProcessName] префикс
            logger.WriteLine(messageBody);
        }
        else
        {
            // Без логгера добавляем префикс вручную
            Console.WriteLine($"[{scraperName}] {messageBody}");
        }
    }
    
    /// <summary>
    /// Логирует прогресс HTTP запроса в параллельном скрапере с использованием ScraperStatistics
    /// </summary>
    /// <param name="logger">Логгер (если null, используется Console.WriteLine)</param>
    /// <param name="statistics">Статистика скрапера</param>
    /// <param name="url">URL запроса</param>
    /// <param name="elapsedSeconds">Время выполнения запроса в секундах</param>
    /// <param name="statusCode">HTTP код ответа</param>
    /// <param name="total">Общее количество элементов</param>
    public static void LogProgress(
        ConsoleLogger? logger,
        ScraperStatistics statistics,
        string url,
        double elapsedSeconds,
        int statusCode,
        int total)
    {
        double percent = statistics.TotalProcessed * 100.0 / total;
        // Формируем сообщение без префикса - ConsoleLogger добавит его сам
        var messageBody = $"HTTP запрос {url}: {elapsedSeconds:F3} сек. " +
                         $"Код ответа {statusCode}. " +
                         $"Обработано: {statistics.TotalProcessed}/{total} ({percent:F2}%). " +
                         $"Параллельных процессов: {statistics.ActiveRequests}.";
        
        if (logger != null)
        {
            // ConsoleLogger уже добавляет [ProcessName] префикс
            logger.WriteLine(messageBody);
        }
        else
        {
            // Без логгера добавляем префикс вручную
            Console.WriteLine($"[{statistics.ScraperName}] {messageBody}");
        }
    }
    
    /// <summary>
    /// Логирует прогресс HTTP запроса с использованием ProgressTracker
    /// </summary>
    /// <param name="logger">Логгер (если null, используется Console.WriteLine)</param>
    /// <param name="scraperName">Название скрапера</param>
    /// <param name="url">URL запроса</param>
    /// <param name="elapsedSeconds">Время выполнения запроса в секундах</param>
    /// <param name="statusCode">HTTP код ответа</param>
    /// <param name="progress">Трекер прогресса</param>
    /// <param name="activeCount">Количество активных параллельных процессов</param>
    public static void LogProgress(
        ConsoleLogger? logger,
        string scraperName,
        string url,
        double elapsedSeconds,
        int statusCode,
        ProgressTracker progress,
        int activeCount)
    {
        // Формируем сообщение без префикса - ConsoleLogger добавит его сам
        var messageBody = $"HTTP {url}: {elapsedSeconds:F3} сек. " +
                         $"Код: {statusCode}. " +
                         $"Прогресс: {progress}. " +
                         $"Параллельных: {activeCount}.";
        
        if (logger != null)
        {
            // ConsoleLogger уже добавляет [ProcessName] префикс
            logger.WriteLine(messageBody);
        }
        else
        {
            // Без логгера добавляем префикс вручную
            Console.WriteLine($"[{scraperName}] {messageBody}");
        }
    }
    
    /// <summary>
    /// Логирует прогресс HTTP запроса с использованием ProgressTracker и ScraperStatistics
    /// </summary>
    /// <param name="logger">Логгер (если null, используется Console.WriteLine)</param>
    /// <param name="statistics">Статистика скрапера</param>
    /// <param name="url">URL запроса</param>
    /// <param name="elapsedSeconds">Время выполнения запроса в секундах</param>
    /// <param name="statusCode">HTTP код ответа</param>
    /// <param name="progress">Трекер прогресса</param>
    public static void LogProgress(
        ConsoleLogger? logger,
        ScraperStatistics statistics,
        string url,
        double elapsedSeconds,
        int statusCode,
        ProgressTracker progress)
    {
        // Формируем сообщение без префикса - ConsoleLogger добавит его сам
        var messageBody = $"{url}: {elapsedSeconds:F3} сек. " +
                         $"Код: {statusCode}. " +
                         $"Прогресс: {progress}. " +
                         $"Параллельных: {statistics.ActiveRequests}.";
        
        if (logger != null)
        {
            // ConsoleLogger уже добавляет [ProcessName] префикс
            logger.WriteLine(messageBody);
        }
        else
        {
            // Без логгера добавляем префикс вручную
            Console.WriteLine($"[{statistics.ScraperName}] {messageBody}");
        }
    }
}
