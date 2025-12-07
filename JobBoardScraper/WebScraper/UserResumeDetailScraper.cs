using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Helper.Utils;
using JobBoardScraper.Models;
using System.Collections.Concurrent;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Обходит страницы профилей пользователей и извлекает детальную информацию о резюме
/// Извлекает: about, навыки, опыт работы
/// TODO начинет сыпать ошибкой "Вы исчерпали суточный лимит на просмотр профилей специалистов. Зарегистрируйтесь или войдите в свой аккаунт, чтобы увидеть больше профилей."
/// или смотреть через selenium или организовывать прокси 
/// TODO версионность сделать
/// </summary>
public sealed class UserResumeDetailScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly Func<List<string>> _getUserCodes;
    private readonly AdaptiveConcurrencyController _controller;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly ConcurrentDictionary<string, Task> _activeRequests = new();
    private readonly Models.ScraperStatistics _statistics;
    private readonly FreeProxyPool? _proxyPool;
    private readonly int _proxyWaitTimeout;

    public UserResumeDetailScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        Func<List<string>> getUserCodes,
        AdaptiveConcurrencyController controller,
        FreeProxyPool? proxyPool = null,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _getUserCodes = getUserCodes ?? throw new ArgumentNullException(nameof(getUserCodes));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _proxyPool = proxyPool;
        _interval = interval ?? TimeSpan.FromDays(30);
        _proxyWaitTimeout = AppConfig.ProxyWaitTimeoutSeconds;
        _statistics = new Models.ScraperStatistics("UserResumeDetailScraper");
        
        _logger = new ConsoleLogger("UserResumeDetailScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация UserResumeDetailScraper с режимом вывода: {outputMode}");
        
        if (_proxyPool != null)
        {
            _logger.WriteLine($"Proxy rotation enabled (pool size: {_proxyPool.GetCount()})");
        }
    }

    /// <summary>
    /// Get proxy from pool with timeout
    /// </summary>
    private async Task<string?> GetProxyWithTimeoutAsync(CancellationToken ct)
    {
        if (_proxyPool == null)
            return null;
        
        var proxy = _proxyPool.GetNextProxy();
        
        if (proxy != null)
            return proxy;
        
        // Pool is empty, wait for new proxies
        _logger.WriteLine($"Proxy pool empty, waiting up to {_proxyWaitTimeout} seconds...");
        
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(_proxyWaitTimeout);
        
        while ((DateTime.UtcNow - startTime) < timeout && !ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);
            
            proxy = _proxyPool.GetNextProxy();
            if (proxy != null)
            {
                _logger.WriteLine($"Proxy became available after waiting");
                return proxy;
            }
        }
        
        _logger.WriteLine($"Timeout waiting for proxy");
        return null;
    }
    
    /// <summary>
    /// Create HttpClient configured with proxy
    /// </summary>
    private HttpClient CreateHttpClientWithProxy(string proxyUrl)
    {
        var proxy = new System.Net.WebProxy(new Uri(proxyUrl));
        var handler = new HttpClientHandler
        {
            Proxy = proxy,
            UseProxy = true,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        
        var client = new HttpClient(handler)
        {
            Timeout = AppConfig.ProxyRequestTimeout // Используем увеличенный таймаут для прокси
        };
        
        return client;
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
            await ScrapeAllUserResumesAsync(ct);
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

    private async Task ScrapeAllUserResumesAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода резюме пользователей...");
        //TOOD поменять стратегию: использовать один прокси, если он работает, до тех пор, пока хабр не начлет отдавать сообщение "Вы исчерпали..."
        
        var userLinks = _getUserCodes();
        var totalLinks = userLinks.Count;
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
                HttpResponseMessage? response = null;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int attempt = 0;
                int maxRetries = _proxyPool != null ? AppConfig.ProxyMaxRetries : 1;
                
                // Retry loop with different proxies
                while (attempt < maxRetries)
                {
                    attempt++;
                    string? proxyUrl = null;
                    HttpClient? proxyHttpClient = null;
                    
                    try
                    {
                        // Get proxy from pool if available
                        if (_proxyPool != null)
                        {
                            proxyUrl = await GetProxyWithTimeoutAsync(ct);
                            
                            if (proxyUrl != null)
                            {
                                _logger.WriteLine($"Using proxy: {proxyUrl} (attempt {attempt}/{maxRetries})");
                                proxyHttpClient = CreateHttpClientWithProxy(proxyUrl);
                            }
                            else
                            {
                                _logger.WriteLine($"No proxy available, proceeding without proxy");
                            }
                        }
                        
                        // Make request
                        if (proxyHttpClient != null)
                        {
                            try
                            {
                                response = await proxyHttpClient.GetAsync(userLink, ct);
                            }
                            finally
                            {
                                proxyHttpClient.Dispose();
                            }
                        }
                        else
                        {
                            response = await _httpClient.GetAsync(userLink, ct);
                        }
                        
                        // Check for retryable HTTP errors
                        if (response != null)
                        {
                            var statusCode = (int)response.StatusCode;
                            bool shouldRetry = false;
                            int delayMs = 0;
                            string errorType = "";
                            
                            // Server errors (5xx) - retry with exponential backoff
                            if (statusCode >= 500 && statusCode < 600)
                            {
                                shouldRetry = true;
                                delayMs = ExponentialBackoff.CalculateServerErrorDelay(attempt);
                                errorType = "Server error";
                            }
                            // 403 Forbidden - likely IP blocked, change proxy immediately
                            else if (statusCode == 403)
                            {
                                shouldRetry = true;
                                delayMs = ExponentialBackoff.CalculateProxyErrorDelay(attempt);
                                errorType = "Forbidden (IP blocked)";
                            }
                            // 429 Too Many Requests - rate limited, need longer delay
                            else if (statusCode == 429)
                            {
                                shouldRetry = true;
                                // Check for Retry-After header
                                if (response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
                                {
                                    var retryAfter = retryAfterValues.FirstOrDefault();
                                    if (int.TryParse(retryAfter, out var seconds))
                                    {
                                        delayMs = seconds * 1000;
                                    }
                                    else
                                    {
                                        delayMs = ExponentialBackoff.CalculateServerErrorDelay(attempt) * 2; // Double delay for rate limiting
                                    }
                                }
                                else
                                {
                                    delayMs = ExponentialBackoff.CalculateServerErrorDelay(attempt) * 2;
                                }
                                errorType = "Rate limited";
                            }
                            // 408 Request Timeout - retry with same or different proxy
                            else if (statusCode == 408)
                            {
                                shouldRetry = true;
                                delayMs = ExponentialBackoff.CalculateProxyErrorDelay(attempt);
                                errorType = "Request timeout";
                            }
                            // 404 Not Found - don't retry, will be handled separately
                            else if (statusCode == 404)
                            {
                                shouldRetry = false;
                            }
                            
                            if (shouldRetry && attempt < maxRetries && _proxyPool != null)
                            {
                                _logger.WriteLine($"{errorType} {statusCode} (attempt {attempt}/{maxRetries}). " +
                                    $"Backoff delay: {ExponentialBackoff.GetDelayDescription(delayMs)}");
                                _logger.WriteLine($"Retrying with next proxy after delay...");
                                response.Dispose();
                                response = null;
                                await Task.Delay(delayMs, ct);
                                continue;
                            }
                            else if (shouldRetry)
                            {
                                _logger.WriteLine($"{errorType} {statusCode} (attempt {attempt}/{maxRetries}). " +
                                    $"No more retries available.");
                            }
                        }
                        
                        // Success (200) - break retry loop
                        if (response != null && response.IsSuccessStatusCode)
                        {
                            break;
                        }
                        
                        // Non-retryable error or no proxy pool - break retry loop
                        if (_proxyPool == null || attempt >= maxRetries)
                        {
                            break;
                        }
                        
                        // Any other non-success status - retry with next proxy (except 404)
                        if (response != null && !response.IsSuccessStatusCode && (int)response.StatusCode != 404)
                        {
                            var statusCode = (int)response.StatusCode;
                            var delayMs = ExponentialBackoff.CalculateProxyErrorDelay(attempt);
                            _logger.WriteLine($"HTTP error {statusCode} (attempt {attempt}/{maxRetries}). " +
                                $"Backoff delay: {ExponentialBackoff.GetDelayDescription(delayMs)}");
                            _logger.WriteLine($"Retrying with next proxy after delay...");
                            response.Dispose();
                            response = null;
                            await Task.Delay(delayMs, ct);
                            continue;
                        }
                    }
                    catch (Exception ex) when (attempt < maxRetries && _proxyPool != null)
                    {
                        // Calculate delay using exponential backoff with jitter
                        var proxyErrorDelay = ExponentialBackoff.CalculateProxyErrorDelay(attempt);
                        
                        _logger.WriteLine($"Proxy error (attempt {attempt}/{maxRetries}): {ex.Message}. " +
                            $"Backoff delay: {ExponentialBackoff.GetDelayDescription(proxyErrorDelay)}");
                        
                        if (attempt < maxRetries)
                        {
                            _logger.WriteLine($"Trying next proxy after delay...");
                            await Task.Delay(proxyErrorDelay, ct);
                        }
                        else
                        {
                            // Last attempt failed, rethrow
                            throw;
                        }
                    }
                }
                
                if (response == null)
                {
                    _logger.WriteLine($"Failed to get response after {maxRetries} attempts");
                    _activeRequests.TryRemove(userLink, out _);
                    return;
                }
                
                sw.Stop();
                _controller.ReportLatency(sw.Elapsed);
                
                double elapsedSeconds = sw.Elapsed.TotalSeconds;
                _statistics.IncrementProcessed();
                _statistics.UpdateActiveRequests(_activeRequests.Count);
                
                Helper.Utils.ParallelScraperLogger.LogProgress(
                    _logger,
                    _statistics,
                    userLink,
                    elapsedSeconds,
                    (int)response.StatusCode,
                    totalLinks);
                
                // Записываем статистику по коду ответа
                _statistics.RecordStatusCode((int)response.StatusCode);
                
                // Handle 404 Not Found - save as "Ошибка 404" and mark as processed
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    const string notFoundMessage = "Ошибка 404";
                    // Используем полную перегрузку с userName для записи в title
                    _db.EnqueueUserResumeDetail(
                        userLink, 
                        about: notFoundMessage, 
                        skills: new List<string>(),
                        age: null,
                        experienceText: null,
                        registration: null,
                        lastVisit: null,
                        citizenship: null,
                        remoteWork: null,
                        userName: notFoundMessage);
                    
                    _logger.WriteLine($"Пользователь {userLink}:");
                    _logger.WriteLine($"  Статус: страница не найдена (404)");
                    _logger.WriteLine($"  Title: {notFoundMessage}");
                    
                    _statistics.IncrementSuccess();
                    _activeRequests.TryRemove(userLink, out _);
                    return;
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    _activeRequests.TryRemove(userLink, out _);
                    return;
                }

                var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct);
                var encoding = response.Content.Headers.ContentType?.CharSet != null
                    ? System.Text.Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet)
                    : System.Text.Encoding.UTF8;
                var html = encoding.GetString(htmlBytes);
                
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
                    _db.EnqueueUserResumeDetail(userLink, privateMessage, new List<string>());
                    _db.EnqueueUpdateUserPublicStatus(userLink, isPublic: false);
                    
                    _logger.WriteLine($"Пользователь {userLink}:");
                    _logger.WriteLine($"  Статус: приватный профиль");
                    _logger.WriteLine($"  Сообщение: {privateMessage}");
                    
                    _statistics.IncrementSuccess();
                    _activeRequests.TryRemove(userLink, out _);
                    return;
                }
                
                var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                // Извлекаем имя пользователя
                var userName = Helper.Dom.ProfileDataExtractor.ExtractUserName(doc);
                
                // Извлекаем техническую информацию и уровень
                var (infoTech, levelTitle) = Helper.Dom.ProfileDataExtractor.ExtractInfoTechAndLevel(doc);
                
                // Извлекаем зарплату и статус поиска работы
                var (salary, jobSearchStatus) = Helper.Dom.ProfileDataExtractor.ExtractSalaryAndJobStatus(doc);

                // Извлекаем текст "О себе" - ищем секцию с заголовком "Обо мне"
                string? about = null;
                var contentSections = doc.QuerySelectorAll(AppConfig.UserResumeDetailContentSelector);
                foreach (var section in contentSections)
                {
                    // Проверяем заголовок секции
                    var titleElement = section.QuerySelector(".content-section__title");
                    var titleText = titleElement?.TextContent?.Trim();
                    
                    // Ищем только секцию "Обо мне"
                    if (titleText != null && titleText.Contains("Обо мне", StringComparison.OrdinalIgnoreCase))
                    {
                        // Берём только содержимое из .style-ugc (без заголовка "Обо мне")
                        var ugcContent = section.QuerySelector(".style-ugc");
                        if (ugcContent != null)
                        {
                            // Сохраняем переносы строк: заменяем <br> и </p> на \n перед извлечением текста
                            var aboutHtml = ugcContent.InnerHtml;
                            // Заменяем <br>, <br/>, <br /> на перенос строки
                            aboutHtml = System.Text.RegularExpressions.Regex.Replace(aboutHtml, @"<br\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            // Заменяем </p> на перенос строки (начало нового абзаца)
                            aboutHtml = System.Text.RegularExpressions.Regex.Replace(aboutHtml, @"</p>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            // Заменяем </li> на перенос строки
                            aboutHtml = System.Text.RegularExpressions.Regex.Replace(aboutHtml, @"</li>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            // Удаляем все остальные HTML теги
                            aboutHtml = System.Text.RegularExpressions.Regex.Replace(aboutHtml, @"<[^>]+>", "");
                            // Декодируем HTML entities (&nbsp; и т.д.)
                            aboutHtml = System.Net.WebUtility.HtmlDecode(aboutHtml);
                            // Убираем множественные переносы строк
                            aboutHtml = System.Text.RegularExpressions.Regex.Replace(aboutHtml, @"\n{3,}", "\n\n");
                            about = aboutHtml.Trim();
                        }
                        break; // Нашли секцию "Обо мне", выходим из цикла
                    }
                }
                // Если секция "Обо мне" не найдена, about останется null - это нормально

                // Извлекаем навыки
                var skills = new List<string>();
                var skillElements = doc.QuerySelectorAll(AppConfig.UserResumeDetailSkillSelector);
                _logger.WriteLine($"  [DEBUG] Найдено элементов навыков: {skillElements.Length} (селектор: {AppConfig.UserResumeDetailSkillSelector})");
                foreach (var skillElement in skillElements)
                {
                    var skillTitle = skillElement.TextContent?.Trim();
                    if (!string.IsNullOrWhiteSpace(skillTitle))
                    {
                        skills.Add(skillTitle);
                    }
                }
                _logger.WriteLine($"  [DEBUG] Извлечено навыков: {skills.Count}");

                // Извлекаем опыт работы
                var experienceCount = 0;
                var isFirstExperience = true;
                var experienceContainer = doc.QuerySelector(AppConfig.UserResumeDetailExperienceContainerSelector);
                if (experienceContainer != null)
                {
                    var experienceItems = experienceContainer.QuerySelectorAll(AppConfig.UserResumeDetailExperienceItemSelector);
                    foreach (var item in experienceItems)
                    {
                        try
                        {
                            // Извлекаем информацию о компании
                            string? companyCode = null;
                            string? companyUrl = null;
                            string? companyTitle = null;
                            var companyLink = item.QuerySelector(AppConfig.UserResumeDetailCompanyLinkSelector);
                            if (companyLink != null)
                            {
                                companyUrl = companyLink.GetAttribute("href");
                                companyTitle = companyLink.TextContent?.Trim();
                                
                                // Извлекаем код компании из URL
                                if (!string.IsNullOrWhiteSpace(companyUrl))
                                {
                                    var match = System.Text.RegularExpressions.Regex.Match(companyUrl, AppConfig.UserResumeDetailCompanyCodeRegex);
                                    if (match.Success)
                                    {
                                        companyCode = match.Groups[1].Value;
                                        companyUrl = string.Format(AppConfig.UserResumeDetailCompanyUrlTemplate, companyCode);
                                    }
                                }
                            }

                            // Извлекаем описание компании
                            string? companyAbout = null;
                            var aboutElement = item.QuerySelector(AppConfig.UserResumeDetailCompanyAboutSelector);
                            if (aboutElement != null)
                            {
                                companyAbout = aboutElement.TextContent?.Trim();
                            }

                            // Извлекаем размер компании
                            string? companySize = null;
                            var sizeLinks = item.QuerySelectorAll(AppConfig.UserResumeDetailCompanyLinkSelector);
                            foreach (var link in sizeLinks)
                            {
                                var href = link.GetAttribute("href");
                                if (!string.IsNullOrWhiteSpace(href) && href.Contains(AppConfig.UserResumeDetailCompanySizeUrlPattern))
                                {
                                    companySize = link.TextContent?.Trim();
                                    break;
                                }
                            }

                            // Извлекаем должность
                            string? position = null;
                            var positionElement = item.QuerySelector(AppConfig.UserResumeDetailPositionSelector);
                            if (positionElement != null)
                            {
                                position = positionElement.TextContent?.Trim();
                                // Очищаем от лишних пробелов
                                if (!string.IsNullOrWhiteSpace(position))
                                {
                                    position = System.Text.RegularExpressions.Regex.Replace(position, @"\s+", " ");
                                }
                            }

                            // Извлекаем продолжительность работы
                            string? duration = null;
                            var durationElement = item.QuerySelector(AppConfig.UserResumeDetailDurationSelector);
                            if (durationElement != null)
                            {
                                duration = durationElement.TextContent?.Trim();
                            }

                            // Извлекаем описание работы
                            string? description = null;
                            var descriptionElement = item.QuerySelector(AppConfig.UserResumeDetailDescriptionSelector);
                            if (descriptionElement != null)
                            {
                                description = descriptionElement.TextContent?.Trim();
                            }

                            // Извлекаем навыки
                            var experienceSkills = new List<(int? SkillId, string SkillName)>();
                            var tagsContainer = item.QuerySelector(AppConfig.UserResumeDetailTagsSelector);
                            if (tagsContainer != null)
                            {
                                var skillLinks = tagsContainer.QuerySelectorAll(AppConfig.UserResumeDetailCompanyLinkSelector);
                                foreach (var skillLink in skillLinks)
                                {
                                    var skillName = skillLink.TextContent?.Trim();
                                    if (string.IsNullOrWhiteSpace(skillName)) continue;

                                    // Извлекаем ID навыка из URL
                                    int? skillId = null;
                                    var skillHref = skillLink.GetAttribute("href");
                                    if (!string.IsNullOrWhiteSpace(skillHref))
                                    {
                                        var skillMatch = System.Text.RegularExpressions.Regex.Match(skillHref, AppConfig.UserResumeDetailSkillIdRegex);
                                        if (skillMatch.Success && int.TryParse(skillMatch.Groups[1].Value, out var id))
                                        {
                                            skillId = id;
                                        }
                                    }

                                    experienceSkills.Add((skillId, skillName));
                                }
                            }

                            // Создаём структуру данных и добавляем в очередь
                            var experienceData = new UserExperienceData(
                                UserLink: userLink,
                                CompanyCode: companyCode,
                                CompanyUrl: companyUrl,
                                CompanyTitle: companyTitle,
                                CompanyAbout: companyAbout,
                                CompanySize: companySize,
                                Position: position,
                                Duration: duration,
                                Description: description,
                                Skills: experienceSkills,
                                IsFirstRecord: isFirstExperience
                            );

                            _db.EnqueueUserExperience(experienceData);
                            experienceCount++;
                            isFirstExperience = false;
                        }
                        catch (Exception expEx)
                        {
                            _logger.WriteLine($"Ошибка при парсинге опыта работы для {userLink}: {expEx.Message}");
                        }
                    }
                }

                // Извлекаем дополнительные данные профиля (возраст, опыт работы, регистрация, последний визит, гражданство, удаленная работа)
                var (age, experienceText, registration, lastVisit, citizenship, remoteWork) = Helper.Dom.ProfileDataExtractor.ExtractAdditionalProfileData(doc);
                
                // Сохраняем информацию для публичного профиля
                _db.EnqueueUserResumeDetail(
                    userLink, 
                    about, 
                    skills, 
                    age, 
                    experienceText, 
                    registration, 
                    lastVisit, 
                    citizenship, 
                    remoteWork,
                    userName,
                    infoTech,
                    levelTitle,
                    salary,
                    jobSearchStatus);
                
                // Если удалось извлечь данные, значит профиль публичный
                // Устанавливаем public = true
                _db.EnqueueUpdateUserPublicStatus(userLink, isPublic: true);
                
                _logger.WriteLine($"Пользователь {userLink}:");
                _logger.WriteLine($"  Имя: {userName ?? "(не найдено)"}");
                _logger.WriteLine($"  Техническая информация: {infoTech ?? "(не найдено)"}");
                _logger.WriteLine($"  Уровень: {levelTitle ?? "(не найдено)"}");
                _logger.WriteLine($"  Зарплата: {(salary.HasValue ? $"{salary.Value} ₽" : "(не найдено)")}");
                _logger.WriteLine($"  Статус поиска работы: {jobSearchStatus ?? "(не найдено)"}");
                _logger.WriteLine($"  О себе: {(string.IsNullOrWhiteSpace(about) ? "(не найдено)" : $"{about.Substring(0, Math.Min(100, about.Length))}...")}");
                _logger.WriteLine($"  Навыки: {skills.Count} шт.");
                _logger.WriteLine($"  Опыт работы: {experienceCount} записей");
                _logger.WriteLine($"  Возраст: {age ?? "(не найдено)"}");
                _logger.WriteLine($"  Опыт работы (текст): {experienceText ?? "(не найдено)"}");
                _logger.WriteLine($"  Регистрация: {registration ?? "(не найдено)"}");
                _logger.WriteLine($"  Последний визит: {lastVisit ?? "(не найдено)"}");
                _logger.WriteLine($"  Гражданство: {citizenship ?? "(не найдено)"}");
                _logger.WriteLine($"  Удаленная работа: {(remoteWork.HasValue ? (remoteWork.Value ? "Да" : "Нет") : "(не найдено)")}");
                _logger.WriteLine($"  Статус: публичный профиль");
                
                _statistics.IncrementSuccess();
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Ошибка при обработке {userLink}: {ex.Message}");
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
        _logger.WriteLine($"Статистика HTTP кодов: {_statistics.GetStatusCodeStatsString()}");
        
        // Записываем статистику в отдельный лог-файл
        _statistics.WriteToLogFile();
    }
}
