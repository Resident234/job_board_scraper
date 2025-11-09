using JobBoardScraper.Helper.ConsoleHelper;
using System.Collections.Concurrent;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Обходит страницы /friends пользователей и собирает ссылки на их друзей
/// </summary>
public sealed class UserFriendsScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly Func<List<string>> _getUserCodes;
    private readonly AdaptiveConcurrencyController _controller;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly ConcurrentDictionary<string, Task> _activeRequests = new();

    public UserFriendsScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        Func<List<string>> getUserCodes,
        AdaptiveConcurrencyController controller,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _getUserCodes = getUserCodes ?? throw new ArgumentNullException(nameof(getUserCodes));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _interval = interval ?? TimeSpan.FromDays(30);
        
        _logger = new ConsoleLogger("UserFriendsScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация UserFriendsScraper с режимом вывода: {outputMode}");
    }

    public void Dispose()
    {
        _logger?.Dispose();
    }

    public Task StartAsync(CancellationToken ct)
    {
        return Task.Run(() => LoopAsync(ct), ct);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        await RunOnceSafe(ct);

        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await RunOnceSafe(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Остановка — ок
        }
    }

    private async Task RunOnceSafe(CancellationToken ct)
    {
        try
        {
            await ScrapeAllUserFriendsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Остановка — ок
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    private async Task ScrapeAllUserFriendsAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода списков друзей пользователей...");
        
        var userLinks = _getUserCodes();
        var totalLinks = userLinks.Count;
        _logger.WriteLine($"Загружено {totalLinks} пользователей из БД.");

        if (totalLinks == 0)
        {
            _logger.WriteLine("Нет пользователей для обработки.");
            return;
        }

        var totalProcessed = 0;
        var totalFriendsFound = 0;

        await AdaptiveForEach.ForEachAdaptiveAsync(
            source: userLinks,
            body: async userLink =>
        {
            _activeRequests.TryAdd(userLink, Task.CurrentId.HasValue ? Task.FromResult(Task.CurrentId.Value) : Task.CompletedTask);
            try
            {
                var friendsUrl = userLink.TrimEnd('/') + "/friends";

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(friendsUrl, ct);
                sw.Stop();
                _controller.ReportLatency(sw.Elapsed);
                
                double elapsedSeconds = sw.Elapsed.TotalSeconds;
                int completed = Interlocked.Increment(ref totalProcessed);
                double percent = completed * 100.0 / totalLinks;
                _logger.WriteLine($"HTTP запрос {friendsUrl}: {elapsedSeconds:F3} сек. Код ответа {(int)response.StatusCode}. Обработано: {completed}/{totalLinks} ({percent:F2}%). Параллельных процессов: {_activeRequests.Count}.");
                
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct);
                var encoding = response.Content.Headers.ContentType?.CharSet != null
                    ? System.Text.Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet)
                    : System.Text.Encoding.UTF8;
                var html = encoding.GetString(htmlBytes);
                
                var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                // Ищем друзей по селектору
                var friendLinks = doc.QuerySelectorAll(AppConfig.UserFriendsFriendLinkSelector);
                var friendsCount = 0;
                
                foreach (var friendLink in friendLinks)
                {
                    var href = friendLink.GetAttribute("href");
                    if (string.IsNullOrWhiteSpace(href))
                        continue;

                    // Формируем полную ссылку
                    var fullLink = AppConfig.UserFriendsBaseUrl + href;
                    
                    // Извлекаем код пользователя из href
                    var userCode = href.TrimStart('/');
                    
                    // Сохраняем в БД: если запись существует по link, обновляем code
                    _db.EnqueueResume(fullLink, title: "", mode: InsertMode.UpdateIfExists, code: userCode);
                    friendsCount++;
                }

                _logger.WriteLine($"Найдено {friendsCount} друзей для {userLink}");
                Interlocked.Add(ref totalFriendsFound, friendsCount);
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Ошибка при обработке {userLink}: {ex.Message}");
            }
            finally
            {
                _activeRequests.TryRemove(userLink, out _);
            }
        },
        controller: _controller,
        ct: ct
        );
        
        _logger.WriteLine($"Обход завершён. Обработано: {totalProcessed}, найдено друзей: {totalFriendsFound}");
    }
}
