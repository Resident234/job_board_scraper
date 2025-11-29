# Руководство по интеграции прокси в существующие скраперы

## Шаг 1: Настройка конфигурации

Добавьте настройки прокси в `App.config`:

```xml
<!-- Proxy Settings -->
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1:8080;http://proxy2:8080" />
<add key="Proxy:RotationIntervalSeconds" value="0" />
<add key="Proxy:AutoRotate" value="false" />
```

## Шаг 2: Обновление Program.cs

### Было (без прокси):

```csharp
var httpClient = HttpClientFactory.CreateDefaultClient(timeoutSeconds: 60);

var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "CompanyRatingScraper",
    trafficStats: trafficStats,
    enableRetry: AppConfig.CompanyRatingEnableRetry,
    enableTrafficMeasuring: AppConfig.CompanyRatingEnableTrafficMeasuring,
    timeout: AppConfig.CompanyRatingTimeout
);
```

### Стало (с прокси):

```csharp
// Создать ProxyRotator из конфигурации
var proxyRotator = HttpClientFactory.CreateProxyRotator();

// Создать HttpClient с прокси
var httpClient = HttpClientFactory.CreateHttpClient(
    proxyRotator,
    timeout: AppConfig.CompanyRatingTimeout
);

// Создать SmartHttpClient с поддержкой прокси
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "CompanyRatingScraper",
    trafficStats: trafficStats,
    enableRetry: AppConfig.CompanyRatingEnableRetry,
    enableTrafficMeasuring: AppConfig.CompanyRatingEnableTrafficMeasuring,
    timeout: AppConfig.CompanyRatingTimeout,
    proxyRotator: proxyRotator  // ← Добавить этот параметр
);
```

## Шаг 3: Проверка работы

Запустите приложение и проверьте логи:

```
✓ Прокси включены: 2 серверов
Proxy 1/2
```

## Примеры для разных скраперов

### CompanyRatingScraper

```csharp
var proxyRotator = HttpClientFactory.CreateProxyRotator();
var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator, AppConfig.CompanyRatingTimeout);

var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "CompanyRatingScraper",
    trafficStats: trafficStats,
    enableRetry: AppConfig.CompanyRatingEnableRetry,
    enableTrafficMeasuring: AppConfig.CompanyRatingEnableTrafficMeasuring,
    timeout: AppConfig.CompanyRatingTimeout,
    proxyRotator: proxyRotator
);

var scraper = new CompanyRatingScraper(
    smartClient,
    db,
    controller,
    outputMode: AppConfig.CompanyRatingOutputMode
);
```

### UserProfileScraper

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

var scraper = new UserProfileScraper(
    smartClient,
    db,
    controller,
    outputMode: AppConfig.UserProfileOutputMode
);
```

### BruteForceUsernameScraper

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

var scraper = new BruteForceUsernameScraper(
    smartClient,
    db,
    controller
);
```

### ExpertsScraper

```csharp
var proxyRotator = HttpClientFactory.CreateProxyRotator();
var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator, AppConfig.ExpertsTimeout);

var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "ExpertsScraper",
    trafficStats: trafficStats,
    enableRetry: AppConfig.ExpertsEnableRetry,
    enableTrafficMeasuring: AppConfig.ExpertsEnableTrafficMeasuring,
    timeout: AppConfig.ExpertsTimeout,
    proxyRotator: proxyRotator
);

var scraper = new ExpertsScraper(
    smartClient,
    db,
    controller,
    outputMode: AppConfig.ExpertsOutputMode
);
```

## Шаг 4: Добавление ручной ротации (опционально)

Если нужно переключать прокси вручную в процессе работы, добавьте в скрапер:

```csharp
// В методе скрапинга
for (int i = 0; i < pages.Count; i++)
{
    var response = await _httpClient.GetAsync(pages[i]);
    
    // Переключить прокси каждые 10 страниц
    if (i > 0 && i % 10 == 0)
    {
        _httpClient.RotateProxy();
        Console.WriteLine($"Прокси переключен: {_httpClient.GetProxyStatus()}");
    }
}
```

## Шаг 5: Отключение прокси для конкретного скрапера

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

## Проверка интеграции

После интеграции проверьте:

1. ✅ Приложение запускается без ошибок
2. ✅ В логах видно "Прокси включены: N серверов" (если включены)
3. ✅ HTTP-запросы проходят через прокси
4. ✅ Ротация работает (если настроена)
5. ✅ Статистика трафика собирается корректно

## Отладка

Если прокси не работают:

1. Проверьте доступность прокси-серверов
2. Проверьте формат URL в конфигурации
3. Проверьте учетные данные (если используются)
4. Включите логирование в SmartHttpClient
5. Используйте локальный прокси (Fiddler) для отладки

## Обратная совместимость

Все изменения полностью обратно совместимы:

- Если `Proxy:Enabled = false`, система работает как раньше
- Если не передавать `proxyRotator`, система работает без прокси
- Старый код продолжает работать без изменений

## Дополнительные ресурсы

- [PROXY_ROTATION.md](PROXY_ROTATION.md) - Полная документация
- [PROXY_USAGE_EXAMPLE.md](PROXY_USAGE_EXAMPLE.md) - Примеры использования
- [PROXY_CONFIG_EXAMPLES.md](PROXY_CONFIG_EXAMPLES.md) - Примеры конфигураций
