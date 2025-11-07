using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Helper.Utils;
using System.Text.RegularExpressions;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Обходит профили пользователей и извлекает детальную информацию
/// </summary>
public sealed class UserProfileScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly DatabaseClient _db;
    private readonly Func<List<string>> _getUserCodes;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;
    private readonly Regex _salaryRegex;

    public UserProfileScraper(
        SmartHttpClient httpClient,
        DatabaseClient db,
        Func<List<string>> getUserCodes,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _getUserCodes = getUserCodes ?? throw new ArgumentNullException(nameof(getUserCodes));
        _interval = interval ?? TimeSpan.FromDays(30);
        _salaryRegex = new Regex(AppConfig.UserProfileSalaryRegex, RegexOptions.Compiled);
        
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
        
        // Получаем список кодов пользователей из БД
        var userCodes = _getUserCodes();
        _logger.WriteLine($"Загружено {userCodes.Count} пользователей из БД.");

        if (userCodes.Count == 0)
        {
            _logger.WriteLine("Нет пользователей для обработки.");
            return;
        }

        var totalProcessed = 0;
        var totalSuccess = 0;
        var totalFailed = 0;
        var totalSkipped = 0;

        foreach (var userCode in userCodes)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var friendsUrl = AppConfig.UserProfileFriendsUrlTemplate.Replace("{0}", userCode);
                _logger.WriteLine($"Обработка пользователя: {userCode} -> {friendsUrl}");

                var response = await _httpClient.GetAsync(friendsUrl, ct);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.WriteLine($"Пользователь {userCode} вернул код {response.StatusCode}. Пропуск.");
                    totalSkipped++;
                    totalProcessed++;
                    continue;
                }

                // Читаем HTML с правильной кодировкой
                var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct);
                var encoding = response.Content.Headers.ContentType?.CharSet != null
                    ? System.Text.Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet)
                    : System.Text.Encoding.UTF8;
                var html = encoding.GetString(htmlBytes);
                
                // Сохраняем HTML в файл для отладки
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

                var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                // Извлекаем имя пользователя
                string? userName = null;
                var pageTitleElement = doc.QuerySelector(AppConfig.UserProfilePageTitleSelector);
                if (pageTitleElement != null)
                {
                    userName = pageTitleElement.TextContent?.Trim();
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

                // Сохраняем информацию о пользователе
                _db.EnqueueUserProfile(userCode, userName, isExpert, levelTitle, infoTech, salary);
                
                _logger.WriteLine($"Пользователь {userCode}: Имя = {userName ?? "(не найдено)"}, Эксперт = {isExpert?.ToString() ?? "нет"}, Уровень = {levelTitle ?? "(не найдено)"}, Зарплата = {salary?.ToString() ?? "(не найдено)"}");
                
                totalSuccess++;
                totalProcessed++;

                // Небольшая задержка между запросами
                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Ошибка при обработке пользователя {userCode}: {ex.Message}");
                totalFailed++;
                totalProcessed++;
            }
        }
        
        _logger.WriteLine($"Обход завершён. Обработано: {totalProcessed}, успешно: {totalSuccess}, ошибок: {totalFailed}, пропущено: {totalSkipped}");
    }
}
