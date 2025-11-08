using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Helper.Utils;
using System.Text.RegularExpressions;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Обходит профили пользователей и извлекает детальную информацию
/// TODO нужен selenium, некоторые профили закрыты настройками приватности
/// TODO вывод информации надо сделать такой же, как в BruteForceUsernameScraper, то есть количество параллельных потоков и тд 
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
        _logger.WriteLine($"Загружено {userLinks.Count} пользователей из БД.");

        if (userLinks.Count == 0)
        {
            _logger.WriteLine("Нет пользователей для обработки.");
            return;
        }

        var totalProcessed = 0;
        var totalSuccess = 0;
        var totalFailed = 0;
        var totalSkipped = 0;

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
                        Interlocked.Increment(ref totalSkipped);
                        Interlocked.Increment(ref totalProcessed);
                        return;
                    }

                    // Формируем URL для /friends, добавляя /friends к исходной ссылке
                    var friendsUrl = userLink.TrimEnd('/') + "/friends";
                    _logger.WriteLine($"Обработка пользователя: {userLink} -> {friendsUrl}");

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(friendsUrl, ct);
                    sw.Stop();
                    _controller.ReportLatency(sw.Elapsed);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.WriteLine($"Пользователь {userCode} вернул код {response.StatusCode}. Пропуск.");
                        Interlocked.Increment(ref totalSkipped);
                        Interlocked.Increment(ref totalProcessed);
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
                    string? userName = null;
                    bool isPublic = false;
                    var pageTitleElement = doc.QuerySelector(AppConfig.UserProfilePageTitleSelector);
                    if (pageTitleElement != null)
                    {
                        userName = pageTitleElement.TextContent?.Trim();
                        // Если имя найдено, профиль публичный
                        if (!string.IsNullOrWhiteSpace(userName))
                        {
                            isPublic = true;
                        }
                    }

                    // Если профиль приватный (редирект на главную), сохраняем только флаг и продолжаем
                    if (!isPublic)
                    {
                        _logger.WriteLine($"Пользователь {userLink}: Приватный профиль (редирект)");
                        _db.EnqueueUserProfile(userLink, userCode, null, null, null, null, null, null, null, false);
                        Interlocked.Increment(ref totalSuccess);
                        Interlocked.Increment(ref totalProcessed);
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
                    string? levelTitle = null;
                    string? infoTech = null;
                    var metaElement = doc.QuerySelector(AppConfig.UserProfileMetaSelector);
                    if (metaElement != null)
                    {
                        var inlineList = metaElement.QuerySelector(AppConfig.UserProfileInlineListSelector);
                        if (inlineList != null)
                        {
                            var spans = inlineList.QuerySelectorAll("span > span:first-child");
                            var textParts = new List<string>();
                            
                            foreach (var span in spans)
                            {
                                var text = span.TextContent?.Trim();
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    textParts.Add(text);
                                }
                            }
                            
                            // Последний элемент - это уровень
                            if (textParts.Count > 0)
                            {
                                levelTitle = textParts[textParts.Count - 1];
                                
                                // Остальные элементы - техническая информация
                                if (textParts.Count > 1)
                                {
                                    infoTech = string.Join(" • ", textParts.Take(textParts.Count - 1));
                                }
                            }
                        }
                    }

                    // Извлекаем зарплату
                    int? salary = null;
                    var careerElement = doc.QuerySelector(AppConfig.UserProfileCareerSelector);
                    if (careerElement != null)
                    {
                        var careerText = careerElement.TextContent?.Trim();
                        if (!string.IsNullOrWhiteSpace(careerText))
                        {
                            var salaryMatch = _salaryRegex.Match(careerText);
                            if (salaryMatch.Success && salaryMatch.Groups.Count >= 2)
                            {
                                var salaryStr = salaryMatch.Groups[1].Value.Replace(" ", "");
                                if (int.TryParse(salaryStr, out var salaryValue))
                                {
                                    salary = salaryValue;
                                }
                            }
                        }
                    }

                    // Извлекаем опыт работы и последний визит из всех секций .basic-section
                    string? workExperience = null;
                    string? lastVisit = null;
                    var basicSectionElements = doc.QuerySelectorAll(AppConfig.UserProfileBasicSectionSelector);
                    foreach (var basicSectionElement in basicSectionElements)
                    {
                        // Ищем все div элементы в секции
                        var divElements = basicSectionElement.QuerySelectorAll("div");
                        foreach (var div in divElements)
                        {
                            var textContent = div.TextContent?.Trim();
                            if (string.IsNullOrWhiteSpace(textContent))
                                continue;
                            
                            // Проверяем на опыт работы
                            if (textContent.Contains("Опыт работы:"))
                            {
                                // Извлекаем текст после "Опыт работы:"
                                var parts = textContent.Split(new[] { "Опыт работы:" }, StringSplitOptions.None);
                                if (parts.Length > 1)
                                {
                                    workExperience = parts[1].Trim();
                                }
                            }
                            
                            // Проверяем на последний визит
                            if (textContent.Contains("Последний визит:"))
                            {
                                // Извлекаем текст после "Последний визит:"
                                var parts = textContent.Split(new[] { "Последний визит:" }, StringSplitOptions.None);
                                if (parts.Length > 1)
                                {
                                    lastVisit = parts[1].Trim();
                                }
                            }
                        }
                    }

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
                    
                    Interlocked.Increment(ref totalSuccess);
                    Interlocked.Increment(ref totalProcessed);
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"Ошибка при обработке пользователя {userLink}: {ex.Message}");
                    Interlocked.Increment(ref totalFailed);
                    Interlocked.Increment(ref totalProcessed);
                }
            },
            controller: _controller,
            ct: ct
        );
        
        _logger.WriteLine($"Обход завершён. Обработано: {totalProcessed}, успешно: {totalSuccess}, ошибок: {totalFailed}, пропущено: {totalSkipped}");
    }
}
