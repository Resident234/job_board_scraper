using JobBoardScraper.Infrastructure.Logging;
using AngleSharp;

namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Базовый класс для скрейперов прокси. Определяет общий интерфейс и логику
/// для всех скрейперов, работающих с FreeProxyPool.
/// </summary>
/// <typeparam name="TProxy">Тип прокси, который парсится конкретным скрейпером</typeparam>
public abstract class ProxyScraper<TProxy> : IDisposable where TProxy : notnull
{
    protected readonly FreeProxyPool _proxyPool;
    protected readonly HttpClient _httpClient;
    protected readonly TimeSpan _refreshInterval;
    protected readonly ConsoleLogger _logger;
    protected readonly CancellationTokenSource _cts;
    protected Task? _scraperTask;
    protected bool _disposed;
    protected readonly string _sourceName;
    protected readonly string _sourceNamePrefix;
    protected readonly string _sourceUrl;
    protected bool _adaptiveModeEnabled;
    protected int _adaptiveTriggerThreshold;
    protected readonly ProxySourceStatistics _statistics;

    protected ProxyScraper(
        FreeProxyPool proxyPool,
        TimeSpan? refreshInterval,
        OutputMode outputMode,
        string sourceName,
        string sourceNamePrefix,
        string sourceUrl,
        bool adaptiveModeEnabled,
        int adaptiveTriggerThreshold)
    {
        _proxyPool = proxyPool ?? throw new ArgumentNullException(nameof(proxyPool));
        _refreshInterval = refreshInterval ?? TimeSpan.FromMinutes(10);
        _sourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
        _sourceNamePrefix = sourceNamePrefix ?? throw new ArgumentNullException(nameof(sourceNamePrefix));
        _sourceUrl = sourceUrl ?? throw new ArgumentNullException(nameof(sourceUrl));
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _logger = new ConsoleLogger(sourceName);
        _logger.SetOutputMode(outputMode);
        _cts = new CancellationTokenSource();
        _adaptiveModeEnabled = adaptiveModeEnabled;
        _adaptiveTriggerThreshold = adaptiveTriggerThreshold;
        _statistics = new ProxySourceStatistics(_sourceName);

        if (_adaptiveModeEnabled)
        {
            _proxyPool.OnPoolLow += HandlePoolLowEvent;
            _logger.WriteLine($"[{_sourceName}] Initialized with ADAPTIVE mode (threshold: {_adaptiveTriggerThreshold}), refresh interval: {_refreshInterval}");
        }
        else
        {
            _logger.WriteLine($"[{_sourceName}] Initialized with FIXED interval mode: {_refreshInterval}");
        }
    }

    public void Start()
    {
        if (_scraperTask != null) return;
        _logger.WriteLine($"[{_sourceName}] Starting background scraper");
        _scraperTask = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
    }

    public void Stop()
    {
        if (_scraperTask == null) return;
        _logger.WriteLine($"[{_sourceName}] Stopping background scraper");
        _cts.Cancel();
        try { _scraperTask.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { }
        _scraperTask = null;
    }

    protected async Task RunAsync(CancellationToken ct)
    {
        await ScrapeProxiesAsync(ct);

        if (_adaptiveModeEnabled)
        {
            await RunAdaptiveModeAsync(ct);
        }
        else
        {
            await RunFixedIntervalModeAsync(ct);
        }
    }

    private async Task RunAdaptiveModeAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_refreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await ScrapeProxiesAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RunFixedIntervalModeAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_refreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await ScrapeProxiesAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    protected async Task ScrapeProxiesAsync(CancellationToken ct)
    {
        try
        {
            _logger.WriteLine($"[{_sourceName}] Fetching from {_sourceUrl}");
            var response = await _httpClient.GetStringAsync(_sourceUrl, ct);
            var proxies = await ParseProxiesAsync(response, ct);
            _logger.WriteLine($"[{_sourceName}] Parsed {proxies.Count} proxies");

            await ProcessScrapedProxiesAsync(proxies, ct);
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"[{_sourceName}] Error: {ex.Message}");
        }
    }

    protected abstract Task<List<TProxy>> ParseProxiesAsync(string content, CancellationToken ct);

    protected async Task ProcessScrapedProxiesAsync(List<TProxy> proxies, CancellationToken ct)
    {
        if (proxies.Count == 0) return;

        // Update statistics for scraped proxies
        foreach (var proxy in proxies)
        {
            _statistics.RecordProxyScraped();
        }

        var added = 0;
        foreach (var proxy in proxies)
        {
            var proxyUrlWithSource = AddSourceToProxyUrl(proxy, _sourceNamePrefix);
            if (_proxyPool.AddProxy(proxyUrlWithSource))
                added++;
        }
        _logger.WriteLine($"[{_sourceName}] Added {added} proxies (total: {_proxyPool.GetCount()})");

        // Log statistics
        _logger.WriteLine($"[{_sourceName}] Stats: {_statistics.GetSummary()}");
    }

