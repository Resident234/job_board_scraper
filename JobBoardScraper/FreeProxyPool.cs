using JobBoardScraper.Helper.ConsoleHelper;

namespace JobBoardScraper;

/// <summary>
/// Thread-safe pool for managing free proxy servers
/// Maintains up to 1000 proxies with FIFO consumption
/// </summary>
public sealed class FreeProxyPool
{
    private readonly Queue<string> _proxies;
    private readonly object _lock;
    private readonly int _maxSize;
    private readonly ConsoleLogger? _logger;

    public FreeProxyPool(int maxSize = 1000, ConsoleLogger? logger = null)
    {
        if (maxSize <= 0)
            throw new ArgumentException("Max size must be positive", nameof(maxSize));

        _proxies = new Queue<string>();
        _lock = new object();
        _maxSize = maxSize;
        _logger = logger;
    }

    /// <summary>
    /// Get and remove the next proxy from the pool
    /// Returns null if pool is empty
    /// </summary>
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

    /// <summary>
    /// Add a proxy to the pool if under max size and not duplicate
    /// Returns true if added, false otherwise
    /// </summary>
    public bool AddProxy(string proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
            return false;

        lock (_lock)
        {
            // Check if already at max size
            if (_proxies.Count >= _maxSize)
            {
                _logger?.WriteLine($"Pool at max size ({_maxSize}), cannot add proxy");
                return false;
            }

            // Check for duplicates
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

    /// <summary>
    /// Get current pool size
    /// </summary>
    public int GetCount()
    {
        lock (_lock)
        {
            return _proxies.Count;
        }
    }

    /// <summary>
    /// Check if pool is empty
    /// </summary>
    public bool IsEmpty()
    {
        lock (_lock)
        {
            return _proxies.Count == 0;
        }
    }

    /// <summary>
    /// Clear all proxies from the pool
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            var count = _proxies.Count;
            _proxies.Clear();
            _logger?.WriteLine($"Cleared {count} proxies from pool");
        }
    }

    /// <summary>
    /// Remove oldest proxies to maintain size limit
    /// </summary>
    public int RemoveOldest(int count)
    {
        if (count <= 0)
            return 0;

        lock (_lock)
        {
            var removed = 0;
            while (removed < count && _proxies.Count > 0)
            {
                _proxies.Dequeue();
                removed++;
            }

            if (removed > 0)
            {
                _logger?.WriteLine($"Removed {removed} oldest proxies (remaining: {_proxies.Count})");
            }

            return removed;
        }
    }

    /// <summary>
    /// Get pool status information
    /// </summary>
    public string GetStatus()
    {
        lock (_lock)
        {
            return $"Pool: {_proxies.Count}/{_maxSize} proxies";
        }
    }
}
