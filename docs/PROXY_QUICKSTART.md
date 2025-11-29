# Быстрый старт с прокси

## 1. Настройка прокси в App.config

```xml
<!-- Включить прокси -->
<add key="Proxy:Enabled" value="true" />

<!-- Список прокси (через точку с запятой или запятую) -->
<add key="Proxy:List" value="http://proxy1.example.com:8080;http://proxy2.example.com:8080" />
```

## 2. Форматы прокси

```
http://proxy.example.com:8080
http://username:password@proxy.example.com:8080
socks5://proxy.example.com:1080
```

## 3. Использование в коде

### Вариант А: Автоматически из конфигурации

```csharp
// Создать прокси-ротатор из App.config
var proxyRotator = HttpClientFactory.CreateProxyRotator();

// Создать HttpClient с прокси
var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator);

// Создать SmartHttpClient
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "MyScraper",
    trafficStats: trafficStats,
    enableRetry: true,
    proxyRotator: proxyRotator
);
```

### Вариант Б: Вручную

```csharp
// Создать список прокси
var proxyList = new List<string>
{
    "http://proxy1.example.com:8080",
    "http://user:pass@proxy2.example.com:8080"
};

// Создать ротатор
var proxyRotator = new ProxyRotator(proxyList);

// Создать HttpClient
var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator);

// Создать SmartHttpClient
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "MyScraper",
    proxyRotator: proxyRotator
);
```

## 4. Ручная ротация

```csharp
// Переключить на следующий прокси
smartClient.RotateProxy();

// Проверить текущий статус
Console.WriteLine(smartClient.GetProxyStatus()); // "Proxy 2/5"
```

## 5. Отключение прокси

### В конфигурации:
```xml
<add key="Proxy:Enabled" value="false" />
```

### В коде:
```csharp
// Не передавать proxyRotator
var httpClient = HttpClientFactory.CreateHttpClient(null);
var smartClient = new SmartHttpClient(httpClient, "MyScraper");
```

## Готово!

Теперь все HTTP-запросы будут идти через прокси с автоматической ротацией.
