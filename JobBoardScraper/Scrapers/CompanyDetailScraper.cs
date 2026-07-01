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

                    // Извлекаем название компании
                    string? companyTitle = null;
                    var companyNameElement = doc.QuerySelector(AppConfig.CompanyDetailCompanyNameSelector);
                    if (companyNameElement != null)
                    {
                        // Ищем ссылку внутри элемента
                        var linkElement =
                            companyNameElement.QuerySelector(AppConfig.CompanyDetailCompanyNameLinkSelector);
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

                    // Извлекаем описание компании
                    string? companyAbout = null;
                    var companyAboutElement = doc.QuerySelector(AppConfig.CompanyDetailCompanyAboutSelector);
                    if (companyAboutElement != null)
                    {
                        companyAbout = companyAboutElement.TextContent?.Trim();
                    }

                    // Извлекаем детальное описание компании (очищаем от HTML тегов)
                    string? companyDescription = null;
                    var companyDescriptionElement = doc.QuerySelector(AppConfig.CompanyDetailDescriptionSelector);
                    if (companyDescriptionElement != null)
                    {
                        companyDescription = companyDescriptionElement.TextContent?.Trim();
                    }

                    // Извлекаем ссылку на сайт компании
                    string? companySite = null;
                    var companySiteElement = doc.QuerySelector(AppConfig.CompanyDetailCompanySiteSelector);
                    if (companySiteElement != null)
                    {
                        var siteLinkElement =
                            companySiteElement.QuerySelector(AppConfig.CompanyDetailCompanySiteLinkSelector);
                        if (siteLinkElement != null)
                        {
                            companySite = siteLinkElement.GetAttribute("href");
                        }
                    }

                    // Извлекаем рейтинг компании
                    decimal? companyRating = null;
                    var companyRatingElement = doc.QuerySelector(AppConfig.CompanyDetailCompanyRatingSelector);
                    if (companyRatingElement != null)
                    {
                        var ratingText = companyRatingElement.TextContent?.Trim();
                        if (!string.IsNullOrWhiteSpace(ratingText) &&
                            decimal.TryParse(ratingText, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var rating))
                        {
                            companyRating = rating;
                        }
                    }

                    // Извлекаем количество сотрудников
                    int? currentEmployees = null;
                    int? pastEmployees = null;
                    var employeesElement = doc.QuerySelector(AppConfig.CompanyDetailEmployeesSelector);
                    if (employeesElement != null)
                    {
                        var countElement =
                            employeesElement.QuerySelector(AppConfig.CompanyDetailEmployeesCountSelector);
                        if (countElement != null)
                        {
                            var countText = countElement.TextContent?.Trim();
                            if (!string.IsNullOrWhiteSpace(countText))
                            {
                                var employeesMatch = _employeesRegex.Match(countText);
                                if (employeesMatch.Success && employeesMatch.Groups.Count >= 3)
                                {
                                    if (int.TryParse(employeesMatch.Groups[1].Value, out var current))
                                    {
                                        currentEmployees = current;
                                    }

                                    if (int.TryParse(employeesMatch.Groups[2].Value, out var past))
                                    {
                                        pastEmployees = past;
                                    }
                                }
                            }
                        }
                    }

                    // Извлекаем количество подписчиков
                    int? followers = null;
                    int? wantWork = null;
                    var followersElement = doc.QuerySelector(AppConfig.CompanyDetailFollowersSelector);
                    if (followersElement != null)
                    {
                        var countElement =
                            followersElement.QuerySelector(AppConfig.CompanyDetailFollowersCountSelector);
                        if (countElement != null)
                        {
                            var countText = countElement.TextContent?.Trim();
                            if (!string.IsNullOrWhiteSpace(countText))
                            {
                                var followersMatch = _followersRegex.Match(countText);
                                if (followersMatch.Success && followersMatch.Groups.Count >= 3)
                                {
                                    if (int.TryParse(followersMatch.Groups[1].Value, out var follower))
                                    {
                                        followers = follower;
                                    }

                                    if (int.TryParse(followersMatch.Groups[2].Value, out var want))
                                    {
                                        wantWork = want;
                                    }
                                }
                            }
                        }
                    }

                    // Ищем элемент с id="company_fav_button_XXXXXXXXXX"
                    var favButton = doc.QuerySelector(AppConfig.CompanyDetailFavButtonSelector);
                    long? companyId = null;
                    bool companyIdFound = false;

                    if (favButton != null)
                    {
                        var elementId = favButton.GetAttribute("id");
                        if (!string.IsNullOrWhiteSpace(elementId))
                        {
                            // Извлекаем числовой ID из атрибута id
                            var companyIdMatch = _companyIdRegex.Match(elementId);
                            if (companyIdMatch.Success)
                            {
                                var companyIdStr = companyIdMatch.Groups[1].Value;
                                if (long.TryParse(companyIdStr, out var parsedId))
                                {
                                    companyId = parsedId;
                                    companyIdFound = true;
                                    _logger.WriteLine(
                                        $"Компания {code}: ID извлечен из company_fav_button: {companyId}");
                                }
                            }
                        }
                    }

                    // Если не нашли через company_fav_button, пробуем альтернативный способ
                    if (!companyIdFound)
                    {
                        var alternativeLink = doc.QuerySelector(AppConfig.CompanyDetailAlternativeLinkSelector);
                        if (alternativeLink != null)
                        {
                            var href = alternativeLink.GetAttribute("href");
                            if (!string.IsNullOrWhiteSpace(href))
                            {
                                var altMatch = _alternativeLinkRegex.Match(href);
                                if (altMatch.Success)
                                {
                                    var companyIdStr = altMatch.Groups[1].Value;
                                    if (long.TryParse(companyIdStr, out var parsedId))
                                    {
                                        companyId = parsedId;
                                        companyIdFound = true;
                                        _logger.WriteLine(
                                            $"Компания {code}: ID извлечен из альтернативной ссылки: {companyId}");
                                    }
                                }
                            }
                        }
                    }

                    if (!companyIdFound)
                    {
                        _logger.WriteLine(
                            $"Компания {code}: не удалось извлечь company_id ни одним из способов. Пропуск.");
                        _statistics.IncrementSkipped();
                        return;
                    }

                    // Извлекаем размер компании (текст целиком)
                    string? employeesCount = null;
                    var employeesCountElement = doc.QuerySelector(AppConfig.CompanyDetailEmployeesCountElementSelector);
                    if (employeesCountElement != null)
                    {
                        employeesCount = employeesCountElement.TextContent?.Trim();
                    }

                    // Извлекаем контактных лиц компании
                    var publicMembers = doc.QuerySelectorAll(AppConfig.CompanyDetailPublicMemberSelector);
                    var memberCount = 0;
                    var memberHrefRegex =
                        new Regex(AppConfig.CompanyDetailPublicMemberHrefRegex, RegexOptions.Compiled);

                    foreach (var member in publicMembers)
                    {
                        try
                        {
                            // Извлекаем имя
                            var nameElement = member.QuerySelector(AppConfig.CompanyDetailPublicMemberNameSelector);
                            var memberName = nameElement?.TextContent?.Trim();

                            // Извлекаем код из href
                            var href = member.GetAttribute("href");
                            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(memberName))
                                continue;

                            var hrefMatch = memberHrefRegex.Match(href);
                            if (!hrefMatch.Success)
                                continue;

                            var memberCode = hrefMatch.Groups[1].Value;

                            // Формируем полную ссылку
                            var memberLink = UrlManager.Combine(AppConfig.CompanyDetailPublicMemberBaseUrl, memberCode);

                            // Сохраняем в БД (title = имя, code = код, link = полная ссылка)
                            // Используем UpdateIfExists чтобы обновить title если запись уже существует
                            _db.EnqueueResume(
                                link: memberLink,
                                title: memberName,
                                slogan: null,
                                mode: InsertMode.UpdateIfExists,
                                code: memberCode,
                                expert: null,
                                workExperience: null
                            );
                            ScraperLogger.LogEnqueue(_logger, memberCode, memberLink);

                            memberCount++;
                        }
                        catch (Exception ex)
                        {
                            ScraperLogger.LogError(_logger, "Ошибка при обработке контактного лица", ex);
                        }
                    }

                    // Извлекаем сотрудников компании из списка
                    var employeeCount = 0;
                    var usersList = doc.QuerySelector(AppConfig.CompanyDetailUsersListSelector);
                    if (usersList != null)
                    {
                        var userLinks = usersList.QuerySelectorAll(AppConfig.CompanyDetailUserLinkSelector);
                        var userHrefRegex = new Regex(AppConfig.CompanyDetailUserHrefRegex, RegexOptions.Compiled);

                        foreach (var userLink in userLinks)
                        {
                            try
                            {
                                // Извлекаем код из href
                                var href = userLink.GetAttribute("href");
                                if (string.IsNullOrWhiteSpace(href))
                                    continue;

                                var hrefMatch = userHrefRegex.Match(href);
                                if (!hrefMatch.Success)
                                    continue;

                                var userCode = hrefMatch.Groups[1].Value;

                                // Формируем полную ссылку
                                var userFullLink = UrlManager.Combine(AppConfig.CompanyDetailUserBaseUrl, userCode);

                                // Сохраняем в БД (code = код, link = полная ссылка, без title)
                                // Используем SkipIfExists чтобы не дублировать записи
                                _db.EnqueueResume(
                                    link: userFullLink,
                                    title: "", // Не сохраняем title для сотрудников
                                    slogan: null,
                                    mode: InsertMode.SkipIfExists,
                                    code: userCode,
                                    expert: null,
                                    workExperience: null
                                );
                                ScraperLogger.LogEnqueue(_logger, userCode, userFullLink);

                                employeeCount++;
                            }
                            catch (Exception ex)
                            {
                                ScraperLogger.LogError(_logger, "Ошибка при обработке сотрудника", ex);
                            }
                        }
                    }

                    // Извлекаем связанные компании из списка
                    var relatedCompanyCount = 0;
                    var inlineCompaniesList = doc.QuerySelector(AppConfig.CompanyDetailInlineCompaniesListSelector);
                    if (inlineCompaniesList != null)
                    {
                        var companyItems =
                            inlineCompaniesList.QuerySelectorAll(AppConfig.CompanyDetailCompanyItemSelector);
                        var companyHrefRegex =
                            new Regex(AppConfig.CompanyDetailCompanyHrefRegex, RegexOptions.Compiled);

                        foreach (var companyItem in companyItems)
                        {
                            try
                            {
                                // Извлекаем название компании и ссылку
                                var titleLink =
                                    companyItem.QuerySelector(AppConfig.CompanyDetailCompanyTitleLinkSelector);
                                if (titleLink == null)
                                    continue;

                                var companyName = titleLink.TextContent?.Trim();
                                var href = titleLink.GetAttribute("href");

                                if (string.IsNullOrWhiteSpace(href))
                                    continue;

                                var hrefMatch = companyHrefRegex.Match(href);
                                if (!hrefMatch.Success)
                                    continue;

                                var companyCode = hrefMatch.Groups[1].Value;

                                // Формируем полную ссылку
                                var companyUrl = UrlManager.Combine(AppConfig.CompanyDetailCompanyBaseUrl, companyCode);

                                // Сохраняем в БД (code = код, url = полная ссылка, title = название)
                                _db.EnqueueCompany(companyCode, companyUrl, companyTitle: companyName);
                                ScraperLogger.LogEnqueue(_logger, companyCode, companyUrl);

                                relatedCompanyCount++;
                            }
                            catch (Exception ex)
                            {
                                ScraperLogger.LogError(_logger, "Ошибка при обработке связанной компании", ex);
                            }
                        }
                    }

                    // Извлекаем навыки компании
                    var skills = new List<SkillsRecord>();
                    var skillsContainer = doc.QuerySelector(AppConfig.CompanyDetailSkillsContainerSelector);
                    if (skillsContainer != null)
                    {
                        var skillElements = skillsContainer.QuerySelectorAll(AppConfig.CompanyDetailSkillSelector);
                        foreach (var skillElement in skillElements)
                        {
                            var skillTitle = skillElement.TextContent?.Trim();
                            if (!string.IsNullOrWhiteSpace(skillTitle))
                            {
                                skills.Add(new SkillsRecord(SkillId: null, SkillTitle: skillTitle));
                            }
                        }
                    }

                    // Проверяем, ведет ли компания блог на Хабре
                    bool? habr = null;
                    var allDivs = doc.QuerySelectorAll("div.title");
                    foreach (var div in allDivs)
                    {
                        var divText = div.TextContent?.Trim();
                        if (divText == AppConfig.CompanyDetailHabrBlogText)
                        {
                            habr = true;
                            break;
                        }
                    }

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
                        ("Members", memberCount.ToString()),
                        ("Employees", employeeCount.ToString()),
                        ("RelatedCompanies", relatedCompanyCount.ToString()));

                    _statistics.IncrementSuccess();
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
