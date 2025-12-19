using System.Net;
using JobBoardScraper.Proxy;

namespace JobBoardScraper.Helper.Http;

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
    /// Создать ProxyRotator из конфигурации
    /// </summary>
    public static ProxyRotator? CreateProxyRotator()
    {
        if (!AppConfig.ProxyEnabled)
            return null;

        var proxyList = AppConfig.ProxyList;
        
        // Если список пуст, загрузить из публичных источников
        if (proxyList.Count == 0)
        {
            Console.WriteLine("[HttpClientFactory] Список прокси пуст. Загрузка из публичных источников...");
            var provider = new ProxyProvider();
            
            try
            {
                // Синхронная загрузка (блокирующая)
                var task1 = provider.LoadFromProxyScrapeAsync();
                task1.Wait();
                Console.WriteLine($"[HttpClientFactory] ProxyScrape: загружено {provider.GetProxies().Count} прокси");
                
                var task2 = provider.LoadFromGeoNodeAsync(50);
                task2.Wait();
                Console.WriteLine($"[HttpClientFactory] GeoNode: загружено {provider.GetProxies().Count} прокси");
                
                proxyList = provider.GetProxies();
                
                if (proxyList.Count == 0)
                {
                    Console.WriteLine("[HttpClientFactory] ⚠️ Не удалось загрузить прокси из публичных источников");
                    return null;
                }
                
                Console.WriteLine($"[HttpClientFactory] ✓ Загружено {proxyList.Count} прокси");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HttpClientFactory] ⚠️ Ошибка загрузки прокси: {ex.Message}");
                return null;
            }
        }

        return new ProxyRotator(proxyList);
    }

    /// <summary>
    /// Создать HttpClient по умолчанию (для обратной совместимости)
    /// </summary>
    public static HttpClient CreateDefaultClient(int timeoutSeconds = 30)
    {
        return CreateHttpClient(null, TimeSpan.FromSeconds(timeoutSeconds));
    }

    /// <summary>
    /// Создать ProxyProvider с автоматической загрузкой прокси
    /// </summary>
    public static async Task<ProxyProvider> CreateProxyProviderAsync()
    {
        var provider = new ProxyProvider();
        
        // Загрузить из конфигурации
        var configProxies = AppConfig.ProxyList;
        foreach (var proxy in configProxies)
        {
            provider.AddProxy(proxy);
        }
        
        // Если в конфиге нет прокси, загрузить из публичных источников
        if (configProxies.Count == 0)
        {
            Console.WriteLine("[HttpClientFactory] Загрузка прокси из публичных источников...");
            await provider.LoadFromProxyScrapeAsync();
            await provider.LoadFromGeoNodeAsync(50);
            
            var count = provider.GetProxies().Count;
            Console.WriteLine($"[HttpClientFactory] Загружено {count} прокси");
        }
        
        return provider;
    }

    /// <summary>
    /// Создать DynamicProxyRotator с автоматическим обновлением
    /// </summary>
    public static async Task<DynamicProxyRotator> CreateDynamicProxyRotatorAsync(
        TimeSpan? updateInterval = null,
        bool autoUpdate = true)
    {
        var provider = await CreateProxyProviderAsync();
        return new DynamicProxyRotator(provider, updateInterval, autoUpdate);
    }
}
