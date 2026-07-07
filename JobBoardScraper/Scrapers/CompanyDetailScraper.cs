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

                    // Создаем объект CompanyRecord для хранения данных компании
                    var companyRecord = new CompanyRecord(
                        CompanyCode: code,
                        CompanyUrl: url,
                        CompanyId: null,
                        CompanyTitle: null,
                        About: null,
                        Description: null,
                        Site: null,
                        Rating: null,
                        CurrentEmployees: null,
                        PastEmployees: null,
                        Followers: null,
                        WantWork: null,
                        EmployeesCount: null,
                        HasHabrBlog: null,
                        Skills: null
                    );

                    // Извлекаем название компании
                    companyRecord.CompanyTitle = CompanyDataExtractor.ExtractCompanyTitle(doc);

                    // Извлекаем краткое описание компании
                    companyRecord.About = CompanyDataExtractor.ExtractCompanyAbout(doc);

                    // Извлекаем детальное описание компании
                    companyRecord.Description = CompanyDataExtractor.ExtractCompanyDescription(doc);

                    // Извлекаем ссылку на сайт компании
                    companyRecord.Site = CompanyDataExtractor.ExtractCompanySite(doc);

                    // Извлекаем рейтинг компании
                    companyRecord.Rating = CompanyDataExtractor.ExtractCompanyRating(doc);

                    // Извлекаем количество сотрудников
                    var (currentEmp, pastEmp) = CompanyDataExtractor.ExtractEmployeesCount(doc, _employeesRegex);
                    companyRecord.CurrentEmployees = currentEmp;
                    companyRecord.PastEmployees = pastEmp;

                    // Извлекаем количество подписчиков и желающих работать
                    var (follower, want) = CompanyDataExtractor.ExtractFollowersCount(doc, _followersRegex);
                    companyRecord.Followers = follower;
                    companyRecord.WantWork = want;

                    // Извлекаем company_id
                    var (id, success) = CompanyDataExtractor.ExtractCompanyId(doc, _companyIdRegex, _alternativeLinkRegex, code, _logger);
                    companyRecord.CompanyId = id;
                    if (!success)
                    {
                        ScraperLogger.LogSkip(
                            _logger,
                            $"Компания {code}: не удалось извлечь company_id ни одним из способов. Пропуск.");
                        _statistics.IncrementSkipped();
                        return;
                    }

                    // Извлекаем размер компании
                    companyRecord.EmployeesCount = CompanyDataExtractor.ExtractCompanySize(doc);

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
                    companyRecord.Skills = skills.Count > 0 ? skills : null;

                    // Проверка на наличие блога на Хабре
                    companyRecord.HasHabrBlog = CompanyDataExtractor.HasHabrBlog(doc);

                    // Сохраняем данные компании в БД
                    _db.EnqueueCompany(
                        companyRecord.CompanyCode,
                        companyRecord.CompanyUrl,
                        companyRecord.CompanyId,
                        companyRecord.CompanyTitle,
                        companyRecord.About,
                        companyRecord.Description,
                        companyRecord.Site,
                        companyRecord.Rating,
                        companyRecord.CurrentEmployees,
                        companyRecord.PastEmployees,
                        companyRecord.Followers,
                        companyRecord.WantWork,
                        companyRecord.EmployeesCount,
                        companyRecord.HasHabrBlog,
                        skills: companyRecord.Skills
                    );

                    ScraperLogger.LogEnqueue(
                        _logger,
                        entityType: "CompanyDetail",
                        entityId: url,
                        ("Code", code),
                        ("CompanyId", companyRecord.CompanyId?.ToString() ?? "(не найдено)"),
                        ("Title", companyRecord.CompanyTitle ?? "(не найдено)"),
                        ("Site", companyRecord.Site ?? "(не найдено)"),
                        ("Rating", companyRecord.Rating?.ToString("F2") ?? "(не найдено)"),
                        ("CurrentEmployees", companyRecord.CurrentEmployees?.ToString() ?? "(не найдено)"),
                        ("PastEmployees", companyRecord.PastEmployees?.ToString() ?? "(не найдено)"),
                        ("Followers", companyRecord.Followers?.ToString() ?? "(не найдено)"),
                        ("WantWork", companyRecord.WantWork?.ToString() ?? "(не найдено)"),
                        ("Size", companyRecord.EmployeesCount ?? "(не найдено)"),
                        ("HabrBlog", (companyRecord.HasHabrBlog == true ? "Да" : "Нет")),
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