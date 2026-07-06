using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Utils;
using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Infrastructure.Url;
using JobBoardScraper.Core;
using JobBoardScraper.Data;
using JobBoardScraper.Parsing;

namespace JobBoardScraper.Scrapers;

public readonly record struct ResumeItem(string link, string title);
/// <summary>
/// Периодически обходит страницы "/resumes?order=last_visited" и "/resumes?skills[]=N"
/// и извлекает ссылки на профили пользователей для сохранения в базу данных.
/// </summary>
public sealed class ResumeListPageScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly Action<ResumeItem> _enqueueToSaveQueue;
    private readonly Func<IReadOnlyList<long>> _getCompanyIds;
    private readonly Func<IReadOnlyList<int>> _getUniversityIds;
    private readonly TimeSpan _interval;
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConsoleLogger _logger;
    private readonly AdaptiveConcurrencyController _adaptiveConcurrencyController;
    private readonly ScraperStatistics _statistics;

    public ResumeListPageScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        Action<ResumeItem> enqueueToSaveQueue,
        Func<IReadOnlyList<long>> getCompanyIds,
        Func<IReadOnlyList<int>> getUniversityIds,
        AdaptiveConcurrencyController controller,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _enqueueToSaveQueue = enqueueToSaveQueue ?? throw new ArgumentNullException(nameof(enqueueToSaveQueue));
        _getCompanyIds = getCompanyIds ?? throw new ArgumentNullException(nameof(getCompanyIds));
        _getUniversityIds = getUniversityIds ?? throw new ArgumentNullException(nameof(getUniversityIds));
        _adaptiveConcurrencyController = controller ?? throw new ArgumentNullException(nameof(controller));
        _interval = interval ?? AppConfig.ResumeListInterval;
        _statistics = new ScraperStatistics("ResumeListPageScraper");
        
        _logger = new ConsoleLogger("ResumeListPageScraper");
        _logger.SetOutputMode(outputMode);
        ScraperLogger.LogInitialization(_logger, "ResumeListPageScraper", outputMode);
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
        // Периодически обходим все включенные страницы
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
            // Обходим все включенные типы страниц
            
            // Запускаем все методы параллельно
            var tasks = new List<Task>();

            // 1. Перебор навыков
            if (AppConfig.ResumeListSkillsEnumerationEnabled)
            {
                tasks.Add(Task.Run(() => ScrapeSkillsEnumerationAsync(ct), ct));
            }

            // 2. Обход статусов поиска работы
            if (AppConfig.ResumeListWorkStatesEnabled)
            {
                tasks.Add(Task.Run(() => ScrapeWorkStatesAsync(ct), ct));
            }

            // 3. Обход по опыту работы
            if (AppConfig.ResumeListExperiencesEnabled)
            {
                tasks.Add(Task.Run(() => ScrapeExperiencesAsync(ct), ct));
            }

            // 4. Перебор qids
            if (AppConfig.ResumeListQidsEnabled)
            {
                tasks.Add(Task.Run(() => ScrapeQidsAsync(ct), ct));
            }

            // 5. Перебор по company_ids
            if (AppConfig.ResumeListCompanyIdsEnabled)
            {
                tasks.Add(Task.Run(() => ScrapeCompanyIdsAsync(ct), ct));
            }

            // 6. Перебор по university_ids
            if (AppConfig.ResumeListUniversityIdsEnabled)
            {
                tasks.Add(Task.Run(() => ScrapeUniversityIdsAsync(ct), ct));
            }

            // 7. Обход обычной страницы
            tasks.Add(Task.Run(() => ScrapeAndEnqueueAsync(ct), ct));

            // Ждем завершения всех задач
            await Task.WhenAll(tasks);

            // После завершения всех параллельных задач — единая точка фиксации
            // времени окончания и выгрузки статистики в лог-файл.
            _statistics.EndTime = DateTime.Now;
            _statistics.WriteToLogFile();
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


    private async Task ScrapeWorkStatesAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало обхода страниц по статусам поиска работы...");
        
        var workStates = AppConfig.ResumeListWorkStates;
        var orders = AppConfig.ResumeListOrderEnabled ? AppConfig.ResumeListOrders : new[] { "" };
        
        // Используем ScraperProgressLogger для отслеживания и вывода прогресса
        var progressLogger = new ScraperProgressLogger(workStates.Length, "ResumeListPageScraper", _logger, "WorkStates");
        var totalProfiles = 0;
        
        ScraperLogger.LogCount(_logger, "Статусов для обхода", workStates.Length, "статусов");
        ScraperLogger.LogCount(_logger, "Сортировок", orders.Length, "сортировок");
        
        foreach (var workState in workStates)
        {
            if (ct.IsCancellationRequested) break;
            
            var isFirstOrder = true;
            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;
                
                try
                {
                    var baseUrl = UrlManager.Format(AppConfig.ResumeListWorkStatesUrlTemplate, workState);
                    var relativeUrl = UrlManager.WithOrder(baseUrl, order);
                    var url = UrlManager.ToAbsolute(relativeUrl);
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);
                    
                    // Увеличиваем счётчик только для первого order каждого статуса
                    if (isFirstOrder)
                    {
                        progressLogger.Increment();
                        isFirstOrder = false;
                    }
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        progressLogger.LogFilter($"Статус {workState}: HTTP {(int)response.StatusCode}", order: order);
                        continue;
                    }
                    
                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);
                    
                    // Парсим профили на странице и сохраняем их в БД
                    var profiles = UserDataExtractor.ParseProfilesFromPage(doc, ct, _logger);
                    foreach (var profile in profiles)
                    {
                        _db.EnqueueResume(profile);
                        ScraperLogger.LogEnqueue(
                            _logger,
                            "Resume",
                            profile.Code ?? profile.Link,
                            ("Link", profile.Link),
                            ("Title", profile.Title),
                            ("Code", profile.Code ?? "(не найдено)"));
                    }
                    var profilesFound = profiles.Count;
                    totalProfiles += profilesFound;
                    _statistics.AddItemsCollected(profilesFound);
                    
                    progressLogger.LogFilter($"Статус {workState}", profilesFound, order);
                }
                catch (OperationCanceledException)
                {
                    ScraperLogger.LogOperationCanceled(_logger, $"статус {workState}");
                    throw;
                }
                catch (Exception ex)
                {
                    ScraperLogger.LogError(_logger, $"Ошибка при обработке статуса {workState}", ex);
                }
            }
        }
        
        progressLogger.LogCompletion(totalProfiles, $"{_statistics}");
    }

    private async Task ScrapeExperiencesAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало обхода страниц по опыту работы...");
        
        var experiences = AppConfig.ResumeListExperiences;
        var orders = AppConfig.ResumeListOrderEnabled ? AppConfig.ResumeListOrders : new[] { "" };
        
        // Используем ScraperProgressLogger для отслеживания и вывода прогресса
        var progressLogger = new ScraperProgressLogger(experiences.Length, "ResumeListPageScraper", _logger, "Experiences");
        var totalProfiles = 0;
        
        ScraperLogger.LogCount(_logger, "Опыт для обхода", experiences.Length, "шт.");
        ScraperLogger.LogCount(_logger, "Сортировок", orders.Length, "сортировок");
        
        foreach (var experience in experiences)
        {
            if (ct.IsCancellationRequested) break;
            
            var isFirstOrder = true;
            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;
                
                try
                {
                    var baseUrl = UrlManager.Format(AppConfig.ResumeListExperiencesUrlTemplate, experience);
                    var relativeUrl = UrlManager.WithOrder(baseUrl, order);
                    var url = UrlManager.ToAbsolute(relativeUrl);
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);
                    
                    // Увеличиваем счётчик только для первого order каждого опыта
                    if (isFirstOrder)
                    {
                        progressLogger.Increment();
                        isFirstOrder = false;
                    }
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        progressLogger.LogFilter($"Опыт {experience}: HTTP {(int)response.StatusCode}", order: order);
                        continue;
                    }
                    
                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);
                    
                    // Парсим профили на странице и сохраняем их в БД
                    var profiles = UserDataExtractor.ParseProfilesFromPage(doc, ct, _logger);
                    foreach (var profile in profiles)
                    {
                        _db.EnqueueResume(profile);
                        ScraperLogger.LogEnqueue(
                            _logger,
                            "Resume",
                            profile.Code ?? profile.Link,
                            ("Link", profile.Link),
                            ("Title", profile.Title),
                            ("Code", profile.Code ?? "(не найдено)"));
                    }
                    var profilesFound = profiles.Count;
                    totalProfiles += profilesFound;
                    _statistics.AddItemsCollected(profilesFound);
                    
                    progressLogger.LogFilter($"Опыт {experience}", profilesFound, order);
                }
                catch (OperationCanceledException)
                {
                    ScraperLogger.LogOperationCanceled(_logger, $"опыт {experience}");
                    throw;
                }
                catch (Exception ex)
                {
                    ScraperLogger.LogError(_logger, $"Ошибка при обработке опыта {experience}", ex);
                }
            }
        }
        
        progressLogger.LogCompletion(totalProfiles, $"{_statistics}");
    }

    private async Task ScrapeQidsAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало обхода страниц по qids...");
        
        var startQid = AppConfig.ResumeListQidsStartId;
        var endQid = AppConfig.ResumeListQidsEndId;
        var totalQids = endQid - startQid + 1;
        var orders = AppConfig.ResumeListOrderEnabled ? AppConfig.ResumeListOrders : new[] { "" };
        
        // Используем ScraperProgressLogger для отслеживания и вывода прогресса
        var progressLogger = new ScraperProgressLogger(totalQids, "ResumeListPageScraper", _logger, "Qids");
        
        ScraperLogger.LogCount(_logger, "Диапазон qids", totalQids, "шт.");
        ScraperLogger.LogCount(_logger, "Сортировок", orders.Length, "сортировок");
        
        for (var qid = startQid; qid <= endQid; qid++)
        {
            if (ct.IsCancellationRequested) break;
            
            var isFirstOrder = true;
            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;
                
                try
                {
                    var baseUrl = UrlManager.Format(AppConfig.ResumeListQidsUrlTemplate, qid);
                    var relativeUrl = UrlManager.WithOrder(baseUrl, order);
                    var url = UrlManager.ToAbsolute(relativeUrl);
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);
                    
                    // Увеличиваем счётчик только для первого order каждого qid
                    if (isFirstOrder)
                    {
                        progressLogger.Increment();
                        _statistics.IncrementProcessed();
                        isFirstOrder = false;
                    }
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        progressLogger.LogFilter($"Qid {qid}: HTTP {(int)response.StatusCode}", order: order);
                        continue;
                    }
                    
                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);
                    
                    // Парсим профили на странице и сохраняем их в БД
                    var profiles = UserDataExtractor.ParseProfilesFromPage(doc, ct, _logger);
                    foreach (var profile in profiles)
                    {
                        _db.EnqueueResume(profile);
                        ScraperLogger.LogEnqueue(
                            _logger,
                            "Resume",
                            profile.Code ?? profile.Link,
                            ("Link", profile.Link),
                            ("Title", profile.Title),
                            ("Code", profile.Code ?? "(не найдено)"));
                    }
                    var profilesFound = profiles.Count;
                    _statistics.AddItemsCollected(profilesFound);
                    
                    progressLogger.LogFilter($"Qid {qid}", profilesFound, order);
                }
                catch (OperationCanceledException)
                {
                    ScraperLogger.LogOperationCanceled(_logger, $"qid {qid}");
                    throw;
                }
                catch (Exception ex)
                {
                    ScraperLogger.LogError(_logger, $"Ошибка при обработке qid {qid}", ex);
                }
            }
        }
        
        progressLogger.LogCompletion(_statistics.TotalItemsCollected, $"{_statistics}");
    }

    private async Task ScrapeCompanyIdsAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало обхода страниц по company_ids...");
        
        var companyIds = _getCompanyIds() ?? Array.Empty<long>();
        
        var totalCompanyIds = companyIds.Count;
        var orders = AppConfig.ResumeListCompanyIdsOrders;
        
        // Два варианта: с current_company и без
        var currentCompanyVariants = new[] { "", AppConfig.ResumeListCompanyIdsCurrentCompanyParam };
        
        // Используем ScraperProgressLogger для отслеживания и вывода прогресса
        var progressLogger = new ScraperProgressLogger(totalCompanyIds, "ResumeListPageScraper", _logger, "CompanyIds");
        var totalProfiles = 0;
        
        ScraperLogger.LogCount(_logger, "Загружено company_ids", totalCompanyIds, "шт.");
        ScraperLogger.LogCount(_logger, "Сортировок", orders.Length, "сортировок");
        ScraperLogger.LogCount(_logger, "Вариантов current_company", currentCompanyVariants.Length, "вариантов");
        
        if (totalCompanyIds == 0)
        {
            ScraperLogger.LogSkip(_logger, "Нет company_ids для обработки.");
            return;
        }
        
        foreach (var companyId in companyIds)
        {
            if (ct.IsCancellationRequested) break;
            
            bool isFirstCombination = true;
            foreach (var currentCompanyParam in currentCompanyVariants)
            {
                if (ct.IsCancellationRequested) break;
                
                foreach (var order in orders)
                {
                    if (ct.IsCancellationRequested) break;
                    
                    try
                    {
                        // Формируем URL с учётом параметра current_company
                        var urlWithCompany = UrlManager.FormatCompanyIdsUrl(companyId, currentCompanyParam);
                        
                        // Добавляем сортировку если указана
                        var relativeUrl = UrlManager.WithOrder(urlWithCompany, order);
                        var url = UrlManager.ToAbsolute(relativeUrl);
                        
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var response = await _httpClient.GetAsync(url, ct);
                        sw.Stop();
                        _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);
                        
                        if (isFirstCombination)
                        {
                            progressLogger.Increment();
                            _statistics.IncrementProcessed();
                            isFirstCombination = false;
                        }
                        
                        if (!response.IsSuccessStatusCode)
                        {
                            progressLogger.LogFilter(
                                $"Company ID {companyId}",
                                order: order,
                                filterParameter: currentCompanyParam,
                                resultDescription: $"HTTP {(int)response.StatusCode}");
                            continue;
                        }
                        
                        var html = await response.Content.ReadAsStringAsync(ct);
                        var doc = await HtmlParser.ParseDocumentAsync(html, ct);
                        
                        var profiles = UserDataExtractor.ParseProfilesFromPage(doc, ct, _logger);
                        foreach (var profile in profiles)
                        {
                            _db.EnqueueResume(profile);
                            ScraperLogger.LogEnqueue(
                                _logger,
                                "Resume",
                                profile.Code ?? profile.Link,
                                ("Link", profile.Link),
                                ("Title", profile.Title),
                                ("Code", profile.Code ?? "(не найдено)"));
                        }
                        var profilesFound = profiles.Count;
                        totalProfiles += profilesFound;
                        _statistics.AddItemsCollected(profilesFound);
                        
                        progressLogger.LogFilter(
                            $"Company ID {companyId}",
                            profilesFound,
                            order,
                            filterParameter: currentCompanyParam);
                    }
                    catch (OperationCanceledException)
                    {
                        ScraperLogger.LogOperationCanceled(_logger, $"company_id {companyId}");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        ScraperLogger.LogError(_logger, $"Ошибка при обработке company_id {companyId}", ex);
                    }
                }
            }
        }
        
        progressLogger.LogCompletion(totalProfiles, $"{_statistics}");
    }

    private async Task ScrapeUniversityIdsAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало обхода страниц по university_ids...");
        
        var universityIds = _getUniversityIds() ?? Array.Empty<int>();
        
        var totalUniversityIds = universityIds.Count;
        var orders = AppConfig.ResumeListUniversityIdsOrders;
        
        // Используем ScraperProgressLogger для отслеживания и вывода прогресса
        var progressLogger = new ScraperProgressLogger(totalUniversityIds, "ResumeListPageScraper", _logger, "UniversityIds");
        var totalProfiles = 0;
        
        ScraperLogger.LogCount(_logger, "Загружено university_ids", totalUniversityIds, "шт.");
        ScraperLogger.LogCount(_logger, "Сортировок", orders.Length, "сортировок");
        
        if (totalUniversityIds == 0)
        {
            ScraperLogger.LogSkip(_logger, "Нет university_ids для обработки.");
            return;
        }
        
        foreach (var universityId in universityIds)
        {
            if (ct.IsCancellationRequested) break;
            
            var isFirstOrder = true;
            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;
                
                try
                {
                    var baseUrl = UrlManager.Format(AppConfig.ResumeListUniversityIdsUrlTemplate, universityId);
                    var relativeUrl = UrlManager.WithOrder(baseUrl, order);
                    var url = UrlManager.ToAbsolute(relativeUrl);
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);
                    
                    // Считаем прогресс только для первой сортировки
                    if (isFirstOrder)
                    {
                        progressLogger.Increment();
                        isFirstOrder = false;
                    }
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        progressLogger.LogFilter($"University ID {universityId}: HTTP {(int)response.StatusCode}", order: order);
                        continue;
                    }
                    
                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);
                    
                    var profiles = UserDataExtractor.ParseProfilesFromPage(doc, ct, _logger);
                    foreach (var profile in profiles)
                    {
                        _db.EnqueueResume(profile);
                        ScraperLogger.LogEnqueue(
                            _logger,
                            "Resume",
                            profile.Code ?? profile.Link,
                            ("Link", profile.Link),
                            ("Title", profile.Title),
                            ("Code", profile.Code ?? "(не найдено)"));
                    }
                    var profilesFound = profiles.Count;
                    totalProfiles += profilesFound;
                    _statistics.AddItemsCollected(profilesFound);
                    
                    progressLogger.LogFilter($"University ID {universityId}", profilesFound, order);
                }
                catch (OperationCanceledException)
                {
                    ScraperLogger.LogOperationCanceled(_logger, $"university_id {universityId}");
                    throw;
                }
                catch (Exception ex)
                {
                    ScraperLogger.LogError(_logger, $"Ошибка при обработке university_id {universityId}", ex);
                }
            }
        }
        
        progressLogger.LogCompletion(totalProfiles, $"{_statistics}");
    }

    private async Task ScrapeAndEnqueueAsync(CancellationToken ct)
    {
        var url = UrlManager.ToAbsolute(AppConfig.ResumeListPageUrl);
        ScraperLogger.LogPage(_logger, url);
        
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        
        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = await HtmlParser.ParseDocumentAsync(html, ct);
        
        // Используем метод парсинга профилей и сохраняем их в БД
        var profiles = UserDataExtractor.ParseProfilesFromPage(doc, ct, _logger);
        foreach (var profile in profiles)
        {
            _db.EnqueueResume(profile);
            ScraperLogger.LogEnqueue(
                _logger,
                "Resume",
                profile.Code ?? profile.Link,
                ("Link", profile.Link),
                ("Title", profile.Title),
                ("Code", profile.Code ?? "(не найдено)"));
        }
         var profilesFound = profiles.Count;

         ScraperLogger.LogCount(_logger, "Найдено", profilesFound, "профилей");
    }

    private async Task ScrapeSkillsEnumerationAsync(CancellationToken ct)
    {
        ScraperLogger.LogStart(_logger, "Начало перебора навыков...");
        
        var startSkillId = AppConfig.ResumeListSkillsStartId;
        var endSkillId = AppConfig.ResumeListSkillsEndId;
        var totalSkills = endSkillId - startSkillId + 1;
        var orders = AppConfig.ResumeListOrderEnabled ? AppConfig.ResumeListOrders : new[] { "" };
        
        // Используем ScraperProgressLogger для отслеживания и вывода прогресса
        var progressLogger = new ScraperProgressLogger(totalSkills, "ResumeListPageScraper", _logger, "Skills");
        var totalProfiles = 0;
        var skillsFound = 0;
        var skillsNotFound = 0;
        
        ScraperLogger.LogCount(_logger, "Диапазон навыков", totalSkills, "навыков");
        ScraperLogger.LogCount(_logger, "Сортировок", orders.Length, "сортировок");
        
        for (var skillId = startSkillId; skillId <= endSkillId; skillId++)
        {
            if (ct.IsCancellationRequested) break;
            
            var skillExists = false;
            var isFirstOrder = true;
            
            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;
                
                try
                {
                    var baseUrl = UrlManager.Format(AppConfig.ResumeListSkillUrlTemplate, skillId);
                    var relativeUrl = UrlManager.WithOrder(baseUrl, order);
                    var url = UrlManager.ToAbsolute(relativeUrl);
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _adaptiveConcurrencyController.ReportLatency(sw.Elapsed);
                    
                    // Увеличиваем счетчик только для первого order каждого навыка
                    if (isFirstOrder)
                    {
                        progressLogger.Increment();
                    }
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        progressLogger.LogFilter($"Навык {skillId}: HTTP {(int)response.StatusCode}", order: order);
                        continue;
                    }
                    
                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);
                    
                    if (UserDataExtractor.IsNotFoundProfiles(doc))
                    {
                        progressLogger.LogFilter($"Навык {skillId}: не найдено специалистов", order: order);
                        // Оптимизация: если на первой сортировке навык не найден, пропускаем остальные сортировки
                        if (isFirstOrder)
                        {
                            break; // Выходим из цикла orders и переходим к следующему навыку
                        }
                        continue;
                    }
                    
                    // Навык существует - добавляем в БД (только один раз)
                    if (!skillExists)
                    {
                        _db.EnqueueSkill(skillId, "");
                        ScraperLogger.LogEnqueue(
                            _logger,
                            "Skill",
                            skillId.ToString(),
                            ("SkillId", skillId.ToString()),
                            ("Type", "Skill"));
                        skillExists = true;
                    }
                    
                    var profiles = UserDataExtractor.ParseProfilesFromPage(doc, ct, _logger);
                    foreach (var profile in profiles)
                    {
                        _db.EnqueueResume(profile);
                        ScraperLogger.LogEnqueue(
                            _logger,
                            "Resume",
                            profile.Code ?? profile.Link,
                            ("Link", profile.Link),
                            ("Title", profile.Title),
                            ("Code", profile.Code ?? "(не найдено)"));
                    }
                    var profilesFound = profiles.Count;
                    totalProfiles += profilesFound;
                    
                    progressLogger.LogFilter($"Навык {skillId}", profilesFound, order);
                    
                    isFirstOrder = false;
                }
                catch (OperationCanceledException)
                {
                    ScraperLogger.LogOperationCanceled(_logger, $"навык {skillId}");
                    throw;
                }
                catch (Exception ex)
                {
                    ScraperLogger.LogError(_logger, $"Ошибка при обработке навыка {skillId}", ex);
                }
            }
            
            // После обработки всех orders для навыка, обновляем счетчики
            if (skillExists)
            {
                _statistics.IncrementFound();
                skillsFound++;
            }
            else
            {
                _statistics.IncrementNotFound();
                skillsNotFound++;
            }
        }
        
        progressLogger.LogCompletion(totalProfiles, $"Найдено навыков: {skillsFound}, Не найдено: {skillsNotFound}. {_statistics}");
    }

}
