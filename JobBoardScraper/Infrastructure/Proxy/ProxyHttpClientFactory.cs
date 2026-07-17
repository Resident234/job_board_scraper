using JobBoardScraper.Core;
using JobBoardScraper.Infrastructure.Logging;

namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Фабрика HttpClient-ов с поддержкой прокси и ретраев.
/// Инкапсулирует создание HttpClient с настройками декомпрессии/таймаута,
/// а также ожидание доступного прокси от координатора.
/// </summary>
public sealed class ProxyHttpClientFactory : IDisposable
{
    private readonly TimeSpan _proxyWaitTimeout;
    private readonly ConsoleLogger _logger;

    /// <summary>
    /// Создаёт фабрику.
    /// </summary>
    /// <param name="proxyWaitTimeout">Максимальное время ожидания доступного прокси (по умолчанию AppConfig.ProxyWaitTimeoutSeconds).</param>
    /// <param name="logger">Логгер для диагностических сообщений.</param>
    public ProxyHttpClientFactory(TimeSpan? proxyWaitTimeout = null, ConsoleLogger? logger = null)
    {
        _proxyWaitTimeout = proxyWaitTimeout ?? TimeSpan.FromSeconds(AppConfig.ProxyWaitTimeoutSeconds);
        _logger = logger ?? new ConsoleLogger(nameof(ProxyHttpClientFactory));
    }

    /// <summary>
    /// Создаёт HttpClient с указанным прокси.
    /// Если proxyUrl == null или пустой, возвращает null (используется SmartHttpClient напрямую).
    /// </summary>
    /// <param name="proxyUrl">URL прокси-сервера (например, http://host:port), или null.</param>
    /// <returns>HttpClient с настроенным прокси, либо null если proxyUrl пуст.</returns>
    public HttpClient? CreateClient(string? proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
            return null;

        var proxy = new System.Net.WebProxy(new Uri(proxyUrl));
        var handler = new HttpClientHandler
        {
            Proxy = proxy,
            UseProxy = true,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
                                    | System.Net.DecompressionMethods.Deflate
        };

        return new HttpClient(handler)
        {
            Timeout = AppConfig.ProxyRequestTimeout
        };
    }

    /// <summary>
    /// Ждёт появления доступного прокси в координаторе с таймаутом.
    /// </summary>
    /// <param name="coordinator">Координатор прокси (если null, метод возвращает null сразу).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>URL прокси или null, если не дождались или координатор отсутствует.</returns>
    public async Task<string?> WaitForProxyAsync(IProxyManager? coordinator, CancellationToken ct)
    {
        if (coordinator == null)
            return null;

        var proxy = coordinator.GetNextProxy();
        if (proxy != null)
            return proxy;

        _logger.WriteLine($"No proxy available, waiting up to {_proxyWaitTimeout.TotalSeconds} seconds...");

        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime) < _proxyWaitTimeout && !ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);
            proxy = coordinator.GetNextProxy();
            if (proxy != null)
            {
                _logger.WriteLine("Proxy became available after waiting");
                return proxy;
            }
        }

        _logger.WriteLine("Timeout waiting for proxy");
        return null;
    }

    /// <summary>
    /// Безопасно освобождает HttpClient (если он был создан этой фабрикой).
    /// Допускает null.
    /// </summary>
    public void DisposeClient(HttpClient? client)
    {
        client?.Dispose();
    }

    public void Dispose()
    {
        // Нам нечего освобождать — HttpClient-ы живут только внутри одного запроса.
    }
}
