using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Helper.Http;
using JobBoardScraper.Helper.Utils;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Обходит профили пользователей и извлекает детальную информацию
/// TODO нужен selenium, некоторые профили закрыты настройками приватности
/// </summary>
public sealed class UserProfileScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly Func<List<string>> _getUserCodes;
    private readonly AdaptiveConcurrencyController _controller;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly Regex _salaryRegex;
    private readonly Regex _workExperienceRegex;
    private readonly Regex _lastVisitRegex;
    private readonly ConcurrentDictionary<string, Task> _activeRequests = new();
    private readonly Models.ScraperStatistics _statistics;
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
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _interval = interval ?? TimeSpan.FromDays(30);
        _salaryRegex = new Regex(AppConfig.UserProfileSalaryRegex, RegexOptions.Compiled);
        _workExperienceRegex = new Regex(AppConfig.UserProfileWorkExperienceRegex, RegexOptions.Compiled);
        _lastVisitRegex = new Regex(AppConfig.UserProfileLastVisitRegex, RegexOptions.Compiled);
        _statistics = new Models.ScraperStatistics("UserProfileScraper");
        
        _logger = new ConsoleLogger("UserProfileScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация UserProfileScraper с режимом вывода: {outputMode}");
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
            _logger.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    private async Task ScrapeAllUserProfilesAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода профилей пользователей...");
        
        // Получаем список ссылок пользователей из БД
        var userLinks = _getUserCodes();
        var totalLinks = userLinks.Count;
        
        // Используем ProgressTracker для отслеживания прогресса
        _progress = new ProgressTracker(totalLinks, "UserProfiles");
        
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
                try
                {
                    // Извлекаем userCode из ссылки (например, из "https://career.habr.com/username" получаем "username")
                    var userCode = userLink.TrimEnd('/').Split('/').LastOrDefault();
                    if (string.IsNullOrWhiteSpace(userCode))
                    {
                        _logger.WriteLine($"Не удалось извлечь код пользователя из ссылки: {userLink}. Пропуск.");
                        _statistics.IncrementSkipped();
                        _statistics.IncrementProcessed();
                        return;
                    }

                    // Формируем URL для /friends, добавляя /friends к исходной ссылке
                    var friendsUrl = userLink.TrimEnd('/') + "/friends";

                    _activeRequests.TryAdd(userLink, Task.CurrentId.HasValue ? Task.FromResult(Task.CurrentId.Value) : Task.CompletedTask);
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(friendsUrl, ct);
                    sw.Stop();
                    _controller.ReportLatency(sw.Elapsed);
                    
                    double elapsedSeconds = sw.Elapsed.TotalSeconds;
                    _statistics.IncrementProcessed();
                    _statistics.UpdateActiveRequests(_activeRequests.Count);
                    _progress?.Increment();
                    
                    if (_progress != null)
                    {
                        ParallelScraperLogger.LogProgress(
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
                    var encoding = response.Content.Headers.ContentType?.CharSet != null
                        ? System.Text.Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet)
                        : System.Text.Encoding.UTF8;
                    var html = encoding.GetString(htmlBytes);
                    
                    // Сохраняем HTML в файл для отладки (если включено)
                    if (AppConfig.UserProfileSaveHtml)
                    {
                        var savedPath = await HtmlDebug.SaveHtmlAsync(
                            html, 
                            "UserProfileScraper", 
                            "last_page.html",
                            encoding: encoding,
                            ct: ct);
                        
                        if (savedPath != null)
                        {
                            _logger.WriteLine($"HTML сохранён: {savedPath} (кодировка: {encoding.WebName})");
                        }
                    }

                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                    // Извлекаем имя пользователя и определяем публичность профиля
                    var userName = Helper.Dom.ProfileDataExtractor.ExtractUserName(doc, AppConfig.UserProfilePageTitleSelector);
                    bool isPublic = !string.IsNullOrWhiteSpace(userName);

                    // Если профиль приватный (редирект на главную), сохраняем только флаг и продолжаем
                    if (!isPublic)
                    {
                        _logger.WriteLine($"Пользователь {userLink}: Приватный профиль (редирект)");
                        _db.EnqueueUserProfile(userLink, userCode, null, null, null, null, null, null, null, false);
                        _statistics.IncrementSuccess();
                        return;
                    }

                    // Проверяем, является ли пользователь экспертом
                    bool? isExpert = null;
                    var expertElement = doc.QuerySelector(AppConfig.UserProfileExpertSelector);
                    if (expertElement != null)
                    {
                        isExpert = true;
                    }

                    // Извлекаем уровень и техническую информацию
                    var (infoTech, levelTitle) = Helper.Dom.ProfileDataExtractor.ExtractInfoTechAndLevel(
                        doc,
                        AppConfig.UserProfileMetaSelector,
                        AppConfig.UserProfileInlineListSelector);

                    // Извлекаем зарплату и статус поиска работы
                    var (salary, jobSearchStatus) = Helper.Dom.ProfileDataExtractor.ExtractSalaryAndJobStatus(
                        doc,
                        AppConfig.UserProfileCareerSelector,
                        AppConfig.UserProfileSalaryRegex);

                    // Извлекаем опыт работы и последний визит из всех секций .basic-section
                    var (workExperience, lastVisit) = Helper.Dom.ProfileDataExtractor.ExtractWorkExperienceAndLastVisit(
                        doc, 
                        AppConfig.UserProfileBasicSectionSelector);

                    // Сохраняем информацию о пользователе (публичный профиль)
                    _db.EnqueueUserProfile(
                        userLink,
                        userCode,
                        userName, 
                        isExpert, 
                        levelTitle, 
                        infoTech, 
                        salary, 
                        workExperience, 
                        lastVisit, 
                        true
                    );
                    
                    _logger.WriteLine($"Пользователь {userLink} (code={userCode}):");
                    _logger.WriteLine($"  Имя: {userName ?? "(не найдено)"}");
                    _logger.WriteLine($"  Эксперт: {(isExpert == true ? "Да" : "Нет")}");
                    _logger.WriteLine($"  Уровень: {levelTitle ?? "(не найдено)"}");
                    _logger.WriteLine($"  Техническая информация: {infoTech ?? "(не найдено)"}");
                    _logger.WriteLine($"  Зарплата: {(salary.HasValue ? $"{salary.Value} ₽" : "(не найдено)")}");
                    _logger.WriteLine($"  Опыт работы: {workExperience ?? "(не найдено)"}");
                    _logger.WriteLine($"  Последний визит: {lastVisit ?? "(не найдено)"}");
                    _logger.WriteLine($"  Публичный профиль: Да");
                    
                    _statistics.IncrementSuccess();
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"Ошибка при обработке пользователя {userLink}: {ex.Message}");
                    _statistics.IncrementFailed();
                }
                finally
                {
                    _activeRequests.TryRemove(userLink, out _);
                }
            },
            controller: _controller,
            ct: ct
        );
        
        _statistics.EndTime = DateTime.Now;
        _logger.WriteLine($"Обход завершён. {_statistics}");
    }
}
