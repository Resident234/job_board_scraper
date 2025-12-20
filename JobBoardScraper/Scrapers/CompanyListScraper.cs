using System.Text.RegularExpressions;
using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Domain.Models;
using JobBoardScraper.Parsing;

namespace JobBoardScraper.Scrapers;

/// <summary>
/// Периодически (раз в неделю) обходит все страницы списка компаний на career.habr.com/companies
/// и извлекает коды компаний для сохранения в базу данных.
/// Показывает прогресс относительно общего количества компаний на сайте.
/// Поддерживает два режима:
/// - Простой (по умолчанию): фильтры применяются по отдельности
/// - Комбинированный (Companies:UseFilterCombinations=true): перебор всех комбинаций фильтров
/// </summary>
public sealed class CompanyListScraper : IDisposable
{
    private readonly Regex _companyHrefRegex;
    private readonly SmartHttpClient _httpClient;
    private readonly Action<string, string, long?> _enqueueCompany;
    private readonly Func<List<string>> _getCategoryIds;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly ScraperStatistics _statistics;
    private ScraperProgressLogger? _progressLogger;

    public CompanyListScraper(
        SmartHttpClient httpClient,
        Action<string, string, long?> enqueueCompany,
        Func<List<string>> getCategoryIds,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _enqueueCompany = enqueueCompany ?? throw new ArgumentNullException(nameof(enqueueCompany));
        _getCategoryIds = getCategoryIds ?? throw new ArgumentNullException(nameof(getCategoryIds));
        _interval = interval ?? TimeSpan.FromDays(7);
        _companyHrefRegex = new Regex(AppConfig.CompaniesHrefRegex, RegexOptions.Compiled);
        _statistics = new ScraperStatistics("CompanyListScraper");
        
        _logger = new ConsoleLogger("CompanyListScraper");
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
        
        // Сначала получаем общее количество компаний с первой страницы
        var totalCompaniesOnSite = await GetTotalCompaniesCountAsync(ct);
        if (totalCompaniesOnSite > 0)
        {
            _logger.WriteLine($"На сайте найдено {totalCompaniesOnSite:N0} компаний");
        }

        var sizeFilters = new int?[] { null, 1, 2, 3, 4, 5 };
        var additionalFilters = new Dictionary<string, string>
        {
            { "with_vacancies", "1" },
            { "with_ratings", "1" },
            { "with_habr_url", "1" },
            { "has_accreditation", "1" }
        };
        
        // Получаем категории из БД
        var categoryIds = _getCategoryIds();
        _logger.WriteLine($"Загружено {categoryIds.Count} категорий для обхода");

        if (AppConfig.CompaniesUseFilterCombinations)
        {
            await ScrapeWithAllCombinationsAsync(sizeFilters, categoryIds, additionalFilters, totalCompaniesOnSite, ct);
        }
        else
        {
            await ScrapeWithSimpleFiltersAsync(sizeFilters, categoryIds, additionalFilters, totalCompaniesOnSite, ct);
        }
        
        _statistics.EndTime = DateTime.Now;
        var completionMessage = totalCompaniesOnSite > 0 
            ? $"Собрано {_statistics.TotalItemsCollected:N0} из {totalCompaniesOnSite:N0} компаний ({(double)_statistics.TotalItemsCollected / totalCompaniesOnSite:P1}). {_statistics}"
            : _statistics.ToString();
        _progressLogger?.LogCompletion(_statistics.TotalItemsCollected, completionMessage);
    }

