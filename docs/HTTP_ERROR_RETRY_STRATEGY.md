# Стратегия повторов для HTTP ошибок

## 🎯 Обзор

Добавлена стратегия автоматических повторов для HTTP ошибок сервера (5xx) с использованием алгоритма **Exponential Backoff with Jitter** и троттлинга через `AdaptiveConcurrencyController`.

## 🔍 Проблема

При работе с прокси часто возникают ошибки:
- **500 Internal Server Error** - внутренняя ошибка сервера
- **502 Bad Gateway** - прокси не может подключиться к серверу
- **503 Service Unavailable** - сервер временно недоступен
- **403 Forbidden** - IP заблокирован или доступ запрещён
- **429 Too Many Requests** - превышен лимит запросов (rate limiting)
- **408 Request Timeout** - таймаут запроса
- **Socket errors** - ошибки подключения к прокси

**Важно:** Страница считается обработанной ТОЛЬКО при получении ответа 200 OK. Любой другой код ответа приводит к повторной попытке со сменой прокси.

## 🛠️ Решение

### 1. Exponential Backoff with Jitter

Реализован класс `ExponentialBackoff` в `JobBoardScraper/Helper.Utils/ExponentialBackoff.cs`:

```csharp
// Для ошибок сервера (5xx)
var delay = ExponentialBackoff.CalculateServerErrorDelay(attempt);
// baseDelay=2000мс, maxDelay=60000мс, jitter=30%

// Для ошибок прокси/сети
var delay = ExponentialBackoff.CalculateProxyErrorDelay(attempt);
// baseDelay=500мс, maxDelay=10000мс, jitter=20%
```

### 2. Обрабатываемые коды ответов

| Код | Описание | Стратегия | Задержка |
|-----|----------|-----------|----------|
| 200 | OK | Успех | - |
| 404 | Not Found | **Не повторять**, записать "Ошибка 404" в title | - |
| 5xx | Server errors (500, 502, 503, 504) | Exponential backoff | 2-60 сек |
| 403 | Forbidden (IP blocked) | Смена прокси | 0.5-10 сек |
| 429 | Rate limited | Retry-After или 2x backoff | По заголовку или 4-120 сек |
| 408 | Request timeout | Смена прокси | 0.5-10 сек |
| Другие | Любой не-200/404 ответ | Смена прокси | 0.5-10 сек |

### 3. Особая обработка 404 Not Found

При получении ответа 404:
- Страница считается обработанной (не повторяется)
- В поле `title` записывается "Ошибка 404"
- В поле `about` записывается "Ошибка 404"
- Это позволяет отслеживать удалённые или несуществующие профили

### 4. Примеры задержек

**Для ошибок сервера (5xx):**
```
Попытка 1:  2.0 ± 0.6 сек  (1.4 - 2.6 сек)
Попытка 2:  4.0 ± 1.2 сек  (2.8 - 5.2 сек)
Попытка 3:  8.0 ± 2.4 сек  (5.6 - 10.4 сек)
Попытка 4: 16.0 ± 4.8 сек  (11.2 - 20.8 сек)
Попытка 5: 32.0 ± 9.6 сек  (22.4 - 41.6 сек)
Попытка 6+: 60.0 сек max   (ограничено maxDelay)
```

**Для ошибок 403/408/прокси/сети:**
```
Попытка 1: 0.5 ± 0.1 сек  (0.4 - 0.6 сек)
Попытка 2: 1.0 ± 0.2 сек  (0.8 - 1.2 сек)
Попытка 3: 2.0 ± 0.4 сек  (1.6 - 2.4 сек)
Попытка 4: 4.0 ± 0.8 сек  (3.2 - 4.8 сек)
Попытка 5+: 10.0 сек max  (ограничено maxDelay)
```

**Для 429 Rate Limited:**
- Если есть заголовок `Retry-After` - используется значение из заголовка
- Иначе - удвоенная задержка server error (4-120 сек)

### 5. Троттлинг

Используется `AdaptiveConcurrencyController` для управления нагрузкой:

```csharp
sw.Stop();
_controller.ReportLatency(sw.Elapsed);
```

`AdaptiveConcurrencyController` автоматически:
- Уменьшает параллелизм при высокой задержке
- Увеличивает параллелизм при низкой задержке
- Предотвращает перегрузку сервера

## 📊 Поведение

### До изменений:
```
[UserResumeDetailScraper] HTTP запрос: 503 Service Unavailable
[UserResumeDetailScraper] Skipping user (non-success status code)
```

### После изменений:
```
[UserResumeDetailScraper] HTTP запрос: 503 Service Unavailable
[UserResumeDetailScraper] Получен код 503. Жду 2.3 сек (попытка 1)...
[UserResumeDetailScraper] Повторный HTTP запрос через прокси: 200 OK ✓
```

## 🔧 Реализация

### 1. Новые классы

