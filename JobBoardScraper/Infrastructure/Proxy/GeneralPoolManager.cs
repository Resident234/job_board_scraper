using JobBoardScraper.Infrastructure.Logging;

namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Менеджер для общего пула прокси (свежие, непроверенные)
/// </summary>
public class GeneralPoolManager : IProxyManager
{
    private readonly FreeProxyPool _pool;
    private readonly ConsoleLogger? _logger;
    private readonly int _maxFailures;
    private readonly Dictionary<string, int> _failureCounts = new();
    private readonly HashSet<string> _blacklist = new();
    private string? _currentProxy;
    private readonly object _lock = new();

    /// <summary>
    /// Событие: прокси подтверждён как рабочий (достиг лимита = работает)
    /// </summary>
    public event Action<string>? OnProxyVerified;

    /// <summary>
    /// Событие: прокси окончательно забанен
    /// </summary>
    public event Action<string>? OnProxyBlacklisted;

    public GeneralPoolManager(
        FreeProxyPool pool,
        int maxFailures = 3,
        ConsoleLogger? logger = null)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _maxFailures = maxFailures;
        _logger = logger;
    }

    public int Count => _pool.GetCount();
    public bool HasAvailableProxies => _pool.GetCount() > 0;
    public string? CurrentProxy => _currentProxy;
    public int BlacklistCount => _blacklist.Count;

    public string? GetNextProxy()
    {
        lock (_lock)
        {
            // Если есть текущий рабочий прокси — возвращаем его
            if (!string.IsNullOrEmpty(_currentProxy))
                return _currentProxy;

            // Пробуем получить новый прокси из пула
            for (int i = 0; i < Math.Min(10, _pool.GetCount() + 1); i++)
            {
                var proxy = _pool.GetNextProxy();
                if (proxy == null)
                    break;

                // Пропускаем забаненные
                if (_blacklist.Contains(proxy))
                    continue;

                _currentProxy = proxy;
                _logger?.WriteLine($"[GENERAL] → Прокси: {proxy}");
                return proxy;
            }

            // Логируем только когда прокси закончились
            _logger?.WriteLine("[GENERAL] ⚠ Нет доступных прокси в пуле");
            return null;
        }
    }

    public void ReportSuccess(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;

        lock (_lock)
        {
            // Сбрасываем счётчик ошибок
            _failureCounts.Remove(proxyUrl);
            _currentProxy = proxyUrl;
            _logger?.WriteLine($"[GENERAL] ✓ Прокси OK: {proxyUrl}");
        }
    }

    public void ReportFailure(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;

        lock (_lock)
        {
            if (!_failureCounts.ContainsKey(proxyUrl))
                _failureCounts[proxyUrl] = 0;

            _failureCounts[proxyUrl]++;
            var failures = _failureCounts[proxyUrl];

            _logger?.WriteLine($"[GENERAL] ⚠ Ошибка #{failures}/{_maxFailures}: {proxyUrl}");

            // Если превысили лимит ошибок — в blacklist
            if (failures >= _maxFailures)
            {
                _blacklist.Add(proxyUrl);
                _failureCounts.Remove(proxyUrl);
                _logger?.WriteLine($"[GENERAL] ✗ В blacklist: {proxyUrl}");
                OnProxyBlacklisted?.Invoke(proxyUrl);
            }

            // Сбрасываем текущий прокси, чтобы взять следующий
            if (_currentProxy == proxyUrl)
                _currentProxy = null;
        }
    }

    public void ReportDailyLimitReached(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;

        lock (_lock)
        {
            _logger?.WriteLine($"[GENERAL] ★ Прокси достиг лимита (работает!): {proxyUrl}");
            
            // Прокси работает — уведомляем координатор для добавления в whitelist
            OnProxyVerified?.Invoke(proxyUrl);
            
            // Сбрасываем текущий, чтобы взять следующий
            _currentProxy = null;
        }
    }

    /// <summary>
    /// Добавить прокси в blacklist вручную
    /// </summary>
    public void AddToBlacklist(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;
        lock (_lock)
        {
            _blacklist.Add(proxyUrl);
            _failureCounts.Remove(proxyUrl);
            if (_currentProxy == proxyUrl)
                _currentProxy = null;
        }
    }

    /// <summary>
    /// Очистить blacklist
    /// </summary>
    public void ClearBlacklist()
    {
        lock (_lock)
        {
            _blacklist.Clear();
            _logger?.WriteLine("[GENERAL] Blacklist очищен");
        }
    }

    public string GetStatus()
    {
        lock (_lock)
        {
            return $"Pool: {_pool.GetCount()} | Blacklist: {_blacklist.Count} | Current: {_currentProxy ?? "none"}";
        }
    }
}
