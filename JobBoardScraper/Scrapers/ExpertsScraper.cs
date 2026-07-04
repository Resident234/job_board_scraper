using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Utils;
using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Infrastructure.Throttling;
using JobBoardScraper.Infrastructure.Url;
using JobBoardScraper.Data;
using JobBoardScraper.Parsing;

namespace JobBoardScraper.Scrapers;

/// <summary>
/// Обходит страницы экспертов и извлекает профили
/// </summary>
/// TODO надо использовать selenium и авторизовываться, сейчас найдет только 700 пользователей
public sealed class ExpertsScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly ScraperStatistics _statistics;
    private ScraperProgressLogger? _progressLogger;

    public ExpertsScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _interval = interval ?? TimeSpan.FromDays(7);
        _statistics = new ScraperStatistics("ExpertsScraper");
        
        _logger = new ConsoleLogger("ExpertsScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация ExpertsScraper с режимом вывода: {outputMode}");
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
            await ScrapeAllExpertsAsync(ct);
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

    private async Task ScrapeAllExpertsAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало обхода экспертов...");
        
        var page = 1;
        var hasMorePages = true;
        var totalExperts = 0;
        var maxPageRetries = AppConfig.ExpertsMaxPageRetries;

        while (hasMorePages && !ct.IsCancellationRequested)
        {
            var pageThrottle = new LinearThrottle(maxPageRetries);
            var pageProcessed = false;

            while (!pageProcessed && pageThrottle.CanAttempt && !ct.IsCancellationRequested)
            {
                var url = UrlManager.BuildExpertsUrl(page);

                try
                {
                    if (pageThrottle.FailedAttempts == 0)
                    {
                        ScraperLogger.LogPage(_logger, page, url);
                    }

                    var response = await _httpClient.GetAsync(url, ct);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        ScraperLogger.LogEnd(_logger, $"Страница {page} вернула код {response.StatusCode}. Завершение обхода.");
                        hasMorePages = false;
                        pageProcessed = true;
                        break;
                    }

                    // Читаем HTML с правильной кодировкой
                    var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct);
                    var encoding = response.GetEncoding();
                    var html = response.DecodeBodyAsString(htmlBytes);
                    
                    // Сохраняем HTML в файл для отладки, если включено в конфиге
                    if (AppConfig.ExpertsSaveHtml)
                    {
                        await HtmlDebug.SaveHtmlAsync(
                            html,
                            "ExpertsScraper",
                            _logger,
                            "last_page.html",
                            encoding: encoding,
                            ct: ct);
                    }
                    
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);
                    var extraction = UserDataExtractor.ParseExpertsFromPage(doc);
                    
                    _logger.WriteLine($"На странице {page} найдено карточек: {extraction.CardCount}");

                    var expertsOnPage = 0;

                    foreach (var expert in extraction.Experts)
                    {
                        try
                        {
                            _db.EnqueueResume(expert.Resume);
                            ScraperLogger.LogEnqueue(
                                _logger,
                                expert.Resume.Title ?? expert.Resume.Link,
                                expert.Resume.Link,
                                expert.Resume.WorkExperience != null ? $"| Exp: {expert.Resume.WorkExperience}" : null);

                            _logger.WriteLine($"Эксперт: {expert.Resume.Title} ({expert.Resume.Code}) -> {expert.Resume.Link}" +
                                (expert.Resume.WorkExperience != null ? $" | Стаж: {expert.Resume.WorkExperience}" : ""));
                            expertsOnPage++;
                            _statistics.IncrementSuccess();

                            if (expert.Company.HasValue)
                            {
                                var company = expert.Company.Value;

                                _db.EnqueueCompany(company.CompanyCode, company.CompanyUrl, companyTitle: company.CompanyTitle);
                                ScraperLogger.LogEnqueue(_logger, company.CompanyCode, company.CompanyUrl);
                                _logger.WriteLine($"Компания: {company.CompanyTitle} ({company.CompanyCode}) -> {company.CompanyUrl}");
                                _statistics.IncrementItemsCollected();
                            }
                        }
                        catch (Exception ex)
                        {
                            ScraperLogger.LogError(_logger, "Ошибка при обработке карточки эксперта", ex);
                            _statistics.IncrementFailed();
                        }
                    }

                    for (var i = 0; i < extraction.FailedCards; i++)
                    {
                        _statistics.IncrementFailed();
                    }

                    _statistics.IncrementProcessed();
                    totalExperts += expertsOnPage;
                    
                    // Инициализируем progressLogger при первой странице (не знаем заранее сколько страниц)
                    if (_progressLogger == null)
                    {
                        _progressLogger = new ScraperProgressLogger(100, "ExpertsScraper", _logger, "Pages");
                    }
                    _progressLogger.Increment();
                    _progressLogger.LogPageProgress(page, expertsOnPage);

                    // Пагинация загружается через AJAX, поэтому просто переходим на следующую страницу
                    // Если на странице нет экспертов, значит достигнут конец
                    if (expertsOnPage == 0)
                    {
                        _logger.WriteLine($"На странице {page} не найдено экспертов. Достигнута последняя страница.");
                        hasMorePages = false;
                    }
                    else
                    {
                        _logger.WriteLine($"Переход на страницу {page + 1}...");
                    }

                    // Страница успешно обработана
                    pageProcessed = true;
                    page++;
                    
                    // Небольшая задержка между запросами
                    await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                }
                catch (OperationCanceledException)
                {
                    // Отмена операции - прерываем обработку
                    ScraperLogger.LogOperationCanceled(_logger, $"страница {page}");
                    throw;
                }
                catch (Exception ex)
                {
                    var failedAttempts = pageThrottle.RegisterFailure();
                    
                    if (pageThrottle.IsExhausted)
                    {
                        ScraperLogger.LogError(_logger, $"Ошибка на странице {page} после {pageThrottle.MaxAttempts} попыток", ex);
                        ScraperLogger.LogSkip(_logger, $"Пропускаем страницу {page} и переходим к следующей.");
                        
                        // Пропускаем проблемную страницу и переходим к следующей
                        pageProcessed = true;
                        page++;
                    }
                    else
                    {
                        var delayMs = pageThrottle.CurrentDelayMs;
                        ScraperLogger.LogThrottleRetry(
                            _logger,
                            failedAttempts,
                            pageThrottle.CurrentAttempt,
                            pageThrottle.MaxAttempts,
                            $"для страницы {page}: {url}",
                            delayMs,
                            ex.Message);
                        
                        // Задержка перед повтором
                        await pageThrottle.DelayAsync(ct);
                    }
                }
            }
        }
        
        _statistics.EndTime = DateTime.Now;
        ScraperLogger.LogEnd(_logger, _statistics);
        _progressLogger?.LogCompletion(totalExperts, $"Страниц: {page - 1}. {_statistics}");
    }
}
