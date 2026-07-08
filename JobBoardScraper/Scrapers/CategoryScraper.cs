using AngleSharp.Dom;
using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Infrastructure.Url;
using JobBoardScraper.Parsing;

namespace JobBoardScraper.Scrapers;

/// <summary>
/// Периодически (раз в неделю) обходит страницу career.habr.com/companies
/// и извлекает все значения category_root_id из select элемента для сохранения в базу данных.
/// </summary>
public sealed class CategoryScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly Action<string, string> _enqueueCategory;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly ScraperStatistics _statistics;

    public CategoryScraper(
        SmartHttpClient httpClient,
        Action<string, string> enqueueCategory,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _enqueueCategory = enqueueCategory ?? throw new ArgumentNullException(nameof(enqueueCategory));
        _interval = interval ?? TimeSpan.FromDays(7);
        _statistics = new ScraperStatistics("CategoryScraper");

        _logger = new ConsoleLogger("CategoryScraper");
        _logger.SetOutputMode(outputMode);
        ScraperLogger.LogInitialization(_logger, "CategoryScraper", outputMode);
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
            await ScrapeCategoryRootIdsAsync(ct);
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

    private async Task ScrapeCategoryRootIdsAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало сбора category_root_id...");

        try
        {
            var url = UrlManager.GetCompaniesListUrl();
            ScraperLogger.LogPage(_logger, url);

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                ScraperLogger.LogEnd(_logger, (int)response.StatusCode);
                return;
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            var doc = await HtmlParser.ParseDocumentAsync(html, ct);

            // Извлекаем категории из документа
            var categories = CompanyDataExtractor.ExtractCategories(doc);

            foreach (var (value, text) in categories)
            {
                _enqueueCategory(value, text);
                ScraperLogger.LogEnqueue(
                    _logger,
                    "Category",
                    value,
                    ("Value", value),
                    ("Title", text));
                _statistics.IncrementItemsCollected();
            }

            _statistics.EndTime = DateTime.Now;
            ScraperLogger.LogEnd(_logger, _statistics);
        }
        catch (OperationCanceledException)
        {
            ScraperLogger.LogOperationCanceled(_logger, "сбор category_root_id");
            throw;
        }
        catch (Exception ex)
        {
            ScraperLogger.LogError(_logger, "Ошибка при сборе category_root_id", ex);
            _statistics.IncrementFailed();
        }
    }
}
