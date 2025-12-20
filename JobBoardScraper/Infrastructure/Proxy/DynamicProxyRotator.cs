namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Ротатор прокси с динамическим обновлением списка
/// </summary>
public class DynamicProxyRotator : IDisposable
{
    private readonly ProxyRotator _rotator;
    private readonly ProxyProvider _provider;
    private readonly Timer? _updateTimer;
    private readonly TimeSpan _updateInterval;
    private bool _disposed;

    public DynamicProxyRotator(
        ProxyProvider provider,
        TimeSpan? updateInterval = null,
        bool autoUpdate = true)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _updateInterval = updateInterval ?? TimeSpan.FromHours(1);
        var proxies = _provider.GetProxies();
        _rotator = new ProxyRotator(proxies);

        if (autoUpdate && _updateInterval > TimeSpan.Zero)
        {
            _updateTimer = new Timer(
                async _ => await UpdateProxiesAsync(),
                null,
                _updateInterval,
                _updateInterval);
        }
    }

    public bool IsEnabled => _rotator.IsEnabled;
    public int ProxyCount => _rotator.ProxyCount;

    public System.Net.WebProxy? GetNextProxy() => _rotator.GetNextProxy();
    public System.Net.WebProxy? GetCurrentProxy() => _rotator.GetCurrentProxy();
    public string GetStatus() => _rotator.GetStatus();
    public List<string> GetProxies() => _provider.GetProxies();

    public async Task UpdateProxiesAsync()
    {
        try
        {
            Console.WriteLine("[DynamicProxyRotator] Updating proxies...");
            var countBefore = _provider.GetProxies().Count;
            await _provider.LoadFromProxyScrapeAsync();
            await _provider.LoadFromGeoNodeAsync(50);
            var countAfter = _provider.GetProxies().Count;
            Console.WriteLine($"[DynamicProxyRotator] Added {countAfter - countBefore} proxies (total: {countAfter})");
            var removed = await _provider.RemoveDeadProxiesAsync();
            Console.WriteLine($"[DynamicProxyRotator] Removed {removed} dead proxies");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DynamicProxyRotator] Error: {ex.Message}");
        }
    }

    public async Task ForceUpdateAsync() => await UpdateProxiesAsync();

    public void Dispose()
    {
        if (_disposed) return;
        _updateTimer?.Dispose();
        _disposed = true;
    }
}
