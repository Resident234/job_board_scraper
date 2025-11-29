using System.Net;

namespace JobBoardScraper;

/// <summary>
/// Ротатор прокси-серверов с поддержкой автоматического переключения
/// </summary>
public sealed class ProxyRotator
{
    private readonly List<WebProxy> _proxies;
    private int _currentIndex = 0;
    private readonly object _lock = new object();
    private readonly bool _enabled;

    public ProxyRotator(List<string> proxyUrls)
    {
        if (proxyUrls == null || proxyUrls.Count == 0)
        {
            _enabled = false;
            _proxies = new List<WebProxy>();
            return;
        }

        _enabled = true;
        _proxies = proxyUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => CreateProxy(url))
            .ToList();

        if (_proxies.Count == 0)
        {
            _enabled = false;
        }
    }

    public bool IsEnabled => _enabled;

    public int ProxyCount => _proxies.Count;

    /// <summary>
    /// Получить следующий прокси из пула (с ротацией)
    /// </summary>
    public WebProxy? GetNextProxy()
    {
        if (!_enabled || _proxies.Count == 0)
            return null;

        lock (_lock)
        {
            var proxy = _proxies[_currentIndex];
            _currentIndex = (_currentIndex + 1) % _proxies.Count;
            return proxy;
        }
    }

    /// <summary>
    /// Получить текущий прокси без ротации
    /// </summary>
    public WebProxy? GetCurrentProxy()
    {
        if (!_enabled || _proxies.Count == 0)
            return null;

        lock (_lock)
        {
            return _proxies[_currentIndex];
        }
    }

    /// <summary>
    /// Создать WebProxy из строки URL
    /// Поддерживает форматы:
    /// - http://proxy:port
    /// - http://user:pass@proxy:port
    /// - socks5://proxy:port
    /// </summary>
    private WebProxy CreateProxy(string proxyUrl)
    {
        var uri = new Uri(proxyUrl);
        var proxy = new WebProxy(uri.Host, uri.Port);

        // Если есть учетные данные в URL
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':');
            if (parts.Length == 2)
            {
                proxy.Credentials = new NetworkCredential(parts[0], parts[1]);
            }
        }

        return proxy;
    }

    /// <summary>
    /// Получить информацию о текущем состоянии ротатора
    /// </summary>
    public string GetStatus()
    {
        if (!_enabled)
            return "Proxy rotation disabled";

        lock (_lock)
        {
            return $"Proxy {_currentIndex + 1}/{_proxies.Count}";
        }
    }
}
