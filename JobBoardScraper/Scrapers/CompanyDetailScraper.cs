using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Utils;
using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Infrastructure.Url;
using JobBoardScraper.Core;
using JobBoardScraper.Data;
using System.Text.RegularExpressions;
using JobBoardScraper.Parsing;

namespace JobBoardScraper.Scrapers;

/// <summary>
/// Обходит детальные страницы компаний и извлекает company_id
/// </summary>
public sealed class CompanyDetailScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly Func<List<(string code, string url)>> _getCompanies;
    private readonly AdaptiveConcurrencyController _adaptiveConcurrencyController;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly Regex _companyIdRegex;
    private readonly Regex _alternativeLinkRegex;
    private readonly Regex _employeesRegex;
    private readonly Regex _followersRegex;
    private readonly ScraperStatistics _statistics;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task> _activeRequests = new();
    private ProgressTracker? _progress;

    public CompanyDetailScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        Func<List<(string code, string url)>> getCompanies,
        AdaptiveConcurrencyController controller,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _getCompanies = getCompanies ?? throw new ArgumentNullException(nameof(getCompanies));
        _adaptiveConcurrencyController = controller ?? throw new ArgumentNullException(nameof(controller));
        _interval = interval ?? TimeSpan.FromDays(30);
        _companyIdRegex = new Regex(AppConfig.CompanyDetailCompanyIdRegex, RegexOptions.Compiled);
        _alternativeLinkRegex = new Regex(AppConfig.CompanyDetailAlternativeLinkRegex, RegexOptions.Compiled);
        _employeesRegex = new Regex(AppConfig.CompanyDetailEmployeesRegex, RegexOptions.Compiled);
        _followersRegex = new Regex(AppConfig.CompanyDetailFollowersRegex, RegexOptions.Compiled);
        _statistics = new ScraperStatistics("CompanyDetailScraper");

        _logger = new ConsoleLogger("CompanyDetailScraper");
        _logger.SetOutputMode(outputMode);
        ScraperLogger.LogInitialization(_logger, "CompanyDetailScraper", outputMode);
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
            ScraperLogger.LogError(_logger, ex);
        }
    }

    private async Task ScrapeAllCompanyDetailsAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало обхода детальных страниц компаний...");

        // Получаем список компаний из БД
        var companies = _getCompanies();
        var totalCompanies = companies.Count;

        // Используем ProgressTracker для отслеживания прогресса
        _progress = new ProgressTracker(totalCompanies, "CompanyDetails");

        ScraperLogger.LogCount(_logger, "Загружено", totalCompanies, "компаний", " из БД");

        if (totalCompanies == 0)
        {
            ScraperLogger.LogSkip(_logger, "Нет компаний для обработки.");
            return;
        }

        await AdaptiveForEach.ForEachAdaptiveAsync(
            source: companies,
            body: async company =>
            {
                var (code, url) = company;
                _activeRequests.TryAdd(code, Task.CompletedTask);

                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);

                    _statistics.IncrementProcessed();
                    _statistics.UpdateActiveRequests(_activeRequests.Count);
                    _progress?.Increment();

                    double elapsedSeconds = sw.Elapsed.TotalSeconds;
                    if (_progress != null)
                    {
                        ScraperParallelLogger.LogProgress(
                            _logger,
                            _statistics,
                            url,
                            elapsedSeconds,
                            (int)response.StatusCode,
                            _progress);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        ScraperLogger.LogError(_logger, $"URL {url}: HTTP {(int)response.StatusCode}");
                        _statistics.IncrementSkipped();
                        return;
                    }

                    // Читаем HTML с правильной кодировкой
                    var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct);
                    var encoding = response.GetEncoding();
                    var html = response.DecodeBodyAsString(htmlBytes);

                    // Сохраняем HTML в файл для отладки (только последнюю страницу)
                    await HtmlDebug.SaveHtmlAsync(
                        html,
                        "CompanyDetailScraper",
                        _logger,
                        "last_page.html",
                        encoding: encoding,
                        ct: ct);

                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                    string? companyTitle = null;
                    string? companyAbout = null;
                    string? companyDescription = null;
                    string? companySite = null;
                    decimal? companyRating = null;
                    int? currentEmployees = null;
                    int? pastEmployees = null;
                    int? followers = null;
                    int? wantWork = null;
                    long? companyId = null;
                    string? employeesCount = null;
                    bool? habr = null;

                    // Извлекаем название компании
                    companyTitle = CompanyDataExtractor.ExtractCompanyTitle(doc);

                    // Извлекаем краткое описание компании
                    companyAbout = CompanyDataExtractor.ExtractCompanyAbout(doc);

                    // Извлекаем детальное описание компании
                    companyDescription = CompanyDataExtractor.ExtractCompanyDescription(doc);

                    // Извлекаем ссылку на сайт компании
                    companySite = CompanyDataExtractor.ExtractCompanySite(doc);

                    // Извлекаем рейтинг компании
                    companyRating = CompanyDataExtractor.ExtractCompanyRating(doc);

                    // Извлекаем количество сотрудников
                    var (currentEmp, pastEmp) = CompanyDataExtractor.ExtractEmployeesCount(doc, _employeesRegex);
                    currentEmployees = currentEmp;
                    pastEmployees = pastEmp;

                    // Извлекаем количество подписчиков и желающих работать
                    var (follower, want) = CompanyDataExtractor.ExtractFollowersCount(doc, _followersRegex);
                    followers = follower;
                    wantWork = want;

                    // Извлекаем company_id
                    var (id, success) = CompanyDataExtractor.ExtractCompanyId(doc, _companyIdRegex, _alternativeLinkRegex, code, _logger);
                    companyId = id;
                    if (!success)
                    {
                        ScraperLogger.LogSkip(
                            _logger,
                            $"Компания {code}: не удалось извлечь company_id ни одним из способов. Пропуск.");
                        _statistics.IncrementSkipped();
                        return;
                    }

                    // Извлекаем размер компании
                    employeesCount = CompanyDataExtractor.ExtractCompanySize(doc);

                    // Извлекаем связанные компании
                    var relatedCompanies = CompanyDataExtractor.ExtractRelatedCompanies(doc);
                    foreach (var relatedCompany in relatedCompanies)
                    {
                        _db.EnqueueCompany(relatedCompany.CompanyCode, relatedCompany.CompanyUrl, relatedCompany.CompanyTitle);
                        _logger.WriteLine($"Добавлена связанная компания: {relatedCompany.CompanyCode} ({relatedCompany.CompanyTitle})");
                    }

                    // Извлекаем сотрудников компании
                    var employees = CompanyDataExtractor.ExtractCompanyEmployees(doc);
                    foreach (var employee in employees)
                    {
                        _db.EnqueueResume(
                            link: employee.Link,
                            title: "",
                            slogan: null,
                            mode: InsertMode.SkipIfExists,
                            code: employee.UserName,
                            expert: null,
                            workExperience: null
                        );
                        _logger.WriteLine($"Добавлен сотрудник компании: {employee.UserName}");
                    }

                    // Извлекаем контактных лиц компании
                    var members = CompanyDataExtractor.ExtractPublicMembers(doc);
                    foreach (var member in members)
                    {
                        _db.EnqueueResume(
                            link: member.Link,
                            title: member.UserName,
                            slogan: null,
                            mode: InsertMode.UpdateIfExists,
                            code: member.UserName,
                            expert: null,
                            workExperience: null
                        );
                        _logger.WriteLine($"Добавлено контактное лицо: {member.UserName}");
                    }

                    // Извлекаем навыки компании
                    var skills = CompanyDataExtractor.ExtractCompanySkills(doc);

                    // Проверка на наличие блога на Хабре
                    habr = CompanyDataExtractor.HasHabrBlog(doc);

                    // Сохраняем данные компании в БД
                    _db.EnqueueCompany(code, url, companyId, companyTitle, companyAbout, companyDescription,
                        companySite, companyRating, currentEmployees, pastEmployees, followers, wantWork,
                        employeesCount, habr, skills: skills.Count > 0 ? skills : null);

                    ScraperLogger.LogEnqueue(
                        _logger,
                        entityType: "CompanyDetail",
                        entityId: url,
                        ("Code", code),
                        ("CompanyId", companyId?.ToString() ?? "(не найдено)"),
                        ("Title", companyTitle ?? "(не найдено)"),
                        ("Site", companySite ?? "(не найдено)"),
                        ("Rating", companyRating?.ToString("F2") ?? "(не найдено)"),
                        ("CurrentEmployees", currentEmployees?.ToString() ?? "(не найдено)"),
                        ("PastEmployees", pastEmployees?.ToString() ?? "(не найдено)"),
                        ("Followers", followers?.ToString() ?? "(не найдено)"),
                        ("WantWork", wantWork?.ToString() ?? "(не найдено)"),
                        ("Size", employeesCount ?? "(не найдено)"),
                        ("HabrBlog", (habr == true ? "Да" : "Нет")),
                        ("Skills", skills.Count.ToString()),
                        ("Members", members.Count.ToString()),
                        ("Employees", employees.Count.ToString()),
                        ("RelatedCompanies", relatedCompanies.Count.ToString()));

                    _statistics.IncrementSuccess();
                }
                catch (OperationCanceledException)
                {
                    ScraperLogger.LogOperationCanceled(_logger, $"детали компании {code}");
                    throw;
                }
                catch (Exception ex)
                {
                    ScraperLogger.LogError(_logger, $"Ошибка при обработке компании {code}", ex);
                    _statistics.IncrementFailed();
                }
                finally
                {
                    _activeRequests.TryRemove(code, out _);
                }
            },
            controller: _adaptiveConcurrencyController,
            ct: ct
        );

        _statistics.EndTime = DateTime.Now;
        ScraperLogger.LogEnd(_logger, _statistics);
    }
}