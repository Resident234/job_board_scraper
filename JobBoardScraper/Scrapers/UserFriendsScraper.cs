using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Utils;
using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Infrastructure.Url;
using JobBoardScraper.Core;
using JobBoardScraper.Data;
using System.Collections.Concurrent;
using JobBoardScraper.Parsing;

namespace JobBoardScraper.Scrapers;

/// <summary>
/// Обходит страницы /friends пользователей и собирает ссылки на их друзей
/// </summary>
public sealed class UserFriendsScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly Func<List<string>> _getUserCodes;
    private readonly AdaptiveConcurrencyController _adaptiveConcurrencyController;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly ConcurrentDictionary<string, Task> _activeRequests = new();
    private readonly ScraperStatistics _statistics;
    private ProgressTracker? _progress;

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
        _adaptiveConcurrencyController = controller ?? throw new ArgumentNullException(nameof(controller));
        _interval = interval ?? TimeSpan.FromDays(30);
        _statistics = new ScraperStatistics("UserFriendsScraper");
        
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
            ScraperLogger.LogError(_logger, ex);
        }
    }

    private async Task ScrapeAllUserFriendsAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало обхода списков друзей пользователей...");
        
        var userLinks = _getUserCodes();
        var totalLinks = userLinks.Count;
        
        // Используем ProgressTracker для отслеживания прогресса
        _progress = new ProgressTracker(totalLinks, "UserFriends");
        
        ScraperLogger.LogCount(_logger, "Загружено", totalLinks, "пользователей", " из БД");

        if (totalLinks == 0)
        {
            ScraperLogger.LogSkip(_logger, "Нет пользователей для обработки.");
            return;
        }

        await AdaptiveForEach.ForEachAdaptiveAsync(
            source: userLinks,
            body: async userLink =>
        {
            _activeRequests.TryAdd(userLink, Task.CurrentId.HasValue ? Task.FromResult(Task.CurrentId.Value) : Task.CompletedTask);
            try
            {
                int page = 1;
                int totalFriendsForUser = 0;
                bool hasMorePages = true;

                // Перебираем страницы, пока находим друзей
                while (hasMorePages && !ct.IsCancellationRequested)
                {
                    var friendsUrl = UrlManager.BuildFriendsUrl(userLink, page);

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(friendsUrl, ct);
                    sw.Stop();
                    _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);
                    
                    double elapsedSeconds = sw.Elapsed.TotalSeconds;
                    
                    if (page == 1)
                    {
                        _statistics.IncrementProcessed();
                        _statistics.UpdateActiveRequests(_activeRequests.Count);
                        _progress?.Increment();
                        
                        if (_progress != null)
                        {
                            ScraperParallelLogger.LogProgress(
                                _logger,
                                _statistics,
                                friendsUrl,
                                elapsedSeconds,
                                (int)response.StatusCode,
                                _progress);
                        }
                    }
                    else
                    {
                        ScraperParallelLogger.LogPage(
                            _logger,
                            _statistics.ScraperName,
                            friendsUrl,
                            page,
                            elapsedSeconds,
                            (int)response.StatusCode);
                    }
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        break;
                    }

                    var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct);
                    var html = response.DecodeBodyAsString(htmlBytes);
                    
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                    // Ищем друзей по селектору
                    var friends = UserDataExtractor.ExtractFriends(doc, AppConfig.UserFriendsFriendLinkSelector);
                    var friendsOnPage = 0;
                    
                    foreach (var (href, userCode) in friends)
                    {
                        if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(userCode))
                            continue;

                        // Формируем полную ссылку
                        var fullLink = UrlManager.Combine(AppConfig.UserFriendsBaseUrl, href);

                        // Сохраняем в БД: если запись существует по link, обновляем code
                        _db.EnqueueResume(fullLink, title: "", mode: InsertMode.UpdateIfExists, code: userCode);
                        ScraperLogger.LogEnqueue(_logger, userCode, fullLink);
                        friendsOnPage++;
                    }

                    totalFriendsForUser += friendsOnPage;
                    
                    if (friendsOnPage == 0)
                    {
                        hasMorePages = false;
                        ScraperLogger.LogEnd(_logger, $"Страница {page} пуста, завершаем обход для {userLink}");
                    }
                    else
                    {
                        ScraperLogger.LogPage(_logger, page, $"{friendsUrl} | найдено {friendsOnPage} друзей для {userLink}");
                        page++;
                    }
                }

                ScraperLogger.LogCount(_logger, "Найдено", totalFriendsForUser, $"друзей для {userLink}", $" ({page - 1} страниц)");
                _statistics.AddItemsCollected(totalFriendsForUser);
            }
            catch (Exception ex)
            {
                ScraperLogger.LogError(_logger, $"Ошибка при обработке {userLink}", ex);
            }
            finally
            {
                _activeRequests.TryRemove(userLink, out _);
            }
        },
        controller: _adaptiveConcurrencyController,
        ct: ct
        );
        
        _statistics.EndTime = DateTime.Now;
        ScraperLogger.LogEnd(_logger, _statistics);
    }
}
