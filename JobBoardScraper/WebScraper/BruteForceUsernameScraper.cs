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
    private readonly ConcurrentDictionary<string, Task> _activeRequests = new();
    private readonly ConcurrentDictionary<int, int> _responseStats = new();

    public BruteForceUsernameScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        AdaptiveConcurrencyController controller)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var conn = _db.DatabaseConnectionInit();
        _db.DatabaseEnsureConnectionOpen(conn);

        for (int len = AppConfig.MinLength; len <= AppConfig.MaxLength; len++)
        {
            var usernames = new List<string>(GenerateUsernames(len));
            int totalLinks = usernames.Count;

            Console.WriteLine($"[BruteForceScraper] Сгенерировано адресов: {totalLinks}");

            int totalLength = (AppConfig.BaseUrl?.Length ?? 0) + AppConfig.MaxLength;
            string lastLink = _db.DatabaseGetLastLink(conn, totalLength);
            Console.WriteLine($"[BruteForceScraper] Последний обработанный link из БД: {lastLink}");

            int startIndex = 0;
            if (!string.IsNullOrEmpty(lastLink))
            {
                int foundIndex = usernames.IndexOf(lastLink.Replace(AppConfig.BaseUrl, ""));
                if (foundIndex >= 0 && foundIndex < usernames.Count - 1)
                {
                    startIndex = foundIndex + 1;
                    Console.WriteLine($"[BruteForceScraper] Продолжаем перебор с {startIndex}-го элемента: {usernames[startIndex]}");
                }
                else
                {
                    Console.WriteLine($"[BruteForceScraper] Последний link из БД не найден, начинаем с начала.");
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
                            infoLog: msg => Console.WriteLine(msg),
                            responseStats: r => RecordResponseStats((int)r.StatusCode)
                        );

                        sw.Stop();
                        _controller.ReportLatency(sw.Elapsed);

                        double elapsedSeconds = sw.Elapsed.TotalSeconds;
                        double percent;
                        lock (usernames)
                        {
                            completed++;
                            percent = completed * 100.0 / totalLinks;
                        }
                        //TODO вот такой вывод распространить на все скраперы, которые работают по параллельному алгоритму
                        Console.WriteLine($"[BruteForceScraper] HTTP запрос {link}: {elapsedSeconds:F3} сек. Код ответа {(int)result.StatusCode}. Обработано: {completed}/{totalLinks} ({percent:F2}%). Параллельных процессов: {_activeRequests.Count}.");

                        if (result.IsNotFound)
                            return;

                        string html = result.Content;
                        var title = HtmlParser.ExtractTitle(html);

                        Console.WriteLine($"[BruteForceScraper] Страница {link}: {title}");

                        _db.EnqueueResume(link, title);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BruteForceScraper] Error for {link}: {ex.Message}");
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
