using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Models;

namespace JobBoardScraper.Proxy;

/// <summary>
/// Менеджер для управления whitelist прокси (проверенные, работающие прокси)
/// Отвечает только за whitelist, не управляет general pool
/// </summary>
public class ProxyWhitelistManager : IProxyManager, IDisposable
{
    private readonly IWhitelistStorage _storage;
    private readonly TimeSpan _cooldownPeriod;
    private readonly TimeSpan _autosaveInterval;
    private readonly int _maxRetryAttempts;
    private readonly ConsoleLogger? _logger;

    private List<WhitelistProxyEntry> _whitelist = new();
    private int _whitelistIndex;
    private DateTime _lastAutosave;
    private string? _currentProxy;
    private bool _disposed;
    private Timer? _autosaveTimer;
    private readonly object _lock = new();

    public ProxyWhitelistManager(
        IWhitelistStorage storage,
        TimeSpan? cooldownPeriod = null,
        int? maxRetryAttempts = null,
        TimeSpan? autosaveInterval = null,
        ConsoleLogger? logger = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _cooldownPeriod = cooldownPeriod ?? AppConfig.ProxyWhitelistCooldownPeriod;
        _maxRetryAttempts = maxRetryAttempts ?? AppConfig.ProxyWhitelistMaxRetryAttempts;
        _autosaveInterval = autosaveInterval ?? AppConfig.ProxyWhitelistAutosaveInterval;
        _logger = logger;
        _lastAutosave = DateTime.UtcNow;
        StartAutosaveTimer();
    }

    private void StartAutosaveTimer()
    {
        _autosaveTimer = new Timer(_ => AutosaveCallback(), null, _autosaveInterval, _autosaveInterval);
        _logger?.WriteLine($"Autosave timer started: {_autosaveInterval.TotalMinutes} min");
    }

    private void AutosaveCallback()
    {
        if (_disposed) return;
        try
        {
            SaveStateAsync().GetAwaiter().GetResult();
            _lastAutosave = DateTime.UtcNow;
            LogStats("Автосохранение");
        }
        catch (Exception ex) { _logger?.WriteLine($"[WHITELIST] Autosave error: {ex.Message}"); }
    }

    public string? CurrentProxy => _currentProxy;
    public int WhitelistCount => _whitelist.Count;
    public int Count => _whitelist.Count;
    public bool HasAvailableProxies => _whitelist.Any(e => IsProxyAvailable(e));

    public async Task LoadStateAsync()
    {
        _whitelist = await _storage.LoadAsync();
        _whitelistIndex = 0;
        LogStats("Whitelist загружен");
    }

    public async Task SaveStateAsync()
    {
        await _storage.SaveAsync(_whitelist);
        _logger?.WriteLine($"[WHITELIST] Дамп в JSON ({_whitelist.Count} прокси)");
    }

    private void LogStats(string context)
    {
        _logger?.WriteLine($"[WHITELIST] {context} | Count: {_whitelist.Count}");
    }

    public void ReportSuccess(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;
        
        lock (_lock)
        {
            var entry = _whitelist.FirstOrDefault(e => e.ProxyUrl == proxyUrl);
            if (entry != null)
            {
                entry.IsFailed = false;
                entry.RetryCount = 0;
                entry.FailedSince = null;
                entry.LastUsed = DateTime.UtcNow;
                _logger?.WriteLine($"[WHITELIST] ✓ Прокси OK: {proxyUrl}");
            }
            _currentProxy = proxyUrl;
        }
    }

    public void ReportFailure(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;
        
        lock (_lock)
        {
            var entry = _whitelist.FirstOrDefault(e => e.ProxyUrl == proxyUrl);
            if (entry != null)
            {
                entry.IsFailed = true;
                entry.RetryCount++;
                entry.FailedSince ??= DateTime.UtcNow;
                if (entry.RetryCount >= _maxRetryAttempts)
                {
                    _whitelist.Remove(entry);
                    _logger?.WriteLine($"[WHITELIST] ✗ Удалён после {_maxRetryAttempts} попыток: {proxyUrl}");
                    LogStats("После удаления");
                }
                else
                    _logger?.WriteLine($"[WHITELIST] ⚠ Ошибка #{entry.RetryCount}/{_maxRetryAttempts}: {proxyUrl}");
            }
            
            if (_currentProxy == proxyUrl)
                _currentProxy = null;
        }
    }

    public void ReportDailyLimitReached(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;
        
        lock (_lock)
        {
            var entry = _whitelist.FirstOrDefault(e => e.ProxyUrl == proxyUrl);
            if (entry != null)
            {
                entry.LastUsed = DateTime.UtcNow;
                _logger?.WriteLine($"[WHITELIST] Лимит для прокси: {proxyUrl}");
            }
            else
            {
                // Добавляем новый прокси в whitelist (он подтверждён как рабочий)
                _whitelist.Add(new WhitelistProxyEntry { ProxyUrl = proxyUrl, LastUsed = DateTime.UtcNow });
                _logger?.WriteLine($"[WHITELIST] ★ Добавлен в whitelist: {proxyUrl}");
                LogStats("После добавления");
            }
            
            _currentProxy = null;
        }
    }
    
    /// <summary>
    /// Добавить прокси в whitelist вручную
    /// </summary>
    public void AddProxy(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;
        
        lock (_lock)
        {
            if (_whitelist.Any(e => e.ProxyUrl == proxyUrl))
                return;
                
            _whitelist.Add(new WhitelistProxyEntry { ProxyUrl = proxyUrl });
            _logger?.WriteLine($"[WHITELIST] + Добавлен: {proxyUrl}");
        }
    }

    public string? GetNextProxy()
    {
        lock (_lock)
        {
            // Если есть текущий рабочий прокси — возвращаем его
            if (!string.IsNullOrEmpty(_currentProxy))
                return _currentProxy;

            var proxy = GetNextWhitelistProxy();
            if (proxy != null)
            {
                _currentProxy = proxy;
                return proxy;
            }

            _logger?.WriteLine("[WHITELIST] ⚠ Нет доступных прокси в whitelist");
            return null;
        }
    }

    private string? GetNextWhitelistProxy()
    {
        var checkedCount = 0;
        while (checkedCount < _whitelist.Count)
        {
            if (_whitelistIndex >= _whitelist.Count) _whitelistIndex = 0;
            var entry = _whitelist[_whitelistIndex++];
            checkedCount++;
            if (IsProxyAvailable(entry))
            {
                _logger?.WriteLine($"[WHITELIST] → WL прокси: {entry.ProxyUrl}");
                return entry.ProxyUrl;
            }
        }
        return null;
    }

    private bool IsProxyAvailable(WhitelistProxyEntry entry)
    {
        var cooldownPassed = entry.IsCooldownPassed(_cooldownPeriod);
        if (!cooldownPassed) return false;
        if (entry.IsFailed && !cooldownPassed) return false;
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _autosaveTimer?.Dispose();
        SaveStateAsync().GetAwaiter().GetResult();
        _logger?.WriteLine("ProxyWhitelistManager disposed");
    }
}
