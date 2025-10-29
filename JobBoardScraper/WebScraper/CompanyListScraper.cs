using System.Text.RegularExpressions;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Периодически (раз в неделю) обходит все страницы списка компаний на career.habr.com/companies
/// и извлекает коды компаний для сохранения в базу данных.
/// </summary>
public sealed class CompanyListScraper
{
    private readonly Regex _companyHrefRegex;
    private readonly HttpClient _httpClient;
    private readonly Action<string, string> _enqueueCompany;
    private readonly TimeSpan _interval;

    public CompanyListScraper(
        HttpClient httpClient,
        Action<string, string> enqueueCompany,
        TimeSpan? interval = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _enqueueCompany = enqueueCompany ?? throw new ArgumentNullException(nameof(enqueueCompany));
        _interval = interval ?? TimeSpan.FromDays(7);
        _companyHrefRegex = new Regex(AppConfig.CompaniesHrefRegex, RegexOptions.Compiled);
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
            Console.Error.WriteLine($"[CompanyListScraper] Ошибка: {ex.Message}");
        }
    }

    private async Task ScrapeAllPagesAsync(CancellationToken ct)
    {
        Console.WriteLine($"[CompanyListScraper] Начало обхода списка компаний...");
        
        var page = 1;
        var totalCompaniesFound = 0;
        var hasMorePages = true;

        while (hasMorePages && !ct.IsCancellationRequested)
        {
            try
            {
                var url = $"{AppConfig.CompaniesListUrl}?page={page}";
                Console.WriteLine($"[CompanyListScraper] Обработка страницы {page}: {url}");

                var response = await _httpClient.GetAsync(url, ct);
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[CompanyListScraper] Страница {page} вернула код {response.StatusCode}. Завершение обхода.");
                    break;
                }

                var html = await response.Content.ReadAsStringAsync(ct);
                var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                var companyLinks = doc.QuerySelectorAll(AppConfig.CompaniesLinkSelector);
                
                if (companyLinks.Length == 0)
                {
                    Console.WriteLine($"[CompanyListScraper] На странице {page} не найдено ссылок на компании. Завершение обхода.");
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
                    Console.WriteLine($"[CompanyListScraper] В очередь: {companyCode} -> {companyUrl}");
                    companiesOnPage++;
                    totalCompaniesFound++;
                }

                Console.WriteLine($"[CompanyListScraper] Страница {page}: найдено {companiesOnPage} уникальных компаний.");

                // Проверяем наличие следующей страницы
                var nextPageSelector = string.Format(AppConfig.CompaniesNextPageSelector, page + 1);
                var nextPageLink = doc.QuerySelector(nextPageSelector);
                if (nextPageLink == null)
                {
                    Console.WriteLine($"[CompanyListScraper] Достигнута последняя страница ({page}). Завершение обхода.");
                    hasMorePages = false;
                }

                page++;
                
                // Небольшая задержка между запросами
                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CompanyListScraper] Ошибка на странице {page}: {ex.Message}");
                hasMorePages = false;
            }
        }

        Console.WriteLine($"[CompanyListScraper] Обход завершён. Всего обработано компаний: {totalCompaniesFound}");
    }
}
