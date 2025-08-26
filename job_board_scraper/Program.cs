using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Npgsql;

namespace job_board_scraper;

/// <summary>
/// Программа перебирает все возможные имена пользователей (a-z, 0-9, -, _) длиной от minLength до maxLength,
/// формирует для каждого имя ссылку http://career.habr.com/USERNAME и выполняет HTTP-запросы параллельно.
/// Одновременно выполняется не более maxConcurrentRequests запросов (SemaphoreSlim).
/// Для отслеживания активных запросов используется ConcurrentDictionary.
/// Если страница не возвращает 404, ссылка и title сохраняются в базу данных PostgreSQL (таблица habr_resumes).
/// В консоль выводится прогресс (количество, процент, время запроса) и результат записи в БД.
/// 
/// План:
/// Используется SemaphoreSlim для ограничения параллелизма.
/// Для отслеживания активных запросов — ConcurrentDictionary.
/// Каждый запрос — отдельная Task, которая добавляет себя в структуру при старте и удаляет при завершении.
/// Основной цикл — запускает задачи, но ждет, если активных задач уже maxConcurrentRequests.
/// 
/// </summary>
class Program
{
    static readonly char[] chars = "abcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();
    static readonly string baseUrl = "http://career.habr.com/";
    static readonly int minLength = 4;
    static readonly int maxLength = 4;
    static readonly int maxConcurrentRequests = 2;
    static readonly int maxRetries = 200;

    static async Task Main(string[] args)
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HabrScraper/1.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
        
        var connectionString = "Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;";
        var db = new DatabaseClient(connectionString);
        using var conn = db.DatabaseConnectionInit();
        db.DatabaseEnsureConnectionOpen(conn);

        var semaphore = new SemaphoreSlim(maxConcurrentRequests);
        var activeRequests = new ConcurrentDictionary<string, Task>();// задачи, выполняющие http запросы
        var responseStats = new ConcurrentDictionary<int, int>(); // статистика кодов ответов
        var saveQueue = new ConcurrentQueue<(string link, string title)>(); // очередь для записи в БД
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        
        // Инициализация вашего существующего saveQueue (у вас он уже есть).
        // Пример адаптации: передаём делегат добавления в вашу очередь.
        var scraper = new ListResumeScraper(
            httpClient,
            enqueueToSaveQueue: item =>
            {
                saveQueue.Enqueue((item.link, item.title));
                Console.WriteLine($"[Main] (debug) Поступило в saveQueue: {item.title} -> {item.link}");
            },
            interval: TimeSpan.FromMinutes(10));

        // Запуск фоновой задачи (работает параллельно)
        _ = scraper.StartAsync(cts.Token);
        
        var dbWriterTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)//  || !saveQueue.IsEmpty
            {
                while (saveQueue.TryDequeue(out var item))
                {
                    db.DatabaseInsert(conn, link: item.link, title: item.title);
                }
                await Task.Delay(500, cts.Token);
            }
        });

        for (int len = minLength; len <= maxLength; len++)
        {
            var usernames = new List<string>(GenerateUsernames(len));
            int totalLinks = usernames.Count;
            int completed = 0;
            var tasks = new List<Task>();

            Console.WriteLine($"Сгенерировано адресов {totalLinks}");

            // Получаем последний обработанный link из БД
            string lastLink = db.DatabaseGetLastLink(conn);
            Console.WriteLine($"Последний обработанный link из БД: {lastLink}");
            
            int startIndex = 0;
            if (!string.IsNullOrEmpty(lastLink))
            {
                int foundIndex = usernames.IndexOf(lastLink.Replace(baseUrl, ""));
                if (foundIndex >= 0 && foundIndex < usernames.Count - 1)
                {
                    startIndex = foundIndex + 1;
                    Console.WriteLine($"Продолжаем перебор с {startIndex}-го элемента: {usernames[startIndex]}");
                }
                else
                {
                    Console.WriteLine($"Последний link из БД не найден в usernames, начинаем с начала.");
                }
            }
            completed = startIndex;

            for (int i = startIndex; i < totalLinks; i++)
            {
                string username = usernames[i];
                string link = baseUrl + username;

                await semaphore.WaitAsync();

                var task = Task.Run(async () =>
                {
                    activeRequests.TryAdd(link, Task.CurrentId.HasValue ? Task.FromResult(Task.CurrentId.Value) : Task.CompletedTask);
                    try
                    {
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        
                        var result = await HttpRetry.FetchAsync(
                            httpClient,
                            link,
                            maxRetries: maxRetries,
                            baseDelay: TimeSpan.FromMilliseconds(400), // стартовая пауза
                            maxDelay: TimeSpan.FromSeconds(30), // верхняя граница
                            infoLog: msg => Console.WriteLine(msg), // куда писать сообщения о повторах
                            responseStats: r => ResponseStats(responseStats, (int)r.StatusCode) // сбор статистики
                        );
                        
                        stopwatch.Stop();
                        double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                        double percent;
                        lock (usernames)
                        {
                            completed++;
                            percent = completed * 100.0 / totalLinks;
                        }
                        Console.WriteLine($"HTTP запрос {link}: {elapsedSeconds:F3} сек. Код ответа {(int)result.StatusCode}. Обработано ссылок: {completed}/{totalLinks} ({percent:F2}%)");
                        
                        if (result.IsNotFound)
                            return;
                        
                        string html = result.Content;
                        
                        var title = ExtractTitle(html);
                        
                        Console.WriteLine($"Страница {link}: {title}");
                            
                        // Кладём в очередь для записи в БД
                        saveQueue.Enqueue((link, title));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error for {link}: {ex.Message}");
                    }
                    finally
                    {
                        activeRequests.TryRemove(link, out _);
                        semaphore.Release();
                        Console.Out.Flush();
                    }
                });
                tasks.Add(task);
            }
            await Task.WhenAll(tasks);
        }
        cts.Cancel(); // Завершаем поток записи в БД
        await dbWriterTask;
        db.DatabaseConnectionClose(conn);
    }

    static IEnumerable<string> GenerateUsernames(int length)
    {
        var arr = new char[length];
        return GenerateUsernamesRecursive(arr, 0);
    }

    static IEnumerable<string> GenerateUsernamesRecursive(char[] arr, int pos)
    {
        if (pos == arr.Length)
        {
            yield return new string(arr);
            yield break;
        }
        foreach (var c in chars)
        {
            arr[pos] = c;
            foreach (var s in GenerateUsernamesRecursive(arr, pos + 1))
                yield return s;
        }
    }

    public static string ExtractTitle(string html)
    {
        var parser = new HtmlParser();
        var doc = parser.ParseDocument(html);
        return doc.QuerySelector("title")?.TextContent?.Trim() ?? string.Empty;
    }

    static void ResponseStats(ConcurrentDictionary<int, int> stats, int code)
    {
        stats.AddOrUpdate(code, 1, (k, v) => v + 1);
        var statsString = string.Join(", ", stats.Select(kv => $"{kv.Key} - {kv.Value} раз"));
        Console.Write($"Статистика кодов ответов: {statsString}\n");
    }
}