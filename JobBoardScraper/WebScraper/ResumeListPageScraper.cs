namespace JobBoardScraper.WebScraper;

public readonly record struct ResumeItem(string link, string title);

/// <summary>
/// Периодически (раз в 10 минут) обходит страницу "/resumes?order=last_visited"
/// и извлекает ссылки на профили пользователей для сохранения в базу данных.
/// </summary>
public sealed class ResumeListPageScraper
{
    private static readonly Uri BaseUri = new(AppConfig.BaseUrl);
    private readonly SmartHttpClient _httpClient;
    private readonly Action<ResumeItem> _enqueueToSaveQueue;
    private readonly TimeSpan _interval;
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    public ResumeListPageScraper(
        SmartHttpClient httpClient,
        Action<ResumeItem> enqueueToSaveQueue,
        TimeSpan? interval = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _enqueueToSaveQueue = enqueueToSaveQueue ?? throw new ArgumentNullException(nameof(enqueueToSaveQueue));
        _interval = interval ?? TimeSpan.FromMinutes(10);
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
            await ScrapeAndEnqueueAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Остановка — ок
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ResumeListPageScraper] Ошибка: {ex.Message}");
        }
    }

    private async Task ScrapeAndEnqueueAsync(CancellationToken ct)
    {
        var response = await _httpClient.GetAsync("/resumes?order=last_visited", ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = await HtmlParser.ParseDocumentAsync(html, ct);
        var anchors = doc.QuerySelectorAll(AppConfig.ResumeListResumeLinkSelector);

        var found = 0;
        foreach (var a in anchors)
        {
            var href = a.GetAttribute("href");
            var name = (a.TextContent ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(name))
                continue;

            if (!Uri.TryCreate(BaseUri, href, out var uri))
                continue;

            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                !uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!uri.Host.Equals(BaseUri.Host, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                continue;

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length != 1)
                continue;

            var cleanUrl = uri.GetLeftPart(UriPartial.Path);

            if (!_seen.Add(cleanUrl))
                continue;

            var item = new ResumeItem(cleanUrl, name);
            _enqueueToSaveQueue(item);
            Console.WriteLine($"[ResumeListPageScraper] В очередь: {item.title} -> {item.link}");
            found++;
        }

        Console.WriteLine($"[ResumeListPageScraper] Обход завершён. Найдено новых записей: {found}");
    }
}
