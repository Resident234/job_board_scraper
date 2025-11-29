# Поддержка прокси в JobBoardScraper

## Быстрый старт

### 1. Настройка

Отредактируйте `JobBoardScraper/App.config`:

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1.example.com:8080;http://proxy2.example.com:8080" />
```

### 2. Использование

```csharp
// Создать прокси-ротатор из конфигурации
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
```

### 3. Ручная ротация

```csharp
// Переключить на следующий прокси
smartClient.RotateProxy();

// Проверить текущий статус
Console.WriteLine(smartClient.GetProxyStatus()); // "Proxy 2/5"
```

## Возможности

- ✅ Автоматическая ротация прокси-серверов
- ✅ Поддержка HTTP, HTTPS, SOCKS5
- ✅ Аутентификация (username:password)
- ✅ Ручная и автоматическая ротация
- ✅ Опциональное использование (можно отключить)
- ✅ Полная обратная совместимость

## Форматы прокси

```
http://proxy.example.com:8080
http://username:password@proxy.example.com:8080
socks5://proxy.example.com:1080
https://proxy.example.com:443
```

## Документация

- [PROXY_ROTATION.md](docs/PROXY_ROTATION.md) - Полная документация
- [PROXY_USAGE_EXAMPLE.md](docs/PROXY_USAGE_EXAMPLE.md) - Примеры использования
- [PROXY_QUICKSTART.md](docs/PROXY_QUICKSTART.md) - Быстрый старт
- [PROXY_CONFIG_EXAMPLES.md](docs/PROXY_CONFIG_EXAMPLES.md) - Примеры конфигураций

## Примеры конфигураций

### Один прокси
```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy.example.com:8080" />
```

### Несколько прокси
```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1:8080;http://proxy2:8080;http://proxy3:8080" />
```

### С аутентификацией
```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://user:pass@proxy.example.com:8080" />
```

### Локальный прокси (Fiddler)
```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://localhost:8888" />
```

## Отключение прокси

### В конфигурации
```xml
<add key="Proxy:Enabled" value="false" />
```

### В коде
```csharp
// Не передавать proxyRotator
var httpClient = HttpClientFactory.CreateHttpClient(null);
var smartClient = new SmartHttpClient(httpClient, "MyScraper");
```

## Рекомендации

1. **Для тестирования**: Используйте локальный прокси (Fiddler, Charles)
2. **Для легкого скрапинга**: 1-2 прокси достаточно
3. **Для агрессивного скрапинга**: 5-10 прокси с ротацией
4. **Для массового скрапинга**: Используйте коммерческие ротирующиеся прокси

## Поддержка

Если у вас есть вопросы или проблемы, создайте issue в репозитории.
