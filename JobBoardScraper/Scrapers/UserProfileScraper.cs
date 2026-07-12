using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Utils;
using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Infrastructure.Url;
using JobBoardScraper.Core;
using JobBoardScraper.Data;
using JobBoardScraper.Domain.Models;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using JobBoardScraper.Parsing;

namespace JobBoardScraper.Scrapers;

/// <summary>
/// Обходит профили пользователей и извлекает детальную информацию
/// TODO нужен selenium, некоторые профили закрыты настройками приватности
/// </summary>
public sealed class UserProfileScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly Func<List<string>> _getUserCodes;
    private readonly AdaptiveConcurrencyController _adaptiveConcurrencyController;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly Regex _salaryRegex;
    private readonly Regex _workExperienceRegex;
    private readonly Regex _lastVisitRegex;
    private readonly ConcurrentDictionary<string, Task> _activeRequests = new();
    private readonly ScraperStatistics _statistics;
    private ProgressTracker? _progress;

    public UserProfileScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        Func<List<string>> getUserCodes,
        AdaptiveConcurrencyController controller,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _getUserCodes = getUserCodes ?? throw new ArgumentNullException(nameof(getUserCodes));
        _adaptiveConcurrencyController = controller ?? throw new ArgumentNullException(nameof(controller));
        _interval = interval ?? TimeSpan.FromDays(30);
        _salaryRegex = new Regex(AppConfig.UserProfileSalaryRegex, RegexOptions.Compiled);
        _workExperienceRegex = new Regex(AppConfig.UserProfileWorkExperienceRegex, RegexOptions.Compiled);
        _lastVisitRegex = new Regex(AppConfig.UserProfileLastVisitRegex, RegexOptions.Compiled);
        _statistics = new ScraperStatistics("UserProfileScraper");

        _logger = new ConsoleLogger("UserProfileScraper");
        _logger.SetOutputMode(outputMode);
        ScraperLogger.LogInitialization(_logger, "UserProfileScraper", outputMode);
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
            await ScrapeAllUserProfilesAsync(ct);
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

    private async Task ScrapeAllUserProfilesAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало обхода профилей пользователей...");

        // Получаем список ссылок пользователей из БД
        var userLinks = _getUserCodes();
        var totalLinks = userLinks.Count;

        // Используем ProgressTracker для отслеживания прогресса
        _progress = new ProgressTracker(totalLinks, "UserProfiles");

        ScraperLogger.LogCount(_logger, "Загружено", totalLinks, "пользователей", " из БД");

        if (totalLinks == 0)
        {
            ScraperLogger.LogSkip(_logger, "Нет пользователей для обработки.");
            return;
        }

        await AdaptiveForEach.ForEachAdaptiveAsync(
            source: userLinks,
            body: async userLink =>
            {
                try
                {
                    // Извлекаем userCode из ссылки (например, из "https://career.habr.com/username" получаем "username")
                    var userCode = UrlManager.GetLastPathSegment(userLink);
                    if (string.IsNullOrWhiteSpace(userCode))
                    {
                        ScraperLogger.LogSkip(_logger, $"Не удалось извлечь код пользователя из ссылки: {userLink}. Пропуск.");
                        _statistics.IncrementSkipped();
                        _statistics.IncrementProcessed();
                        return;
                    }

                    // Формируем URL для /friends, добавляя /friends к исходной ссылке
                    var friendsUrl = UrlManager.BuildFriendsUrl(userLink);

                    _activeRequests.TryAdd(userLink, Task.CurrentId.HasValue ? Task.FromResult(Task.CurrentId.Value) : Task.CompletedTask);

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(friendsUrl, ct);
                    sw.Stop();
                    _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);

                    double elapsedSeconds = sw.Elapsed.TotalSeconds;
                    _statistics.IncrementProcessed();
                    _statistics.UpdateActiveRequests(_activeRequests.Count);
                    _progress?.Increment();

                    if (_progress != null)
                    {
                        ScraperParallelLogger.LogProgress(
                            _logger,
                            _statistics,
                            friendsUrl,
                            elapsedSeconds,
                            (int)response.StatusCode,
                            _progress);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _statistics.IncrementSkipped();
                        _activeRequests.TryRemove(userLink, out _);
                        return;
                    }

                    // Читаем HTML с правильной кодировкой
                    var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct);
                    var encoding = response.GetEncoding();
                    var html = response.DecodeBodyAsString(htmlBytes);

                    // Сохраняем HTML в файл для отладки (если включено)
                    if (AppConfig.UserProfileSaveHtml)
                    {
                        await HtmlDebug.SaveHtmlAsync(
                            html,
                            "UserProfileScraper",
                            _logger,
                            encoding: encoding,
                            ct: ct);
                    }

                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                    // Извлекаем имя пользователя и определяем публичность профиля
                    var (userName, isPublic) = UserDataExtractor.IsPublicProfile(
                        doc, AppConfig.UserProfilePageTitleSelector);

                    // Если профиль приватный (редирект на главную), сохраняем только флаг и продолжаем
                    if (!isPublic)
                    {
                        _db.EnqueueResume(
                            link: userLink,
                            title: "",
                            mode: InsertMode.UpdateIfExists,
                            code: userCode,
                            userCode: userCode,
                            isPublic: false);
                        ScraperLogger.LogEnqueue(
                            _logger,
                            "Resume",
                            userCode,
                            ("Link", userLink),
                            ("Code", userCode),
                            ("Type", "Private"));
                        _statistics.IncrementSuccess();
                        return;
                    }

                    // Проверяем, является ли пользователь экспертом
                    bool? isExpert = UserDataExtractor.IsExpertProfile(doc, AppConfig.UserProfileExpertSelector);

                    // Извлекаем уровень и техническую информацию
                    var (infoTech, levelTitle) = UserDataExtractor.ExtractInfoTechAndLevel(
                        doc,
                        AppConfig.UserProfileMetaSelector,
                        AppConfig.UserProfileInlineListSelector);

                    // Извлекаем зарплату и статус поиска работы
                    var (salary, jobSearchStatus) = UserDataExtractor.ExtractSalaryAndJobStatus(
                        doc,
                        AppConfig.UserProfileCareerSelector,
                        AppConfig.UserProfileSalaryRegex);

                    // Извлекаем опыт работы и последний визит из всех секций .basic-section
                    var (workExperience, lastVisit) = UserDataExtractor.ExtractWorkExperienceAndLastVisit(
                        doc,
                        AppConfig.UserProfileBasicSectionSelector);

                    // Сохраняем информацию о пользователе (публичный профиль)
                    _db.EnqueueResume(
                        link: userLink,
                        title: userName ?? "",
                        mode: InsertMode.UpdateIfExists,
                        code: userCode,
                        userCode: userCode,
                        userName: userName,
                        isExpert: isExpert,
                        levelTitle: levelTitle,
                        infoTech: infoTech,
                        salary: salary,
                        workExperience: workExperience,
                        lastVisit: lastVisit,
                        isPublic: true);

                    ScraperLogger.LogEnqueue(
                        _logger,
                        entityType: "UserProfile",
                        entityId: userLink,
                        ("Name", userName ?? "(не найдено)"),
                        ("Expert", (isExpert == true ? "Да" : "Нет")),
                        ("Level", levelTitle ?? "(не найдено)"),
                        ("InfoTech", infoTech ?? "(не найдено)"),
                        ("Salary", salary.HasValue ? $"{salary.Value} ₽" : "(не найдено)"),
                        ("WorkExperience", workExperience ?? "(не найдено)"),
                        ("LastVisit", lastVisit ?? "(не найдено)"),
                        ("IsPublic", "Да"),
                        ("Type", (isExpert == true ? "Expert" : "User")));

                    _statistics.IncrementSuccess();
                }
                catch (OperationCanceledException)
                {
                    ScraperLogger.LogOperationCanceled(_logger, $"профиль пользователя {userLink}");
                    throw;
                }
                catch (Exception ex)
                {
                    ScraperLogger.LogError(_logger, $"Ошибка при обработке пользователя {userLink}", ex);
                    _statistics.IncrementFailed();
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
    }
}