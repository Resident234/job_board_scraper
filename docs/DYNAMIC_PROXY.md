# Динамическое обновление прокси

## Описание

Система поддерживает автоматическое получение и обновление списка прокси из публичных источников.

## Источники бесплатных прокси

### 1. ProxyScrape API
- URL: https://api.proxyscrape.com/
- Бесплатный
- Обновляется регулярно
- Поддерживает фильтрацию по стране и таймауту

### 2. GeoNode API
- URL: https://proxylist.geonode.com/
- Бесплатный
- JSON API
- Информация о последней проверке

### 3. Файл с прокси
- Загрузка из локального файла
- Один прокси на строку
- Поддержка комментариев (#)

## Использование

### Базовое использование

```csharp
// Создать провайдер прокси
var provider = new ProxyProvider();

// Загрузить из ProxyScrape
await provider.LoadFromProxyScrapeAsync();

// Загрузить из GeoNode
await provider.LoadFromGeoNodeAsync(limit: 100);

// Загрузить из файла
await provider.LoadFromFileAsync("proxies.txt");

// Получить список
var proxies = provider.GetProxies();
Console.WriteLine($"Загружено {proxies.Count} прокси");

// Создать ротатор
var rotator = new ProxyRotator(proxies);
```

### Автоматическое обновление

```csharp
// Создать динамический ротатор с автообновлением каждый час
var provider = await HttpClientFactory.CreateProxyProviderAsync();
var dynamicRotator = new DynamicProxyRotator(
    provider,
    updateInterval: TimeSpan.FromHours(1),
    autoUpdate: true
);

// Использовать как обычный ротатор
var httpClient = HttpClientFactory.CreateHttpClient(dynamicRotator);
var smartClient = new SmartHttpClient(httpClient, "MyScraper");

// Принудительно обновить список
await dynamicRotator.ForceUpdateAsync();
```

### Проверка работоспособности прокси

```csharp
var provider = new ProxyProvider();
await provider.LoadFromProxyScrapeAsync();

// Проверить один прокси
var isAlive = await provider.TestProxyAsync("http://proxy.example.com:8080");
Console.WriteLine($"Прокси работает: {isAlive}");

// Удалить все нерабочие прокси
var removed = await provider.RemoveDeadProxiesAsync();
Console.WriteLine($"Удалено {removed} нерабочих прокси");
```

### Сохранение и загрузка из файла

```csharp
var provider = new ProxyProvider();

// Загрузить прокси из разных источников
await provider.LoadFromProxyScrapeAsync();
await provider.LoadFromGeoNodeAsync();

// Сохранить в файл
await provider.SaveToFileAsync("proxies.txt");

// Загрузить из файла
var provider2 = new ProxyProvider();
await provider2.LoadFromFileAsync("proxies.txt");
```

## Полный пример

```csharp
public async Task RunWithDynamicProxiesAsync()
{
    // 1. Создать провайдер
    var provider = new ProxyProvider();
    
    // 2. Загрузить прокси из конфигурации (если есть)
    foreach (var proxy in AppConfig.ProxyList)
    {
        provider.AddProxy(proxy);
    }
    
    // 3. Дополнить из публичных источников
    if (provider.GetProxies().Count < 10)
    {
        Console.WriteLine("Загрузка прокси из публичных источников...");
        await provider.LoadFromProxyScrapeAsync();
        await provider.LoadFromGeoNodeAsync(50);
    }
    
    // 4. Проверить и удалить нерабочие
    Console.WriteLine("Проверка работоспособности прокси...");
    var removed = await provider.RemoveDeadProxiesAsync();
    Console.WriteLine($"Удалено {removed} нерабочих прокси");
    
    // 5. Сохранить рабочие прокси в файл
    await provider.SaveToFileAsync("working_proxies.txt");
    
    // 6. Создать динамический ротатор
    var dynamicRotator = new DynamicProxyRotator(
        provider,
        updateInterval: TimeSpan.FromHours(2),
        autoUpdate: true
    );
    
    // 7. Использовать в скрапере
    var httpClient = HttpClientFactory.CreateHttpClient(dynamicRotator);
    var smartClient = new SmartHttpClient(
        httpClient,
        scraperName: "MyScraper",
        trafficStats: trafficStats,
        enableRetry: true
    );
    
    // 8. Работать как обычно
    for (int i = 0; i < 100; i++)
    {
        var response = await smartClient.GetAsync($"https://example.com/page/{i}");
        Console.WriteLine($"Page {i}: {response.StatusCode}");
    }
}
```

## Упрощенный вариант

```csharp
// Создать динамический ротатор одной строкой
var dynamicRotator = await HttpClientFactory.CreateDynamicProxyRotatorAsync(
    updateInterval: TimeSpan.FromHours(1),
    autoUpdate: true
);

// Использовать
var httpClient = HttpClientFactory.CreateHttpClient(dynamicRotator);
var smartClient = new SmartHttpClient(httpClient, "MyScraper");
```

## Формат файла с прокси

```
# Комментарии начинаются с #
http://proxy1.example.com:8080
http://user:pass@proxy2.example.com:8080
socks5://proxy3.example.com:1080

# Можно группировать
# Быстрые прокси
http://fast1.example.com:8080
http://fast2.example.com:8080

# Медленные прокси
http://slow1.example.com:8080
```

## Рекомендации

### Для разработки
- Используйте статический список из конфигурации
- Или локальный прокси (Fiddler)

### Для тестирования
- Загрузите 10-20 прокси из публичных источников
- Проверьте работоспособность
- Сохраните рабочие в файл

### Для продакшена
- Используйте коммерческие прокси-сервисы
- Или комбинируйте: коммерческие + бесплатные
- Настройте автообновление каждые 1-2 часа
- Регулярно проверяйте работоспособность

## Ограничения бесплатных прокси

⚠️ **Важно:**
- Бесплатные прокси часто нестабильны
- Низкая скорость
- Могут быть небезопасны
- Часто блокируются сайтами

Для серьезных задач рекомендуется использовать коммерческие прокси-сервисы:
- BrightData (Luminati)
- Smartproxy
- Oxylabs
- ProxyMesh

## Мониторинг

```csharp
// Периодически выводить статистику
var timer = new System.Timers.Timer(60000); // каждую минуту
timer.Elapsed += (s, e) =>
{
    var proxies = provider.GetProxies();
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Доступно прокси: {proxies.Count}");
};
timer.Start();
```

## Отладка

```csharp
// Включить подробное логирование
var provider = new ProxyProvider();

// Загрузить с логированием
var count1 = await provider.LoadFromProxyScrapeAsync();
Console.WriteLine($"ProxyScrape: {count1} прокси");

var count2 = await provider.LoadFromGeoNodeAsync();
Console.WriteLine($"GeoNode: {count2} прокси");

// Проверить каждый прокси
var proxies = provider.GetProxies();
foreach (var proxy in proxies)
{
    var isAlive = await provider.TestProxyAsync(proxy);
    Console.WriteLine($"{proxy}: {(isAlive ? "✓" : "✗")}");
}
```
