using JobBoardScraper;
using JobBoardScraper.Models;

namespace JobBoardScraper.Examples;

/// <summary>
/// Примеры использования ProxyRotator в скраперах
/// </summary>
public static class ProxyExample
{
    /// <summary>
    /// Пример 1: Базовое использование с конфигурацией из App.config
    /// </summary>
    public static async Task Example1_BasicUsage()
    {
        // 1. Создать ProxyRotator из конфигурации
        var proxyRotator = HttpClientFactory.CreateProxyRotator();

        if (proxyRotator?.IsEnabled == true)
        {
            Console.WriteLine($"✓ Прокси включены: {proxyRotator.ProxyCount} серверов");
        }
        else
        {
            Console.WriteLine("○ Прокси отключены");
        }

        // 2. Создать HttpClient с прокси
        var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator);

        // 3. Создать SmartHttpClient с поддержкой прокси
        var trafficStats = new TrafficStatistics("./logs/traffic.txt", TimeSpan.FromMinutes(5));
        var smartClient = new SmartHttpClient(
            httpClient,
            scraperName: "ExampleScraper",
            trafficStats: trafficStats,
            enableRetry: true,
            enableTrafficMeasuring: true,
            proxyRotator: proxyRotator
        );

        // 4. Использовать как обычно
        var response = await smartClient.GetAsync("https://example.com");
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Proxy: {smartClient.GetProxyStatus()}");
    }

    /// <summary>
    /// Пример 2: Ручная ротация прокси
    /// </summary>
    public static async Task Example2_ManualRotation()
    {
        var proxyRotator = HttpClientFactory.CreateProxyRotator();
        var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator);
        var smartClient = new SmartHttpClient(httpClient, "ManualRotationExample", proxyRotator: proxyRotator);

        // Сделать несколько запросов с ротацией
        for (int i = 0; i < 10; i++)
        {
            var response = await smartClient.GetAsync($"https://example.com/page/{i}");
            Console.WriteLine($"Page {i}: {response.StatusCode} via {smartClient.GetProxyStatus()}");

            // Ротировать прокси каждые 3 запроса
            if (i > 0 && i % 3 == 0)
            {
                smartClient.RotateProxy();
                Console.WriteLine($"→ Прокси переключен: {smartClient.GetProxyStatus()}");
            }
        }
    }

    /// <summary>
    /// Пример 3: Создание ProxyRotator вручную
    /// </summary>
    public static async Task Example3_ManualProxyList()
    {
        // Создать список прокси вручную
        var proxyList = new List<string>
        {
            "http://proxy1.example.com:8080",
            "http://user:pass@proxy2.example.com:8080",
            "socks5://proxy3.example.com:1080"
        };

        // Создать ProxyRotator
        var proxyRotator = new ProxyRotator(proxyList);
        Console.WriteLine($"Создан ProxyRotator с {proxyRotator.ProxyCount} прокси");

        // Создать HttpClient и SmartHttpClient
        var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator);
        var smartClient = new SmartHttpClient(httpClient, "ManualProxyExample", proxyRotator: proxyRotator);

        // Использовать
        var response = await smartClient.GetAsync("https://example.com");
        Console.WriteLine($"Status: {response.StatusCode}");
    }

    /// <summary>
    /// Пример 4: Использование без прокси (по умолчанию)
    /// </summary>
    public static async Task Example4_WithoutProxy()
    {
        // Не передавать proxyRotator
        var httpClient = HttpClientFactory.CreateHttpClient(null);
        var smartClient = new SmartHttpClient(httpClient, "NoProxyExample");

        // Использовать как обычно
        var response = await smartClient.GetAsync("https://example.com");
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Proxy: {smartClient.GetProxyStatus()}"); // "No proxy"
    }

    /// <summary>
    /// Пример 5: Использование в скрапере
    /// </summary>
    public static void Example5_InScraper()
    {
        // В Program.cs или в конструкторе скрапера:

        // 1. Создать ProxyRotator
        var proxyRotator = HttpClientFactory.CreateProxyRotator();

        // 2. Создать HttpClient с прокси
        var httpClient = HttpClientFactory.CreateHttpClient(
            proxyRotator,
            timeout: TimeSpan.FromSeconds(60)
        );

        // 3. Создать SmartHttpClient
        var trafficStats = new TrafficStatistics("./logs/traffic.txt", TimeSpan.FromMinutes(5));
        var smartClient = new SmartHttpClient(
            httpClient,
            scraperName: "CompanyRatingScraper",
            trafficStats: trafficStats,
            enableRetry: true,
            enableTrafficMeasuring: true,
            timeout: TimeSpan.FromSeconds(60),
            proxyRotator: proxyRotator
        );

        // 4. Передать в скрапер
        // var scraper = new CompanyRatingScraper(smartClient, db, controller);
    }

    /// <summary>
    /// Пример 6: Мониторинг прокси
    /// </summary>
    public static void Example6_Monitoring()
    {
        var proxyRotator = HttpClientFactory.CreateProxyRotator();
        var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator);
        var smartClient = new SmartHttpClient(httpClient, "MonitoringExample", proxyRotator: proxyRotator);

        // Вывести информацию о прокси
        if (proxyRotator?.IsEnabled == true)
        {
            Console.WriteLine("=== Proxy Information ===");
            Console.WriteLine($"Enabled: Yes");
            Console.WriteLine($"Count: {proxyRotator.ProxyCount}");
            Console.WriteLine($"Current: {smartClient.GetProxyStatus()}");
        }
        else
        {
            Console.WriteLine("=== Proxy Information ===");
            Console.WriteLine($"Enabled: No");
        }

        // Периодический мониторинг
        var timer = new System.Timers.Timer(60000); // каждую минуту
        timer.Elapsed += (s, e) =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Proxy status: {smartClient.GetProxyStatus()}");
        };
        timer.Start();
    }
}
