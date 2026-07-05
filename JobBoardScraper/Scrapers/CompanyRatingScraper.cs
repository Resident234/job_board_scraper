using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Utils;
using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Infrastructure.Url;
using JobBoardScraper.Core;
using JobBoardScraper.Data;
using System.Linq;
using System.Text;
using JobBoardScraper.Parsing;

namespace JobBoardScraper.Scrapers;

/// <summary>
/// Скрапер для сбора данных о рейтингах компаний с career.habr.com/companies/ratings
/// Перебирает все комбинации параметров sz (размер компании) и y (год)
/// </summary>
public sealed class CompanyRatingScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly AdaptiveConcurrencyController _adaptiveConcurrencyController;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly ScraperStatistics _statistics;
    private ScraperProgressLogger? _progressLogger;

    private static readonly int[] CompanySizes = { 2, 3, 4, 5 };
    private static readonly int[] Years = { 2024, 2023, 2022, 2021, 2020, 2019, 2018 };

    public CompanyRatingScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        AdaptiveConcurrencyController controller,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _adaptiveConcurrencyController = controller ?? throw new ArgumentNullException(nameof(controller));
        _interval = interval ?? TimeSpan.FromDays(30);
        _statistics = new ScraperStatistics("CompanyRatingScraper");

        _logger = new ConsoleLogger("CompanyRatingScraper");
        _logger.SetOutputMode(outputMode);
        ScraperLogger.LogInfo(_logger, $"Инициализация CompanyRatingScraper с режимом вывода: {outputMode}");
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
            await ScrapeAllRatingsAsync(ct);
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

    private async Task ScrapeAllRatingsAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало обхода страниц рейтингов компаний...");
        _statistics.StartTime = DateTime.Now;

        var urlCombinations = GenerateUrlCombinations();

        // Используем ScraperProgressLogger для отслеживания и вывода прогресса
        _progressLogger = new ScraperProgressLogger(urlCombinations.Count, "CompanyRatingScraper", _logger, "RatingUrls");

        ScraperLogger.LogCount(_logger, "Сгенерировано", urlCombinations.Count, "комбинаций URL", " для обхода");

        foreach (var url in urlCombinations)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await ScrapeRatingPagesAsync(url, ct);
                _progressLogger.Increment();
                _progressLogger.LogItemProgress($"URL {url}");
            }
            catch (OperationCanceledException)
            {
                ScraperLogger.LogOperationCanceled(_logger, $"URL {url}");
                throw;
            }
            catch (Exception ex)
            {
                _progressLogger.Increment();
                _progressLogger.LogError($"URL {url}: {ex.Message}");
            }
        }

        _statistics.EndTime = DateTime.Now;
        ScraperLogger.LogEnd(_logger, _statistics);
    }

    private List<string> GenerateUrlCombinations()
    {
        var combinations = new List<string>();
        var baseUrl = AppConfig.CompanyRatingBaseUrl;

        // 1. Базовый URL без параметров
        combinations.Add(baseUrl);

        // 2. Только sz параметры
        foreach (var sz in CompanySizes)
        {
            combinations.Add(UrlManager.AddQueryParameter(baseUrl, "sz", sz.ToString()));
        }

        // 3. Только y параметры
        foreach (var year in Years)
        {
            combinations.Add(UrlManager.AddQueryParameter(baseUrl, "y", year.ToString()));
        }

        // 4. Комбинации sz + y
        foreach (var sz in CompanySizes)
        {
            foreach (var year in Years)
            {
                var urlWithSz = UrlManager.AddQueryParameter(baseUrl, "sz", sz.ToString());
                combinations.Add(UrlManager.AddQueryParameter(urlWithSz, "y", year.ToString()));
            }
        }

        return combinations;
    }

    private async Task ScrapeRatingPagesAsync(string baseUrl, CancellationToken ct)
    {
        var page = 1;
        var hasMorePages = true;

        while (hasMorePages && !ct.IsCancellationRequested)
        {
            var url = UrlManager.WithPage(baseUrl, page);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync(url, ct);
            sw.Stop();
            _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);

            _statistics.IncrementProcessed();

            if (!response.IsSuccessStatusCode)
            {
                ScraperLogger.LogError(_logger, $"URL {url}: HTTP {(int)response.StatusCode}");
                break;
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            var doc = await HtmlParser.ParseDocumentAsync(html, ct);

            // Парсим компании на странице
            var companies = CompanyDataExtractor.ParseCompaniesFromPage(doc, ct);

            // Добавляем в очередь БД
            foreach (var company in companies)
            {
                _db.EnqueueCompany(
                    companyCode: company.CompanyCode,
                    companyUrl: company.CompanyUrl,
                    companyTitle: company.CompanyTitle,
                    companyRating: company.Rating,
                    companyAbout: company.About,
                    city: company.City,
                    awards: company.Awards,
                    scores: company.Scores,
                    reviewRecords: company.ReviewRecords);

                ScraperLogger.LogEnqueue(
                    _logger,
                    "Company",
                    company.CompanyCode,
                    ("Url", company.CompanyUrl),
                    ("Title", company.CompanyTitle),
                    ("Rating", company.Rating?.ToString("F2") ?? "N/A"),
                    ("City", company.City ?? "N/A"));
            }

            var companiesCount = companies.Count;
            _statistics.AddItemsCollected(companiesCount);

            ScraperLogger.LogInfo(_logger, $"URL {url}: найдено {companiesCount} компаний");

            // Если компаний не найдено, останавливаем пагинацию
            if (companiesCount == 0)
            {
                hasMorePages = false;
            }
            else
            {
                page++;
            }
        }
    }
}