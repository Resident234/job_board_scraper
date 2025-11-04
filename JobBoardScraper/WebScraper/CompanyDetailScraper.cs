using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Helper.Utils;
using System.Text.RegularExpressions;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Обходит детальные страницы компаний и извлекает company_id
/// </summary>
public sealed class CompanyDetailScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly Func<List<(string code, string url)>> _getCompanies;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly Regex _companyIdRegex;

    public CompanyDetailScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        Func<List<(string code, string url)>> getCompanies,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _getCompanies = getCompanies ?? throw new ArgumentNullException(nameof(getCompanies));
        _interval = interval ?? TimeSpan.FromDays(30);
        _companyIdRegex = new Regex(AppConfig.CompanyDetailCompanyIdRegex, RegexOptions.Compiled);
        
        _logger = new ConsoleLogger("CompanyDetailScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация CompanyDetailScraper с режимом вывода: {outputMode}");
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
            await ScrapeAllCompanyDetailsAsync(ct);
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

    private async Task ScrapeAllCompanyDetailsAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода детальных страниц компаний...");
        
        // Получаем список компаний из БД
        var companies = _getCompanies();
        _logger.WriteLine($"Загружено {companies.Count} компаний из БД.");

        if (companies.Count == 0)
        {
            _logger.WriteLine("Нет компаний для обработки.");
            return;
        }

        var totalProcessed = 0;
        var totalSuccess = 0;
        var totalFailed = 0;
        var totalSkipped = 0;

        foreach (var (code, url) in companies)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                _logger.WriteLine($"Обработка компании: {code} -> {url}");

                var response = await _httpClient.GetAsync(url, ct);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.WriteLine($"Компания {code} вернула код {response.StatusCode}. Пропуск.");
                    totalSkipped++;
                    totalProcessed++;
                    continue;
                }

                // Читаем HTML с правильной кодировкой
                var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct);
                
                // Определяем кодировку из заголовков или используем UTF-8 по умолчанию
                var encoding = response.Content.Headers.ContentType?.CharSet != null
                    ? System.Text.Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet)
                    : System.Text.Encoding.UTF8;
                
                var html = encoding.GetString(htmlBytes);
                
                // Сохраняем HTML в файл для отладки (только последнюю страницу)
                var savedPath = await HtmlDebug.SaveHtmlAsync(
                    html, 
                    "CompanyDetailScraper", 
                    "last_page.html",
                    encoding: encoding,
                    ct: ct);
                
                if (savedPath != null)
                {
                    _logger.WriteLine($"HTML сохранён: {savedPath} (кодировка: {encoding.WebName})");
                }

                var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                // Извлекаем название компании
                string? companyTitle = null;
                var companyNameElement = doc.QuerySelector(AppConfig.CompanyDetailCompanyNameSelector);
                if (companyNameElement != null)
                {
                    // Ищем ссылку внутри элемента
                    var linkElement = companyNameElement.QuerySelector(AppConfig.CompanyDetailCompanyNameLinkSelector);
                    if (linkElement != null)
                    {
                        companyTitle = linkElement.TextContent?.Trim();
                    }
                    else
                    {
                        // Если ссылки нет, берём текст из самого элемента
                        companyTitle = companyNameElement.TextContent?.Trim();
                    }
                }

                // Ищем элемент с id="company_fav_button_XXXXXXXXXX"
                var favButton = doc.QuerySelector(AppConfig.CompanyDetailFavButtonSelector);
                
                if (favButton == null)
                {
                    _logger.WriteLine($"Компания {code}: не найден элемент company_fav_button. Пропуск.");
                    totalSkipped++;
                    totalProcessed++;
                    continue;
                }

                var elementId = favButton.GetAttribute("id");
                if (string.IsNullOrWhiteSpace(elementId))
                {
                    _logger.WriteLine($"Компания {code}: элемент найден, но id пустой. Пропуск.");
                    totalSkipped++;
                    totalProcessed++;
                    continue;
                }

                // Извлекаем числовой ID из атрибута id
                var match = _companyIdRegex.Match(elementId);
                if (!match.Success)
                {
                    _logger.WriteLine($"Компания {code}: не удалось извлечь ID из '{elementId}'. Пропуск.");
                    totalSkipped++;
                    totalProcessed++;
                    continue;
                }

                var companyIdStr = match.Groups[1].Value;
                if (!long.TryParse(companyIdStr, out var companyId))
                {
                    _logger.WriteLine($"Компания {code}: не удалось преобразовать '{companyIdStr}' в число. Пропуск.");
                    totalSkipped++;
                    totalProcessed++;
                    continue;
                }

                // Сохраняем company_id и title в БД
                _db.EnqueueCompanyDetails(code, companyId, companyTitle);
                _logger.WriteLine($"Компания {code}: ID = {companyId}, Название = {companyTitle ?? "(не найдено)"}");
                
                totalSuccess++;
                totalProcessed++;

                // Небольшая задержка между запросами
                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Ошибка при обработке компании {code}: {ex.Message}");
                totalFailed++;
                totalProcessed++;
            }
        }
        
        _logger.WriteLine($"Обход завершён. Обработано: {totalProcessed}, успешно: {totalSuccess}, ошибок: {totalFailed}, пропущено: {totalSkipped}");
    }
}
