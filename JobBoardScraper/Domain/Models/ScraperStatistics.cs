using System.Collections.Concurrent;
using System.Text;

namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Статистика работы скрапера
/// </summary>
public class ScraperStatistics
{
    // Приватные поля для потокобезопасных операций
    private int _initialRecordCount;
    private int _totalProcessed;
    private int _totalSuccess;
    private int _totalFailed;
    private int _totalSkipped;
    private int _totalFound;
    private int _totalNotFound;
    private int _totalItemsCollected;
    private int _activeRequests;

    // Статистика по HTTP кодам ответов
    private readonly ConcurrentDictionary<int, int> _finalStatusCodeCounts = new();
    private readonly ConcurrentDictionary<int, int> _allStatusCodeCounts = new();

    // Публичные свойства для чтения
    public string ScraperName { get; set; } = string.Empty;
    public int InitialRecordCount => _initialRecordCount;
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
    public void SetInitialRecordCount(int count) => Interlocked.Exchange(ref _initialRecordCount, count);
    public void IncrementProcessed() => Interlocked.Increment(ref _totalProcessed);
    public void IncrementSuccess() => Interlocked.Increment(ref _totalSuccess);
    public void IncrementFailed() => Interlocked.Increment(ref _totalFailed);
    public void IncrementSkipped() => Interlocked.Increment(ref _totalSkipped);
    public void IncrementFound() => Interlocked.Increment(ref _totalFound);
    public void IncrementNotFound() => Interlocked.Increment(ref _totalNotFound);
    public void IncrementItemsCollected() => Interlocked.Increment(ref _totalItemsCollected);
    public void AddItemsCollected(int count) => Interlocked.Add(ref _totalItemsCollected, count);
    public void UpdateActiveRequests(int count) => Interlocked.Exchange(ref _activeRequests, count);

    /// <summary>
    /// Записать код ответа HTTP
    /// </summary>
    public void RecordFinalStatusCode(int statusCode)
    {
        _finalStatusCodeCounts.AddOrUpdate(statusCode, 1, (_, count) => count + 1);
    }

    public void RecordAllStatusCodes(int statusCode)
    {
        _allStatusCodeCounts.AddOrUpdate(statusCode, 1, (_, count) => count + 1);
    }

    public IReadOnlyDictionary<int, int> GetFinalStatusCodeStats() => _finalStatusCodeCounts;

    /// <summary>
    /// Получить статистику по кодам ответов
    /// </summary>
    public IReadOnlyDictionary<int, int> GetAllStatusCodeStats() => _allStatusCodeCounts;

    /// <summary>
    /// Получить форматированную строку статистики по кодам ответов
    /// </summary>
    private string GetStatusCodeStatsString(IReadOnlyDictionary<int, int> stats)
    {
        if (stats.Count == 0)
            return "Нет данных";

        var sb = new StringBuilder();
        foreach (var kvp in stats.OrderBy(x => x.Key))
        {
            if (sb.Length > 0)
                sb.Append(", ");
            sb.Append($"{kvp.Key}: {kvp.Value}");
        }
        return sb.ToString();
    }

    public string GetFinalStatusCodeStatsString() => GetStatusCodeStatsString(_finalStatusCodeCounts);

    public string GetAllStatusCodeStatsString() => GetStatusCodeStatsString(_allStatusCodeCounts);

    public override string ToString()
    {
        var sb = new StringBuilder();
        var successRatio = InitialRecordCount > 0 ? (double)TotalSuccess / InitialRecordCount * 100 : 0;

        sb.AppendLine($"[{ScraperName}] Итоговая статистика:");
        sb.AppendLine($"  Выбрано записей из БД: {InitialRecordCount}");
        sb.AppendLine($"  Обработано успешно: {TotalSuccess} ({successRatio:F2}%)");

        if (TotalFailed > 0)
            sb.AppendLine($"  Ошибок: {TotalFailed}");

        if (TotalSkipped > 0)
            sb.AppendLine($"  Пропущено: {TotalSkipped}");

        sb.AppendLine($"  Время выполнения: {ElapsedTime.TotalMinutes:F2} мин");

        if (ActiveRequests > 0)
            sb.AppendLine($"  Активных запросов: {ActiveRequests}");

        return sb.ToString();
    }
    
