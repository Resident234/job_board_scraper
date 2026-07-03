using System.Collections.Concurrent;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Infrastructure.Url;
using JobBoardScraper.Core;
using JobBoardScraper.Data;
using JobBoardScraper.Parsing;

namespace JobBoardScraper.Scrapers;

/// <summary>
/// Перебирает все возможные имена пользователей (a-z, 0-9, -, _) длиной от minLength до maxLength,
/// формирует для каждого ссылку http://career.habr.com/USERNAME и выполняет HTTP-запросы параллельно.
/// Если страница не возвращает 404, ссылка и title сохраняются в базу данных.
/// </summary>
public sealed class BruteForceUsernameScraper
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly Func<int, string?> _getLastResumeLink;
    private readonly AdaptiveConcurrencyController _adaptiveConcurrencyController;
    private readonly ConsoleLogger _logger;
    private readonly ConcurrentDictionary<string, Task> _activeRequests = new();
    private readonly ScraperStatistics _statistics;

    public BruteForceUsernameScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        Func<int, string?> getLastResumeLink,
        AdaptiveConcurrencyController controller)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _getLastResumeLink = getLastResumeLink ?? throw new ArgumentNullException(nameof(getLastResumeLink));
        _adaptiveConcurrencyController = controller ?? throw new ArgumentNullException(nameof(controller));
        _statistics = new ScraperStatistics("BruteForceUsernameScraper");
        
        _logger = new ConsoleLogger("BruteForceUsernameScraper");
        _logger.SetOutputMode(OutputMode.ConsoleOnly);
        _logger.WriteLine("Инициализация BruteForceUsernameScraper");
    }

    public async Task RunAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало перебора имён пользователей (brute force)...");
        _statistics.StartTime = DateTime.Now;

        for (int len = AppConfig.MinLength; len <= AppConfig.MaxLength; len++)
        {
            var usernames = new List<string>(GenerateUsernames(len));
            int totalLinks = usernames.Count;

            _logger.WriteLine($"Сгенерировано адресов: {totalLinks}");

            int totalLength = (AppConfig.BaseUrl?.Length ?? 0) + AppConfig.MaxLength;
            var lastLink = _getLastResumeLink(totalLength);
            _logger.WriteLine($"Последний обработанный link из БД: {lastLink}");

            int startIndex = 0;
            if (!string.IsNullOrEmpty(lastLink))
            {
                int foundIndex = usernames.IndexOf(UrlManager.StripBase(lastLink));
                if (foundIndex >= 0 && foundIndex < usernames.Count - 1)
                {
                    startIndex = foundIndex + 1;
                    _logger.WriteLine($"Продолжаем перебор с {startIndex}-го элемента: {usernames[startIndex]}");
                }
                else
                {
                    _logger.WriteLine("Последний link из БД не найден, начинаем с начала.");
                }
            }

            usernames = usernames.Skip(startIndex).ToList();

            await AdaptiveForEach.ForEachAdaptiveAsync(
                source: usernames,
                body: async username =>
                {
                    string link = UrlManager.Combine(AppConfig.BaseUrl, username);

                    _activeRequests.TryAdd(link, Task.CurrentId.HasValue ? Task.FromResult(Task.CurrentId.Value) : Task.CompletedTask);
                    try
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();

                        var result = await _httpClient.FetchAsync(
                            link,
                            infoLog: msg => _logger.WriteLine(msg),
                            responseStats: r => _statistics.RecordAllStatusCodes((int)r.StatusCode)
                        );

                        sw.Stop();
                        _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);

                        double elapsedSeconds = sw.Elapsed.TotalSeconds;

                        // Обновляем статистику
                        _statistics.IncrementProcessed();
                        _statistics.UpdateActiveRequests(_activeRequests.Count);
                        _statistics.AverageRequestTime = elapsedSeconds;

                        if (result.IsNotFound)
                        {
                            _statistics.IncrementSkipped();
                        }
                        else if (result.IsSuccess)
                        {
                            _statistics.IncrementSuccess();
                        }
                        else
                        {
                            _statistics.IncrementFailed();
                        }

                        ScraperParallelLogger.LogProgress(
                            _logger,
                            _statistics,
                            link,
                            elapsedSeconds,
                            (int)result.StatusCode,
                            totalLinks);

                        if (result.IsNotFound)
                            return;

                        string html = result.Content;
                        var title = HtmlParser.ExtractTitle(html);

                        _logger.WriteLine($"Страница {link}: {title}");

                        _db.EnqueueResume(link, title);
                        ScraperLogger.LogEnqueue(_logger, link, link);
                    }
                    catch (Exception ex)
                    {
                        _statistics.IncrementFailed();
                        ScraperLogger.LogError(_logger, $"Error for {link}", ex);
                    }
                    finally
                    {
                        _activeRequests.TryRemove(link, out _);
                        Console.Out.Flush();
                    }
                },
                controller: _adaptiveConcurrencyController,
                ct: ct
            );
        }

        _statistics.EndTime = DateTime.Now;
        ScraperLogger.LogEnd(_logger, _statistics);

    }

    private static IEnumerable<string> GenerateUsernames(int length)
    {
        var arr = new char[length];
        return GenerateUsernamesRecursive(arr, 0);
    }

    private static IEnumerable<string> GenerateUsernamesRecursive(char[] arr, int pos)
    {
        if (pos == arr.Length)
        {
            yield return new string(arr);
            yield break;
        }
        foreach (var c in AppConfig.Chars)
        {
            arr[pos] = c;
            foreach (var s in GenerateUsernamesRecursive(arr, pos + 1))
                yield return s;
        }
    }

}
