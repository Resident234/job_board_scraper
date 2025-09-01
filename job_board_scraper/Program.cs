using System.Collections.Concurrent;
using AngleSharp.Html.Parser;


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
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri(AppConfig.BaseUrl)
    };

    static async Task Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(AppConfig.BaseUrl)
        };
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HabrScraper/1.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
        
        var db = new DatabaseClient(AppConfig.ConnectionString);
        using var conn = db.DatabaseConnectionInit();
        db.DatabaseEnsureConnectionOpen(conn);

        // Инициализация адаптивного контроллера вместо фиксированного SemaphoreSlim
        var controller = new AdaptiveConcurrencyController(
            defaultConcurrency: AppConfig.MaxConcurrentRequests, // раньше было фиксировано через SemaphoreSlim
            minConcurrency: 1,
            maxConcurrency: 128,
            fastThreshold: TimeSpan.FromMilliseconds(250),
            slowThreshold: TimeSpan.FromSeconds(1),
            evaluationPeriod: TimeSpan.FromSeconds(2),
            emaAlpha: 0.2,
            increaseStep: 1,
            decreaseFactor: 0.75
        );
        
        var activeRequests = new ConcurrentDictionary<string, Task>();// задачи, выполняющие http запросы
        var responseStats = new ConcurrentDictionary<int, int>(); // статистика кодов ответов
        
        // Запускаем цикл оценки (регулирует DesiredConcurrency на основе ReportLatency)
        var controllerLoop = controller.RunAsync(cts.Token);

        // Запускаем задачу фоновой записи в БД и получаем созданную очередь
        db.StartWriterTask(conn, cts.Token, delayMs: 500);

        // Инициализация скрапера с передачей делегата для добавления в очередь
        var scraper = new ListResumeScraper(
            httpClient,
            enqueueToSaveQueue: item =>
            {
                db.EnqueueItem(item.link, item.title);
            },
            interval: TimeSpan.FromMinutes(10));

        // Запуск фоновой задачи (работает параллельно)
        _ = scraper.StartAsync(cts.Token);

        for (int len = AppConfig.MinLength; len <= AppConfig.MaxLength; len++)
        {
            var usernames = new List<string>(GenerateUsernames(len));
            int totalLinks = usernames.Count;

            Console.WriteLine($"Сгенерировано адресов {totalLinks}");

            // Получаем последний обработанный link из БД
            int totalLength = (AppConfig.BaseUrl?.Length ?? 0) + AppConfig.MaxLength;
            string lastLink = db.DatabaseGetLastLink(conn, totalLength);
            Console.WriteLine($"Последний обработанный link из БД: {lastLink}");
            
            int startIndex = 0;
            if (!string.IsNullOrEmpty(lastLink))
            {
                int foundIndex = usernames.IndexOf(lastLink.Replace(AppConfig.BaseUrl, ""));
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
            var completed = startIndex;

            //от usernames отрезать всю предыдущую часть до startIndex
            usernames = usernames.Skip(startIndex).ToList();
                
            await AdaptiveForEach.ForEachAdaptiveAsync(
                source: usernames,
                body: async username =>
                {
                    string link = AppConfig.BaseUrl + username;
    
                    activeRequests.TryAdd(link, Task.CurrentId.HasValue ? Task.FromResult(Task.CurrentId.Value) : Task.CompletedTask);
                    try
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        
                        var result = await HttpRetry.FetchAsync(
                            httpClient,
                            link,
                            maxRetries: AppConfig.MaxRetries,
                            baseDelay: TimeSpan.FromMilliseconds(400), // стартовая пауза
                            maxDelay: TimeSpan.FromSeconds(30), // верхняя граница
                            infoLog: msg => Console.WriteLine(msg), // куда писать сообщения о повторах
                            responseStats: r => ResponseStats(responseStats, (int)r.StatusCode) // сбор статистики
                        );
                        
                        sw.Stop();
                        controller.ReportLatency(sw.Elapsed); // важный сигнал для адаптации конкуренции
                        
                        double elapsedSeconds = sw.Elapsed.TotalSeconds;
                        double percent;
                        lock (usernames)
                        {
                            completed++;
                            percent = completed * 100.0 / totalLinks;
                        }
                        Console.WriteLine($"HTTP запрос {link}: {elapsedSeconds:F3} сек. Код ответа {(int)result.StatusCode}. Обработано ссылок: {completed}/{totalLinks} ({percent:F2}%). Параллельных процессов: {activeRequests.Count}.");
                        
                        if (result.IsNotFound)
                            return;
                        
                        string html = result.Content;
                        
                        var title = ExtractTitle(html);
                        
                        Console.WriteLine($"Страница {link}: {title}");
                            
                        // Кладём в очередь для записи в БД
                        db.EnqueueItem(link, title);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error for {link}: {ex.Message}");
                    }
                    finally
                    {
                        activeRequests.TryRemove(link, out _);
                        Console.Out.Flush();
                    }
                },
                controller: controller,
                ct: cts.Token
            );
        }
        
        // Корректно завершаем фоновую задачу контроллера
        try
        {
            await controllerLoop;
        }
        catch (OperationCanceledException)
        {
            // игнорируем отмену при выходе
        }

        cts.Cancel(); // Сигнал всем задачам на завершение

        // Корректно останавливаем задачу записи в БД
        await db.StopWriterTask();

        // Закрываем соединение с БД
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
        foreach (var c in AppConfig.Chars)
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