| Файл | Класс | Назначение |
|------|-------|------------|
| `Infrastructure/Retry/ExponentialBackoff.cs` | `ExponentialBackoff` | Расчёт задержек для повторов |
| `Infrastructure/Retry/HttpRetryHandler.cs` | `HttpRetryHandler` | Логика повторов HTTP запросов |
| `Infrastructure/Proxy/AdaptiveConcurrencyController.cs` | `AdaptiveConcurrencyController` | Троттлинг и контроль параллелизма |
| `Infrastructure/Proxy/ProxyRetryInfo.cs` | `ProxyRetryInfo` | Информация о ретраях для прокси |

### 2. Изменения в существующих классах

| Файл | Изменения |
|------|-----------|
| `Infrastructure/Proxy/ProxyInfoCache.cs` | Добавлена очистка retry-счётчиков |
| `Infrastructure/Proxy/ProxyCoordinator.cs` | Добавлен retry-счётчик для прокси |
| `Infrastructure/Proxy/ProxyCacheManager.cs` | Улучшен health-check |
| `Infrastructure/Proxy/ProxyHttpClientFactory.cs` | Добавлена интеграция с HttpRetryHandler |
| `Scrapers/UserResumeDetailScraper.cs` | Переход на единый механизм retry |
| `Infrastructure/Proxy/ProxyRetryExecutor.cs` | Добавлен статический `ReportSuccessSafe` |

## ⚙️ Конфигурация

Все параметры настраиваются в `App.config`:

```xml
<add key="HttpRetry:Enabled" value="true"/>
<add key="HttpRetry:MaxAttempts" value="3"/>
<add key="HttpRetry:ServerErrorBaseDelayMs" value="2000"/>
<add key="HttpRetry:ServerErrorMaxDelayMs" value="60000"/>
<add key="HttpRetry:ProxyErrorBaseDelayMs" value="500"/>
<add key="HttpRetry:ProxyErrorMaxDelayMs" value="10000"/>
<add key="HttpRetry:JitterPercent" value="30"/>
```

## 📈 Метрики

Для отслеживания эффективности добавлены метрики:
- `RetryAttempts` - количество повторных попыток
- `RetrySuccessRate` - процент успешных повторов
- `AverageRetryDelay` - средняя задержка перед повтором

## 🔄 Интеграция с прокси-системой

```
┌─────────────────────────────────────────────────────────────┐
│                    Proxy HTTP Client                        │
│                                                             │
│  ┌──────────────┐    ┌─────────────────┐    ┌────────────┐ │
│  │   Request    │───▶│  Retry Handler  │───▶│  Response  │ │
│  └──────────────┘    └─────────────────┘    └────────────┘ │
│                            │                               │
│                            ▼                               │
│  ┌─────────────────────────────────────────┐               │
│  │         Proxy Coordinator               │               │
│  │  ┌──────────┐  ┌──────────┐  ┌────────┐ │               │
│  │  │ Whitelist │  │Pool Proxies│  │ Retry  │ │               │
│  │  │ Manager  │  │ Manager  │  │  Info  │ │               │
│  │  └──────────┘  └──────────┘  └────────┘ │               │
│  └─────────────────────────────────────────┘               │
└─────────────────────────────────────────────────────────────┘
```

## 🔁 Алгоритм работы

1. `ProxyHttpClientFactory` отправляет запрос через выбранный прокси
2. При получении кода ответа:
   - **200 OK** → успех, сброс retry-счётчика
   - **404 Not Found** → фиксация как "обработано", без повторов
   - **5xx / 403 / 408 / другие** → вызов `HttpRetryHandler`
3. `HttpRetryHandler`:
   - Рассчитывает задержку через `ExponentialBackoff`
   - Ждёт, затем повторяет запрос
   - Если все попытки исчерпаны или другая критическая ошибка:
     - Сообщает `ProxyCoordinator` о неудаче
     - Запрашивает новый прокси через `IProxyManager`
4. При превышении лимита ошибок для прокси — прокси удаляется из пула

## 🧩 Компоненты

### `ProxyRetryInfo`

Хранит информацию о ретраях для конкретного прокси:
```csharp
public class ProxyRetryInfo
{
    public string? ProxyUrl { get; set; }
    public int RetryCount { get; set; }
    public DateTime LastRetryTime { get; set; }
    public bool IsBlocked { get; set; }
}
```

### `ProxyCoordinator` (дополнение)

```csharp
public class ProxyCoordinator : IProxyManager, IDisposable
{
    private readonly Dictionary<string, ProxyRetryInfo> _proxyRetryInfos = new();
    
    public void ReportProxyBlocked(string proxyUrl)
    {
        if (_proxyRetryInfos.ContainsKey(proxyUrl))
            _proxyRetryInfos[proxyUrl].IsBlocked = true;
    }
}
```

### `ExponentialBackoff`

