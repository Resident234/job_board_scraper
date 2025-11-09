using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Helper.Utils;
using System.Collections.Concurrent;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Обходит страницы профилей пользователей и извлекает детальную информацию о резюме
/// Извлекает: about, навыки, опыт работы
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

    public UserResumeDetailScraper(
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
        
        _logger = new ConsoleLogger("UserResumeDetailScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация UserResumeDetailScraper с режимом вывода: {outputMode}");
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
        
        var userLinks = _getUserCodes();
        var totalLinks = userLinks.Count;
        _logger.WriteLine($"Загружено {totalLinks} пользователей из БД.");

        if (totalLinks == 0)
        {
            _logger.WriteLine("Нет пользователей для обработки.");
            return;
        }

        var totalProcessed = 0;
        var totalSuccess = 0;
        var totalFailed = 0;

        await AdaptiveForEach.ForEachAdaptiveAsync(
            source: userLinks,
            body: async userLink =>
        {
            _activeRequests.TryAdd(userLink, Task.CurrentId.HasValue ? Task.FromResult(Task.CurrentId.Value) : Task.CompletedTask);
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(userLink, ct);
                sw.Stop();
                _controller.ReportLatency(sw.Elapsed);
                
                double elapsedSeconds = sw.Elapsed.TotalSeconds;
                int completed = Interlocked.Increment(ref totalProcessed);
                double percent = completed * 100.0 / totalLinks;
                _logger.WriteLine($"HTTP запрос {userLink}: {elapsedSeconds:F3} сек. Код ответа {(int)response.StatusCode}. Обработано: {completed}/{totalLinks} ({percent:F2}%). Параллельных процессов: {_activeRequests.Count}.");
                
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
                
                var doc = await HtmlParser.ParseDocumentAsync(html, ct);

                // Извлекаем текст "О себе"
                string? about = null;
                var contentSection = doc.QuerySelector(AppConfig.UserResumeDetailContentSelector);
                if (contentSection != null)
                {
                    about = contentSection.TextContent?.Trim();
                }

                // Извлекаем навыки
                var skills = new List<string>();
                var skillElements = doc.QuerySelectorAll(AppConfig.UserResumeDetailSkillSelector);
                foreach (var skillElement in skillElements)
                {
                    var skillTitle = skillElement.TextContent?.Trim();
                    if (!string.IsNullOrWhiteSpace(skillTitle))
                    {
                        skills.Add(skillTitle);
                    }
                }

                // Сохраняем информацию
                _db.EnqueueUserResumeDetail(userLink, about, skills);
                
                _logger.WriteLine($"Пользователь {userLink}:");
                _logger.WriteLine($"  О себе: {(string.IsNullOrWhiteSpace(about) ? "(не найдено)" : $"{about.Substring(0, Math.Min(100, about.Length))}...")}");
                _logger.WriteLine($"  Навыки: {skills.Count} шт.");
                
                Interlocked.Increment(ref totalSuccess);
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Ошибка при обработке {userLink}: {ex.Message}");
                Interlocked.Increment(ref totalFailed);
            }
            finally
            {
                _activeRequests.TryRemove(userLink, out _);
            }
        },
        controller: _controller,
        ct: ct
        );
        
        _logger.WriteLine($"Обход завершён. Обработано: {totalProcessed}, успешно: {totalSuccess}, ошибок: {totalFailed}");
    }
}
