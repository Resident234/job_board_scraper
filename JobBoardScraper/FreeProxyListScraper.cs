using JobBoardScraper.Helper.ConsoleHelper;
using AngleSharp;
using AngleSharp.Html.Dom;

namespace JobBoardScraper;

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

    public FreeProxyListScraper(
        FreeProxyPool proxyPool,
        TimeSpan? refreshInterval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly,
        string? proxyListUrl = null)
    {
        _proxyPool = proxyPool ?? throw new ArgumentNullException(nameof(proxyPool));
        _refreshInterval = refreshInterval ?? TimeSpan.FromMinutes(10);
        _proxyListUrl = proxyListUrl ?? AppConfig.FreeProxyListUrl;
        
        _httpClient = new HttpClient 
        { 
            Timeout = TimeSpan.FromSeconds(30) 
        };
        
        _logger = new ConsoleLogger("FreeProxyListScraper");
        _logger.SetOutputMode(outputMode);
        _cts = new CancellationTokenSource();
        
        _logger.WriteLine($"[FreeProxyListScraper] Initialized with refresh interval: {_refreshInterval}");
    }

    /// <summary>
    /// Start the background scraping task
    /// </summary>
    public void Start()
    {
        if (_scraperTask != null)
        {
            _logger.WriteLine("[FreeProxyListScraper] Already started");
            return;
        }

        _logger.WriteLine("[FreeProxyListScraper] Starting background scraper");
        _scraperTask = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
    }

    /// <summary>
    /// Stop the background scraping task
    /// </summary>
    public void Stop()
    {
        if (_scraperTask == null)
        {
            _logger.WriteLine("[FreeProxyListScraper] Not running");
            return;
        }

        _logger.WriteLine("[FreeProxyListScraper] Stopping background scraper");
        _cts.Cancel();
        
        try
        {
            _scraperTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
            // Expected when cancelling
        }
        
        _scraperTask = null;
        _logger.WriteLine("[FreeProxyListScraper] Stopped");
    }

    /// <summary>
    /// Main background loop
    /// </summary>
    private async Task RunAsync(CancellationToken ct)
    {
        // Perform initial scrape immediately
        await ScrapeProxiesAsync(ct);

        // Then run periodic refresh
        using var timer = new PeriodicTimer(_refreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await ScrapeProxiesAsync(ct);
                
                // Check if pool needs immediate refresh
                if (_proxyPool.GetCount() < 100)
                {
                    _logger.WriteLine("[FreeProxyListScraper] Pool below 100 proxies, triggering immediate refresh");
                    await ScrapeProxiesAsync(ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.WriteLine("[FreeProxyListScraper] Scraper cancelled");
        }
    }

    /// <summary>
    /// Perform one scrape cycle
    /// </summary>
    public async Task ScrapeProxiesAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.WriteLine($"[FreeProxyListScraper] Scraping proxies from {_proxyListUrl}");
            
            var countBefore = _proxyPool.GetCount();
            
            // Fetch HTML
            var html = await _httpClient.GetStringAsync(_proxyListUrl, ct);
            
            // Parse proxy table
            var proxies = await ParseProxyTableAsync(html, ct);
            _logger.WriteLine($"[FreeProxyListScraper] Parsed {proxies.Count} proxies from HTML");
            
            // Filter proxies
            var filtered = FilterProxies(proxies);
            _logger.WriteLine($"[FreeProxyListScraper] Filtered to {filtered.Count} proxies (elite/anonymous only)");
            
            // Add to pool
            var added = 0;
            foreach (var proxy in filtered)
            {
                if (_proxyPool.AddProxy(proxy.ToProxyUrl()))
                {
                    added++;
                }
            }
            
            var countAfter = _proxyPool.GetCount();
            _logger.WriteLine($"[FreeProxyListScraper] Added {added} proxies to pool (total: {countAfter})");
            
            // Remove oldest if over limit
            if (countAfter > 1000)
            {
                var toRemove = countAfter - 1000;
                _proxyPool.RemoveOldest(toRemove);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.WriteLine($"[FreeProxyListScraper] HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            _logger.WriteLine($"[FreeProxyListScraper] Request timeout: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"[FreeProxyListScraper] Error scraping proxies: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse HTML table and extract proxy data
    /// </summary>
    public async Task<List<ProxyInfo>> ParseProxyTableAsync(string html, CancellationToken ct = default)
    {
        var proxies = new List<ProxyInfo>();
        
        try
        {
            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html), ct);
            
            // Find the table
            var table = document.QuerySelector("table.table.table-striped.table-bordered");
            if (table == null)
            {
                _logger.WriteLine("[FreeProxyListScraper] Could not find proxy table in HTML");
                return proxies;
            }
            
            // Get tbody rows
            var rows = table.QuerySelectorAll("tbody tr");
            
            foreach (var row in rows)
            {
                try
                {
                    var cells = row.QuerySelectorAll("td");
                    if (cells.Length < 8)
                        continue;
                    
                    var ipAddress = cells[0].TextContent.Trim();
                    var port = cells[1].TextContent.Trim();
                    var countryCode = cells[2].TextContent.Trim();
                    var anonymity = cells[4].TextContent.Trim();
                    var httpsText = cells[6].TextContent.Trim();
                    var lastChecked = cells[7].TextContent.Trim();
                    
                    var httpsSupport = httpsText.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    
                    var proxyInfo = new ProxyInfo(
                        ipAddress,
                        port,
                        countryCode,
                        anonymity,
                        httpsSupport,
                        lastChecked
                    );
                    
                    proxies.Add(proxyInfo);
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"[FreeProxyListScraper] Error parsing row: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"[FreeProxyListScraper] Error parsing HTML: {ex.Message}");
        }
        
        return proxies;
    }

    /// <summary>
    /// Filter proxies by quality criteria
    /// </summary>
    public List<ProxyInfo> FilterProxies(List<ProxyInfo> proxies)
    {
        var filtered = proxies
            .Where(p => !p.IsTransparent())  // Exclude transparent
            .Where(p => p.IsValidIp())       // Exclude invalid IPs
            .Where(p => p.IsElite() || p.IsAnonymous())  // Only elite or anonymous
            .OrderByDescending(p => p.GetQualityScore())  // Elite first
            .ThenByDescending(p => p.IsRecentlyChecked()) // Recent first
            .ToList();
        
        return filtered;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        
        Stop();
        _cts?.Dispose();
        _httpClient?.Dispose();
        _logger?.Dispose();
        _disposed = true;
    }
}
