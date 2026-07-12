using JobBoardScraper.Infrastructure.Logging;

namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Thread-safe pool for managing free proxy servers with adaptive monitoring
/// </summary>
public sealed class ProxyPool
{
    private readonly Queue<string> _proxies;
    private readonly object _lock;
    private readonly int _maxSize;
    private readonly ConsoleLogger? _logger;
    private int _lowWaterMark;

    // Event for notifying when pool level drops below threshold
    public event Action<int>? OnPoolLow;

    public ProxyPool(int maxSize = 1000, ConsoleLogger? logger = null, int lowWaterMark = 100)
    {
        if (maxSize <= 0)
            throw new ArgumentException("Max size must be positive", nameof(maxSize));
        if (lowWaterMark <= 0 || lowWaterMark > maxSize)
            throw new ArgumentException("Low water mark must be positive and less than max size", nameof(lowWaterMark));

        _proxies = new Queue<string>();
        _lock = new object();
        _maxSize = maxSize;
        _logger = logger;
        _lowWaterMark = lowWaterMark;
    }

    public string? GetNextProxy()
    {
        lock (_lock)
        {
            if (_proxies.Count == 0)
            {
                _logger?.WriteLine("Pool empty, no proxy available");
                return null;
            }
            var proxy = _proxies.Dequeue();
            _logger?.WriteLine($"Retrieved proxy: {proxy} (remaining: {_proxies.Count})");
            return proxy;
        }
    }

    public bool AddProxy(string proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl)) return false;

        lock (_lock)
        {
            if (_proxies.Count >= _maxSize)
            {
                _logger?.WriteLine($"Pool at max size ({_maxSize}), cannot add proxy");
                return false;
            }
            if (_proxies.Contains(proxyUrl))
            {
                _logger?.WriteLine($"Proxy already in pool: {proxyUrl}");
                return false;
            }
            _proxies.Enqueue(proxyUrl);
            _logger?.WriteLine($"Added proxy: {proxyUrl} (total: {_proxies.Count})");
            return true;
        }
    }

    public int GetCount()
    {
        lock (_lock) { return _proxies.Count; }
    }

    public bool IsEmpty()
    {
        lock (_lock) { return _proxies.Count == 0; }
    }

    public void Clear()
    {
        lock (_lock)
        {
            var count = _proxies.Count;
            _proxies.Clear();
            _logger?.WriteLine($"Cleared {count} proxies from pool");
        }
    }

    public int RemoveOldest(int count)
    {
        if (count <= 0) return 0;
        lock (_lock)
        {
            var removed = 0;
            while (removed < count && _proxies.Count > 0)
            {
                _proxies.Dequeue();
                removed++;
            }
            if (removed > 0)
                _logger?.WriteLine($"Removed {removed} oldest proxies (remaining: {_proxies.Count})");
            return removed;
        }
    }

    public string GetStatus()
    {
        lock (_lock) { return $"Pool: {_proxies.Count}/{_maxSize} proxies"; }
    }

    /// <summary>
    /// Check if pool level is below low water mark and trigger event if needed
    /// </summary>
    public void CheckPoolLevel()
    {
        lock (_lock)
        {
            if (_proxies.Count < _lowWaterMark)
            {
                _logger?.WriteLine($"[POOL] ⚠ Low pool level: {_proxies.Count}/{_maxSize} (threshold: {_lowWaterMark})");
                OnPoolLow?.Invoke(_proxies.Count);
            }
        }
    }

    /// <summary>
    /// Get the low water mark threshold
    /// </summary>
    public int GetLowWaterMark() => _lowWaterMark;

    /// <summary>
    /// Set a new low water mark threshold
    /// </summary>
    public void SetLowWaterMark(int newThreshold)
    {
        if (newThreshold <= 0 || newThreshold > _maxSize)
            throw new ArgumentException("Low water mark must be positive and less than max size", nameof(newThreshold));

        lock (_lock)
        {
            _lowWaterMark = newThreshold;
            _logger?.WriteLine($"[POOL] Low water mark updated to: {_lowWaterMark}");
        }
    }
}
