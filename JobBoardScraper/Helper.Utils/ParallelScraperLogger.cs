using JobBoardScraper.Helper.ConsoleHelper;

namespace JobBoardScraper.Helper.Utils;

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
        var message = $"[{scraperName}] HTTP запрос {url}: {elapsedSeconds:F3} сек. " +
                     $"Код ответа {statusCode}. " +
                     $"Обработано: {completed}/{total} ({percent:F2}%). " +
                     $"Параллельных процессов: {activeCount}.";
        
        if (logger != null)
        {
            logger.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }
}
