using JobBoardScraper.Infrastructure.Logging;
using AngleSharp;
using System.Text.Json;

namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Базовый класс для скрейперов прокси. Определяет общий интерфейс и логику
/// для всех скрейперов, работающих с ProxyPool.
/// </summary>
/// <typeparam name="TProxy">Тип прокси, который парсится конкретным скрейпером</typeparam>
public abstract class ProxyScraper<TProxy> : IDisposable where TProxy : notnull
{
    protected readonly ProxyPool _proxyPool;
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
        ProxyPool proxyPool,
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
            _logger.WriteLine($"Initialized with ADAPTIVE mode (threshold: {_adaptiveTriggerThreshold}), refresh interval: {_refreshInterval}");
        }
        else
        {
            _logger.WriteLine($"Initialized with FIXED interval mode: {_refreshInterval}");
        }
    }

    public void Start()
    {
        if (_scraperTask != null) return;
        _logger.WriteLine("Starting background scraper");
        _scraperTask = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
    }

    public void Stop()
    {
        if (_scraperTask == null) return;
        _logger.WriteLine("Stopping background scraper");
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
            _logger.WriteLine($"Fetching from {_sourceUrl}");
            var response = await _httpClient.GetStringAsync(_sourceUrl, ct);
            var proxies = await ParseProxiesAsync(response, ct);
            _logger.WriteLine($"Parsed {proxies.Count} proxies");

            await ProcessScrapedProxiesAsync(proxies, ct);
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"Error: {ex.Message}");
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
            var proxyStr = proxy.ToString() ?? string.Empty;
            var proxyUrlWithSource = ProxyInfo.AddSourceToProxyUrl(proxyStr, _sourceNamePrefix);
            if (_proxyPool.AddProxy(proxyUrlWithSource))
                added++;
        }
        _logger.WriteLine($"Added {added} proxies (total: {_proxyPool.GetCount()})");

        // Log statistics
        _logger.WriteLine($"Stats: {_statistics.GetSummary()}");
    }

    private async void HandlePoolLowEvent(int currentCount)
    {
        try
        {
            _logger.WriteLine($"🚨 Pool low event triggered! Current: {currentCount}, threshold: {_adaptiveTriggerThreshold}");
            await ScrapeProxiesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"Error handling pool low event: {ex.Message}");
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

}

/// <summary>
/// Scraper for free-proxy-list.net that periodically fetches and maintains a pool of free proxies
/// </summary>
public sealed class FreeProxyListScraper : ProxyScraper<ProxyInfo>
{
    public FreeProxyListScraper(
        ProxyPool proxyPool,
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
        catch (Exception ex) { _logger.WriteLine($"Parse error: {ex.Message}"); }
        return proxies;
    }

}

/// <summary>
/// Scraper для ProxyScrape API - простой GET запрос, возвращает список прокси в формате ip:port
/// https://api.proxyscrape.com/v4/free-proxy-list/get?request=displayproxies&protocol=http&timeout=10000&country=all&ssl=all&anonymity=all
/// </summary>
public sealed class ProxyScrapeScraper : ProxyScraper<string>
{
    public ProxyScrapeScraper(
        ProxyPool proxyPool,
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
            var proxyUrl = ProxyInfo.ToProxyUrl(trimmed);
            if (proxyUrl != null)
            {
                proxies.Add(proxyUrl);
            }
        }

        return proxies;
    });
}


}

/// <summary>
/// Scraper для GeoNode API - возвращает список прокси в JSON формате.
/// https://proxylist.geonode.com/api/proxy-list?limit=200&page=1&sort_by=lastChecked&sort_type=desc
/// </summary>
public sealed class GeoNodeScraper : ProxyScraper<string>
{
    public GeoNodeScraper(
        ProxyPool proxyPool,
        TimeSpan? refreshInterval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly,
        string? apiUrl = null,
        bool adaptiveModeEnabled = true,
        int adaptiveTriggerThreshold = 100)
        : base(proxyPool, refreshInterval, outputMode, "GeoNodeScraper", "GeoNode",
               apiUrl ?? AppConfig.GeoNodeApiUrl, adaptiveModeEnabled, adaptiveTriggerThreshold)
    {
    }

    protected override Task<List<string>> ParseProxiesAsync(string json, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var proxies = new List<string>();

            if (string.IsNullOrWhiteSpace(json))
                return proxies;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // GeoNode API возвращает { "data": [ { "ip": "...", "port": "...", "protocols": ["http", ...] }, ... ] }
                if (!root.TryGetProperty("data", out var data))
                    return proxies;

                foreach (var item in data.EnumerateArray())
                {
                    if (!item.TryGetProperty("ip", out var ipEl) || ipEl.ValueKind != JsonValueKind.String)
                        continue;
                    if (!item.TryGetProperty("port", out var portEl) || portEl.ValueKind != JsonValueKind.String)
                        continue;
                    if (!item.TryGetProperty("protocols", out var protocolsEl) || protocolsEl.ValueKind != JsonValueKind.Array)
                        continue;

                    var ip = ipEl.GetString();
                    var port = portEl.GetString();
                    if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(port))
                        continue;

                    // Добавляем для каждого протокола (http, https, socks4, socks5)
                    foreach (var protoEl in protocolsEl.EnumerateArray())
                    {
                        var proto = protoEl.GetString()?.ToLower();
                        if (proto == "http" || proto == "https" || proto == "socks4" || proto == "socks5")
                        {
                            proxies.Add(ProxyInfo.BuildProxyUrl(ip, port, proto));
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.WriteLine($"JSON parse error: {ex.Message}");
            }

            return proxies;
        });
    }
}

