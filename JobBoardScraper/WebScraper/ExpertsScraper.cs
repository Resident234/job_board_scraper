using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Helper.Utils;
using System.Text.RegularExpressions;

namespace JobBoardScraper.WebScraper;

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
    private readonly Regex _userCodeRegex;
    private readonly Regex _companyCodeRegex;

    public ExpertsScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _interval = interval ?? TimeSpan.FromDays(7);
        _userCodeRegex = new Regex(AppConfig.ExpertsUserCodeRegex, RegexOptions.Compiled);
        _companyCodeRegex = new Regex(AppConfig.ExpertsCompanyCodeRegex, RegexOptions.Compiled);
        
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
            _logger.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    private async Task ScrapeAllExpertsAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода экспертов...");
        
        var page = 1;
        var totalExpertsFound = 0;
        var totalCompaniesFound = 0;
        var hasMorePages = true;
        const int maxPageRetries = 3; // Максимум попыток для одной страницы

        while (hasMorePages && !ct.IsCancellationRequested)
        {
            var pageRetryCount = 0;
            var pageProcessed = false;

            while (!pageProcessed && pageRetryCount < maxPageRetries && !ct.IsCancellationRequested)
            {
                try
                {
                    var url = BuildUrl(page);
                    
                    if (pageRetryCount > 0)
                    {
                        _logger.WriteLine($"Повторная попытка {pageRetryCount + 1}/{maxPageRetries} для страницы {page}: {url}");
                    }
                    else
                    {
                        _logger.WriteLine($"Обработка страницы {page}: {url}");
                    }

                    var response = await _httpClient.GetAsync(url, ct);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.WriteLine($"Страница {page} вернула код {response.StatusCode}. Завершение обхода.");
                        hasMorePages = false;
                        pageProcessed = true;
                        break;
                    }

                    // Читаем HTML с правильной кодировкой
                    var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct);
                    
                    // Определяем кодировку из заголовков или используем UTF-8 по умолчанию
                    var encoding = response.Content.Headers.ContentType?.CharSet != null
                        ? System.Text.Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet)
                        : System.Text.Encoding.UTF8;
                    
                    var html = encoding.GetString(htmlBytes);
                    
                    // Сохраняем HTML в файл для отладки
                    var savedPath = await HtmlDebug.SaveHtmlAsync(
                        html, 
                        "ExpertsScraper", 
                        "last_page.html",
                        encoding: encoding,
                        ct: ct);
                    
                    if (savedPath != null)
                    {
                        _logger.WriteLine($"HTML сохранён: {savedPath} (кодировка: {encoding.WebName})");
                    }
                    else
                    {
                        _logger.WriteLine("Не удалось сохранить HTML для отладки.");
                    }
                    
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                    // Ищем карточки экспертов
                    var expertCards = doc.QuerySelectorAll(AppConfig.ExpertsExpertCardSelector);
                    
                    _logger.WriteLine($"На странице {page} найдено карточек: {expertCards.Length}");

                    var expertsOnPage = 0;
                    var companiesOnPage = 0;

                    foreach (var card in expertCards)
                    {
                        try
                        {
                            // Извлекаем данные эксперта
                            var titleLink = card.QuerySelector(AppConfig.ExpertsTitleLinkSelector);
                            if (titleLink == null) continue;

                            var name = titleLink.TextContent?.Trim();
                            var href = titleLink.GetAttribute("href");
                            
                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(href))
                                continue;

                            // Извлекаем код из href
                            var match = _userCodeRegex.Match(href);
                            var code = match.Success ? match.Groups[1].Value : null;

                            // Формируем полный URL
                            var fullUrl = href.StartsWith("http") 
                                ? href 
                                : $"{AppConfig.BaseUrl.TrimEnd('/')}{href}";

                            // Извлекаем стаж работы
                            string? workExperience = null;
                            var spans = card.QuerySelectorAll(AppConfig.ExpertsSpanSelector);
                            foreach (var span in spans)
                            {
                                var text = span.TextContent?.Trim();
                                if (!string.IsNullOrWhiteSpace(text) && text.StartsWith("Стаж "))
                                {
                                    workExperience = text.Replace("Стаж ", "").Trim();
                                    break;
                                }
                            }

                            // Сохраняем эксперта
                            _db.EnqueueResume(
                                link: fullUrl,
                                title: name,
                                slogan: null,
                                mode: InsertMode.UpdateIfExists,
                                code: code,
                                expert: true,
                                workExperience: workExperience);

                            _logger.WriteLine($"Эксперт: {name} ({code}) -> {fullUrl}" + 
                                (workExperience != null ? $" | Стаж: {workExperience}" : ""));
                            expertsOnPage++;

                            // Извлекаем компанию
                            var companyLink = card.QuerySelector(AppConfig.ExpertsCompanyLinkSelector);
                            if (companyLink != null)
                            {
                                var companyName = companyLink.TextContent?.Trim();
                                var companyHref = companyLink.GetAttribute("href");
                                
                                if (!string.IsNullOrWhiteSpace(companyName) && !string.IsNullOrWhiteSpace(companyHref))
                                {
                                    // Извлекаем код компании из href
                                    var companyCodeMatch = _companyCodeRegex.Match(companyHref);
                                    if (companyCodeMatch.Success)
                                    {
                                        var companyCode = companyCodeMatch.Groups[1].Value;
                                        var companyUrl = companyHref.StartsWith("http") 
                                            ? companyHref 
                                            : $"https://career.habr.com{companyHref}";

                                        _db.EnqueueCompany(companyCode, companyUrl);
                                        _logger.WriteLine($"Компания: {companyName} ({companyCode}) -> {companyUrl}");
                                        companiesOnPage++;
                                    }
                                }
                            }

                            totalExpertsFound++;
                        }
                        catch (Exception ex)
                        {
                            _logger.WriteLine($"Ошибка при обработке карточки эксперта: {ex.Message}");
                        }
                    }

                    totalCompaniesFound += companiesOnPage;
                    _logger.WriteLine($"Страница {page}: найдено {expertsOnPage} экспертов, {companiesOnPage} компаний.");

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
                    _logger.WriteLine($"Операция отменена на странице {page}.");
                    throw;
                }
                catch (Exception ex)
                {
                    pageRetryCount++;
                    
                    if (pageRetryCount >= maxPageRetries)
                    {
                        _logger.WriteLine($"Ошибка на странице {page} после {maxPageRetries} попыток: {ex.Message}");
                        _logger.WriteLine($"Пропускаем страницу {page} и переходим к следующей.");
                        
                        // Пропускаем проблемную страницу и переходим к следующей
                        pageProcessed = true;
                        page++;
                    }
                    else
                    {
                        _logger.WriteLine($"Ошибка на странице {page} (попытка {pageRetryCount}/{maxPageRetries}): {ex.Message}");
                        _logger.WriteLine($"Повтор через 2 секунды...");
                        
                        // Задержка перед повтором
                        await Task.Delay(TimeSpan.FromSeconds(2), ct);
                    }
                }
            }
        }
        
        _logger.WriteLine($"Обход завершён. Найдено экспертов: {totalExpertsFound}, компаний: {totalCompaniesFound}");
    }

    private string BuildUrl(int page)
    {
        var baseUrl = AppConfig.ExpertsListUrl;
        
        if (page == 1)
        {
            return baseUrl;
        }

        return $"{baseUrl}&page={page}";
    }
}
