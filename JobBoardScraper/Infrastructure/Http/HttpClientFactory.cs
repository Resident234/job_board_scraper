using System.Net;
using JobBoardScraper.Infrastructure.Proxy;

namespace JobBoardScraper.Infrastructure.Http;

/// <summary>
/// Фабрика для создания HttpClient с поддержкой прокси
/// </summary>
public static class HttpClientFactory
{
    /// <summary>
    /// Создать HttpClient с опциональной поддержкой прокси
    /// </summary>
    public static HttpClient CreateHttpClient(ProxyRotator? proxyRotator = null, TimeSpan? timeout = null)
    {
        HttpMessageHandler handler;

        if (proxyRotator?.IsEnabled == true)
        {
            var proxy = proxyRotator.GetNextProxy();
            var httpClientHandler = new HttpClientHandler
            {
                Proxy = proxy,
                UseProxy = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            handler = httpClientHandler;
        }
        else
        {
            var httpClientHandler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            handler = httpClientHandler;
        }

        var client = new HttpClient(handler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };

        return client;
    }

    /// <summary>
    /// Создать HttpClient по умолчанию (для обратной совместимости)
    /// </summary>
    public static HttpClient CreateDefaultClient(int timeoutSeconds = 30)
    {
        return CreateHttpClient(null, TimeSpan.FromSeconds(timeoutSeconds));
    }
}