    private async void HandlePoolLowEvent(int currentCount)
    {
        try
        {
            _logger.WriteLine($"[{_sourceName}] 🚨 Pool low event triggered! Current: {currentCount}, threshold: {_adaptiveTriggerThreshold}");
            await ScrapeProxiesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"[{_sourceName}] Error handling pool low event: {ex.Message}");
        }
    }

    public ProxySourceStatistics GetStatistics() => _statistics;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        // Unsubscribe from events to prevent memory leaks
        if (_adaptiveModeEnabled)
        {
            _proxyPool.OnPoolLow -= HandlePoolLowEvent;
        }

        Stop();
        _cts?.Dispose();
        _httpClient?.Dispose();
        _logger?.Dispose();
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private string AddSourceToProxyUrl(TProxy proxy, string sourceNamePrefix)
    {
        var proxyStr = proxy.ToString();
        return ProxySourceHelper.AddSourceToProxyUrl(proxyStr, sourceNamePrefix);
    }
}

/// <summary>
/// Scraper for free-proxy-list.net that periodically fetches and maintains a pool of free proxies
/// </summary>
public sealed class FreeProxyListScraper : ProxyScraper<ProxyInfo>
{
    public FreeProxyListScraper(
        FreeProxyPool proxyPool,
        TimeSpan? refreshInterval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly,
        string? proxyListUrl = null,
        bool adaptiveModeEnabled = true,
        int adaptiveTriggerThreshold = 100)
        : base(proxyPool, refreshInterval, outputMode, "FreeProxyListScraper", "FreeProxyList.net",
               proxyListUrl ?? AppConfig.FreeProxyListUrl, adaptiveModeEnabled, adaptiveTriggerThreshold)
    {
    }

    protected override async Task<List<ProxyInfo>> ParseProxiesAsync(string html, CancellationToken ct)
    {
        var proxies = new List<ProxyInfo>();
        try
        {
            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html), ct);
            var table = document.QuerySelector("table.table.table-striped.table-bordered");
            if (table == null) return proxies;
            var rows = table.QuerySelectorAll("tbody tr");
            foreach (var row in rows)
            {
                var cells = row.QuerySelectorAll("td");
                if (cells.Length < 8) continue;
                proxies.Add(new ProxyInfo(
                    cells[0].TextContent.Trim(),
                    cells[1].TextContent.Trim(),
                    cells[2].TextContent.Trim(),
                    cells[4].TextContent.Trim(),
                    cells[6].TextContent.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase),
                    cells[7].TextContent.Trim()));
            }
        }
        catch (Exception ex) { _logger.WriteLine($"[FreeProxyListScraper] Parse error: {ex.Message}"); }
        return proxies;
    }

    public List<ProxyInfo> FilterProxies(List<ProxyInfo> proxies) =>
        proxies.Where(p => !p.IsTransparent() && p.IsValidIp() && (p.IsElite() || p.IsAnonymous()))
               .OrderByDescending(p => p.GetQualityScore())
               .ThenByDescending(p => p.IsRecentlyChecked())
               .ToList();
}

/// <summary>
/// Scraper для ProxyScrape API - простой GET запрос, возвращает список прокси в формате ip:port
/// https://api.proxyscrape.com/v4/free-proxy-list/get?request=displayproxies&protocol=http&timeout=10000&country=all&ssl=all&anonymity=all
/// </summary>
public sealed class ProxyScrapeScraper : ProxyScraper<string>
{
    public ProxyScrapeScraper(
        FreeProxyPool proxyPool,
        TimeSpan? refreshInterval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly,
        string? apiUrl = null,
        bool adaptiveModeEnabled = true,
        int adaptiveTriggerThreshold = 100)
        : base(proxyPool, refreshInterval, outputMode, "ProxyScrapeScraper", "ProxyScrape API",
               apiUrl ?? AppConfig.ProxyScrapeApiUrl, adaptiveModeEnabled, adaptiveTriggerThreshold)
    {
    }

protected override Task<List<string>> ParseProxiesAsync(string response, CancellationToken ct)
{
    return Task.Run(() =>
    {
        var proxies = new List<string>();

        if (string.IsNullOrWhiteSpace(response))
            return proxies;

        var lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Формат: ip:port
            if (IsValidProxyFormat(trimmed))
            {
                // Добавляем http:// префикс
                proxies.Add($"http://{trimmed}");
            }
        }

        return proxies;
    });
}


    /// <summary>
    /// Проверяет формат ip:port
    /// </summary>
    private bool IsValidProxyFormat(string proxy)
    {
        var parts = proxy.Split(':');
        if (parts.Length != 2)
            return false;

        var ip = parts[0];
        var portStr = parts[1];

        // Проверка IP
        if (string.IsNullOrWhiteSpace(ip) || ip == "0.0.0.0" || ip.StartsWith("127."))
            return false;

        // Проверка порта
        if (!int.TryParse(portStr, out var port) || port < 1 || port > 65535)
            return false;

        return true;
    }
}