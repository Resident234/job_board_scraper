using JobBoardScraper.Infrastructure.Logging;

namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Координатор прокси — управляет переключением между WhitelistManager и GeneralPoolManager
/// </summary>
public class ProxyCoordinator : IProxyManager, IDisposable
{
    private readonly ProxyWhitelistManager _whitelistManager;
    private readonly GeneralPoolManager _generalPoolManager;
    private readonly ConsoleLogger? _logger;
    private readonly TimeSpan _whitelistRecheckInterval;
    
    private bool _usingWhitelist = true;
    private DateTime _switchedToGeneralAt;
    private string? _lastUsedProxy;
    private ProxySource _lastProxySource;
    private bool _disposed;

    public enum ProxySource
    {
        None,
        Whitelist,
        GeneralPool
    }

    public ProxyCoordinator(
        ProxyWhitelistManager whitelistManager,
        GeneralPoolManager generalPoolManager,
        TimeSpan? whitelistRecheckInterval = null,
        ConsoleLogger? logger = null)
    {
        _whitelistManager = whitelistManager ?? throw new ArgumentNullException(nameof(whitelistManager));
        _generalPoolManager = generalPoolManager ?? throw new ArgumentNullException(nameof(generalPoolManager));
        _whitelistRecheckInterval = whitelistRecheckInterval ?? TimeSpan.FromMinutes(5);
        _logger = logger;

        // Подписываемся на события GeneralPoolManager
        _generalPoolManager.OnProxyVerified += OnGeneralProxyVerified;
        _generalPoolManager.OnProxyBlacklisted += OnGeneralProxyBlacklisted;
    }

    public int Count => _whitelistManager.WhitelistCount + _generalPoolManager.Count;
    public bool HasAvailableProxies => _whitelistManager.WhitelistCount > 0 || _generalPoolManager.HasAvailableProxies;
    public string? CurrentProxy => _lastUsedProxy;
    public ProxySource LastProxySource => _lastProxySource;
    public bool IsUsingWhitelist => _usingWhitelist;

    public string? GetNextProxy()
    {
        // Проверяем, не пора ли вернуться к whitelist
        if (!_usingWhitelist && DateTime.UtcNow - _switchedToGeneralAt > _whitelistRecheckInterval)
        {
            _usingWhitelist = true;
            _logger?.WriteLine("[COORDINATOR] Возврат к whitelist");
        }

        string? proxy = null;

        // Сначала пробуем whitelist
        if (_usingWhitelist)
        {
            proxy = _whitelistManager.GetNextProxy();
            if (proxy != null)
            {
                _lastUsedProxy = proxy;
                _lastProxySource = ProxySource.Whitelist;
                _logger?.WriteLine($"[COORDINATOR] Whitelist → {proxy}");
                return proxy;
            }

            // Whitelist исчерпан — переключаемся на general pool
            _usingWhitelist = false;
            _switchedToGeneralAt = DateTime.UtcNow;
            _logger?.WriteLine("[COORDINATOR] Whitelist исчерпан → General Pool");
        }

        // Пробуем general pool
        proxy = _generalPoolManager.GetNextProxy();
        if (proxy != null)
        {
            _lastUsedProxy = proxy;
            _lastProxySource = ProxySource.GeneralPool;
            _logger?.WriteLine($"[COORDINATOR] General Pool → {proxy}");
            return proxy;
        }

        _lastUsedProxy = null;
        _lastProxySource = ProxySource.None;
        _logger?.WriteLine("[COORDINATOR] ⚠ Нет доступных прокси!");
        return null;
    }

    public void ReportSuccess(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;

        // Маршрутизируем в нужный менеджер
        if (_lastProxySource == ProxySource.Whitelist)
        {
            _whitelistManager.ReportSuccess(proxyUrl);
        }
        else
        {
            _generalPoolManager.ReportSuccess(proxyUrl);
        }
    }

    public void ReportFailure(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;

        // Маршрутизируем в нужный менеджер
        if (_lastProxySource == ProxySource.Whitelist)
        {
            _whitelistManager.ReportFailure(proxyUrl);
        }
        else
        {
            _generalPoolManager.ReportFailure(proxyUrl);
        }

        // Сбрасываем текущий прокси
        _lastUsedProxy = null;
    }

    public void ReportDailyLimitReached(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return;

        // Маршрутизируем в нужный менеджер
        if (_lastProxySource == ProxySource.Whitelist)
        {
            _whitelistManager.ReportDailyLimitReached(proxyUrl);
        }
        else
        {
            _generalPoolManager.ReportDailyLimitReached(proxyUrl);
        }

        // Сбрасываем текущий прокси
        _lastUsedProxy = null;
    }

    /// <summary>
    /// Обработчик: прокси из general pool подтверждён как рабочий
    /// </summary>
    private void OnGeneralProxyVerified(string proxyUrl)
    {
        _logger?.WriteLine($"[COORDINATOR] ★ Прокси верифицирован, добавляем в whitelist: {proxyUrl}");
        _whitelistManager.ReportDailyLimitReached(proxyUrl); // Это добавит в whitelist
    }

    /// <summary>
    /// Обработчик: прокси из general pool забанен
    /// </summary>
    private void OnGeneralProxyBlacklisted(string proxyUrl)
    {
        _logger?.WriteLine($"[COORDINATOR] ✗ Прокси забанен в general pool: {proxyUrl}");
        // Можно добавить логику удаления из whitelist, если он там есть
    }

    /// <summary>
    /// Принудительно переключиться на general pool
    /// </summary>
    public void ForceUseGeneralPool()
    {
        _usingWhitelist = false;
        _switchedToGeneralAt = DateTime.UtcNow;
        _logger?.WriteLine("[COORDINATOR] Принудительное переключение на General Pool");
    }

    /// <summary>
    /// Принудительно переключиться на whitelist
    /// </summary>
    public void ForceUseWhitelist()
    {
        _usingWhitelist = true;
        _logger?.WriteLine("[COORDINATOR] Принудительное переключение на Whitelist");
    }

    public string GetStatus()
    {
        var source = _usingWhitelist ? "Whitelist" : "General Pool";
        return $"[{source}] WL: {_whitelistManager.WhitelistCount} | Pool: {_generalPoolManager.Count} | " +
               $"Blacklist: {_generalPoolManager.BlacklistCount} | Current: {_lastUsedProxy ?? "none"}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _generalPoolManager.OnProxyVerified -= OnGeneralProxyVerified;
        _generalPoolManager.OnProxyBlacklisted -= OnGeneralProxyBlacklisted;

        _whitelistManager.Dispose();
        _logger?.WriteLine("[COORDINATOR] Disposed");
    }
}
