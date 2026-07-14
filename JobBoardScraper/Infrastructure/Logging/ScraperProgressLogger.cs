using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Infrastructure.Utils;

namespace JobBoardScraper.Infrastructure.Logging;

/// <summary>
/// Объединяет счётчик прогресса и логирование для скраперов.
/// </summary>
public sealed class ScraperProgressLogger
{
    private readonly ProgressTracker _progress;
    private readonly ConsoleLogger _logger;
    private readonly string _scraperName;
    private int _activeRequests;

    public int Processed => _progress.Processed;
    public int Total => _progress.Total;
    public double Percent => _progress.Percent;
    public string TaskName => _progress.TaskName;
    public int ActiveRequests => _activeRequests;

    public ScraperProgressLogger(int total, string scraperName, ConsoleLogger? logger = null, string? taskName = null)
    {
        _progress = new ProgressTracker(total, taskName ?? scraperName);
        _scraperName = scraperName;
        _logger = logger ?? new ConsoleLogger(scraperName);
        _activeRequests = 0;
    }

    public int Increment() => _progress.Increment();

    public void UpdateActiveRequests(int count) => Interlocked.Exchange(ref _activeRequests, count);

    public void Reset(int newTotal = 0) => _progress.Reset(newTotal > 0 ? newTotal : _progress.Total);

    public bool IsComplete => _progress.IsComplete;

    public void LogHttpProgress(string url, double elapsedSeconds, int statusCode)
    {
        var message = $"[{_scraperName}] HTTP {url}: {elapsedSeconds:F3} сек. " +
                     $"Код: {statusCode}. Прогресс: {_progress}. Параллельных: {_activeRequests}.";
        WriteMessage(message);
    }

    public void LogHttpProgress(ScraperStatistics statistics, string url, double elapsedSeconds, int statusCode)
    {
        var message = $"[{statistics.ScraperName}] HTTP {url}: {elapsedSeconds:F3} сек. " +
                     $"Код: {statusCode}. Прогресс: {_progress}. Параллельных: {statistics.ActiveRequests}.";
        WriteMessage(message);
    }

    public void LogItemProgress(string itemDescription, int? itemsFound = null)
    {
        var foundPart = itemsFound.HasValue ? $" найдено {itemsFound.Value}." : "";
        WriteMessage($"[{_scraperName}] {itemDescription}:{foundPart} Прогресс: {_progress}.");
    }

    public void LogPageProgress(int pageNumber, int itemsFound)
    {
        WriteMessage($"[{_scraperName}] Страница {pageNumber}: найдено {itemsFound}. Прогресс: {_progress}.");
    }

    public void LogFilter(
        string filterDescription,
        int? itemsFound = null,
        string? order = null,
        string? filterParameter = null,
        string? resultDescription = null)
    {
        var normalizedFilterParameter = filterParameter?.Trim().TrimStart('?', '&');
        var filterParameterDesc = string.IsNullOrWhiteSpace(normalizedFilterParameter) ? "" : $" ({normalizedFilterParameter})";
        var resultDesc = string.IsNullOrWhiteSpace(resultDescription) ? "" : $": {resultDescription}";
        var orderDesc = string.IsNullOrWhiteSpace(order) ? "" : $" (order={order})";
        var foundPart = itemsFound.HasValue ? $" найдено {itemsFound.Value} профилей." : "";
        WriteMessage($"[{_scraperName}] {filterDescription}{filterParameterDesc}{resultDesc}{orderDesc}:{foundPart} Прогресс: {_progress}.");
    }

    public void LogCompletion(int totalItemsCollected, int totalOnSite, ScraperStatistics statistics)
    {
        if (totalOnSite > 0)
        {
            var percent = (double)totalItemsCollected / totalOnSite * 100;
            WriteMessage($"[{_scraperName}] Собрано {totalItemsCollected:N0} из {totalOnSite:N0} компаний ({percent:P1}). {statistics}");
        }
        else
        {
            WriteMessage($"[{_scraperName}] {statistics}");
        }
    }

    public void LogError(string errorMessage)
    {
        WriteMessage($"[{_scraperName}] Ошибка: {errorMessage}. Прогресс: {_progress}.");
    }

    public void LogInfo(string infoMessage)
    {
        WriteMessage($"[{_scraperName}] {infoMessage} Прогресс: {_progress}.");
    }

    public override string ToString() => _progress.ToString();

    private void WriteMessage(string message)
    {
        _logger.WriteLine(message);
    }
}
