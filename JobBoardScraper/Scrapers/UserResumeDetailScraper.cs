using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Throttling;
using JobBoardScraper.Infrastructure.Utils;
using JobBoardScraper.Infrastructure.Proxy;
using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Core;
using JobBoardScraper.Data;
using JobBoardScraper.Domain.Models;
using System.Collections.Concurrent;
using JobBoardScraper.Parsing;

namespace JobBoardScraper.Scrapers;

/// <summary>
/// Обходит страницы профилей пользователей и извлекает детальную информацию о резюме
/// Извлекает: about, навыки, опыт работы
/// TODO начинет сыпать ошибкой "Вы исчерпали суточный лимит на просмотр профилей специалистов. Зарегистрируйтесь или войдите в свой аккаунт, чтобы увидеть больше профилей."
/// смотреть через selenium
/// TODO версионность сделать
/// </summary>
public sealed class UserResumeDetailScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly Func<List<string>> _getUserCodes;
    private readonly AdaptiveConcurrencyController _adaptiveConcurrencyController;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly ConcurrentDictionary<string, Task> _activeRequests = new();
    private readonly ScraperStatistics _statistics;
    private readonly ProxyCoordinator? _proxyCoordinator;
    private readonly ProxyRetryExecutor _retryExecutor;
    private ProgressTracker? _progress;

    public UserResumeDetailScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        Func<List<string>> getUserCodes,
        AdaptiveConcurrencyController controller,
        ProxyCoordinator? proxyCoordinator = null,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _getUserCodes = getUserCodes ?? throw new ArgumentNullException(nameof(getUserCodes));
        _adaptiveConcurrencyController = controller ?? throw new ArgumentNullException(nameof(controller));
        _proxyCoordinator = proxyCoordinator;
        _interval = interval ?? TimeSpan.FromDays(30);
        _statistics = new ScraperStatistics("UserResumeDetailScraper");

        _logger = new ConsoleLogger("UserResumeDetailScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация UserResumeDetailScraper с режимом вывода: {outputMode}");

        if (_proxyCoordinator != null)
        {
            _logger.WriteLine($"Proxy coordinator enabled: {_proxyCoordinator.GetStatus()}");
        }

        // Инициализируем инфраструктуру прокси/retry в одном месте.
        // Вся логика выбора прокси, ожидания, retry по 5xx/408/429, обработки 403 и т.д.
        // инкапсулирована в ProxyRetryExecutor и ProxyHttpClientFactory.
        var clientFactory = new ProxyHttpClientFactory(logger: _logger);
        _retryExecutor = new ProxyRetryExecutor(clientFactory, logger: _logger);
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
            // Stop is OK
        }
    }

    private async Task RunOnceSafe(CancellationToken ct)
    {
        try
        {
            await ScrapeAllUserResumesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Stop is OK
        }
        catch (Exception ex)
        {
            ScraperLogger.LogError(_logger, ex);
        }
    }

    private async Task ScrapeAllUserResumesAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода резюме пользователей...");

        var userLinks = _getUserCodes();
        var totalLinks = userLinks.Count;
        _statistics.SetInitialRecordCount(totalLinks);

        // Используем ProgressTracker для отслеживания прогресса
        _progress = new ProgressTracker(totalLinks, "UserResumeDetail");

        _logger.WriteLine($"Загружено {totalLinks} пользователей из БД.");

        if (totalLinks == 0)
        {
            _logger.WriteLine("Нет пользователей для обработки.");
            return;
        }

        await AdaptiveForEach.ForEachAdaptiveAsync(
            source: userLinks,
            body: async userLink =>
        {
            _activeRequests.TryAdd(userLink, Task.CurrentId.HasValue ? Task.FromResult(Task.CurrentId.Value) : Task.CompletedTask);
            try
            {
                await ProcessUserAsync(userLink, ct);
            }
            finally
            {
                _activeRequests.TryRemove(userLink, out _);
            }
        },
        controller: _adaptiveConcurrencyController,
        ct: ct
        );

        _statistics.EndTime = DateTime.Now;
        ScraperLogger.LogEnd(_logger, _statistics);

        _statistics.WriteToLogFile();
    }

    /// <summary>
    /// Обработка одного пользователя: HTTP-запрос с retry/переключением прокси,
    /// парсинг HTML и сохранение в БД.
    /// </summary>
    private async Task ProcessUserAsync(string userLink, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 1) HTTP-запрос с retry/proxy через инкапсулированный executor.
        //    Вся "прокси" механика (ожидание, ретраи, 403/5xx/408/429, backoff, переключение) — в нём.
        var result = await _retryExecutor.ExecuteAsync(
            url: userLink,
            coordinator: _proxyCoordinator,
            fallbackSend: () => _httpClient.GetAsync(userLink, ct),
            proxySend: client => client.GetAsync(userLink, ct),
            ct: ct).ConfigureAwait(false);

        if (result.Response == null)
        {
            _statistics.IncrementFailed();
            return;
        }

        var proxyUrl = result.ProxyUrl;
        var response = result.Response;

        try
        {
            _statistics.RecordAllStatusCodes((int)response.StatusCode);

            sw.Stop();
            _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);

            double elapsedSeconds = sw.Elapsed.TotalSeconds;
            _statistics.RecordFinalStatusCode((int)response.StatusCode);
            _statistics.IncrementProcessed();
            _statistics.UpdateActiveRequests(_activeRequests.Count);
            _progress?.Increment();

            if (_progress != null)
            {
                ParallelScraperLogger.LogProgress(
                    _logger,
                    _statistics,
                    userLink,
                    elapsedSeconds,
                    (int)response.StatusCode,
                    _progress);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.WriteLine($"Пользователь {userLink}:");
                _logger.WriteLine($"  Статус: страница не найдена (404) — пропуск без записи в БД");
                _statistics.IncrementSkipped();
                return;
            }

            var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            var encoding = response.Content.Headers.ContentType?.CharSet != null
                ? System.Text.Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet)
                : System.Text.Encoding.UTF8;
            var html = encoding.GetString(htmlBytes);

            if (IsDeletedProfile(html))
            {
                const string deletedTitle = "Профиль удален";
                const string deletedAbout = "Профиль пользователя удален со всей информацией, которую он о себе оставлял";
                _db.EnqueueResume(
                    link: userLink,
                    title: deletedTitle,
                    mode: InsertMode.UpdateIfExists,
                    isDeleted: true,
                    about: deletedAbout);
                ScraperLogger.LogEnqueue(_logger, userLink, userLink, "| deleted");

                ProxyRetryExecutor.ReportSuccessSafe(_proxyCoordinator, proxyUrl);
                _statistics.IncrementSuccess();
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _statistics.IncrementFailed();
                return;
            }

            // Проверяем на приватный профиль сразу в HTML
            // Проверяем несколько признаков приватного профиля:
            // 1. Текст "Доступ ограничен настройками приватности"
            // 2. Текст "Информация скрыта"
            // 3. CSS класс "user-page-sidebar--status-hidden"
            const string privateProfileText1 = "Доступ ограничен настройками приватности";
            const string privateProfileText2 = "Информация скрыта";
            const string privateProfileClass = "user-page-sidebar--status-hidden";

            bool isPrivateProfile = html.Contains(privateProfileText1) ||
                                    html.Contains(privateProfileText2) ||
                                    html.Contains(privateProfileClass);

            if (isPrivateProfile)
            {
                // Профиль приватный - сохраняем статус и переходим к следующему
                const string privateMessage = "Доступ ограничен настройками приватности";
                _db.EnqueueResume(
                    link: userLink,
                    title: string.Empty,
                    mode: InsertMode.UpdateIfExists,
                    about: privateMessage,
                    isPublic: false);
                ScraperLogger.LogEnqueue(_logger, userLink, userLink, "| private");

                ProxyRetryExecutor.ReportSuccessSafe(_proxyCoordinator, proxyUrl);
                _statistics.IncrementSuccess();
                return;
            }

            // Проверяем на сообщение о суточном лимите
            if (HtmlParser.ContainsDailyLimitMessage(html))
            {
                _logger.WriteLine($"Обнаружен суточный лимит для прокси: {proxyUrl}");

                ProxyRetryExecutor.ReportDailyLimitSafe(_proxyCoordinator, proxyUrl);

                var newProxy = _proxyCoordinator?.GetNextProxy();
                if (newProxy != null)
                {
                    ScraperLogger.LogSkip(_logger, $"Переключение на новый прокси: {newProxy}");
                    // Не сохраняем результат, пропускаем этот профиль для повторной обработки
                    return;
                }

                ScraperLogger.LogSkip(_logger, $"Нет доступных прокси, пропускаем профиль: {userLink}");
                _statistics.IncrementSkipped();
                return;
            }

            await ParseAndSaveAsync(userLink, html, ct).ConfigureAwait(false);

            ProxyRetryExecutor.ReportSuccessSafe(_proxyCoordinator, proxyUrl);
            _statistics.IncrementSuccess();
        }
        finally
        {
            response.Dispose();
        }
    }

    /// <summary>
    /// Парсит HTML ответа и сохраняет данные резюме в БД.
    /// </summary>
    private async Task ParseAndSaveAsync(string userLink, string html, CancellationToken ct)
    {
        var doc = await HtmlParser.ParseDocumentAsync(html, ct).ConfigureAwait(false);

        // Извлекаем имя пользователя
        var userName = ProfileDataExtractor.ExtractUserName(doc);

        // Извлекаем техническую информацию и уровень
        var (infoTech, levelTitle) = ProfileDataExtractor.ExtractInfoTechAndLevel(doc);

        // Извлекаем зарплату и статус поиска работы
        var (salary, jobSearchStatus) = ProfileDataExtractor.ExtractSalaryAndJobStatus(doc);

        // Извлекаем текст "О себе"
        string? about = ExtractAboutSection(doc);

        // Извлекаем навыки
        var skills = ExtractSkills(doc);

        // Извлекаем опыт работы
        var (experienceCount, userExperiences) = ExtractExperience(doc, userLink);

        // Извлекаем дополнительные данные профиля
        var (age, experienceText, registration, lastVisit, citizenship, remoteWork) =
            ProfileDataExtractor.ExtractAdditionalProfileData(doc);

        // Извлекаем данные о высшем образовании
        var (educationCount, userUniversities) = ExtractEducation(doc, userLink);

        // Извлекаем данные о дополнительном образовании
        var (additionalEducationCount, additionalEducations) = ExtractAdditionalEducation(doc, userLink);

        // Извлекаем данные об участии в профсообществах
        var communityParticipation = ProfileDataExtractor.ExtractCommunityParticipationRecords(doc);

        // Определяем, является ли профиль пустым
        bool isEmpty = ComputeIsEmpty(about, experienceCount, educationCount, additionalEducationCount, communityParticipation);
        if (isEmpty)
        {
            about = "Пустой профиль";
        }

        // Сохраняем информацию для публичного профиля
        _db.EnqueueResume(
            link: userLink,
            title: userName ?? string.Empty,
            mode: InsertMode.UpdateIfExists,
            userName: userName,
            levelTitle: levelTitle,
            infoTech: infoTech,
            salary: salary,
            lastVisit: lastVisit,
            workExperience: experienceText,
            age: age,
            registration: registration,
            citizenship: citizenship,
            remoteWork: remoteWork,
            jobSearchStatus: jobSearchStatus,
            isEmpty: isEmpty,
            skills: skills?
                .Where(skill => !string.IsNullOrWhiteSpace(skill))
                .Select(skill => new SkillsRecord(SkillId: null, SkillTitle: skill.Trim()))
                .ToList(),
            about: about,
            communityParticipation: communityParticipation,
            userExperience: userExperiences,
            userUniversities: userUniversities,
            additionalEducations: additionalEducations,
            isPublic: true);

        ScraperLogger.LogEnqueue(
            _logger,
            entityType: "Resume",
            entityId: userLink,
            ("Name", userName ?? "(не найдено)"),
            ("InfoTech", infoTech ?? "(не найдено)"),
            ("Level", levelTitle ?? "(не найдено)"),
            ("Salary", salary.HasValue ? $"{salary.Value} ₽" : "(не найдено)"),
            ("JobSearchStatus", jobSearchStatus ?? "(не найдено)"),
            ("About", string.IsNullOrWhiteSpace(about) ? "(не найдено)" : about),
            ("Skills", skills),
            ("Experience", $"{experienceCount} записей"),
            ("Age", age ?? "(не найдено)"),
            ("ExperienceText", experienceText ?? "(не найдено)"),
            ("Registration", registration ?? "(не найдено)"),
            ("LastVisit", lastVisit ?? "(не найдено)"),
            ("Citizenship", citizenship ?? "(не найдено)"),
            ("RemoteWork", remoteWork.HasValue ? (remoteWork.Value ? "Да" : "Нет") : "(не найдено)"),
            ("Education", $"{educationCount} записей"),
            ("AdditionalEducation", $"{additionalEducationCount} записей"),
            ("CommunityParticipation", communityParticipation),
            ("IsPublic", true));
    }

    /// <summary>
    /// Извлекает текст "О себе" из секции с заголовком "Обо мне".
    /// </summary>
    private static string? ExtractAboutSection(AngleSharp.Html.Dom.IHtmlDocument doc)
    {
        var contentSections = doc.QuerySelectorAll(AppConfig.UserResumeDetailContentSelector);
        foreach (var section in contentSections)
        {
            var titleElement = section.QuerySelector(".content-section__title");
            var titleText = titleElement?.TextContent?.Trim();
            if (titleText != null && titleText.Contains("Обо мне", StringComparison.OrdinalIgnoreCase))
            {
                var ugcContent = section.QuerySelector(".style-ugc");
                if (ugcContent != null)
                {
                    return NormalizeHtmlToText(ugcContent.InnerHtml);
                }
                break;
            }
        }
        return null;
    }

    /// <summary>
    /// Преобразует HTML-фрагмент в читаемый текст с сохранением переносов строк.
    /// </summary>
    private static string NormalizeHtmlToText(string html)
    {
        var text = html;
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<br\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"</p>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"</li>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    /// <summary>
    /// Извлекает список навыков из профиля.
    /// </summary>
    private static List<string> ExtractSkills(AngleSharp.Html.Dom.IHtmlDocument doc)
    {
        var skills = new List<string>();
        var skillElements = doc.QuerySelectorAll(AppConfig.UserResumeDetailSkillSelector);
        foreach (var skillElement in skillElements)
        {
            var skillTitle = skillElement.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(skillTitle))
                skills.Add(skillTitle);
        }
        return skills;
    }

    /// <summary>
    /// Извлекает данные об опыте работы и возвращает количество записей и список.
    /// </summary>
    private static (int Count, List<UserExperienceRecord> Experiences) ExtractExperience(
        AngleSharp.Html.Dom.IHtmlDocument doc, string userLink)
    {
        var experiences = new List<UserExperienceRecord>();
        var experienceContainer = doc.QuerySelector(AppConfig.UserResumeDetailExperienceContainerSelector);
        if (experienceContainer == null)
            return (0, experiences);

        var experienceItems = experienceContainer.QuerySelectorAll(AppConfig.UserResumeDetailExperienceItemSelector);
        var isFirst = true;
        foreach (var item in experienceItems)
        {
            try
            {
                experiences.Add(BuildExperienceRecord(item, userLink, isFirst));
                isFirst = false;
            }
            catch
            {
                // Подавляем ошибки парсинга отдельных записей — они не критичны для всего профиля.
            }
        }
        return (experiences.Count, experiences);
    }

    private static UserExperienceRecord BuildExperienceRecord(
        AngleSharp.Dom.IElement item, string userLink, bool isFirst)
    {
        string? companyCode = null;
        string? companyUrl = null;
        string? companyTitle = null;

        var companyLink = item.QuerySelector(AppConfig.UserResumeDetailCompanyLinkSelector);
        if (companyLink != null)
        {
            companyUrl = companyLink.GetAttribute("href");
            companyTitle = companyLink.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(companyUrl))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    companyUrl, AppConfig.UserResumeDetailCompanyCodeRegex);
                if (match.Success)
                {
                    companyCode = match.Groups[1].Value;
                    companyUrl = string.Format(AppConfig.UserResumeDetailCompanyUrlTemplate, companyCode);
                }
            }
        }

        string? companyAbout = item.QuerySelector(AppConfig.UserResumeDetailCompanyAboutSelector)?.TextContent?.Trim();

        string? companySize = null;
        foreach (var link in item.QuerySelectorAll(AppConfig.UserResumeDetailCompanyLinkSelector))
        {
            var href = link.GetAttribute("href");
            if (!string.IsNullOrWhiteSpace(href) && href.Contains(AppConfig.UserResumeDetailCompanySizeUrlPattern))
            {
                companySize = link.TextContent?.Trim();
                break;
            }
        }

        string? position = item.QuerySelector(AppConfig.UserResumeDetailPositionSelector)?.TextContent?.Trim();
        if (!string.IsNullOrWhiteSpace(position))
        {
            position = System.Text.RegularExpressions.Regex.Replace(position, @"\s+", " ");
        }

        string? duration = item.QuerySelector(AppConfig.UserResumeDetailDurationSelector)?.TextContent?.Trim();
        string? description = item.QuerySelector(AppConfig.UserResumeDetailDescriptionSelector)?.TextContent?.Trim();

        var experienceSkills = new List<SkillsRecord>();
        var tagsContainer = item.QuerySelector(AppConfig.UserResumeDetailTagsSelector);
        if (tagsContainer != null)
        {
            foreach (var skillLink in tagsContainer.QuerySelectorAll(AppConfig.UserResumeDetailCompanyLinkSelector))
            {
                var skillName = skillLink.TextContent?.Trim();
                if (string.IsNullOrWhiteSpace(skillName)) continue;

                int? skillId = null;
                var skillHref = skillLink.GetAttribute("href");
                if (!string.IsNullOrWhiteSpace(skillHref))
                {
                    var skillMatch = System.Text.RegularExpressions.Regex.Match(skillHref, AppConfig.UserResumeDetailSkillIdRegex);
                    if (skillMatch.Success && int.TryParse(skillMatch.Groups[1].Value, out var id))
                        skillId = id;
                }
                experienceSkills.Add(new SkillsRecord(SkillId: skillId, SkillTitle: skillName));
            }
        }

        return new UserExperienceRecord(
            UserLink: userLink,
            Company: new CompanyRecord(
                CompanyCode: companyCode ?? string.Empty,
                CompanyUrl: companyUrl ?? string.Empty,
                CompanyTitle: companyTitle,
                About: companyAbout,
                EmployeesCount: companySize),
            Position: position,
            Duration: duration,
            Description: description,
            Skills: experienceSkills,
            IsFirstRecord: isFirst);
    }

    /// <summary>
    /// Извлекает данные о высшем образовании.
    /// </summary>
    private static (int Count, List<UserUniversityRecord> Universities) ExtractEducation(
        AngleSharp.Html.Dom.IHtmlDocument doc, string userLink)
    {
        var educationData = ProfileDataExtractor.ExtractEducationData(doc);
        var universities = new List<UserUniversityRecord>();
        foreach (var education in educationData)
        {
            universities.Add(new UserUniversityRecord(
                UserLink: userLink,
                University: new UniversityRecord(
                    HabrId: education.University.HabrId,
                    Name: education.University.Name,
                    City: education.University.City,
                    GraduateCount: education.University.GraduateCount),
                Courses: education.Courses,
                Description: education.Description));
        }
        return (universities.Count, universities);
    }

    /// <summary>
    /// Извлекает данные о дополнительном образовании.
    /// </summary>
    private static (int Count, List<AdditionalEducationRecord> Educations) ExtractAdditionalEducation(
        AngleSharp.Html.Dom.IHtmlDocument doc, string userLink)
    {
        var data = ProfileDataExtractor.ExtractAdditionalEducationData(doc, userLink);
        var educations = new List<AdditionalEducationRecord>(data.Count);
        foreach (var item in data)
        {
            educations.Add(new AdditionalEducationRecord(
                UserLink: item.UserLink,
                Title: item.Title,
                Course: item.Course,
                Duration: item.Duration));
        }
        return (educations.Count, educations);
    }

    /// <summary>
    /// Определяет, является ли профиль пустым (нет ни одного блока данных).
    /// </summary>

    private static bool ComputeIsEmpty(
        string? about,
        int experienceCount,
        int educationCount,
        int additionalEducationCount,
        System.Collections.Generic.List<CommunityParticipationData>? communityParticipation)
    {
        bool isServiceMessage = !string.IsNullOrWhiteSpace(about) &&
                                (about == "Доступ ограничен настройками приватности" || about == "Ошибка 404");
        return !isServiceMessage
            && string.IsNullOrWhiteSpace(about)
            && experienceCount == 0
            && educationCount == 0
            && additionalEducationCount == 0
            && (communityParticipation == null || communityParticipation.Count == 0);
    }

    private static bool IsDeletedProfile(string html)
    {
        const string deletedMarker1 = "Профиль удален";
        const string deletedMarker2 = "user-profile__deleted";
        const string deletedMarker3 = "Страница удалена";
        return html.Contains(deletedMarker1) ||
               html.Contains(deletedMarker2) ||
               html.Contains(deletedMarker3);
    }
}
