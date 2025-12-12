using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Models;

namespace JobBoardScraper.WebScraper;

public readonly record struct ResumeItem(string link, string title);

/// <summary>
/// Периодически обходит страницы "/resumes?order=last_visited" и "/resumes?skills[]=N"
/// и извлекает ссылки на профили пользователей для сохранения в базу данных.
/// </summary>
public sealed class ResumeListPageScraper : IDisposable
{
    private static readonly Uri BaseUri = new(AppConfig.BaseUrl);
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly Action<ResumeItem> _enqueueToSaveQueue;
    private readonly TimeSpan _interval;
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConsoleLogger _logger;
    private readonly AdaptiveConcurrencyController _controller;
    private readonly Models.ScraperStatistics _statistics;

    public ResumeListPageScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        Action<ResumeItem> enqueueToSaveQueue,
        AdaptiveConcurrencyController controller,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _enqueueToSaveQueue = enqueueToSaveQueue ?? throw new ArgumentNullException(nameof(enqueueToSaveQueue));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _interval = interval ?? AppConfig.ResumeListInterval;
        _statistics = new Models.ScraperStatistics("ResumeListPageScraper");
        
        _logger = new ConsoleLogger("ResumeListPageScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация ResumeListPageScraper с режимом вывода: {outputMode}");
    }

    public void Dispose()
    {
        _logger?.Dispose();
    }

    /// <summary>
    /// Преобразует относительный URL в абсолютный, используя BaseUri
    /// </summary>
    private string GetAbsoluteUrl(string relativeUrl)
    {
        if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out _))
            return relativeUrl; // Уже абсолютный URL
        
        return new Uri(BaseUri, relativeUrl).ToString();
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
            // TODO проверить счетчик прогресса во всех тасках, он не увеличивается при обходе по qids
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



    private async Task ScrapeWorkStatesAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода страниц по статусам поиска работы...");
        
        var workStates = AppConfig.ResumeListWorkStates;
        var orders = AppConfig.ResumeListOrderEnabled ? AppConfig.ResumeListOrders : new[] { "" };
        _logger.WriteLine($"Статусы для обхода: {string.Join(", ", workStates)} ({workStates.Length} шт.), сортировок: {orders.Length}");

        foreach (var workState in workStates)
        {
            if (ct.IsCancellationRequested) break;

            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var baseUrl = string.Format(AppConfig.ResumeListWorkStatesUrlTemplate, workState);
                    var relativeUrl = string.IsNullOrWhiteSpace(order) ? baseUrl : $"{baseUrl}&order={order}";
                    var url = GetAbsoluteUrl(relativeUrl);
                    var orderDesc = string.IsNullOrWhiteSpace(order) ? "" : $" (order={order})";
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _controller.ReportLatency(sw.Elapsed);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.WriteLine($"Статус {workState}{orderDesc}: HTTP {(int)response.StatusCode}");
                        continue;
                    }

                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                    // Парсим профили на странице
                    var profilesFound = await ParseProfilesFromPage(doc, 0, ct);
                    _statistics.AddItemsCollected(profilesFound);

                    _logger.WriteLine($"Статус {workState}{orderDesc}: найдено {profilesFound} профилей");
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"Ошибка при обработке статуса {workState}: {ex.Message}");
                }
            }
        }

        _statistics.EndTime = DateTime.Now;
        _logger.WriteLine($"Обход статусов завершён. {_statistics}");
    }

    private async Task ScrapeExperiencesAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода страниц по опыту работы...");
        
        var experiences = AppConfig.ResumeListExperiences;
        var orders = AppConfig.ResumeListOrderEnabled ? AppConfig.ResumeListOrders : new[] { "" };
        _logger.WriteLine($"Опыт для обхода: {string.Join(", ", experiences)} ({experiences.Length} шт.), сортировок: {orders.Length}");

        foreach (var experience in experiences)
        {
            if (ct.IsCancellationRequested) break;

            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var baseUrl = string.Format(AppConfig.ResumeListExperiencesUrlTemplate, experience);
                    var relativeUrl = string.IsNullOrWhiteSpace(order) ? baseUrl : $"{baseUrl}&order={order}";
                    var url = GetAbsoluteUrl(relativeUrl);
                    var orderDesc = string.IsNullOrWhiteSpace(order) ? "" : $" (order={order})";

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _controller.ReportLatency(sw.Elapsed);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.WriteLine($"Опыт {experience}{orderDesc}: HTTP {(int)response.StatusCode}");
                        continue;
                    }

                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                    // Парсим профили на странице
                    var profilesFound = await ParseProfilesFromPage(doc, 0, ct);
                    _statistics.AddItemsCollected(profilesFound);

                    _logger.WriteLine($"Опыт {experience}{orderDesc}: найдено {profilesFound} профилей");
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"Ошибка при обработке опыта {experience}: {ex.Message}");
                }
            }
        }

        _statistics.EndTime = DateTime.Now;
        _logger.WriteLine($"Обход по опыту завершён. {_statistics}");
    }

    private async Task ScrapeQidsAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода страниц по qids...");
        
        var startQid = AppConfig.ResumeListQidsStartId;
        var endQid = AppConfig.ResumeListQidsEndId;
        var totalQids = endQid - startQid + 1;
        var orders = AppConfig.ResumeListOrderEnabled ? AppConfig.ResumeListOrders : new[] { "" };
        
        _logger.WriteLine($"Диапазон qids: {startQid} - {endQid} ({totalQids} шт.), сортировок: {orders.Length}");

        for (var qid = startQid; qid <= endQid; qid++)
        {
            if (ct.IsCancellationRequested) break;

            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var baseUrl = string.Format(AppConfig.ResumeListQidsUrlTemplate, qid);
                    var relativeUrl = string.IsNullOrWhiteSpace(order) ? baseUrl : $"{baseUrl}&order={order}";
                    var url = GetAbsoluteUrl(relativeUrl);
                    var orderDesc = string.IsNullOrWhiteSpace(order) ? "" : $" (order={order})";
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _controller.ReportLatency(sw.Elapsed);

                    if (string.IsNullOrWhiteSpace(order))
                    {
                        _statistics.IncrementProcessed();
                    }
                    var percent = _statistics.TotalProcessed * 100.0 / totalQids;

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.WriteLine($"Qid {qid}{orderDesc}: HTTP {(int)response.StatusCode}. Прогресс: {_statistics.TotalProcessed}/{totalQids} ({percent:F2}%)");
                        continue;
                    }

                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                    // Парсим профили на странице
                    var profilesFound = await ParseProfilesFromPage(doc, 0, ct);
                    _statistics.AddItemsCollected(profilesFound);

                    _logger.WriteLine($"Qid {qid}{orderDesc}: найдено {profilesFound} профилей. Прогресс: {_statistics.TotalProcessed}/{totalQids} ({percent:F2}%)");
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"Ошибка при обработке qid {qid}: {ex.Message}");
                }
            }
        }

        _statistics.EndTime = DateTime.Now;
        _logger.WriteLine($"Обход по qids завершён. {_statistics}");
    }

    private async Task ScrapeCompanyIdsAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода страниц по company_ids...");
        
        // Получаем список company_id из БД
        using var conn = _db.DatabaseConnectionInit();
        var companyIds = _db.GetAllCompanyIds(conn);
        _db.DatabaseConnectionClose(conn);
        
        var totalCompanyIds = companyIds.Count;
        var orders = AppConfig.ResumeListCompanyIdsOrders;
        
        // Два варианта: с current_company и без
        var currentCompanyVariants = new[] { "", "&current_company=1" };
        
        _logger.WriteLine($"Загружено company_ids: {totalCompanyIds} шт., сортировок: {orders.Length}, вариантов current_company: {currentCompanyVariants.Length}");

        if (totalCompanyIds == 0)
        {
            _logger.WriteLine("Нет company_ids для обработки.");
            return;
        }

        foreach (var companyId in companyIds)
        {
            if (ct.IsCancellationRequested) break;

            foreach (var currentCompanyParam in currentCompanyVariants)
            {
                if (ct.IsCancellationRequested) break;

                foreach (var order in orders)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        // Формируем базовый URL без current_company из шаблона
                        var baseUrlTemplate = AppConfig.ResumeListCompanyIdsUrlTemplate.Replace("&current_company=1", "");
                        var baseUrl = string.Format(baseUrlTemplate, companyId);
                        
                        // Добавляем current_company если нужно
                        var urlWithCompany = baseUrl + currentCompanyParam;
                        
                        // Добавляем сортировку если указана
                        var relativeUrl = string.IsNullOrWhiteSpace(order) ? urlWithCompany : $"{urlWithCompany}&order={order}";
                        var url = GetAbsoluteUrl(relativeUrl);
                        
                        var currentCompanyDesc = string.IsNullOrWhiteSpace(currentCompanyParam) ? "" : " (current_company=1)";
                        var orderDesc = string.IsNullOrWhiteSpace(order) ? "" : $" (order={order})";
                        
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var response = await _httpClient.GetAsync(url, ct);
                        sw.Stop();
                        _controller.ReportLatency(sw.Elapsed);

                        // Считаем прогресс только для первой комбинации (без сортировки и без current_company)
                        if (string.IsNullOrWhiteSpace(order) && string.IsNullOrWhiteSpace(currentCompanyParam))
                        {
                            _statistics.IncrementProcessed();
                        }
                        var percent = _statistics.TotalProcessed * 100.0 / totalCompanyIds;

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.WriteLine($"Company ID {companyId}{currentCompanyDesc}{orderDesc}: HTTP {(int)response.StatusCode}. Прогресс: {_statistics.TotalProcessed}/{totalCompanyIds} ({percent:F2}%)");
                            continue;
                        }

                        var html = await response.Content.ReadAsStringAsync(ct);
                        var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                        // Парсим профили на странице
                        var profilesFound = await ParseProfilesFromPage(doc, 0, ct);
                        _statistics.AddItemsCollected(profilesFound);

                        _logger.WriteLine($"Company ID {companyId}{currentCompanyDesc}{orderDesc}: найдено {profilesFound} профилей. Прогресс: {_statistics.TotalProcessed}/{totalCompanyIds} ({percent:F2}%)");
                    }
                    catch (Exception ex)
                    {
                        _logger.WriteLine($"Ошибка при обработке company_id {companyId}: {ex.Message}");
                    }
                }
            }
        }

        _statistics.EndTime = DateTime.Now;
        _logger.WriteLine($"Обход по company_ids завершён. {_statistics}");
    }

    private async Task ScrapeUniversityIdsAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода страниц по university_ids...");
        
        // Получаем список university_id из БД
        using var conn = _db.DatabaseConnectionInit();
        var universityIds = _db.GetAllUniversityIds(conn);
        _db.DatabaseConnectionClose(conn);
        
        var totalUniversityIds = universityIds.Count;
        var orders = AppConfig.ResumeListUniversityIdsOrders;
        
        _logger.WriteLine($"Загружено university_ids: {totalUniversityIds} шт., сортировок: {orders.Length}");

        if (totalUniversityIds == 0)
        {
            _logger.WriteLine("Нет university_ids для обработки.");
            return;
        }

        var processedCount = 0;
        foreach (var universityId in universityIds)
        {
            if (ct.IsCancellationRequested) break;

            var isFirstOrder = true;
            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var baseUrl = string.Format(AppConfig.ResumeListUniversityIdsUrlTemplate, universityId);
                    var relativeUrl = string.IsNullOrWhiteSpace(order) ? baseUrl : $"{baseUrl}&order={order}";
                    var url = GetAbsoluteUrl(relativeUrl);
                    var orderDesc = string.IsNullOrWhiteSpace(order) ? "" : $" (order={order})";
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _controller.ReportLatency(sw.Elapsed);

                    // Считаем прогресс только для первой сортировки
                    if (isFirstOrder)
                    {
                        processedCount++;
                        isFirstOrder = false;
                    }
                    var percent = processedCount * 100.0 / totalUniversityIds;

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.WriteLine($"University ID {universityId}{orderDesc}: HTTP {(int)response.StatusCode}. Прогресс: {processedCount}/{totalUniversityIds} ({percent:F2}%)");
                        continue;
                    }

                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                    // Парсим профили на странице
                    var profilesFound = await ParseProfilesFromPage(doc, 0, ct);
                    _statistics.AddItemsCollected(profilesFound);

                    _logger.WriteLine($"University ID {universityId}{orderDesc}: найдено {profilesFound} профилей. Прогресс: {processedCount}/{totalUniversityIds} ({percent:F2}%)");
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"Ошибка при обработке university_id {universityId}: {ex.Message}");
                }
            }
        }

        _statistics.EndTime = DateTime.Now;
        _logger.WriteLine($"Обход по university_ids завершён. {_statistics}");
    }

    private async Task ScrapeAndEnqueueAsync(CancellationToken ct)
    {
        var url = GetAbsoluteUrl(AppConfig.ResumeListPageUrl);
        _logger.WriteLine($"Начало обхода страницы {url}...");
        
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = await HtmlParser.ParseDocumentAsync(html, ct);

        // Используем новый метод парсинга профилей
        var profilesFound = await ParseProfilesFromPage(doc, 0, ct);

        _logger.WriteLine($"Обход завершён. Найдено профилей: {profilesFound}");
    }

    private async Task ScrapeSkillsEnumerationAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало перебора навыков...");
        
        var startSkillId = AppConfig.ResumeListSkillsStartId;
        var endSkillId = AppConfig.ResumeListSkillsEndId;
        var totalSkills = endSkillId - startSkillId + 1;
        var orders = AppConfig.ResumeListOrderEnabled ? AppConfig.ResumeListOrders : new[] { "" };
        
        _logger.WriteLine($"Диапазон навыков: {startSkillId} - {endSkillId} ({totalSkills} навыков), сортировок: {orders.Length}");

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
                    var baseUrl = string.Format(AppConfig.ResumeListSkillUrlTemplate, skillId);
                    var relativeUrl = string.IsNullOrWhiteSpace(order) ? baseUrl : $"{baseUrl}&order={order}";
                    var url = GetAbsoluteUrl(relativeUrl);
                    var orderDesc = string.IsNullOrWhiteSpace(order) ? "" : $" (order={order})";
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _controller.ReportLatency(sw.Elapsed);

                    // Увеличиваем счетчик только для первого order каждого навыка
                    if (isFirstOrder)
                    {
                        _statistics.IncrementProcessed();
                    }
                    var percent = _statistics.TotalProcessed * 100.0 / totalSkills;

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.WriteLine($"Навык {skillId}{orderDesc}: HTTP {(int)response.StatusCode}. Прогресс: {_statistics.TotalProcessed}/{totalSkills} ({percent:F2}%)");
                        continue;
                    }

                    var html = await response.Content.ReadAsStringAsync(ct);
                    
                    // Проверяем наличие сообщения "Специалисты не найдены"
                    if (html.Contains("Специалисты не найдены") || html.Contains("Specialists not found"))
                    {
                        _logger.WriteLine($"Навык {skillId}{orderDesc}: не найдено специалистов. Прогресс: {_statistics.TotalProcessed}/{totalSkills} ({percent:F2}%)");
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
                        skillExists = true;
                    }

                    // Парсим профили на странице
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);
                    var profilesFound = await ParseProfilesFromPage(doc, skillId, ct);

                    _logger.WriteLine($"Навык {skillId}{orderDesc}: найдено {profilesFound} профилей. Прогресс: {_statistics.TotalProcessed}/{totalSkills} ({percent:F2}%)");
                
                    isFirstOrder = false;
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"Ошибка при обработке навыка {skillId}: {ex.Message}");
                }
            }
            
            // После обработки всех orders для навыка, обновляем счетчики
            if (skillExists)
            {
                _statistics.IncrementFound();
            }
            else
            {
                _statistics.IncrementNotFound();
            }
        }

        _statistics.EndTime = DateTime.Now;
        _logger.WriteLine($"Перебор навыков завершён. {_statistics}");
    }

    private async Task<int> ParseProfilesFromPage(AngleSharp.Dom.IDocument doc, int skillId, CancellationToken ct)
    {
        var profileCount = 0;
        var sections = doc.QuerySelectorAll(AppConfig.ResumeListProfileSectionSelector);

        foreach (var section in sections)
        {
            try
            {
                // 1) Извлекаем ссылку и имя
                var profileLink = section.QuerySelector(AppConfig.ResumeListProfileLinkSelector);
                if (profileLink == null) continue;

                var href = profileLink.GetAttribute("href");
                
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                // Проверяем и извлекаем код пользователя с помощью regex
                // Валидные: /username, https://career.habr.com/username
                // Невалидные: https://habr.com/users/username, /some/path
                var cleanHref = href.TrimStart('/');
                var match = System.Text.RegularExpressions.Regex.Match(cleanHref, AppConfig.ResumeListProfileLinkRegex);
                if (!match.Success)
                    continue;
                
                var code = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(code))
                    continue;
                
                var link = string.Format(AppConfig.ResumeListProfileUrlTemplate, code);

                // 2) Проверяем признак эксперта
                var expertIcon = section.QuerySelector(AppConfig.ResumeListExpertIconSelector);
                var isExpert = expertIcon != null;

                // 3) Извлекаем имя, должности и уровень используя Helper.Dom.ProfileDataExtractor
                var (name, infoTech, levelTitle) = Helper.Dom.ProfileDataExtractor.ExtractNameInfoTechAndLevel(
                    section, 
                    AppConfig.ResumeListProfileLinkSelector, 
                    AppConfig.ResumeListSeparatorSelector);

                // 4) Извлекаем зарплату используя Helper.Dom.ProfileDataExtractor
                var salary = Helper.Dom.ProfileDataExtractor.ExtractSalaryFromSection(
                    section, 
                    AppConfig.ResumeListSalaryRegex);

                // 5) Извлекаем навыки
                var skills = new List<string>();
                var skillsSection = section.QuerySelector(AppConfig.ResumeListSkillsSectionSelector);
                if (skillsSection != null)
                {
                    var skillButtons = skillsSection.QuerySelectorAll(AppConfig.ResumeListSkillButtonSelector);
                    foreach (var skillSpan in skillButtons)
                    {
                        var skillName = skillSpan.TextContent?.Trim();
                        if (!string.IsNullOrWhiteSpace(skillName))
                        {
                            skills.Add(skillName);
                        }
                    }
                }

                // Проверяем, что имя не null
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                // Создаём структуру данных и добавляем в очередь
                var profileData = new ResumeProfileData(
                    Code: code,
                    Link: link,
                    Title: name,
                    IsExpert: isExpert,
                    InfoTech: infoTech,
                    LevelTitle: levelTitle,
                    Salary: salary,
                    Skills: skills.Count > 0 ? skills : null
                );

                _db.EnqueueResumeProfile(profileData);
                profileCount++;
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Ошибка при парсинге профиля: {ex.Message}");
            }
        }

        return profileCount;
    }
}
