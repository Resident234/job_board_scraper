using JobBoardScraper.Helper.ConsoleHelper;

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
        _interval = interval ?? TimeSpan.FromMinutes(10);
        
        _logger = new ConsoleLogger("ResumeListPageScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация ResumeListPageScraper с режимом вывода: {outputMode}");
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

            // 5. Обход обычной страницы
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

        var totalProfiles = 0;

        foreach (var workState in workStates)
        {
            if (ct.IsCancellationRequested) break;

            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var baseUrl = string.Format(AppConfig.ResumeListWorkStatesUrlTemplate, workState);
                    var url = string.IsNullOrWhiteSpace(order) ? baseUrl : $"{baseUrl}&order={order}";
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
                    totalProfiles += profilesFound;

                    _logger.WriteLine($"Статус {workState}{orderDesc}: найдено {profilesFound} профилей");
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"Ошибка при обработке статуса {workState}: {ex.Message}");
                }
            }
        }

        _logger.WriteLine($"Обход статусов завершён. Всего найдено профилей: {totalProfiles}");
    }

    private async Task ScrapeExperiencesAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода страниц по опыту работы...");
        
        var experiences = AppConfig.ResumeListExperiences;
        var orders = AppConfig.ResumeListOrderEnabled ? AppConfig.ResumeListOrders : new[] { "" };
        _logger.WriteLine($"Опыт для обхода: {string.Join(", ", experiences)} ({experiences.Length} шт.), сортировок: {orders.Length}");

        var totalProfiles = 0;

        foreach (var experience in experiences)
        {
            if (ct.IsCancellationRequested) break;

            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var baseUrl = string.Format(AppConfig.ResumeListExperiencesUrlTemplate, experience);
                    var url = string.IsNullOrWhiteSpace(order) ? baseUrl : $"{baseUrl}&order={order}";
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
                    totalProfiles += profilesFound;

                    _logger.WriteLine($"Опыт {experience}{orderDesc}: найдено {profilesFound} профилей");
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"Ошибка при обработке опыта {experience}: {ex.Message}");
                }
            }
        }

        _logger.WriteLine($"Обход по опыту завершён. Всего найдено профилей: {totalProfiles}");
    }

    private async Task ScrapeQidsAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало обхода страниц по qids...");
        
        var startQid = AppConfig.ResumeListQidsStartId;
        var endQid = AppConfig.ResumeListQidsEndId;
        var totalQids = endQid - startQid + 1;
        var orders = AppConfig.ResumeListOrderEnabled ? AppConfig.ResumeListOrders : new[] { "" };
        
        _logger.WriteLine($"Диапазон qids: {startQid} - {endQid} ({totalQids} шт.), сортировок: {orders.Length}");

        var processedCount = 0;
        var totalProfiles = 0;

        for (var qid = startQid; qid <= endQid; qid++)
        {
            if (ct.IsCancellationRequested) break;

            foreach (var order in orders)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var baseUrl = string.Format(AppConfig.ResumeListQidsUrlTemplate, qid);
                    var url = string.IsNullOrWhiteSpace(order) ? baseUrl : $"{baseUrl}&order={order}";
                    var orderDesc = string.IsNullOrWhiteSpace(order) ? "" : $" (order={order})";
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _controller.ReportLatency(sw.Elapsed);

                    if (string.IsNullOrWhiteSpace(order))
                    {
                        processedCount++;
                    }
                    var percent = processedCount * 100.0 / totalQids;

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.WriteLine($"Qid {qid}{orderDesc}: HTTP {(int)response.StatusCode}. Прогресс: {processedCount}/{totalQids} ({percent:F2}%)");
                        continue;
                    }

                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                    // Парсим профили на странице
                    var profilesFound = await ParseProfilesFromPage(doc, 0, ct);
                    totalProfiles += profilesFound;

                    _logger.WriteLine($"Qid {qid}{orderDesc}: найдено {profilesFound} профилей. Прогресс: {processedCount}/{totalQids} ({percent:F2}%)");
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"Ошибка при обработке qid {qid}: {ex.Message}");
                }
            }
        }

        _logger.WriteLine($"Обход по qids завершён. Обработано: {processedCount}, всего найдено профилей: {totalProfiles}");
    }

    private async Task ScrapeAndEnqueueAsync(CancellationToken ct)
    {
        _logger.WriteLine($"Начало обхода страницы {AppConfig.ResumeListPageUrl}...");
        
        var response = await _httpClient.GetAsync(AppConfig.ResumeListPageUrl, ct);
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

        var processedCount = 0;
        var foundCount = 0;
        var notFoundCount = 0;

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
                    var url = string.IsNullOrWhiteSpace(order) ? baseUrl : $"{baseUrl}&order={order}";
                    var orderDesc = string.IsNullOrWhiteSpace(order) ? "" : $" (order={order})";
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, ct);
                    sw.Stop();
                    _controller.ReportLatency(sw.Elapsed);

                    // Увеличиваем счетчик только для первого order каждого навыка
                    if (isFirstOrder)
                    {
                        processedCount++;
                        isFirstOrder = false;
                    }
                    var percent = processedCount * 100.0 / totalSkills;

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.WriteLine($"Навык {skillId}{orderDesc}: HTTP {(int)response.StatusCode}. Прогресс: {processedCount}/{totalSkills} ({percent:F2}%)");
                        continue;
                    }

                    var html = await response.Content.ReadAsStringAsync(ct);
                    
                    // Проверяем наличие сообщения "Специалисты не найдены"
                    if (html.Contains("Специалисты не найдены") || html.Contains("Specialists not found"))
                    {
                        _logger.WriteLine($"Навык {skillId}{orderDesc}: не найдено специалистов. Прогресс: {processedCount}/{totalSkills} ({percent:F2}%)");
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

                    _logger.WriteLine($"Навык {skillId}{orderDesc}: найдено {profilesFound} профилей. Прогресс: {processedCount}/{totalSkills} ({percent:F2}%)");
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"Ошибка при обработке навыка {skillId}: {ex.Message}");
                }
            }
            
            // После обработки всех orders для навыка, обновляем счетчики
            if (skillExists)
            {
                foundCount++;
            }
            else
            {
                notFoundCount++;
            }
        }

        _logger.WriteLine($"Перебор навыков завершён. Обработано: {processedCount}, найдено: {foundCount}, не найдено: {notFoundCount}");
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
                var name = profileLink.TextContent?.Trim();
                
                if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(name))
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

                // 3) Извлекаем должности и уровень
                string? infoTech = null;
                string? levelTitle = null;
                
                var positionsDiv = section.QuerySelector("div");
                if (positionsDiv != null)
                {
                    var separators = positionsDiv.QuerySelectorAll(AppConfig.ResumeListSeparatorSelector);
                    if (separators.Length > 0)
                    {
                        var allText = positionsDiv.TextContent?.Trim();
                        if (!string.IsNullOrWhiteSpace(allText))
                        {
                            // Разбиваем по разделителю
                            var parts = allText.Split('•', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0)
                            {
                                // Последний элемент - уровень
                                levelTitle = parts[^1].Trim();
                                
                                // Остальные - должности
                                if (parts.Length > 1)
                                {
                                    infoTech = string.Join(" • ", parts[..^1]);
                                }
                            }
                        }
                    }
                }

                // 4) Извлекаем зарплату
                int? salary = null;
                var salarySpans = section.QuerySelectorAll("span");
                foreach (var span in salarySpans)
                {
                    var text = span.TextContent?.Trim();
                    if (!string.IsNullOrWhiteSpace(text) && text.Contains("От") && text.Contains("₽"))
                    {
                        // Извлекаем число
                        var salaryMatch = System.Text.RegularExpressions.Regex.Match(text, AppConfig.ResumeListSalaryRegex);
                        if (salaryMatch.Success)
                        {
                            var salaryStr = salaryMatch.Groups[1].Value.Replace(" ", "");
                            if (int.TryParse(salaryStr, out var salaryValue))
                            {
                                salary = salaryValue;
                            }
                        }
                        break;
                    }
                }

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
