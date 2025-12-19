using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Helper.Http;
using JobBoardScraper.Helper.Logger;
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
    private readonly AdaptiveConcurrencyController _controller;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly Regex _companyIdRegex;
    private readonly Regex _alternativeLinkRegex;
    private readonly Regex _employeesRegex;
    private readonly Regex _followersRegex;
    private readonly Models.ScraperStatistics _statistics;
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
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _interval = interval ?? TimeSpan.FromDays(30);
        _companyIdRegex = new Regex(AppConfig.CompanyDetailCompanyIdRegex, RegexOptions.Compiled);
        _alternativeLinkRegex = new Regex(AppConfig.CompanyDetailAlternativeLinkRegex, RegexOptions.Compiled);
        _employeesRegex = new Regex(AppConfig.CompanyDetailEmployeesRegex, RegexOptions.Compiled);
        _followersRegex = new Regex(AppConfig.CompanyDetailFollowersRegex, RegexOptions.Compiled);
        _statistics = new Models.ScraperStatistics("CompanyDetailScraper");

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
        var totalCompanies = companies.Count;
        
        // Используем ProgressTracker для отслеживания прогресса
        _progress = new ProgressTracker(totalCompanies, "CompanyDetails");
        
        _logger.WriteLine($"Загружено {totalCompanies} компаний из БД.");

        if (totalCompanies == 0)
        {
            _logger.WriteLine("Нет компаний для обработки.");
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
                    _controller.ReportLatency(sw.Elapsed);

                    _statistics.IncrementProcessed();
                    _statistics.UpdateActiveRequests(_activeRequests.Count);
                    _progress?.Increment();

                    double elapsedSeconds = sw.Elapsed.TotalSeconds;
                    if (_progress != null)
                    {
                        ParallelScraperLogger.LogProgress(
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
                            var memberLink = AppConfig.CompanyDetailPublicMemberBaseUrl + memberCode;

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

                            memberCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.WriteLine($"Ошибка при обработке контактного лица: {ex.Message}");
                        }
                    }

                    if (memberCount > 0)
                    {
                        _logger.WriteLine($"Найдено контактных лиц: {memberCount}");
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
                                var userFullLink = AppConfig.CompanyDetailUserBaseUrl + userCode;

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

                                employeeCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.WriteLine($"Ошибка при обработке сотрудника: {ex.Message}");
                            }
                        }
                    }

                    if (employeeCount > 0)
                    {
                        _logger.WriteLine($"Найдено сотрудников: {employeeCount}");
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
                                var companyUrl = AppConfig.CompanyDetailCompanyBaseUrl + companyCode;

                                // Сохраняем в БД (code = код, url = полная ссылка, title = название)
                                _db.EnqueueCompany(companyCode, companyUrl, companyTitle: companyName);

                                relatedCompanyCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.WriteLine($"Ошибка при обработке связанной компании: {ex.Message}");
                            }
                        }
                    }

                    if (relatedCompanyCount > 0)
                    {
                        _logger.WriteLine($"Найдено связанных компаний: {relatedCompanyCount}");
                    }

                    // Извлекаем навыки компании
                    var skills = new List<string>();
                    var skillsContainer = doc.QuerySelector(AppConfig.CompanyDetailSkillsContainerSelector);
                    if (skillsContainer != null)
                    {
                        var skillElements = skillsContainer.QuerySelectorAll(AppConfig.CompanyDetailSkillSelector);
                        foreach (var skillElement in skillElements)
                        {
                            var skillTitle = skillElement.TextContent?.Trim();
                            if (!string.IsNullOrWhiteSpace(skillTitle))
                            {
                                skills.Add(skillTitle);
                            }
                        }
                    }

                    if (skills.Count > 0)
                    {
                        _logger.WriteLine($"Найдено навыков: {skills.Count}");
                        _db.EnqueueCompanySkills(code, skills);
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
                            _logger.WriteLine($"Компания ведет блог на Хабре");
                            break;
                        }
                    }

                    // Сохраняем company_id, url, title, about, description, site, rating, employees, followers, employees_count и habr в БД
                    _db.EnqueueCompanyDetails(code, url, companyId, companyTitle, companyAbout, companyDescription,
                        companySite, companyRating, currentEmployees, pastEmployees, followers, wantWork,
                        employeesCount, habr);

                    // Детальный вывод всех спарсенных данных
                    _logger.WriteLine($"=== Компания {code} ===");
                    _logger.WriteLine($"  Company ID: {companyId}");
                    _logger.WriteLine($"  Название: {companyTitle ?? "(не найдено)"}");
                    _logger.WriteLine(
                        $"  Описание (about): {(companyAbout != null ? (companyAbout.Length > 100 ? companyAbout.Substring(0, 100) + "..." : companyAbout) : "(не найдено)")}");
                    _logger.WriteLine(
                        $"  Детальное описание: {(companyDescription != null ? (companyDescription.Length > 100 ? companyDescription.Substring(0, 100) + "..." : companyDescription) : "(не найдено)")}");
                    _logger.WriteLine($"  Сайт: {companySite ?? "(не найдено)"}");
                    _logger.WriteLine($"  Рейтинг: {companyRating?.ToString("F2") ?? "(не найдено)"}");
                    _logger.WriteLine($"  Текущие сотрудники: {currentEmployees?.ToString() ?? "(не найдено)"}");
                    _logger.WriteLine($"  Все сотрудники: {pastEmployees?.ToString() ?? "(не найдено)"}");
                    _logger.WriteLine($"  Подписчики: {followers?.ToString() ?? "(не найдено)"}");
                    _logger.WriteLine($"  Хотят работать: {wantWork?.ToString() ?? "(не найдено)"}");
                    _logger.WriteLine($"  Размер компании: {employeesCount ?? "(не найдено)"}");
                    _logger.WriteLine($"  Блог на Хабре: {(habr == true ? "Да" : "Нет")}");
                    _logger.WriteLine($"  Навыков: {skills.Count}");
                    _logger.WriteLine($"  Контактных лиц: {memberCount}");
                    _logger.WriteLine($"  Сотрудников в списке: {employeeCount}");
                    _logger.WriteLine($"  Связанных компаний: {relatedCompanyCount}");

                    _statistics.IncrementSuccess();
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"Ошибка при обработке компании {code}: {ex.Message}");
                    _statistics.IncrementFailed();
                }
                finally
                {
                    _activeRequests.TryRemove(code, out _);
                }
            },
            controller: _controller,
            ct: ct
        );

        _statistics.EndTime = DateTime.Now;
        _logger.WriteLine($"Обход завершён. {_statistics}");
    }
}