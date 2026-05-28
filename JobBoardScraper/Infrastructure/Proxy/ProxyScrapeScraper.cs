using JobBoardScraper.Infrastructure.Logging;

namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Scraper для ProxyScrape API - простой GET запрос, возвращает список прокси в формате ip:port
/// https://api.proxyscrape.com/v4/free-proxy-list/get?request=displayproxies&protocol=http&timeout=10000&country=all&ssl=all&anonymity=all
/// </summary>
public sealed class ProxyScrapeScraper : IDisposable
{
    private readonly FreeProxyPool _proxyPool;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _refreshInterval;
    private readonly ConsoleLogger _logger;
    private readonly CancellationTokenSource _cts;
    private Task? _scraperTask;
    private bool _disposed;
    private readonly string _apiUrl;
    private bool _adaptiveModeEnabled;
    private int _adaptiveTriggerThreshold;

    public ProxyScrapeScraper(
        FreeProxyPool proxyPool,
        TimeSpan? refreshInterval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly,
        string? apiUrl = null,
        bool adaptiveModeEnabled = true,
        int adaptiveTriggerThreshold = 100)
    {
        _proxyPool = proxyPool ?? throw new ArgumentNullException(nameof(proxyPool));
        _refreshInterval = refreshInterval ?? TimeSpan.FromMinutes(10);
        _apiUrl = apiUrl ?? AppConfig.ProxyScrapeApiUrl;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _logger = new ConsoleLogger("ProxyScrapeScraper");
        _logger.SetOutputMode(outputMode);
        _cts = new CancellationTokenSource();
        _adaptiveModeEnabled = adaptiveModeEnabled;
        _adaptiveTriggerThreshold = adaptiveTriggerThreshold;

        if (_adaptiveModeEnabled)
        {
            _proxyPool.OnPoolLow += HandlePoolLowEvent;
            _logger.WriteLine($"Initialized with ADAPTIVE mode (threshold: {_adaptiveTriggerThreshold}), refresh interval: {_refreshInterval}");
        }
        else
        {
            _logger.WriteLine($"Initialized with refresh interval: {_refreshInterval}");
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
            _logger.WriteLine($"🚨 Pool low event triggered! Current: {currentCount}, threshold: {_adaptiveTriggerThreshold}");
            await ScrapeProxiesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"Error handling pool low event: {ex.Message}");
        }
    }

    public async Task ScrapeProxiesAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.WriteLine($"Fetching from API...");
            var response = await _httpClient.GetStringAsync(_apiUrl, ct);
            var proxies = ParseProxyList(response);
            _logger.WriteLine($"Parsed {proxies.Count} proxies");

            var added = 0;
            foreach (var proxyUrl in proxies)
            {
                if (_proxyPool.AddProxy(proxyUrl))
                    added++;
            }
            _logger.WriteLine($"Added {added} proxies (total: {_proxyPool.GetCount()})");
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Парсит ответ API - каждая строка содержит ip:port
    /// </summary>
    public List<string> ParseProxyList(string response)
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
