using System.Collections.Concurrent;
using System.Text;

namespace JobBoardScraper;

/// <summary>
/// Статистика трафика для конкретного скрапера
/// </summary>
public class ScraperTrafficStats
{
    private long _totalBytes;
    private long _requestCount;
    private readonly object _lock = new object();

    public long TotalBytes => Interlocked.Read(ref _totalBytes);
    public long RequestCount => Interlocked.Read(ref _requestCount);
    public double AverageBytesPerRequest => RequestCount > 0 ? (double)TotalBytes / RequestCount : 0;

    public void AddRequest(long bytes)
    {
        Interlocked.Add(ref _totalBytes, bytes);
        Interlocked.Increment(ref _requestCount);
    }

    public string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public override string ToString()
    {
        return $"Requests: {RequestCount}, Total: {FormatBytes(TotalBytes)}, Avg: {FormatBytes((long)AverageBytesPerRequest)}";
    }
}

/// <summary>
/// Глобальная статистика трафика для всех скраперов
/// </summary>
public sealed class TrafficStatistics : IDisposable
{
    private readonly ConcurrentDictionary<string, ScraperTrafficStats> _scraperStats = new();
    private readonly string _outputFile;
    private readonly Timer _saveTimer;
    private bool _disposed;

    public TrafficStatistics(string outputFile, TimeSpan? saveInterval = null)
    {
        _outputFile = outputFile ?? throw new ArgumentNullException(nameof(outputFile));
        
        var interval = saveInterval ?? TimeSpan.FromMinutes(5);
        _saveTimer = new Timer(_ => SaveToFile(), null, interval, interval);
    }

    /// <summary>
    /// Зарегистрировать HTTP-запрос для конкретного скрапера
    /// </summary>
    public void RecordRequest(string scraperName, long bytes)
    {
        if (string.IsNullOrWhiteSpace(scraperName))
            throw new ArgumentException("Scraper name cannot be empty", nameof(scraperName));

        var stats = _scraperStats.GetOrAdd(scraperName, _ => new ScraperTrafficStats());
        stats.AddRequest(bytes);
    }

    /// <summary>
    /// Получить статистику для конкретного скрапера
    /// </summary>
    public ScraperTrafficStats? GetStats(string scraperName)
    {
        return _scraperStats.TryGetValue(scraperName, out var stats) ? stats : null;
    }

    /// <summary>
    /// Получить общую статистику по всем скраперам
    /// </summary>
    public (long TotalBytes, long TotalRequests) GetTotalStats()
    {
        long totalBytes = 0;
        long totalRequests = 0;

        foreach (var stats in _scraperStats.Values)
        {
            totalBytes += stats.TotalBytes;
            totalRequests += stats.RequestCount;
        }

        return (totalBytes, totalRequests);
    }

    /// <summary>
    /// Сохранить статистику в файл
    /// </summary>
    public void SaveToFile()
    {
        if (_disposed) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine($"Traffic Statistics Report - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine();

            var (totalBytes, totalRequests) = GetTotalStats();
            var helper = new ScraperTrafficStats();
            
            sb.AppendLine("OVERALL STATISTICS:");
            sb.AppendLine($"  Total Requests: {totalRequests:N0}");
            sb.AppendLine($"  Total Traffic:  {helper.FormatBytes(totalBytes)}");
            if (totalRequests > 0)
            {
                sb.AppendLine($"  Average/Request: {helper.FormatBytes(totalBytes / totalRequests)}");
            }
            sb.AppendLine();

            sb.AppendLine("PER-SCRAPER STATISTICS:");
            sb.AppendLine("-".PadRight(80, '-'));

            foreach (var kvp in _scraperStats.OrderBy(x => x.Key))
            {
                var scraperName = kvp.Key;
                var stats = kvp.Value;

                sb.AppendLine($"  {scraperName}:");
                sb.AppendLine($"    Requests:     {stats.RequestCount:N0}");
                sb.AppendLine($"    Total:        {stats.FormatBytes(stats.TotalBytes)}");
                sb.AppendLine($"    Avg/Request:  {stats.FormatBytes((long)stats.AverageBytesPerRequest)}");
                sb.AppendLine();
            }

            sb.AppendLine("=".PadRight(80, '='));

            // Создаём директорию, если не существует
            var directory = Path.GetDirectoryName(_outputFile);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_outputFile, sb.ToString());
            Console.WriteLine($"[TrafficStats] Статистика сохранена в {_outputFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TrafficStats] Ошибка при сохранении статистики: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _saveTimer?.Dispose();
        SaveToFile(); // Финальное сохранение
        _disposed = true;
    }
}
