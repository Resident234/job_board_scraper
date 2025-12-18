using JobBoardScraper.Helper.ConsoleHelper;

namespace JobBoardScraper.Proxy;

/// <summary>
/// Менеджер для управления whitelist прокси с умным алгоритмом выбора
/// </summary>
public class ProxyWhitelistManager : IDisposable
{
    private readonly IWhitelistStorage _storage;
    private readonly FreeProxyPool _generalPool;
    private readonly TimeSpan _cooldownPeriod;
    private readonly TimeSpan _recheckInterval;
    private readonly TimeSpan _autosaveInterval;
    private readonly int _maxRetryAttempts;
    private readonly ConsoleLogger? _logger;

    private List<WhitelistProxyEntry> _whitelist = new();
    private int _whitelistIndex;
    private DateTime _lastWhitelistCheck;
    private DateTime _switchedToGeneralPoolAt;
    private DateTime _lastAutosave;
    private string? _currentProxy;
    private bool _usingGeneralPool;
    private bool _disposed;
    private Timer? _autosaveTimer;

    public ProxyWhitelistManager(
        IWhitelistStorage storage,
        FreeProxyPool generalPool,
        TimeSpan? cooldownPeriod = null,
        TimeSpan? recheckInterval = null,
        int? maxRetryAttempts = null,
        TimeSpan? autosaveInterval = null,
        ConsoleLogger? logger = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _generalPool = generalPool ?? throw new ArgumentNullException(nameof(generalPool));
        _cooldownPeriod = cooldownPeriod ?? AppConfig.ProxyWhitelistCooldownPeriod;
        _recheckInterval = recheckInterval ?? AppConfig.ProxyWhitelistRecheckInterval;
        _maxRetryAttempts = maxRetryAttempts ?? AppConfig.ProxyWhitelistMaxRetryAttempts;
        _autosaveInterval = autosaveInterval ?? AppConfig.ProxyWhitelistAutosaveInterval;
        _logger = logger;
        _lastWhitelistCheck = DateTime.UtcNow;
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
            LogPoolStats("Автосохранение");
        }
        catch (Exception ex) { _logger?.WriteLine($"[WHITELIST] Autosave error: {ex.Message}"); }
    }

    public string? CurrentProxy => _currentProxy;
    public int WhitelistCount => _whitelist.Count;
    public bool IsUsingGeneralPool => _usingGeneralPool;

    public async Task LoadStateAsync()
    {
        _whitelist = await _storage.LoadAsync();
        _whitelistIndex = 0;
        LogPoolStats("Whitelist загружен");
    }

    public async Task SaveStateAsync()
    {
        await _storage.SaveAsync(_whitelist);
        _logger?.WriteLine($"[WHITELIST] Дамп в JSON ({_whitelist.Count} прокси)");
    }

    private void LogPoolStats(string context)
    {
        var activePool = _usingGeneralPool ? "общий пул" : "белый список";
        _logger?.WriteLine($"[WHITELIST] {context} | Пул: {activePool} | WL: {_whitelist.Count} | Pool: {_generalPool.GetCount()}");
    }

    public void ReportSuccess(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;
        var entry = _whitelist.FirstOrDefault(e => e.ProxyUrl == proxyUrl);
        if (entry != null)
        {
            entry.IsFailed = false;
            entry.RetryCount = 0;
            entry.FailedSince = null;
            entry.LastUsed = DateTime.UtcNow;
            _logger?.WriteLine($"[WHITELIST] Прокси OK: {proxyUrl}");
        }
        _currentProxy = proxyUrl;
    }

    public void ReportFailure(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;
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
                LogPoolStats("После удаления");
            }
            else
                _logger?.WriteLine($"[WHITELIST] ⚠ Ошибка #{entry.RetryCount}/{_maxRetryAttempts}: {proxyUrl}");
        }
        if (_currentProxy == proxyUrl) _currentProxy = null;
    }

    public void ReportDailyLimitReached(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;
        var entry = _whitelist.FirstOrDefault(e => e.ProxyUrl == proxyUrl);
        if (entry != null)
        {
            entry.LastUsed = DateTime.UtcNow;
            _logger?.WriteLine($"[WHITELIST] Лимит для WL прокси: {proxyUrl}");
        }
        else
        {
            _whitelist.Add(new WhitelistProxyEntry { ProxyUrl = proxyUrl, LastUsed = DateTime.UtcNow });
            _logger?.WriteLine($"[WHITELIST] ★ Добавлен в WL: {proxyUrl}");
            LogPoolStats("После добавления");
        }
        _currentProxy = null;
        _logger?.WriteLine("[WHITELIST] Переключение...");
    }

    public string? GetNextProxy()
    {
        if (!string.IsNullOrEmpty(_currentProxy)) return _currentProxy;

        if (_usingGeneralPool && DateTime.UtcNow - _switchedToGeneralPoolAt > _recheckInterval)
        {
            _usingGeneralPool = false;
            _whitelistIndex = 0;
            _lastWhitelistCheck = DateTime.UtcNow;
            _logger?.WriteLine("[WHITELIST] Возврат к WL");
            LogPoolStats("Возврат к WL");
        }

        if (!_usingGeneralPool)
        {
            var proxy = GetNextWhitelistProxy();
            if (proxy != null) { _currentProxy = proxy; return proxy; }
            _usingGeneralPool = true;
            _switchedToGeneralPoolAt = DateTime.UtcNow;
            _logger?.WriteLine("[WHITELIST] WL исчерпан → общий пул");
            LogPoolStats("Переключение");
        }

        var poolProxy = _generalPool.GetNextProxy();
        if (poolProxy != null)
        {
            _currentProxy = poolProxy;
            _logger?.WriteLine($"[WHITELIST] Из пула: {poolProxy}");
        }
        else _logger?.WriteLine("[WHITELIST] ⚠ Нет прокси!");
        return poolProxy;
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