```csharp
public static class ExponentialBackoff
{
    public static TimeSpan CalculateServerErrorDelay(int attempt)
    {
        // baseDelay: 2000ms, maxDelay: 60000ms, jitter: 30%
        var delay = Math.Min(baseDelay * Math.Pow(2, attempt - 1), maxDelay);
        return ApplyJitter(delay, jitterPercent);
    }
}
```

## 🔌 IProxyManager

Интерфейс `IProxyManager` (`JobBoardScraper/Infrastructure/Proxy/IProxyManager.cs`) — общий контракт для всех менеджеров прокси. Определяет методы получения прокси, отчётов об успехе/ошибке/лимите.

#### Реализации (3 класса)

| Файл | Класс | Назначение |
|------|-------|------------|
| `ProxyCoordinator.cs` | `ProxyCoordinator : IProxyManager, IDisposable` | Центральный координатор — управляет списком прокси, ротацией и health-мониторингом |
| `GeneralPoolManager.cs` | `GeneralPoolManager : IProxyManager` | Менеджер общего пула прокси из `ProxyPool` |
| `ProxyWhitelistManager.cs` | `ProxyWhitelistManager : IProxyManager, IDisposable` | Менеджер белого списка — приоритетные прокси из JSON/БД |

#### Использование как параметр (2 файла)

| Файл | Метод |
|------|-------|
| `ProxyHttpClientFactory.cs` | `WaitForProxyAsync(IProxyManager? coordinator, CancellationToken ct)` |
| `ProxyRetryExecutor.cs` | `ExecuteAsync(..., IProxyManager? coordinator, ...)` |
| `ProxyRetryExecutor.cs` | `ReportSuccessSafe(IProxyManager? coordinator, string? proxyUrl)` — статический |
| `ProxyRetryExecutor.cs` | `ReportDailyLimitSafe(IProxyManager? coordinator, ...)` — статический |
| `ProxyRetryExecutor.cs` | `HandleDailyLimit(IProxyManager? coordinator, ...)` — статический |

### `HttpRetryHandler`

```csharp
public class HttpRetryHandler
{
    public async Task<HttpResponseMessage> ExecuteWithRetryAsync(
        Func<Task<HttpResponseMessage>> requestFunc,
        string? proxyUrl,
        IProxyManager? proxyManager,
        CancellationToken ct)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var response = await requestFunc();
            if (response.IsSuccessStatusCode) return response;
            
            // Обработка ошибок
            var delay = CalculateDelay(response.StatusCode, attempt);
            await Task.Delay(delay, ct);
        }
    }
}
```

### `ProxyHttpClientFactory` (дополнение)

```csharp
public class ProxyHttpClientFactory
{
    private readonly HttpRetryHandler _retryHandler;
    
    public async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpRequestMessage request,
        string? proxyUrl,
        IProxyManager? proxyManager,
        CancellationToken ct)
    {
        return await _retryHandler.ExecuteWithRetryAsync(
            () => SendAsync(request, proxyUrl, ct),
            proxyUrl,
            proxyManager,
            ct);
    }
}
```

## ✅ Тестирование стратегии повторов

Для тестирования стратегии можно использовать нашу тестовую утилиту `FixExceptions/Program.cs`:

```csharp
// Пример тестирования с имитацией ошибок
var retryHandler = new HttpRetryHandler(maxAttempts: 3);
var result = await retryHandler.ExecuteWithRetryAsync(async () =>
{
    var response = await httpClient.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new HttpRequestException($"HTTP {response.StatusCode}");
    return response;
}, proxyUrl, proxyManager, ct);
```

## 📚 Связанные документы

- [DYNAMIC_PROXY.md](DYNAMIC_PROXY.md) — динамическая ротация прокси
- [USERRESUME_WITH_PROXY.md](USERRESUME_WITH_PROXY.md) — интеграция прокси в UserResumeDetailScraper
- [PROGRESS_TRACKING.md](PROGRESS_TRACKING.md) — отслеживание прогресса скрапинга
- [BACKOFF_ALGORITHMS.md](BACKOFF_ALGORITHMS.md) — алгоритмы backoff

## 🚀 Примеры использования

### Пример 1: Базовая retry-логика

```csharp
var retryHandler = new HttpRetryHandler(maxAttempts: 3);
var response = await retryHandler.ExecuteWithRetryAsync(
    () => httpClient.GetAsync(url),
    proxyUrl,
    proxyManager,
    ct);
```

### Пример 2: Обработка 404

```csharp
if (response.StatusCode == HttpStatusCode.NotFound)
{
    // Не повторяем, фиксируем как "обработано"
    logger.LogWarning($"404 для {url}");
    return;
}
```

### Пример 3: Rate limiting

```csharp
if (response.StatusCode == HttpStatusCode.TooManyRequests)
{
    var retryAfter = response.Headers.RetryAfter?.Delta;
    var delay = retryAfter ?? TimeSpan.FromSeconds(60);
    await Task.Delay(delay, ct);
}