    /// <summary>
    /// Получить полную статистику включая коды ответов
    /// </summary>
    public string ToDetailedString()
    {
        var sb = new StringBuilder();
        sb.Append(ToString());

        if (!_finalStatusCodeCounts.IsEmpty)
        {
            sb.AppendLine($"[{ScraperName}] Статистика по окончательным кодам ответа: {GetFinalStatusCodeStatsString()}");
        }

        if (!_allStatusCodeCounts.IsEmpty)
        {
            sb.AppendLine($"[{ScraperName}] Статистика по всем кодам ответа: {GetAllStatusCodeStatsString()}");
        }

        return sb.ToString();
    }
    
    /// <summary>
    /// Записать статистику в лог-файл
    /// </summary>
    public void WriteToLogFile(string logDirectory = "logs")
    {
        try
        {
            if (!Directory.Exists(logDirectory))
                Directory.CreateDirectory(logDirectory);

            var fileName = $"{ScraperName}_stats_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            var filePath = Path.Combine(logDirectory, fileName);

            var successRatio = InitialRecordCount > 0 ? (double)TotalSuccess / InitialRecordCount * 100 : 0;

            var sb = new StringBuilder();
            sb.AppendLine($"=== Статистика {ScraperName} ===");
            sb.AppendLine($"Время начала: {StartTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Время окончания: {EndTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Длительность: {ElapsedTime.TotalMinutes:F2} мин");
            sb.AppendLine();
            sb.AppendLine($"Выбрано записей из БД: {InitialRecordCount}");
            sb.AppendLine($"Обработано всего: {TotalProcessed}");
            sb.AppendLine($"Обработано успешно: {TotalSuccess} ({successRatio:F2}%)");
            sb.AppendLine($"Ошибок: {TotalFailed}");
            sb.AppendLine($"Пропущено: {TotalSkipped}");
            sb.AppendLine($"Найдено: {TotalFound}");
            sb.AppendLine($"Не найдено: {TotalNotFound}");
            sb.AppendLine($"Собрано элементов: {TotalItemsCollected}");
            sb.AppendLine();
            sb.AppendLine("=== Статистика по окончательным кодам ответа ===");

            if (_finalStatusCodeCounts.IsEmpty)
            {
                sb.AppendLine("Нет данных");
            }
            else
            {
                foreach (var kvp in _finalStatusCodeCounts.OrderBy(x => x.Key))
                {
                    var description = GetStatusCodeDescription(kvp.Key);
                    sb.AppendLine($"  {kvp.Key} ({description}): {kvp.Value}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== Статистика по всем кодам ответа ===");

            if (_allStatusCodeCounts.IsEmpty)
            {
                sb.AppendLine("Нет данных");
            }
            else
            {
                foreach (var kvp in _allStatusCodeCounts.OrderBy(x => x.Key))
                {
                    var description = GetStatusCodeDescription(kvp.Key);
                    sb.AppendLine($"  {kvp.Key} ({description}): {kvp.Value}");
                }
            }

            File.WriteAllText(filePath, sb.ToString());
        }
        catch
        {
            // Игнорируем ошибки записи статистики
        }
    }

    private static string GetStatusCodeDescription(int statusCode) => statusCode switch
    {
        200 => "OK",
        201 => "Created",
        204 => "No Content",
        301 => "Moved Permanently",
        302 => "Found",
        304 => "Not Modified",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        408 => "Request Timeout",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        504 => "Gateway Timeout",
        _ => "Unknown"
    };
}
