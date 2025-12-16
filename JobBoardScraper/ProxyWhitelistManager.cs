using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Models;

namespace JobBoardScraper;

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
        
        // Запускаем таймер автосохранения
        StartAutosaveTimer();
    }
    
    private void StartAutosaveTimer()
    {
        _autosaveTimer = new Timer(
            callback: _ => AutosaveCallback(),
            state: null,
            dueTime: _autosaveInterval,
            period: _autosaveInterval);
        
        _logger?.WriteLine($"Autosave timer started with interval: {_autosaveInterval.TotalMinutes} minutes");
    }
    
    private void AutosaveCallback()
    {
        if (_disposed)
            return;
            
        try
        {
            SaveStateAsync().GetAwaiter().GetResult();
            _lastAutosave = DateTime.UtcNow;
            _logger?.WriteLine($"Whitelist autosaved ({_whitelist.Count} proxies)");
        }
        catch (Exception ex)
        {
            _logger?.WriteLine($"Autosave error: {ex.Message}");
        }
    }

    /// <summary>
    /// Текущий используемый прокси
    /// </summary>
    public string? CurrentProxy => _currentProxy;

    /// <summary>
    /// Количество прокси в whitelist
    /// </summary>
    public int WhitelistCount => _whitelist.Count;

    /// <summary>
    /// Использует ли менеджер general pool
    /// </summary>
    public bool IsUsingGeneralPool => _usingGeneralPool;


    /// <summary>
    /// Загрузить состояние whitelist из хранилища
    /// </summary>
    public async Task LoadStateAsync()
    {
        _whitelist = await _storage.LoadAsync();
        _whitelistIndex = 0;
        _logger?.WriteLine($"Loaded whitelist with {_whitelist.Count} proxies");
    }

    /// <summary>
    /// Сохранить состояние whitelist в хранилище
    /// </summary>
    public async Task SaveStateAsync()
    {
        await _storage.SaveAsync(_whitelist);
    }

    /// <summary>
    /// Сообщить об успешном использовании прокси
    /// </summary>
    public void ReportSuccess(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl))
            return;

        var entry = _whitelist.FirstOrDefault(e => e.ProxyUrl == proxyUrl);
        
        if (entry != null)
        {
            // Сбрасываем состояние ошибки при успехе
            entry.IsFailed = false;
            entry.RetryCount = 0;
            entry.FailedSince = null;
            entry.LastUsed = DateTime.UtcNow;
            _logger?.WriteLine($"Proxy success, reset failure state: {proxyUrl}");
        }
        else
        {
            // Добавляем новый прокси в whitelist
            var newEntry = new WhitelistProxyEntry
            {
                ProxyUrl = proxyUrl,
                LastUsed = DateTime.UtcNow,
                IsFailed = false,
                RetryCount = 0
            };
            _whitelist.Add(newEntry);
            _logger?.WriteLine($"Added new proxy to whitelist: {proxyUrl}");
        }

        _currentProxy = proxyUrl;
    }

    /// <summary>
    /// Сообщить об ошибке соединения через прокси
    /// </summary>
    public void ReportFailure(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl))
            return;

        var entry = _whitelist.FirstOrDefault(e => e.ProxyUrl == proxyUrl);
        
        if (entry != null)
        {
            entry.IsFailed = true;
            entry.RetryCount++;
            entry.FailedSince ??= DateTime.UtcNow;

            if (entry.RetryCount >= _maxRetryAttempts)
            {
                _whitelist.Remove(entry);
                _logger?.WriteLine($"Removed proxy after {_maxRetryAttempts} failures: {proxyUrl}");
            }
            else
            {
                _logger?.WriteLine($"Proxy failure #{entry.RetryCount}: {proxyUrl}");
            }
        }

        if (_currentProxy == proxyUrl)
            _currentProxy = null;
    }

    /// <summary>
    /// Сообщить о достижении суточного лимита
    /// </summary>
    public void ReportDailyLimitReached(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl))
            return;

        var entry = _whitelist.FirstOrDefault(e => e.ProxyUrl == proxyUrl);
        
        if (entry != null)
        {
            entry.LastUsed = DateTime.UtcNow;
            _logger?.WriteLine($"Daily limit reached for proxy: {proxyUrl}");
        }

        _currentProxy = null;
    }


    /// <summary>
    /// Получить следующий доступный прокси
    /// </summary>
    public string? GetNextProxy()
    {
        // Если текущий прокси работает, продолжаем его использовать
        if (!string.IsNullOrEmpty(_currentProxy))
            return _currentProxy;

        // Проверяем, нужно ли вернуться к whitelist после recheck interval
        if (_usingGeneralPool && DateTime.UtcNow - _switchedToGeneralPoolAt > _recheckInterval)
        {
            _usingGeneralPool = false;
            _whitelistIndex = 0;
            _lastWhitelistCheck = DateTime.UtcNow;
            _logger?.WriteLine("Recheck interval passed, returning to whitelist");
        }

        // Пробуем получить прокси из whitelist
        if (!_usingGeneralPool)
        {
            var proxy = GetNextWhitelistProxy();
            if (proxy != null)
            {
                _currentProxy = proxy;
                return proxy;
            }

            // Whitelist исчерпан, переключаемся на general pool
            _usingGeneralPool = true;
            _switchedToGeneralPoolAt = DateTime.UtcNow;
            _logger?.WriteLine("Whitelist exhausted, switching to general pool");
        }

        // Получаем прокси из general pool
        var poolProxy = _generalPool.GetNextProxy();
        if (poolProxy != null)
        {
            _currentProxy = poolProxy;
            _logger?.WriteLine($"Using proxy from general pool: {poolProxy}");
        }

        return poolProxy;
    }

    private string? GetNextWhitelistProxy()
    {
        var startIndex = _whitelistIndex;
        var checkedCount = 0;

        while (checkedCount < _whitelist.Count)
        {
            if (_whitelistIndex >= _whitelist.Count)
                _whitelistIndex = 0;

            var entry = _whitelist[_whitelistIndex];
            _whitelistIndex++;
            checkedCount++;

            // Проверяем доступность прокси
            if (IsProxyAvailable(entry))
            {
                _logger?.WriteLine($"Selected whitelist proxy: {entry.ProxyUrl}");
                return entry.ProxyUrl;
            }
        }

        return null;
    }

    private bool IsProxyAvailable(WhitelistProxyEntry entry)
    {
        var cooldownPassed = entry.IsCooldownPassed(_cooldownPeriod);

        // Прокси доступен если:
        // 1. Прошёл cooldown период И
        // 2. Либо не помечен как failed, либо прошёл cooldown (даёт шанс восстановиться)
        if (!cooldownPassed)
            return false;

        if (entry.IsFailed && !cooldownPassed)
            return false;

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        
        // Останавливаем таймер автосохранения
        _autosaveTimer?.Dispose();
        _autosaveTimer = null;
        
        // Сохраняем состояние при завершении
        SaveStateAsync().GetAwaiter().GetResult();
        _logger?.WriteLine("ProxyWhitelistManager disposed, final state saved");
    }
}