/// <summary>
/// Запускает все прокси-скрейперы и управляет их жизненным циклом.
/// </summary>
public sealed class ProxyScraperLauncher : IDisposable
{
    private readonly ProxyPool _proxyPool;
    private readonly ConsoleLogger _logger;
    private readonly FreeProxyListScraper? _freeProxyListScraper;
    private readonly ProxyScrapeScraper? _proxyScrapeScraper;
    private readonly GeoNodeScraper? _geoNodeScraper;
    private bool _disposed;

    private ProxyScraperLauncher(
        ProxyPool pool,
        ConsoleLogger logger,
        FreeProxyListScraper? free,
        ProxyScrapeScraper? scrape,
        GeoNodeScraper? geoNode)
    {
        _proxyPool = pool;
        _logger = logger;
        _freeProxyListScraper = free;
        _proxyScrapeScraper = scrape;
        _geoNodeScraper = geoNode;
    }

    /// <summary>
    /// Создать и запустить все скрейперы.
    /// </summary>
    public static ProxyScraperLauncher LaunchAll(
        int poolMaxSize,
        int refreshIntervalMinutes,
        int adaptiveTriggerThreshold,
        string freeProxyListUrl,
        string proxyScrapeApiUrl,
        string geoNodeApiUrl,
        bool freeProxyListEnabled,
        bool proxyScrapeEnabled,
        bool geoNodeEnabled,
        OutputMode outputMode)
    {
        var logger = new ConsoleLogger("ProxyScraperLauncher");
        logger.SetOutputMode(outputMode);

        logger.WriteLine($"Refresh interval: {refreshIntervalMinutes} минут");
        logger.WriteLine($"Pool max size: {poolMaxSize}");
        logger.WriteLine($"Proxy list URL: {freeProxyListUrl}");

        var pool = new ProxyPool(
            maxSize: poolMaxSize,
            logger: new ConsoleLogger("ProxyPool"),
            lowWaterMark: 200);

        FreeProxyListScraper? freeScraper = null;
        if (freeProxyListEnabled)
        {
            freeScraper = new FreeProxyListScraper(
                pool,
                refreshInterval: TimeSpan.FromMinutes(refreshIntervalMinutes),
                outputMode: outputMode,
                proxyListUrl: freeProxyListUrl,
                adaptiveModeEnabled: true,
                adaptiveTriggerThreshold: adaptiveTriggerThreshold);
            freeScraper.Start();
        }

        ProxyScrapeScraper? scrapeScraper = null;
        if (proxyScrapeEnabled)
        {
            scrapeScraper = new ProxyScrapeScraper(
                pool,
                refreshInterval: TimeSpan.FromMinutes(refreshIntervalMinutes),
                outputMode: outputMode,
                apiUrl: proxyScrapeApiUrl,
                adaptiveModeEnabled: true,
                adaptiveTriggerThreshold: adaptiveTriggerThreshold);
            scrapeScraper.Start();
        }

        GeoNodeScraper? geoScraper = null;
        if (geoNodeEnabled)
        {
            geoScraper = new GeoNodeScraper(
                pool,
                refreshInterval: TimeSpan.FromMinutes(refreshIntervalMinutes),
                outputMode: outputMode,
                apiUrl: geoNodeApiUrl,
                adaptiveModeEnabled: true,
                adaptiveTriggerThreshold: adaptiveTriggerThreshold);
            geoScraper.Start();
        }

        logger.WriteLine($"General Pool: {pool.GetCount()} прокси");

        return new ProxyScraperLauncher(pool, logger, freeScraper, scrapeScraper, geoScraper);
    }

    /// <summary>
    /// Пул прокси.
    /// </summary>
    public ProxyPool Pool => _proxyPool;

    /// <summary>
    /// Зарегистрировать статистику скрейперов в координаторе.
    /// </summary>
    public void RegisterStatistics(ProxyCoordinator coordinator)
    {
        if (_freeProxyListScraper != null)
            coordinator.RegisterScraperStatistics(_freeProxyListScraper.GetStatistics());
        if (_proxyScrapeScraper != null)
            coordinator.RegisterScraperStatistics(_proxyScrapeScraper.GetStatistics());
        if (_geoNodeScraper != null)
            coordinator.RegisterScraperStatistics(_geoNodeScraper.GetStatistics());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _freeProxyListScraper?.Stop();
        _freeProxyListScraper?.Dispose();
        _proxyScrapeScraper?.Stop();
        _proxyScrapeScraper?.Dispose();
        _geoNodeScraper?.Stop();
        _geoNodeScraper?.Dispose();
        _logger.Dispose();
    }
}
