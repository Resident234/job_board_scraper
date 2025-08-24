using AngleSharp.Html.Parser;


namespace job_board_scraper
{
    public readonly record struct ResumeItem(string link, string title);

    // Отдельный класс, запускающий фоновую задачу с интервалом 10 минут
    public sealed class ListResumeScraper
    {
        private static readonly Uri BaseUri = new("https://career.habr.com");
        private readonly HttpClient _httpClient;
        private readonly Action<ResumeItem> _enqueueToSaveQueue;
        private readonly TimeSpan _interval;
        private readonly HtmlParser _parser = new();
        private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

        public ListResumeScraper(
            HttpClient httpClient,
            Action<ResumeItem> enqueueToSaveQueue,
            TimeSpan? interval = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _enqueueToSaveQueue = enqueueToSaveQueue ?? throw new ArgumentNullException(nameof(enqueueToSaveQueue));
            _interval = interval ?? TimeSpan.FromMinutes(10);
        }

        // Запускает параллельный цикл. Завершается по токену отмены.
        public Task StartAsync(CancellationToken ct)
        {
            return Task.Run(() => LoopAsync(ct), ct);
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            // Первый запуск — сразу
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
                Console.Error.WriteLine($"[ListScraper] Ошибка: {ex.Message}");
            }
        }

        private async Task ScrapeAndEnqueueAsync(CancellationToken ct)
        {
            var response = await _httpClient.GetAsync("/resumes?order=last_visited", ct);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(ct);

            var doc = await _parser.ParseDocumentAsync(html, ct);

            var anchors = doc.QuerySelectorAll("a.resume-card__title-link");

            var found = 0;
            foreach (var a in anchors)
            {
                var href = a.GetAttribute("href");
                var name = (a.TextContent ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(name))
                    continue;

                if (!Uri.TryCreate(BaseUri, href, out var uri))
                    continue;

                // Только http/https
                if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                    !uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Только домен career.habr.com
                if (!uri.Host.Equals(BaseUri.Host, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Без query/fragment
                if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                    continue;

                // Путь ровно из одного сегмента: /{slug}
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (segments.Length != 1) 
                    continue;

                var cleanUrl = uri.GetLeftPart(UriPartial.Path);

                // Дедупликация между итерациями
                if (!_seen.Add(cleanUrl))
                    continue;

                var item = new ResumeItem(cleanUrl, name);

                _enqueueToSaveQueue(item);
                Console.WriteLine($"[ListScraper] В очередь: {item.title} -> {item.link}");
                found++;
            }

            Console.WriteLine($"[ListScraper] Обход завершён. Найдено новых записей: {found}");
        }
    }
}
