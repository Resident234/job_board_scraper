# Пример использования прокси в скраперах

## Быстрый старт

### 1. Настройка конфигурации

Отредактируйте `App.config`:

```xml
<!-- Включить прокси -->
<add key="Proxy:Enabled" value="true" />

<!-- Список прокси-серверов (через точку с запятой) -->
<add key="Proxy:List" value="http://proxy1.example.com:8080;http://proxy2.example.com:8080" />

<!-- Автоматическая ротация отключена (управляем вручную) -->
<add key="Proxy:AutoRotate" value="false" />
```

### 2. Использование в Program.cs

```csharp
// Создать ProxyRotator из конфигурации
var proxyRotator = HttpClientFactory.CreateProxyRotator();

if (proxyRotator?.IsEnabled == true)
{
    Console.WriteLine($"✓ Прокси включены: {proxyRotator.ProxyCount} серверов");
}
else
{
    Console.WriteLine("○ Прокси отключены");
}

// Создать HttpClient с прокси
var httpClient = HttpClientFactory.CreateHttpClient(
    proxyRotator, 
    timeout: AppConfig.CompanyRatingTimeout
);

// Создать SmartHttpClient с прокси
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "CompanyRatingScraper",
    trafficStats: trafficStats,
    enableRetry: AppConfig.CompanyRatingEnableRetry,
    enableTrafficMeasuring: AppConfig.CompanyRatingEnableTrafficMeasuring,
    timeout: AppConfig.CompanyRatingTimeout,
    proxyRotator: proxyRotator  // ← Передаём ProxyRotator
);

// Создать скрапер
var scraper = new CompanyRatingScraper(
    smartClient,
    db,
    controller,
    outputMode: AppConfig.CompanyRatingOutputMode
);
```

### 3. Ручная ротация прокси

Если нужно переключать прокси вручную в процессе работы:

```csharp
// В цикле скрапинга
for (int i = 0; i < pages.Count; i++)
{
    var response = await smartClient.GetAsync(pages[i]);
    
    // Переключить прокси каждые 10 страниц
    if (i > 0 && i % 10 == 0)
    {
        smartClient.RotateProxy();
        Console.WriteLine($"Прокси переключен: {smartClient.GetProxyStatus()}");
    }
}
```

## Полный пример для CompanyRatingScraper

```csharp
// В Program.cs, в методе Main или RunAsync

// 1. Создать ProxyRotator
var proxyRotator = HttpClientFactory.CreateProxyRotator();

// 2. Создать HttpClient с прокси
var httpClient = HttpClientFactory.CreateHttpClient(
    proxyRotator,
    timeout: AppConfig.CompanyRatingTimeout
);

// 3. Создать SmartHttpClient
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "CompanyRatingScraper",
    trafficStats: trafficStats,
    enableRetry: AppConfig.CompanyRatingEnableRetry,
    enableTrafficMeasuring: AppConfig.CompanyRatingEnableTrafficMeasuring,
    timeout: AppConfig.CompanyRatingTimeout,
    proxyRotator: proxyRotator
);

// 4. Создать скрапер
var companyRatingScraper = new CompanyRatingScraper(
    smartClient,
    db,
    controller,
    outputMode: AppConfig.CompanyRatingOutputMode
);

// 5. Запустить
await companyRatingScraper.StartAsync(cts.Token);
```

## Для других скраперов

### UserProfileScraper с прокси

```csharp
var proxyRotator = HttpClientFactory.CreateProxyRotator();
var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator, AppConfig.UserProfileTimeout);

var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "UserProfileScraper",
    trafficStats: trafficStats,
    enableRetry: AppConfig.UserProfileEnableRetry,
    enableTrafficMeasuring: AppConfig.UserProfileEnableTrafficMeasuring,
    timeout: AppConfig.UserProfileTimeout,
    proxyRotator: proxyRotator
);

var userProfileScraper = new UserProfileScraper(
    smartClient,
    db,
    controller,
    outputMode: AppConfig.UserProfileOutputMode
);
```

### BruteForceUsernameScraper с прокси

```csharp
var proxyRotator = HttpClientFactory.CreateProxyRotator();
var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator);

var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "BruteForceUsernameScraper",
    trafficStats: trafficStats,
    enableRetry: AppConfig.BruteForceEnableRetry,
    enableTrafficMeasuring: AppConfig.BruteForceEnableTrafficMeasuring,
    maxRetries: AppConfig.MaxRetries,
    proxyRotator: proxyRotator
);

var bruteForce = new BruteForceUsernameScraper(
    smartClient,
    db,
    controller
);
```

## Отключение прокси для конкретного скрапера

Если нужно отключить прокси только для одного скрапера:

```csharp
// Не передавать proxyRotator (или передать null)
var httpClient = HttpClientFactory.CreateHttpClient(null);

var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "MyScraper",
    trafficStats: trafficStats,
    // proxyRotator не передаём
);
```

## Мониторинг прокси

```csharp
// Проверить статус прокси
if (proxyRotator?.IsEnabled == true)
{
    Console.WriteLine($"Прокси активны: {proxyRotator.ProxyCount} серверов");
    Console.WriteLine($"Текущий: {smartClient.GetProxyStatus()}");
}

// Периодически выводить статус
var timer = new System.Timers.Timer(60000); // каждую минуту
timer.Elapsed += (s, e) => 
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {smartClient.GetProxyStatus()}");
};
timer.Start();
```

## Рекомендации

1. **Для агрессивного скрапинга** (BruteForce, массовые запросы):
   - Включите прокси
   - Используйте несколько серверов
   - Ротируйте каждые 5-10 запросов

2. **Для обычного скрапинга** (CompanyRating, UserProfile):
   - Прокси опциональны
   - Можно использовать 1-2 сервера
   - Ротация не обязательна

3. **Для тестирования**:
   - Отключите прокси (`Proxy:Enabled = false`)
   - Или используйте локальный прокси (Fiddler, Charles)

4. **При ошибках**:
   - Проверьте доступность прокси
   - Убедитесь в правильности формата URL
   - Проверьте логи SmartHttpClient (retry покажет проблемы)