    /// <summary>
    /// Простой обход фильтров по отдельности (по умолчанию)
    /// </summary>
    private async Task ScrapeWithSimpleFiltersAsync(
        int?[] sizeFilters, 
        List<string> categoryIds, 
        Dictionary<string, string> additionalFilters,
        int totalCompaniesOnSite,
        CancellationToken ct)
    {
        var totalFilters = sizeFilters.Length + categoryIds.Count + additionalFilters.Count;
        _progressLogger = new ScraperProgressLogger(totalFilters, "CompanyListScraper", _logger, "Filters");
        _logger.WriteLine($"Режим: простой обход фильтров. Всего фильтров: {totalFilters}");
        
        // Обходим с параметрами sz (включая null = без фильтра)
        foreach (var sz in sizeFilters)
        {
            if (ct.IsCancellationRequested) break;
            
            var filterName = sz.HasValue ? $"sz={sz}" : "без фильтра";
            _logger.WriteLine($"Обход компаний с фильтром {filterName}...");
            await ScrapeWithFiltersAsync(sz, null, null, ct);
            _progressLogger.Increment();
            LogCompanyProgress(totalCompaniesOnSite, $"Фильтр {filterName}");
        }
        
        // Обходим по категориям
        foreach (var categoryId in categoryIds)
        {
            if (ct.IsCancellationRequested) break;
            
            _logger.WriteLine($"Обход компаний с фильтром category_root_id={categoryId}...");
            await ScrapeWithFiltersAsync(null, categoryId, null, ct);
            _progressLogger.Increment();
            LogCompanyProgress(totalCompaniesOnSite, $"Категория {categoryId}");
        }
        
        // Обходим с дополнительными фильтрами
        foreach (var filter in additionalFilters)
        {
            if (ct.IsCancellationRequested) break;
            
            _logger.WriteLine($"Обход компаний с фильтром {filter.Key}={filter.Value}...");
            await ScrapeWithFiltersAsync(null, null, filter, ct);
            _progressLogger.Increment();
            LogCompanyProgress(totalCompaniesOnSite, $"Фильтр {filter.Key}={filter.Value}");
        }
    }

    /// <summary>
    /// Полный обход всех комбинаций фильтров (опционально)
    /// Генерирует: sz * category * additional комбинаций
    /// </summary>
    private async Task ScrapeWithAllCombinationsAsync(
        int?[] sizeFilters, 
        List<string> categoryIds, 
        Dictionary<string, string> additionalFilters,
        int totalCompaniesOnSite,
        CancellationToken ct)
    {
        // Добавляем null для категорий и дополнительных фильтров (означает "без этого фильтра")
        var categoryOptions = new List<string?> { null };
        categoryOptions.AddRange(categoryIds);
        
        var additionalOptions = new List<KeyValuePair<string, string>?> { null };
        additionalOptions.AddRange(additionalFilters.Select(f => (KeyValuePair<string, string>?)f));
        
        // Общее количество комбинаций
        var totalCombinations = sizeFilters.Length * categoryOptions.Count * additionalOptions.Count;
        _progressLogger = new ScraperProgressLogger(totalCombinations, "CompanyListScraper", _logger, "Combinations");
        _logger.WriteLine($"Режим: полный перебор комбинаций. Всего комбинаций: {totalCombinations}");
        _logger.WriteLine($"  - Размеры (sz): {sizeFilters.Length} вариантов");
        _logger.WriteLine($"  - Категории: {categoryOptions.Count} вариантов");
        _logger.WriteLine($"  - Доп. фильтры: {additionalOptions.Count} вариантов");
        
        var combinationIndex = 0;
        
        foreach (var sz in sizeFilters)
        {
            if (ct.IsCancellationRequested) break;
            
            foreach (var categoryId in categoryOptions)
            {
                if (ct.IsCancellationRequested) break;
                
                foreach (var additionalFilter in additionalOptions)
                {
                    if (ct.IsCancellationRequested) break;
                    
                    combinationIndex++;
                    var filterDesc = BuildFilterDescription(sz, categoryId, additionalFilter);
                    _logger.WriteLine($"Комбинация {combinationIndex}/{totalCombinations}: {filterDesc}");
                    
                    await ScrapeWithFiltersAsync(sz, categoryId, additionalFilter, ct);
                    _progressLogger.Increment();
                    LogCompanyProgress(totalCompaniesOnSite, filterDesc);
                }
            }
        }
    }

    /// <summary>
    /// Формирует описание комбинации фильтров для логирования
    /// </summary>
    private static string BuildFilterDescription(int? sz, string? categoryId, KeyValuePair<string, string>? additionalFilter)
    {
        var parts = new List<string>();
        
        if (sz.HasValue)
            parts.Add($"sz={sz}");
        
        if (!string.IsNullOrWhiteSpace(categoryId))
            parts.Add($"cat={categoryId}");
        
        if (additionalFilter.HasValue)
            parts.Add($"{additionalFilter.Value.Key}");
        
        return parts.Count > 0 ? string.Join("+", parts) : "без фильтров";
    }

