using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Npgsql;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;

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
    static readonly int maxConcurrentRequests = 20;
    static readonly int maxRetries = 3; // Количество попыток запроса
    static readonly int retryDelay = 1000; // Задержка между попытками

    static async Task Main(string[] args)
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        await using var conn = DatabaseConnectionInit();

        var semaphore = new SemaphoreSlim(maxConcurrentRequests);
        var activeRequests = new ConcurrentDictionary<string, Task>();// задачи, выполняющие http запросы
        var responseStats = new ConcurrentDictionary<int, int>(); // статистика кодов ответов
        var saveQueue = new ConcurrentQueue<(string link, string title)>(); // очередь для записи в БД
        var cts = new CancellationTokenSource();
        
        var dbWriterTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)//  || !saveQueue.IsEmpty
            {
                while (saveQueue.TryDequeue(out var item))
                {
                    try
                    {
                        DatabaseInsert(conn, item.link, item.title);
                    }
                    catch (NpgsqlException dbEx)
                    {
                        Console.WriteLine($"Ошибка БД для {item.link}: {dbEx.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Неожиданная ошибка при записи в БД для {item.link}: {ex.Message}");
                    }
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
            
            for (int i = 0; i < totalLinks; i++)
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
                        HttpResponseMessage response = null;
                        string html = null;
                        int attempt = 0;
                        while (attempt < maxRetries)
                        {
                            response = await client.GetAsync(link);
                            html = await response.Content.ReadAsStringAsync();
                            ResponseStats(responseStats, (int)response.StatusCode);
                            if ((int)response.StatusCode != 503)
                                break;
                            attempt++;
                            if (attempt < maxRetries)
                                await Task.Delay(retryDelay);
                        }
                        stopwatch.Stop();
                        double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                        double percent;
                        lock (usernames)
                        {
                            completed++;
                            percent = completed * 100.0 / totalLinks;
                        }
                        Console.WriteLine($"HTTP запрос {link}: {elapsedSeconds:F3} сек. Код ответа {(int)response.StatusCode}. Обработано ссылок: {completed}/{totalLinks} ({percent:F2}%)");
                        
                        if ((int)response.StatusCode == 404)
                            return;
                        
                        var title = ExtractTitle(html);
                        
                        // Кладём в очередь для записи в БД
                        saveQueue.Enqueue((link, title));
                    }
                    catch (Exception ex)
                    {
                        // Console.WriteLine($"Error for {link}: {ex.Message}");
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
        DatabaseConnectionClose(conn);
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

    static string ExtractTitle(string html)
    {
        var match = Regex.Match(html, "<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : "(no title)";
    }

    static void ResponseStats(ConcurrentDictionary<int, int> stats, int code)
    {
        stats.AddOrUpdate(code, 1, (k, v) => v + 1);
        var statsString = string.Join(", ", stats.Select(kv => $"{kv.Key} - {kv.Value} раз"));
        Console.Write($"Статистика кодов ответов: {statsString}\n");
    }

    static void DatabaseInsert(NpgsqlConnection conn, string link, string title)
    {
        DatabaseEnsureConnectionOpen(conn);
        using var insertCommand = new NpgsqlCommand("INSERT INTO habr_resumes (link, title) VALUES (@link, @title)", conn);
        insertCommand.Parameters.AddWithValue("@link", link);
        insertCommand.Parameters.AddWithValue("@title", title);
        int rowsAffected = insertCommand.ExecuteNonQuery();
        Console.WriteLine($"Записано в БД: {rowsAffected} строка, {link} | {title}");
    }

    static void DatabaseEnsureConnectionOpen(NpgsqlConnection conn)
    {
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();
    }

    static void DatabaseConnectionClose(NpgsqlConnection conn)
    {
        conn.Close();
    }
    
    static NpgsqlConnection DatabaseConnectionInit()
    {
        NpgsqlConnection conn = new NpgsqlConnection("Server=localhost:5432;User Id=postgres; Password=admin;Database=jobs;");
        conn.Open();
        
        return conn;
    }
}
