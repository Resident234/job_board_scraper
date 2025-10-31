using System.Text.RegularExpressions;
using JobBoardScraper.Helper;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Периодически (раз в неделю) обходит все страницы списка компаний на career.habr.com/companies
/// и извлекает коды компаний для сохранения в базу данных.
/// </summary>
/// TODO подсчет процента найденных компаний отталкиваясь от значения, записанного на странице https://career.habr.com/companies
/// TODO использование всех возможных комбинаций фильтров
public sealed class CompanyListScraper : IDisposable
{
    private readonly Regex _companyHrefRegex;
    private readonly HttpClient _httpClient;
    private readonly Action<string, string> _enqueueCompany;
    private readonly Func<List<string>> _getCategoryIds;
    private readonly TimeSpan _interval;
    private readonly Helper.ConsoleLogger _logger;

    public CompanyListScraper(
        HttpClient httpClient,
        Action<string, string> enqueueCompany,
        Func<List<string>> getCategoryIds,
        TimeSpan? interval = null,
        Helper.OutputMode outputMode = Helper.OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _enqueueCompany = enqueueCompany ?? throw new ArgumentNullException(nameof(enqueueCompany));
        _getCategoryIds = getCategoryIds ?? throw new ArgumentNullException(nameof(getCategoryIds));
        _interval = interval ?? TimeSpan.FromDays(7);
        _companyHrefRegex = new Regex(AppConfig.CompaniesHrefRegex, RegexOptions.Compiled);
        
        _logger = new Helper.ConsoleLogger("CompanyListScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация CompanyListScraper с режимом вывода: {outputMode}");
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
            await ScrapeAllPagesAsync(ct);
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

    private async Task ScrapeAllPagesAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода списка компаний...");
        
        var totalCompaniesFound = 0;
        
        // Сначала обходим базовый URL без фильтров
        _logger.WriteLine("Обход компаний без фильтров...");
        totalCompaniesFound += await ScrapeWithFiltersAsync(null, null, null, ct);
        
        // Затем обходим с параметрами sz от 1 до 5
        var sizeFilters = new[] { 1, 2, 3, 4, 5 };
        foreach (var sz in sizeFilters)
        {
            if (ct.IsCancellationRequested)
                break;
                
            _logger.WriteLine($"Обход компаний с фильтром sz={sz}...");
            totalCompaniesFound += await ScrapeWithFiltersAsync(sz, null, null, ct);
        }
        
        // Получаем категории из БД и обходим по ним
        var categoryIds = _getCategoryIds();
        _logger.WriteLine($"Загружено {categoryIds.Count} категорий для обхода");
        
        foreach (var categoryId in categoryIds)
        {
            if (ct.IsCancellationRequested)
                break;
                
            _logger.WriteLine($"Обход компаний с фильтром category_root_id={categoryId}...");
            totalCompaniesFound += await ScrapeWithFiltersAsync(null, categoryId, null, ct);
        }
        
        // Обходим с дополнительными фильтрами
        var additionalFilters = new Dictionary<string, string>
        {
            { "with_vacancies", "1" },
            { "with_ratings", "1" },
            { "with_habr_url", "1" },
            { "has_accreditation", "1" }
        };
        
        foreach (var filter in additionalFilters)
        {
            if (ct.IsCancellationRequested)
                break;
                
            _logger.WriteLine($"Обход компаний с фильтром {filter.Key}={filter.Value}...");
            totalCompaniesFound += await ScrapeWithFiltersAsync(null, null, filter, ct);
        }
        
        _logger.WriteLine($"Обход завершён. Всего обработано компаний: {totalCompaniesFound}");
    }

    private async Task<int> ScrapeWithFiltersAsync(int? sizeFilter, string? categoryId, KeyValuePair<string, string>? additionalFilter, CancellationToken ct)
    {
        var page = 1;
        var totalCompaniesFound = 0;
        var hasMorePages = true;

        while (hasMorePages && !ct.IsCancellationRequested)
        {
            try
            {
                var url = BuildUrl(page, sizeFilter, categoryId, additionalFilter);
                _logger.WriteLine($"Обработка страницы {page}: {url}");

                var response = await _httpClient.GetAsync(url, ct);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.WriteLine($"Страница {page} вернула код {response.StatusCode}. Завершение обхода.");
                    break;
                }

                var html = await response.Content.ReadAsStringAsync(ct);
                var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                var companyLinks = doc.QuerySelectorAll(AppConfig.CompaniesLinkSelector);
                
                if (companyLinks.Length == 0)
                {
                    _logger.WriteLine($"На странице {page} не найдено ссылок на компании. Завершение обхода.");
                    hasMorePages = false;
                    break;
                }

                var companiesOnPage = 0;
                var seenOnPage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var link in companyLinks)
                {
                    var href = link.GetAttribute("href");
                    if (string.IsNullOrWhiteSpace(href))
                        continue;

                    var match = _companyHrefRegex.Match(href);
                    if (!match.Success)
                        continue;

                    var companyCode = match.Groups[1].Value;
                    
                    if (string.IsNullOrWhiteSpace(companyCode))
                        continue;

                    // Дедупликация на странице
                    if (!seenOnPage.Add(companyCode))
                        continue;

                    var companyUrl = $"{AppConfig.CompaniesBaseUrl}{companyCode}";
                    
                    _enqueueCompany(companyCode, companyUrl);
                    _logger.WriteLine($"В очередь: {companyCode} -> {companyUrl}");
                    companiesOnPage++;
                    totalCompaniesFound++;
                }

                _logger.WriteLine($"Страница {page}: найдено {companiesOnPage} уникальных компаний.");

                // Проверяем наличие следующей страницы
                var nextPageSelector = string.Format(AppConfig.CompaniesNextPageSelector, page + 1);
                var nextPageLink = doc.QuerySelector(nextPageSelector);
                if (nextPageLink == null)
                {
                    _logger.WriteLine($"Достигнута последняя страница ({page}). Завершение обхода.");
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

        return totalCompaniesFound;
    }

    private string BuildUrl(int page, int? sizeFilter, string? categoryId, KeyValuePair<string, string>? additionalFilter)
    {
        var baseUrl = AppConfig.CompaniesListUrl;
        
        if (page == 1 && !sizeFilter.HasValue && string.IsNullOrWhiteSpace(categoryId) && !additionalFilter.HasValue)
        {
            return baseUrl;
        }

        var queryParams = new List<string>();
        
        if (page > 1)
        {
            queryParams.Add($"page={page}");
        }
        
        if (sizeFilter.HasValue)
        {
            queryParams.Add($"sz={sizeFilter.Value}");
        }
        
        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            queryParams.Add($"category_root_id={categoryId}");
        }
        
        if (additionalFilter.HasValue)
        {
            queryParams.Add($"{additionalFilter.Value.Key}={additionalFilter.Value.Value}");
        }

        return queryParams.Count > 0 
            ? $"{baseUrl}?{string.Join("&", queryParams)}"
            : baseUrl;
    }
}
