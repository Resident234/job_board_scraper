using JobBoardScraper.Proxy;

namespace JobBoardScraper.Examples;

/// <summary>
/// Примеры использования динамических прокси
/// </summary>
public static class DynamicProxyExample
{
    /// <summary>
    /// Пример 1: Загрузка прокси из публичных источников
    /// </summary>
    public static async Task Example1_LoadFromPublicSources()
    {
        var provider = new ProxyProvider();
        
        Console.WriteLine("Загрузка прокси из ProxyScrape...");
        var count1 = await provider.LoadFromProxyScrapeAsync();
        Console.WriteLine($"Загружено: {count1}");
        
        Console.WriteLine("Загрузка прокси из GeoNode...");
        var count2 = await provider.LoadFromGeoNodeAsync(limit: 50);
        Console.WriteLine($"Загружено: {count2}");
        
        var total = provider.GetProxies().Count;
        Console.WriteLine($"Всего прокси: {total}");
    }

    /// <summary>
    /// Пример 2: Проверка работоспособности прокси
    /// </summary>
    public static async Task Example2_TestProxies()
    {
        var provider = new ProxyProvider();
        await provider.LoadFromProxyScrapeAsync();
        
        Console.WriteLine("Проверка работоспособности прокси...");
        var removed = await provider.RemoveDeadProxiesAsync();
        
        Console.WriteLine($"Удалено нерабочих: {removed}");
        Console.WriteLine($"Осталось рабочих: {provider.GetProxies().Count}");
    }

    /// <summary>
    /// Пример 3: Сохранение и загрузка из файла
    /// </summary>
    public static async Task Example3_SaveAndLoad()
    {
        // Загрузить и проверить
        var provider = new ProxyProvider();
        await provider.LoadFromProxyScrapeAsync();
        await provider.RemoveDeadProxiesAsync();
        
        // Сохранить рабочие прокси
        await provider.SaveToFileAsync("working_proxies.txt");
        Console.WriteLine("Прокси сохранены в working_proxies.txt");
        
        // Загрузить из файла
        var provider2 = new ProxyProvider();
        await provider2.LoadFromFileAsync("working_proxies.txt");
        Console.WriteLine($"Загружено из файла: {provider2.GetProxies().Count}");
    }

    /// <summary>
    /// Пример 4: Динамический ротатор с автообновлением
    /// </summary>
    public static async Task Example4_DynamicRotator()
    {
        // Создать провайдер и загрузить прокси
        var provider = await HttpClientFactory.CreateProxyProviderAsync();
        
        // Создать динамический ротатор с обновлением каждый час
        var dynamicRotator = new DynamicProxyRotator(
            provider,
            updateInterval: TimeSpan.FromHours(1),
            autoUpdate: true
        );
        
        Console.WriteLine($"Динамический ротатор создан: {dynamicRotator.ProxyCount} прокси");
        
        // Использовать в SmartHttpClient
        // Примечание: DynamicProxyRotator используется внутри, HttpClient создается без прокси
        var httpClient = HttpClientFactory.CreateDefaultClient();
        var smartClient = new SmartHttpClient(httpClient, "DynamicExample");
        
        // Работать как обычно
        var response = await smartClient.GetAsync("https://httpbin.org/ip");
        Console.WriteLine($"Status: {response.StatusCode}");
    }

    /// <summary>
    /// Пример 5: Комбинирование источников
    /// </summary>
    public static async Task Example5_CombineSources()
    {
        var provider = new ProxyProvider();
        
        // 1. Загрузить из конфигурации
        foreach (var proxy in AppConfig.ProxyList)
        {
            provider.AddProxy(proxy);
        }
        Console.WriteLine($"Из конфигурации: {provider.GetProxies().Count}");
        
        // 2. Дополнить из файла
        await provider.LoadFromFileAsync("my_proxies.txt");
        Console.WriteLine($"После файла: {provider.GetProxies().Count}");
        
        // 3. Дополнить из публичных источников
        if (provider.GetProxies().Count < 10)
        {
            await provider.LoadFromProxyScrapeAsync();
            await provider.LoadFromGeoNodeAsync(50);
        }
        Console.WriteLine($"После публичных источников: {provider.GetProxies().Count}");
        
        // 4. Проверить и оставить только рабочие
        var removed = await provider.RemoveDeadProxiesAsync();
        Console.WriteLine($"Удалено нерабочих: {removed}");
        Console.WriteLine($"Итого рабочих: {provider.GetProxies().Count}");
    }

    /// <summary>
    /// Пример 6: Использование в скрапере с автообновлением
    /// </summary>
    public static async Task Example6_InScraperWithAutoUpdate()
    {
        // Создать динамический ротатор одной строкой
        var dynamicRotator = await HttpClientFactory.CreateDynamicProxyRotatorAsync(
            updateInterval: TimeSpan.FromHours(2),
            autoUpdate: true
        );
        
        // Создать HttpClient и SmartHttpClient
        // Примечание: Для динамического ротатора нужно создать ProxyRotator из провайдера
        var proxies = dynamicRotator.GetProxies();
        var rotator = new ProxyRotator(proxies);
        var httpClient = HttpClientFactory.CreateHttpClient(rotator);
        var smartClient = new SmartHttpClient(
            httpClient,
            scraperName: "AutoUpdateExample",
            enableRetry: true,
            proxyRotator: rotator
        );
        
        // Работать - прокси будут обновляться автоматически каждые 2 часа
        for (int i = 0; i < 1000; i++)
        {
            var response = await smartClient.GetAsync($"https://example.com/page/{i}");
            Console.WriteLine($"Page {i}: {response.StatusCode}");
            
            // Каждые 100 запросов - принудительное обновление
            if (i > 0 && i % 100 == 0)
            {
                await dynamicRotator.ForceUpdateAsync();
            }
        }
    }

    /// <summary>
    /// Пример 7: Мониторинг прокси
    /// </summary>
    public static async Task Example7_Monitoring()
    {
        var provider = await HttpClientFactory.CreateProxyProviderAsync();
        var dynamicRotator = new DynamicProxyRotator(provider, TimeSpan.FromMinutes(30), true);
        
        // Периодический мониторинг
        var timer = new System.Timers.Timer(60000); // каждую минуту
        timer.Elapsed += (s, e) =>
        {
            var proxies = provider.GetProxies();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Доступно прокси: {proxies.Count}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Статус: {dynamicRotator.GetStatus()}");
        };
        timer.Start();
        
        // Работать
        var proxies = provider.GetProxies();
        var rotator = new ProxyRotator(proxies);
        var httpClient = HttpClientFactory.CreateHttpClient(rotator);
        var smartClient = new SmartHttpClient(httpClient, "MonitoringExample", proxyRotator: rotator);
        
        for (int i = 0; i < 100; i++)
        {
            var response = await smartClient.GetAsync("https://httpbin.org/ip");
            await Task.Delay(1000);
        }
        
        timer.Stop();
    }
}