    /// <summary>
    /// Получить общее количество компаний с первой страницы
    /// </summary>
    private async Task<int> GetTotalCompaniesCountAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(AppConfig.CompaniesListUrl, ct);
            if (!response.IsSuccessStatusCode)
                return 0;

            var html = await response.Content.ReadAsStringAsync(ct);
            var doc = await HtmlParser.ParseDocumentAsync(html, ct);

            var totalElement = doc.QuerySelector(AppConfig.CompaniesTotalSelector);
            if (totalElement == null)
                return 0;

            var text = totalElement.TextContent;
            var match = System.Text.RegularExpressions.Regex.Match(text, AppConfig.CompaniesTotalRegex);
            if (!match.Success)
                return 0;

            // Убираем пробелы из числа (37 847 -> 37847)
            var numberStr = match.Groups[1].Value.Replace(" ", "").Replace("\u00A0", "");
            if (int.TryParse(numberStr, out var total))
                return total;
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"Ошибка при получении общего количества компаний: {ex.Message}");
        }

        return 0;
    }

    /// <summary>
    /// Логирование прогресса с процентом от общего количества компаний на сайте
    /// </summary>
    private void LogCompanyProgress(int totalOnSite, string filterName)
    {
        if (totalOnSite > 0)
        {
            var percent = (double)_statistics.TotalItemsCollected / totalOnSite * 100;
            _progressLogger?.LogFilterProgress($"{filterName}: собрано {_statistics.TotalItemsCollected:N0}/{totalOnSite:N0} ({percent:F1}%)");
        }
        else
        {
            _progressLogger?.LogItemProgress(filterName);
        }
    }

    private async Task ScrapeWithFiltersAsync(int? sizeFilter, string? categoryId, KeyValuePair<string, string>? additionalFilter, CancellationToken ct)
    {
        var page = 1;
        var hasMorePages = true;

        while (hasMorePages && !ct.IsCancellationRequested)
        {
            try
            {
                var url = BuildUrl(page, sizeFilter, categoryId, additionalFilter);
                _logger.WriteLine($"Обработка страницы {page}: {url}");

                var response = await _httpClient.GetAsync(url, ct);
                
                _statistics.IncrementProcessed();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.WriteLine($"Страница {page} вернула код {response.StatusCode}. Завершение обхода.");
                    _statistics.IncrementFailed();
                    break;
                }

                var html = await response.Content.ReadAsStringAsync(ct);
                var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                // Ищем элементы компаний, которые содержат data-company-id
                var companyItems = doc.QuerySelectorAll(AppConfig.CompaniesItemSelector);
                
                if (companyItems.Length == 0)
                {
                    _logger.WriteLine($"На странице {page} не найдено элементов компаний ({AppConfig.CompaniesItemSelector}). Завершение обхода.");
                    hasMorePages = false;
                    break;
                }

                var companiesOnPage = 0;
                var seenOnPage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in companyItems)
                {
                    // Извлекаем company_id из атрибута
                    var companyIdStr = item.GetAttribute(AppConfig.CompaniesIdAttribute);
                    long? companyId = null;
                    if (!string.IsNullOrWhiteSpace(companyIdStr) && long.TryParse(companyIdStr, out var id))
                    {
                        companyId = id;
                    }
                    
                    // Ищем ссылку внутри элемента
                    var link = item.QuerySelector(AppConfig.CompaniesLinkSelector);
                    if (link == null)
                        continue;
                    
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
                    
                    _enqueueCompany(companyCode, companyUrl, companyId);
                    _logger.WriteLine($"В очередь: {companyCode} -> {companyUrl}" + (companyId.HasValue ? $" (ID: {companyId})" : ""));
                    companiesOnPage++;
                }

                _statistics.AddItemsCollected(companiesOnPage);
                _statistics.IncrementSuccess();
                _logger.WriteLine($"Страница {page}: найдено {companiesOnPage} уникальных компаний. Всего собрано: {_statistics.TotalItemsCollected}");

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
                _statistics.IncrementFailed();
                hasMorePages = false;
            }
        }
    }

    private static string BuildUrl(int page, int? sizeFilter, string? categoryId, KeyValuePair<string, string>? additionalFilter)
    {
        var baseUrl = AppConfig.CompaniesListUrl;
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
