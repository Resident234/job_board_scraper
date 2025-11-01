using JobBoardScraper.Helper.ConsoleHelper;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Обходит страницы подписчиков компаний и извлекает профили пользователей
/// </summary>
public sealed class CompanyFollowersScraper : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Action<string, string, string?> _enqueueUser;
    private readonly Func<List<string>> _getCompanyCodes;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;

    public CompanyFollowersScraper(
        HttpClient httpClient,
        Action<string, string, string?> enqueueUser,
        Func<List<string>> getCompanyCodes,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _enqueueUser = enqueueUser ?? throw new ArgumentNullException(nameof(enqueueUser));
        _getCompanyCodes = getCompanyCodes ?? throw new ArgumentNullException(nameof(getCompanyCodes));
        _interval = interval ?? TimeSpan.FromDays(7);
        
        _logger = new ConsoleLogger("CompanyFollowersScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация CompanyFollowersScraper с режимом вывода: {outputMode}");
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
            _logger.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    private async Task ScrapeAllCompaniesAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода подписчиков компаний...");
        
        var companyCodes = _getCompanyCodes();
        _logger.WriteLine($"Загружено {companyCodes.Count} компаний для обхода");
        
        var totalUsersFound = 0;
        
        foreach (var companyCode in companyCodes)
        {
            if (ct.IsCancellationRequested)
                break;
                
            _logger.WriteLine($"Обработка компании: {companyCode}");
            var usersFound = await ScrapeCompanyFollowersAsync(companyCode, ct);
            totalUsersFound += usersFound;
            
            // Небольшая задержка между компаниями
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
        
        _logger.WriteLine($"Обход завершён. Всего найдено пользователей: {totalUsersFound}");
    }

    private async Task<int> ScrapeCompanyFollowersAsync(string companyCode, CancellationToken ct)
    {
        var page = 1;
        var totalUsersFound = 0;
        var hasMorePages = true;

        while (hasMorePages && !ct.IsCancellationRequested)
        {
            try
            {
                var url = BuildUrl(companyCode, page);
                _logger.WriteLine($"Обработка страницы {page}: {url}");

                var response = await _httpClient.GetAsync(url, ct);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.WriteLine($"Страница {page} вернула код {response.StatusCode}. Завершение обхода компании {companyCode}.");
                    break;
                }

                var html = await response.Content.ReadAsStringAsync(ct);
                var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                var userItems = doc.QuerySelectorAll(AppConfig.CompanyFollowersUserItemSelector);
                
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
                        var linkElement = userItem.QuerySelector("a");
                        if (linkElement == null)
                            continue;

                        var href = linkElement.GetAttribute("href");
                        if (string.IsNullOrWhiteSpace(href))
                            continue;

                        // Формируем полный URL
                        var fullUrl = href.StartsWith("http") 
                            ? href 
                            : $"{AppConfig.BaseUrl.TrimEnd('/')}{href}";

                        // Извлекаем слоган (может отсутствовать)
                        var sloganElement = userItem.QuerySelector(AppConfig.CompanyFollowersSloganSelector);
                        var slogan = sloganElement?.TextContent?.Trim();

                        _enqueueUser(fullUrl, username, slogan);
                        _logger.WriteLine($"В очередь: {username} -> {fullUrl}" + 
                            (string.IsNullOrWhiteSpace(slogan) ? "" : $" ({slogan})"));
                        usersOnPage++;
                        totalUsersFound++;
                    }
                    catch (Exception ex)
                    {
                        _logger.WriteLine($"Ошибка при обработке пользователя: {ex.Message}");
                    }
                }

                _logger.WriteLine($"Страница {page}: найдено {usersOnPage} пользователей.");

                // Проверяем наличие следующей страницы
                var nextPageSelector = string.Format(AppConfig.CompanyFollowersNextPageSelector, page + 1);
                var nextPageLink = doc.QuerySelector(nextPageSelector);
                if (nextPageLink == null)
                {
                    _logger.WriteLine($"Достигнута последняя страница ({page}) для компании {companyCode}.");
                    hasMorePages = false;
                }

                page++;
                
                // Небольшая задержка между запросами
                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Ошибка на странице {page} компании {companyCode}: {ex.Message}");
                hasMorePages = false;
            }
        }

        return totalUsersFound;
    }

    private string BuildUrl(string companyCode, int page)
    {
        var baseUrl = string.Format(AppConfig.CompanyFollowersUrlTemplate, companyCode);
        
        if (page == 1)
        {
            return baseUrl;
        }

        return $"{baseUrl}?page={page}";
    }
}
