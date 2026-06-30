using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Utils;
using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Core;
using JobBoardScraper.Data;
using System.Linq;
using System.Security.Cryptography;
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
    private readonly AdaptiveConcurrencyController _controller;
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
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _interval = interval ?? TimeSpan.FromDays(30);
        _statistics = new ScraperStatistics("CompanyRatingScraper");

        _logger = new ConsoleLogger("CompanyRatingScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация CompanyRatingScraper с режимом вывода: {outputMode}");
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
        _logger.WriteLine("Начало обхода страниц рейтингов компаний...");
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
            combinations.Add($"{baseUrl}?sz={sz}");
        }

        // 3. Только y параметры
        foreach (var year in Years)
        {
            combinations.Add($"{baseUrl}?y={year}");
        }

        // 4. Комбинации sz + y
        foreach (var sz in CompanySizes)
        {
            foreach (var year in Years)
            {
                combinations.Add($"{baseUrl}?sz={sz}&y={year}");
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
            var url = page == 1 ? baseUrl : $"{baseUrl}{(baseUrl.Contains('?') ? "&" : "?")}page={page}";

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.GetAsync(url, ct);
            sw.Stop();
            _controller.ReportLatency(sw.Elapsed);

            _statistics.IncrementProcessed();

            if (!response.IsSuccessStatusCode)
            {
                _logger.WriteLine($"URL {url}: HTTP {(int)response.StatusCode}");
                break;
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            var doc = await HtmlParser.ParseDocumentAsync(html, ct);

            // Парсим компании на странице
            var companies = await ParseCompaniesFromPage(doc, ct);
            
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
                    
                ScraperLogger.LogEnqueue(_logger, company.CompanyCode, company.CompanyUrl);
            }

            var companiesCount = companies.Count;
            _statistics.AddItemsCollected(companiesCount);

            _logger.WriteLine($"URL {url}: найдено {companiesCount} компаний");

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

    private async Task<List<CompanyRecord>> ParseCompaniesFromPage(AngleSharp.Dom.IDocument doc, CancellationToken ct)
    {
        var companies = new List<CompanyRecord>();
        var sections = doc.QuerySelectorAll(AppConfig.CompanyRatingSectionSelector);

        foreach (var section in sections)
        {
            try
            {
                if (ExtractCompanyData(section) is { } company)
                {
                    companies.Add(company);
                }
            }
            catch (Exception ex)
            {
                ScraperLogger.LogError(_logger, "Ошибка при парсинге компании", ex);
            }
        }

        return companies;
    }

    private CompanyRecord? ExtractCompanyData(AngleSharp.Dom.IElement section)
    {
        // 1. Извлекаем код компании из ссылки
        var titleLink = section.QuerySelector(AppConfig.CompanyRatingTitleLinkSelector);
        if (titleLink == null) return null;

        var href = titleLink.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(href)) return null;

        // Извлекаем код из href (например, "/companies/tensor" или "https://career.habr.com/companies/tensor" -> "tensor")
        var uri = new Uri(href, UriKind.RelativeOrAbsolute);
        var path = uri.IsAbsoluteUri ? uri.AbsolutePath : href;
        var code = path.TrimStart('/').Replace(AppConfig.CompanyRatingCompanyPathPrefix, "").Trim('/');
        if (string.IsNullOrWhiteSpace(code)) return null;

        var url = string.Format(AppConfig.CompanyRatingCompanyUrlTemplate, code);

        // 2. Извлекаем название компании
        var titleElement = section.QuerySelector(AppConfig.CompanyRatingTitleTextSelector);
        var title = titleElement?.TextContent?.Trim();

        // 3. Извлекаем рейтинг
        decimal? rating = null;
        var ratingElement = section.QuerySelector(AppConfig.CompanyRatingRatingSelector);
        if (ratingElement != null)
        {
            // Получаем весь текст, включая дочерние элементы
            var fullText = ratingElement.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(fullText))
            {
                // Ищем числовое значение с помощью регулярного выражения
                var match = System.Text.RegularExpressions.Regex.Match(fullText, @"(\d+[.,]\d+|\d+)");
                if (match.Success && decimal.TryParse(match.Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ratingValue))
                {
                    rating = ratingValue;
                }
            }
        }

        // 4. Извлекаем описание
        var descriptionElement = section.QuerySelector(AppConfig.CompanyRatingDescriptionSelector);
        var about = descriptionElement?.TextContent?.Trim();

        // 5. Извлекаем город (первый элемент до разделителя)
        string? city = null;
        var metaElement = section.QuerySelector(AppConfig.CompanyRatingMetaSelector);
        if (metaElement != null)
        {
            var cityLink = metaElement.QuerySelector(AppConfig.CompanyRatingCityLinkSelector);
            if (cityLink != null)
            {
                city = cityLink.TextContent?.Trim();
            }
        }

        // 6. Извлекаем награды
        var awards = new List<string>();
        var awardsContainer = section.QuerySelector(AppConfig.CompanyRatingAwardsSelector);
        if (awardsContainer != null)
        {
            var awardImages = awardsContainer.QuerySelectorAll(AppConfig.CompanyRatingAwardImageSelector);
            foreach (var img in awardImages)
            {
                var alt = img.GetAttribute("alt");
                if (!string.IsNullOrWhiteSpace(alt))
                {
                    awards.Add(alt.Trim());
                }
            }
        }

        // 7. Извлекаем среднюю оценку
        decimal? scores = null;
        var scoresElement = section.QuerySelector(AppConfig.CompanyRatingScoresSelector);
        if (scoresElement != null)
        {
            var scoresText = scoresElement.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(scoresText) && decimal.TryParse(scoresText.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var scoresValue))
            {
                scores = scoresValue;
            }
        }

                // 8. Извлекаем текст отзыва (очищенный от HTML) и вычисляем хеш
                List<CompanyReviewRecord>? reviewRecords = null;
                var reviewElement = section.QuerySelector(AppConfig.CompanyRatingReviewSelector);
                if (reviewElement != null)
                {
                    var reviewText = reviewElement.TextContent?.Trim();
                    if (!string.IsNullOrWhiteSpace(reviewText))
                    {
                        reviewRecords = new List<CompanyReviewRecord> 
                        { 
                            new CompanyReviewRecord(
                                CompanyCode: code, 
                                ReviewHash: ComputeReviewHash(reviewText), 
                                ReviewText: reviewText
                            ) 
                        };
                    }
                }

                return new CompanyRecord(
                    CompanyCode: code,
                    CompanyUrl: url,
                    CompanyTitle: title,
                    Rating: rating,
                    About: about,
                    City: city,
                    Awards: awards.Count > 0 ? awards : null,
                    Scores: scores,
                    ReviewRecords: reviewRecords
                );
    }

    /// <summary>
    /// Вычисляет SHA256 хеш для текста отзыва
    /// </summary>
    public static string ComputeReviewHash(string reviewText)
    {
        if (string.IsNullOrWhiteSpace(reviewText))
            return string.Empty;

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(reviewText);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
