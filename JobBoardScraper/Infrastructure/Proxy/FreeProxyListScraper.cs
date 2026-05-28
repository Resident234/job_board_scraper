using JobBoardScraper.Infrastructure.Logging;
using AngleSharp;

namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Scraper for free-proxy-list.net that periodically fetches and maintains a pool of free proxies
/// </summary>
public sealed class FreeProxyListScraper : IDisposable
{
    private readonly FreeProxyPool _proxyPool;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _refreshInterval;
    private readonly ConsoleLogger _logger;
    private readonly CancellationTokenSource _cts;
    private Task? _scraperTask;
    private bool _disposed;
    private readonly string _proxyListUrl;
    private bool _adaptiveModeEnabled;
    private int _adaptiveTriggerThreshold;
    private readonly ProxySourceStatistics _statistics;

    public FreeProxyListScraper(
        FreeProxyPool proxyPool,
        TimeSpan? refreshInterval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly,
        string? proxyListUrl = null,
        bool adaptiveModeEnabled = true,
        int adaptiveTriggerThreshold = 100)
    {
        _proxyPool = proxyPool ?? throw new ArgumentNullException(nameof(proxyPool));
        _refreshInterval = refreshInterval ?? TimeSpan.FromMinutes(10);
        _proxyListUrl = proxyListUrl ?? AppConfig.FreeProxyListUrl;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _logger = new ConsoleLogger("FreeProxyListScraper");
        _logger.SetOutputMode(outputMode);
        _cts = new CancellationTokenSource();
        _adaptiveModeEnabled = adaptiveModeEnabled;
        _adaptiveTriggerThreshold = adaptiveTriggerThreshold;
        _statistics = new ProxySourceStatistics("FreeProxyList.net");

        if (_adaptiveModeEnabled)
        {
            _proxyPool.OnPoolLow += HandlePoolLowEvent;
            _logger.WriteLine($"[FreeProxyListScraper] Initialized with ADAPTIVE mode (threshold: {_adaptiveTriggerThreshold}), refresh interval: {_refreshInterval}");
        }
        else
        {
            _logger.WriteLine($"[FreeProxyListScraper] Initialized with FIXED interval mode: {_refreshInterval}");
        }
    }

    public void Start()
    {
        if (_scraperTask != null) return;
        _logger.WriteLine("[FreeProxyListScraper] Starting background scraper");
        _scraperTask = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
    }

    public void Stop()
    {
        if (_scraperTask == null) return;
        _logger.WriteLine("[FreeProxyListScraper] Stopping background scraper");
        _cts.Cancel();
        try { _scraperTask.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { }
        _scraperTask = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        await ScrapeProxiesAsync(ct);

        if (_adaptiveModeEnabled)
        {
            // In adaptive mode, we still run periodic checks but rely more on event-driven refreshes
            using var timer = new PeriodicTimer(_refreshInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    // Periodic health check - scrape even if pool is not low to refresh stale proxies
                    await ScrapeProxiesAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
        }
        else
        {
            // Legacy fixed interval mode
            using var timer = new PeriodicTimer(_refreshInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    await ScrapeProxiesAsync(ct);
                    if (_proxyPool.GetCount() < 100)
                    {
                        _logger.WriteLine("[FreeProxyListScraper] Pool below 100, immediate refresh");
                        await ScrapeProxiesAsync(ct);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// Handle pool low event - trigger immediate proxy scraping
    /// </summary>
    private async void HandlePoolLowEvent(int currentCount)
    {
        try
        {
            _logger.WriteLine($"[FreeProxyListScraper] 🚨 Pool low event triggered! Current: {currentCount}, threshold: {_adaptiveTriggerThreshold}");
            await ScrapeProxiesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"[FreeProxyListScraper] Error handling pool low event: {ex.Message}");
        }
    }

    public async Task ScrapeProxiesAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.WriteLine($"[FreeProxyListScraper] Scraping from {_proxyListUrl}");
            var html = await _httpClient.GetStringAsync(_proxyListUrl, ct);
            var proxies = await ParseProxyTableAsync(html, ct);
            _logger.WriteLine($"[FreeProxyListScraper] Parsed {proxies.Count} proxies");

            // Update statistics for scraped proxies
            foreach (var proxy in proxies)
            {
                _statistics.RecordProxyScraped();
            }

            var filtered = FilterProxies(proxies);
            _logger.WriteLine($"[FreeProxyListScraper] Filtered to {filtered.Count} proxies");
            var added = 0;
            foreach (var proxy in filtered)
            {
                var proxyUrlWithSource = ProxySourceHelper.AddSourceToProxyUrl(proxy.ToProxyUrl(), "FreeProxyList.net");
                if (_proxyPool.AddProxy(proxyUrlWithSource)) added++;
            }
            _logger.WriteLine($"[FreeProxyListScraper] Added {added} proxies (total: {_proxyPool.GetCount()})");

            // Log statistics
            _logger.WriteLine($"[FreeProxyListScraper] Stats: {_statistics.GetSummary()}");
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"[FreeProxyListScraper] Error: {ex.Message}");
        }
    }

    public async Task<List<ProxyInfo>> ParseProxyTableAsync(string html, CancellationToken ct = default)
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

    public ProxySourceStatistics GetStatistics() => _statistics;

    public void Dispose()
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
}
