using System.Collections.Concurrent;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Перебирает все возможные имена пользователей (a-z, 0-9, -, _) длиной от minLength до maxLength,
/// формирует для каждого ссылку http://career.habr.com/USERNAME и выполняет HTTP-запросы параллельно.
/// Если страница не возвращает 404, ссылка и title сохраняются в базу данных.
/// </summary>
public sealed class BruteForceUsernameScraper
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly AdaptiveConcurrencyController _controller;
    private readonly Helper.ConsoleHelper.ConsoleLogger _logger;
    private readonly ConcurrentDictionary<string, Task> _activeRequests = new();
    private readonly ConcurrentDictionary<int, int> _responseStats = new();
    private readonly Models.ScraperStatistics _statistics;

    public BruteForceUsernameScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        AdaptiveConcurrencyController controller,
        Helper.ConsoleHelper.ConsoleLogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _statistics = new Models.ScraperStatistics("BruteForceUsernameScraper");
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var conn = _db.DatabaseConnectionInit();
        _db.DatabaseEnsureConnectionOpen(conn);

        for (int len = AppConfig.MinLength; len <= AppConfig.MaxLength; len++)
        {
            var usernames = new List<string>(GenerateUsernames(len));
            int totalLinks = usernames.Count;

            _logger.WriteLine($"[BruteForceScraper] Сгенерировано адресов: {totalLinks}");

            int totalLength = (AppConfig.BaseUrl?.Length ?? 0) + AppConfig.MaxLength;
            string lastLink = _db.DatabaseGetLastLink(conn, totalLength);
            _logger.WriteLine($"[BruteForceScraper] Последний обработанный link из БД: {lastLink}");

            int startIndex = 0;
            if (!string.IsNullOrEmpty(lastLink))
            {
                int foundIndex = usernames.IndexOf(lastLink.Replace(AppConfig.BaseUrl, ""));
                if (foundIndex >= 0 && foundIndex < usernames.Count - 1)
                {
                    startIndex = foundIndex + 1;
                    _logger.WriteLine($"[BruteForceScraper] Продолжаем перебор с {startIndex}-го элемента: {usernames[startIndex]}");
                }
                else
                {
                    _logger.WriteLine($"[BruteForceScraper] Последний link из БД не найден, начинаем с начала.");
                }
            }

            var completed = startIndex;
            usernames = usernames.Skip(startIndex).ToList();

            await AdaptiveForEach.ForEachAdaptiveAsync(
                source: usernames,
                body: async username =>
                {
                    string link = AppConfig.BaseUrl + username;

                    _activeRequests.TryAdd(link, Task.CurrentId.HasValue ? Task.FromResult(Task.CurrentId.Value) : Task.CompletedTask);
                    try
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();

                        var result = await _httpClient.FetchAsync(
                            link,
                            infoLog: msg => _logger.WriteLine(msg),
                            responseStats: r => RecordResponseStats((int)r.StatusCode)
                        );

                        sw.Stop();
                        _controller.ReportLatency(sw.Elapsed);

                        double elapsedSeconds = sw.Elapsed.TotalSeconds;
                        int completedCount;
                        lock (usernames)
                        {
                            completed++;
                            completedCount = completed;
                        }
                        
                        // Обновляем статистику
                        _statistics.TotalProcessed = completedCount;
                        _statistics.ActiveRequests = _activeRequests.Count;
                        _statistics.AverageRequestTime = elapsedSeconds;
                        
                        if (result.IsNotFound)
                        {
                            _statistics.TotalSkipped++;
                        }
                        else if (result.IsSuccess)
                        {
                            _statistics.TotalSuccess++;
                        }
                        else
                        {
                            _statistics.TotalFailed++;
                        }
                        
                        Helper.Utils.ParallelScraperLogger.LogProgress(
                            _logger,
                            "BruteForceScraper",
                            link,
                            elapsedSeconds,
                            (int)result.StatusCode,
                            completedCount,
                            totalLinks,
                            _activeRequests.Count);

                        if (result.IsNotFound)
                            return;

                        string html = result.Content;
                        var title = HtmlParser.ExtractTitle(html);

                        _logger.WriteLine($"[BruteForceScraper] Страница {link}: {title}");

                        _db.EnqueueResume(link, title);
                    }
                    catch (Exception ex)
                    {
                        _statistics.TotalFailed++;
                        _logger.WriteLine($"[BruteForceScraper] Error for {link}: {ex.Message}");
                    }
                    finally
                    {
                        _activeRequests.TryRemove(link, out _);
                        Console.Out.Flush();
                    }
                },
                controller: _controller,
                ct: ct
            );
        }

        _db.DatabaseConnectionClose(conn);
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

    private void RecordResponseStats(int code)
    {
        _responseStats.AddOrUpdate(code, 1, (k, v) => v + 1);
        var statsString = string.Join(", ", _responseStats.Select(kv => $"{kv.Key} - {kv.Value} раз"));
        Console.Write($"[BruteForceScraper] Статистика кодов ответов: {statsString}\n");
    }
}
