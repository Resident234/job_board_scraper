using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Utils;
using JobBoardScraper.Infrastructure.Url;
using JobBoardScraper.Core;
using JobBoardScraper.Data;
using JobBoardScraper.Parsing;

namespace JobBoardScraper.Scrapers;

/// <summary>
/// Обходит страницы подписчиков компаний и извлекает профили пользователей
/// Использует AdaptiveConcurrencyController для параллельной обработки компаний
/// </summary>
public sealed class CompanyFollowersScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly Action<string, string, string?, InsertMode> _enqueueUser;
    private readonly Func<List<string>> _getCompanyCodes;
    private readonly AdaptiveConcurrencyController _adaptiveConcurrencyController;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task> _activeRequests = new();

    public CompanyFollowersScraper(
        SmartHttpClient httpClient,
        Action<string, string, string?, InsertMode> enqueueUser,
        Func<List<string>> getCompanyCodes,
        AdaptiveConcurrencyController controller,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _enqueueUser = enqueueUser ?? throw new ArgumentNullException(nameof(enqueueUser));
        _getCompanyCodes = getCompanyCodes ?? throw new ArgumentNullException(nameof(getCompanyCodes));
        _adaptiveConcurrencyController = controller ?? throw new ArgumentNullException(nameof(controller));
        _interval = interval ?? TimeSpan.FromDays(7);
        
        _logger = new ConsoleLogger("CompanyFollowersScraper");
        _logger.SetOutputMode(outputMode);
        ScraperLogger.LogInitialization(_logger, "CompanyFollowersScraper", outputMode);
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
            await ScrapeAllCompaniesAsync(ct);
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

    private async Task ScrapeAllCompaniesAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало обхода подписчиков компаний...");
        
        var companyCodes = _getCompanyCodes();
        ScraperLogger.LogCount(_logger, "Загружено", companyCodes.Count, "компаний", " для обхода");
        
        var totalUsersFound = 0;
        var completed = 0;
        var lockObj = new object();
        
        // Используем AdaptiveForEach для параллельной обработки компаний
        await AdaptiveForEach.ForEachAdaptiveAsync(
            source: companyCodes,
            body: async companyCode =>
            {
                _activeRequests.TryAdd(companyCode, Task.CurrentId.HasValue ? Task.FromResult(Task.CurrentId.Value) : Task.CompletedTask);
                
                var sw = System.Diagnostics.Stopwatch.StartNew();
                
                try
                {
                    _logger.WriteLine($"Обработка компании: {companyCode}");
                    var (usersFound, statusCode) = await ScrapeCompanyFollowersAsync(companyCode, ct);
                    
                    sw.Stop();
                    _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);
                    
                    lock (lockObj)
                    {
                        totalUsersFound += usersFound;
                        completed++;
                        var percent = completed * 100.0 / companyCodes.Count;
                        _logger.WriteLine($"Компания {companyCode}: найдено {usersFound} пользователей. " +
                            $"Код ответа: {statusCode}. " +
                            $"Обработано: {completed}/{companyCodes.Count} ({percent:F2}%). " +
                            $"Время: {sw.Elapsed.TotalSeconds:F2}с. " + 
                            $"Параллельных процессов: {_activeRequests.Count}");
                    }
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    throw;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    ScraperLogger.LogError(_logger, $"Ошибка при обработке компании {companyCode}", ex);
                }
                finally
                {
                    _activeRequests.TryRemove(companyCode, out _);
                }
            },
            controller: _adaptiveConcurrencyController,
            ct: ct
        );
        
        ScraperLogger.LogCount(_logger, "Найдено пользователей", totalUsersFound, "пользователей");
    }

    private async Task<(int UsersFound, int LastStatusCode)> ScrapeCompanyFollowersAsync(string companyCode, CancellationToken ct)
    {
        var page = 1;
        var totalUsersFound = 0;
        var hasMorePages = true;
        var lastStatusCode = 0;

        while (hasMorePages && !ct.IsCancellationRequested)
        {
            try
            {
                var url = UrlManager.BuildCompanyFollowersUrl(companyCode, page);
                _logger.WriteLine($"Обработка страницы {page}: {url}");

                var response = await _httpClient.GetAsync(url, ct);
                lastStatusCode = (int)response.StatusCode;
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.WriteLine($"Страница {page} вернула код {response.StatusCode}. Завершение обхода компании {companyCode}.");
                    break;
                }

                // Получаем HTML с правильной кодировкой
                var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct);
                var encoding = response.GetEncoding();
                var html = response.DecodeBodyAsString(htmlBytes);
                
                // Сохраняем HTML в файл для отладки
                await HtmlDebug.SaveHtmlAsync(
                    html,
                    "CompanyFollowersScraper",
                    _logger,
                    "last_page.html",
                    encoding: encoding,
                    ct: ct);
                
                var doc = await HtmlParser.ParseDocumentAsync(html, ct);
                
                var userItems = doc.QuerySelectorAll(AppConfig.CompanyFollowersUserItemSelector);
                _logger.WriteLine($"Селектор '{AppConfig.CompanyFollowersUserItemSelector}' нашёл {userItems.Length} элементов");
                
                if (userItems.Length == 0)
                {
                    _logger.WriteLine($"На странице {page} не найдено пользователей. Завершение обхода компании {companyCode}.");
                    hasMorePages = false;
                    break;
                }

                var usersOnPage = 0;

                foreach (var userItem in userItems)
                {
                    try
                    {
                        // Извлекаем имя пользователя
                        var usernameElement = userItem.QuerySelector(AppConfig.CompanyFollowersUsernameSelector);
                        if (usernameElement == null)
                            continue;

                        var username = usernameElement.TextContent?.Trim();
                        if (string.IsNullOrWhiteSpace(username))
                            continue;

                        // Извлекаем ссылку
                        var linkElement = userItem.QuerySelector(AppConfig.CompanyFollowersLinkSelector);
                        if (linkElement == null)
                            continue;

                        var href = linkElement.GetAttribute("href");
                        if (string.IsNullOrWhiteSpace(href))
                            continue;

                        // Формируем полный URL
                        var fullUrl = UrlManager.ToAbsolute(href);

                        // Извлекаем слоган (может отсутствовать)
                        var sloganElement = userItem.QuerySelector(AppConfig.CompanyFollowersSloganSelector);
                        var slogan = sloganElement?.TextContent?.Trim();

                        _enqueueUser(fullUrl, username, slogan, InsertMode.UpdateIfExists);
                        ScraperLogger.LogEnqueue(
                            _logger,
                            "Resume",
                            username,
                            ("Link", fullUrl),
                            ("Slogan", slogan ?? "(нет)"));
                        usersOnPage++;
                        totalUsersFound++;
                    }
                    catch (Exception ex)
                    {
                        ScraperLogger.LogError(_logger, "Ошибка при обработке пользователя", ex);
                    }
                }

                ScraperLogger.LogCount(_logger, $"Страница {page}", usersOnPage, "пользователей");

                // Проверяем наличие следующей страницы
                hasMorePages = CompanyDataExtractor.HasNextFollowersPage(doc, page, companyCode);

                page++;
                
                // Небольшая задержка между запросами
                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            }
            catch (OperationCanceledException)
            {
                ScraperLogger.LogOperationCanceled(_logger, $"страница {page} компании {companyCode}");
                throw;
            }
            catch (Exception ex)
            {
                ScraperLogger.LogError(_logger, $"Ошибка на странице {page} компании {companyCode}", ex);
                hasMorePages = false;
            }
        }

        return (totalUsersFound, lastStatusCode);
    }

}
