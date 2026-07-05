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
        ScraperLogger.LogStart(_logger, "Начало обхода резюме пользователей...");

        var userLinks = _getUserCodes();
        var totalLinks = userLinks.Count;
        _statistics.SetInitialRecordCount(totalLinks);

        // Используем ProgressTracker для отслеживания прогресса
        _progress = new ProgressTracker(totalLinks, "UserResumeDetail");

        ScraperLogger.LogCount(_logger, "Загружено", totalLinks, "пользователей", " из БД.");

        if (totalLinks == 0)
        {
            ScraperLogger.LogSkip(_logger, "Нет пользователей для обработки.");
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
                ScraperParallelLogger.LogProgress(
                    _logger,
                    _statistics,
                    userLink,
                    elapsedSeconds,
                    (int)response.StatusCode,
                    _progress);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                ScraperLogger.LogSkip(_logger, $"Пользователь {userLink}: страница не найдена (404) — пропуск без записи в БД");
                _statistics.IncrementSkipped();
                return;
            }

            var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            string html = response.DecodeBodyAsString(htmlBytes);

            if (!response.IsSuccessStatusCode)
            {
                _statistics.IncrementFailed();
                return;
            }

            // Парсим документ заранее, чтобы использовать его в проверках
            var doc = await HtmlParser.ParseDocumentAsync(html, ct).ConfigureAwait(false);

            if (UserDataExtractor.IsDeletedProfile(doc))
            {
                const string deletedTitle = "Профиль удален";
                const string deletedAbout = "Профиль пользователя удален со всей информацией, которую он о себе оставлял";
                _db.EnqueueResume(
                    link: userLink,
                    title: deletedTitle,
                    mode: InsertMode.UpdateIfExists,
                    isDeleted: true,
                    about: deletedAbout);
                ScraperLogger.LogEnqueue(
                    _logger,
                    "Resume",
                    userLink,
                    ("Link", userLink),
                    ("Status", "Deleted"));

                ProxyRetryExecutor.ReportSuccessSafe(_proxyCoordinator, proxyUrl);
                _statistics.IncrementSuccess();
                return;
            }

            // Проверяем на приватный профиль (доступ ограничен настройками приватности)
            if (UserDataExtractor.IsPrivateProfile(doc))
            {
                // Профиль приватный - сохраняем статус и переходим к следующему
                const string privateMessage = "Доступ ограничен настройками приватности";
                _db.EnqueueResume(
                    link: userLink,
                    title: string.Empty,
                    mode: InsertMode.UpdateIfExists,
                    about: privateMessage,
                    isPublic: false);
                ScraperLogger.LogEnqueue(
                    _logger,
                    "Resume",
                    userLink,
                    ("Link", userLink),
                    ("Status", "Private"));

                ProxyRetryExecutor.ReportSuccessSafe(_proxyCoordinator, proxyUrl);
                _statistics.IncrementSuccess();
                return;
            }

            // Проверяем на сообщение о суточном лимите
            if (HtmlParser.ContainsDailyLimitMessage(html))
            {
                bool hasNewProxy = ProxyRetryExecutor.HandleDailyLimit(_proxyCoordinator, proxyUrl, userLink, _logger);

                if (hasNewProxy)
                {
                    // Не сохраняем результат, пропускаем этот профиль для повторной обработки
                    return;
                }

                _statistics.IncrementSkipped();
                return;
            }

            await ParseAndSaveAsync(userLink, doc, ct).ConfigureAwait(false);

            ProxyRetryExecutor.ReportSuccessSafe(_proxyCoordinator, proxyUrl);
            _statistics.IncrementSuccess();
        }
        finally
        {
            response.Dispose();
        }
    }

    /// <summary>
    /// Извлекает данные из распарсенного документа и сохраняет данные резюме в БД.
    /// </summary>
    private Task ParseAndSaveAsync(string userLink, AngleSharp.Html.Dom.IHtmlDocument doc, CancellationToken ct)
    {
        ParseAndSave(userLink, doc, ct);
        return Task.CompletedTask;
    }

    private void ParseAndSave(string userLink, AngleSharp.Html.Dom.IHtmlDocument doc, CancellationToken ct)
    {
        _ = ct; // параметр оставлен для совместимости с прежней сигнатурой
        // Извлекаем имя пользователя
        var userName = UserDataExtractor.ExtractUserName(doc);

        // Извлекаем техническую информацию и уровень
        var (infoTech, levelTitle) = UserDataExtractor.ExtractInfoTechAndLevel(doc);

        // Извлекаем зарплату и статус поиска работы
        var (salary, jobSearchStatus) = UserDataExtractor.ExtractSalaryAndJobStatus(doc);

        // Извлекаем текст "О себе"
        string? about = UserDataExtractor.ExtractAboutSection(doc);

        // Извлекаем навыки
        var skills = UserDataExtractor.ExtractSkills(doc);

        // Извлекаем опыт работы
        var (experienceCount, userExperiences) = UserDataExtractor.ExtractExperience(doc, userLink);

        // Извлекаем дополнительные данные профиля
        var additionalProfile = UserDataExtractor.ExtractAdditionalProfileData(doc);

        // Извлекаем данные о высшем образовании
        var (educationCount, userUniversities) = UserDataExtractor.ExtractEducation(doc, userLink);

        // Извлекаем данные о дополнительном образовании
        var (additionalEducationCount, additionalEducations) = UserDataExtractor.ExtractAdditionalEducation(doc, userLink);

        // Извлекаем данные об участии в профсообществах
        var communityParticipation = UserDataExtractor.ExtractCommunityParticipationRecords(doc);

        // Определяем, является ли профиль пустым
        bool isEmpty = UserDataExtractor.IsEmptyProfile(doc);
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
            lastVisit: additionalProfile.LastVisit,
            workExperience: additionalProfile.WorkExperience,
            age: additionalProfile.Age,
            registration: additionalProfile.Registration,
            citizenship: additionalProfile.Citizenship,
            remoteWork: additionalProfile.RemoteWork,
            jobSearchStatus: jobSearchStatus,
            isEmpty: isEmpty,
            skills: skills,
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
            ("Age", additionalProfile.Age ?? "(не найдено)"),
            ("ExperienceText", additionalProfile.WorkExperience ?? "(не найдено)"),
            ("Registration", additionalProfile.Registration ?? "(не найдено)"),
            ("LastVisit", additionalProfile.LastVisit ?? "(не найдено)"),
            ("Citizenship", additionalProfile.Citizenship ?? "(не найдено)"),
            ("RemoteWork", additionalProfile.RemoteWork.HasValue ? (additionalProfile.RemoteWork.Value ? "Да" : "Нет") : "(не найдено)"),
            ("Education", $"{educationCount} записей"),
            ("AdditionalEducation", $"{additionalEducationCount} записей"),
            ("CommunityParticipation", communityParticipation),
            ("IsPublic", true));
    }

}
