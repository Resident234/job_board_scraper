namespace JobBoardScraper;

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
        
        // Создать начальный ротатор
        var proxies = _provider.GetProxies();
        _rotator = new ProxyRotator(proxies);

        // Настроить автоматическое обновление
        if (autoUpdate && _updateInterval > TimeSpan.Zero)
        {
            _updateTimer = new Timer(
                async _ => await UpdateProxiesAsync(),
                null,
                _updateInterval,
                _updateInterval
            );
        }
    }

    public bool IsEnabled => _rotator.IsEnabled;
    public int ProxyCount => _rotator.ProxyCount;

    /// <summary>
    /// Получить следующий прокси
    /// </summary>
    public System.Net.WebProxy? GetNextProxy()
    {
        return _rotator.GetNextProxy();
    }

    /// <summary>
    /// Получить текущий прокси
    /// </summary>
    public System.Net.WebProxy? GetCurrentProxy()
    {
        return _rotator.GetCurrentProxy();
    }

    /// <summary>
    /// Получить статус
    /// </summary>
    public string GetStatus()
    {
        return _rotator.GetStatus();
    }

    /// <summary>
    /// Получить текущий список прокси
    /// </summary>
    public List<string> GetProxies()
    {
        return _provider.GetProxies();
    }

    /// <summary>
    /// Обновить список прокси из провайдера
    /// </summary>
    public async Task UpdateProxiesAsync()
    {
        try
        {
            Console.WriteLine("[DynamicProxyRotator] Обновление списка прокси...");
            
            // Загрузить новые прокси
            var countBefore = _provider.GetProxies().Count;
            
            // Загрузить из разных источников
            await _provider.LoadFromProxyScrapeAsync();
            await _provider.LoadFromGeoNodeAsync(50);
            
            var countAfter = _provider.GetProxies().Count;
            var added = countAfter - countBefore;
            
            Console.WriteLine($"[DynamicProxyRotator] Добавлено {added} новых прокси (всего: {countAfter})");
            
            // Проверить и удалить нерабочие
            var removed = await _provider.RemoveDeadProxiesAsync();
            Console.WriteLine($"[DynamicProxyRotator] Удалено {removed} нерабочих прокси");
            
            var finalCount = _provider.GetProxies().Count;
            Console.WriteLine($"[DynamicProxyRotator] Итого рабочих прокси: {finalCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DynamicProxyRotator] Ошибка обновления: {ex.Message}");
        }
    }

    /// <summary>
    /// Принудительно обновить список прокси
    /// </summary>
    public async Task ForceUpdateAsync()
    {
        await UpdateProxiesAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _updateTimer?.Dispose();
        _disposed = true;
    }
}
