using JobBoardScraper.Helper.ConsoleHelper;
using System.Text.RegularExpressions;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Обходит страницы экспертов и извлекает профили
/// </summary>
public sealed class ExpertsScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly Regex _codeRegex = new Regex(@"^/([^/]+)$", RegexOptions.Compiled);

    public ExpertsScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _interval = interval ?? TimeSpan.FromDays(7);
        
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

        while (hasMorePages && !ct.IsCancellationRequested)
        {
            try
            {
                var url = BuildUrl(page);
                _logger.WriteLine($"Обработка страницы {page}: {url}");

                var response = await _httpClient.GetAsync(url, ct);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.WriteLine($"Страница {page} вернула код {response.StatusCode}. Завершение обхода.");
                    break;
                }

                var html = await response.Content.ReadAsStringAsync(ct);
                var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                // Ищем карточки экспертов
                var expertCards = doc.QuerySelectorAll(".expert-card");
                
                if (expertCards.Length == 0)
                {
                    _logger.WriteLine($"На странице {page} не найдено экспертов. Завершение обхода.");
                    hasMorePages = false;
                    break;
                }

                var expertsOnPage = 0;
                var companiesOnPage = 0;

                foreach (var card in expertCards)
                {
                    try
                    {
                        // Извлекаем данные эксперта
                        var titleLink = card.QuerySelector("a.expert-card__title-link");
                        if (titleLink == null) continue;

                        var name = titleLink.TextContent?.Trim();
                        var href = titleLink.GetAttribute("href");
                        
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(href))
                            continue;

                        // Извлекаем код из href
                        var match = _codeRegex.Match(href);
                        var code = match.Success ? match.Groups[1].Value : null;

                        // Формируем полный URL
                        var fullUrl = href.StartsWith("http") 
                            ? href 
                            : $"{AppConfig.BaseUrl.TrimEnd('/')}{href}";

                        // Извлекаем стаж работы
                        string? workExperience = null;
                        var spans = card.QuerySelectorAll("span");
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
                        var companyLink = card.QuerySelector("a.link-comp");
                        if (companyLink != null)
                        {
                            var companyName = companyLink.TextContent?.Trim();
                            var companyHref = companyLink.GetAttribute("href");
                            
                            if (!string.IsNullOrWhiteSpace(companyName) && !string.IsNullOrWhiteSpace(companyHref))
                            {
                                // Извлекаем код компании из href
                                var companyCodeMatch = Regex.Match(companyHref, @"/companies/([^/]+)");
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

                // Проверяем наличие следующей страницы
                var nextPageLink = doc.QuerySelector($"a.page[href*='page={page + 1}']");
                if (nextPageLink == null)
                {
                    _logger.WriteLine($"Достигнута последняя страница ({page}).");
                    hasMorePages = false;
                }

                page++;
                
                // Небольшая задержка между запросами
                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Ошибка на странице {page}: {ex.Message}");
                hasMorePages = false;
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
