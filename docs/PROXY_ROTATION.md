# Ротация прокси-серверов

## Описание

Система поддерживает автоматическую ротацию прокси-серверов для распределения нагрузки и обхода ограничений по IP.

## Конфигурация

### App.config

```xml
<!-- Proxy Settings -->
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1:8080;http://user:pass@proxy2:8080;socks5://proxy3:1080" />
<add key="Proxy:RotationIntervalSeconds" value="60" />
<add key="Proxy:AutoRotate" value="false" />
```

### Параметры

- **Proxy:Enabled** - включить/выключить использование прокси (true/false)
- **Proxy:List** - список прокси-серверов через точку с запятой или запятую
- **Proxy:RotationIntervalSeconds** - интервал автоматической ротации (0 = отключено)
- **Proxy:AutoRotate** - автоматическая ротация при каждом запросе (true/false)

### Форматы прокси

Поддерживаются следующие форматы:

```
http://proxy.example.com:8080
http://username:password@proxy.example.com:8080
socks5://proxy.example.com:1080
https://proxy.example.com:443
```

## Использование

### Базовое использование

```csharp
// Создать ProxyRotator из конфигурации
var proxyRotator = HttpClientFactory.CreateProxyRotator();

// Создать HttpClient с прокси
var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator);

// Создать SmartHttpClient с поддержкой прокси
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "MyScraper",
    trafficStats: trafficStats,
    enableRetry: true,
    proxyRotator: proxyRotator
);

// Использовать как обычно
var response = await smartClient.GetAsync("https://example.com");
```

### Ручная ротация

```csharp
// Переключиться на следующий прокси вручную
smartClient.RotateProxy();

// Проверить текущий статус прокси
var status = smartClient.GetProxyStatus();
Console.WriteLine(status); // "Proxy 2/5"
```

### Использование в скрапере

```csharp
public class MyScraperWithProxy
{
    private readonly SmartHttpClient _httpClient;
    private readonly ProxyRotator? _proxyRotator;

    public MyScraperWithProxy(TrafficStatistics trafficStats)
    {
        // Создать прокси-ротатор (опционально)
        _proxyRotator = HttpClientFactory.CreateProxyRotator();
        
        // Создать HttpClient с прокси
        var httpClient = HttpClientFactory.CreateHttpClient(_proxyRotator);
        
        // Создать SmartHttpClient
        _httpClient = new SmartHttpClient(
            httpClient,
            scraperName: "MyScraperWithProxy",
            trafficStats: trafficStats,
            enableRetry: true,
            enableTrafficMeasuring: true,
            proxyRotator: _proxyRotator
        );
    }

    public async Task ScrapeAsync()
    {
        for (int i = 0; i < 100; i++)
        {
            var url = $"https://example.com/page/{i}";
            var response = await _httpClient.GetAsync(url);
            
            // Обработка ответа...
            
            // Опционально: ротировать прокси каждые N запросов
            if (i % 10 == 0)
            {
                _httpClient.RotateProxy();
                Console.WriteLine($"Rotated proxy: {_httpClient.GetProxyStatus()}");
            }
        }
    }
}
```

### Без прокси (по умолчанию)

Если прокси не настроены или отключены, система работает как обычно:

```csharp
// ProxyRotator будет null
var proxyRotator = HttpClientFactory.CreateProxyRotator(); // null

// HttpClient без прокси
var httpClient = HttpClientFactory.CreateHttpClient(null);

// SmartHttpClient без прокси
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "MyScraper",
    proxyRotator: null // или просто не передавать параметр
);
```

## Примеры конфигураций

### Для тестирования

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://localhost:8888" />
<add key="Proxy:AutoRotate" value="false" />
```

### Для продакшена с несколькими прокси

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1.example.com:8080;http://proxy2.example.com:8080;http://proxy3.example.com:8080" />
<add key="Proxy:AutoRotate" value="false" />
```

### С аутентификацией

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://user1:pass1@proxy1.example.com:8080;http://user2:pass2@proxy2.example.com:8080" />
<add key="Proxy:AutoRotate" value="false" />
```

## Рекомендации

1. **Тестирование**: Сначала протестируйте прокси с одним адресом
2. **Ротация**: Используйте ручную ротацию для лучшего контроля
3. **Мониторинг**: Следите за статусом прокси через `GetProxyStatus()`
4. **Ошибки**: При проблемах с прокси система автоматически повторит запрос (если включен retry)
5. **Производительность**: Не используйте слишком много прокси одновременно

## Отладка

Для проверки работы прокси:

```csharp
var proxyRotator = HttpClientFactory.CreateProxyRotator();
if (proxyRotator?.IsEnabled == true)
{
    Console.WriteLine($"Proxy enabled: {proxyRotator.ProxyCount} proxies");
    Console.WriteLine($"Current status: {proxyRotator.GetStatus()}");
}
else
{
    Console.WriteLine("Proxy disabled");
}
```
