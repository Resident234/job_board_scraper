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
Страница считалась обработанной, хотя данные не были получены.

### После изменений:
```
[UserResumeDetailScraper] Using proxy: http://213.157.6.50:80 (attempt 1/30)
[UserResumeDetailScraper] Server error 500 (attempt 1/30). Backoff delay: 2.1с
[UserResumeDetailScraper] Retrying with next proxy after delay...
[UserResumeDetailScraper] Using proxy: http://159.203.61.169:3128 (attempt 2/30)
[UserResumeDetailScraper] Forbidden (IP blocked) 403 (attempt 2/30). Backoff delay: 0.6с
[UserResumeDetailScraper] Retrying with next proxy after delay...
[UserResumeDetailScraper] Using proxy: http://77.76.189.189:8092 (attempt 3/30)
[UserResumeDetailScraper] HTTP запрос: 200 OK
```
Страница повторяется до получения 200 OK или исчерпания попыток.

## ✅ Преимущества

1. **Страница не считается обработанной до 200 OK** - гарантия получения данных
2. **Обработка всех типов ошибок** - 5xx, 403, 429, 408 и любые другие не-200
3. **Экспоненциальный рост задержки** - дает серверу время восстановиться
4. **Jitter (рандомизация)** - предотвращает "thundering herd" (одновременные повторы)
5. **Разные параметры** - для server errors и proxy/client errors
6. **Поддержка Retry-After** - для 429 используется значение из заголовка
7. **Автоматическая смена прокси** - при каждой ошибке берётся новый прокси
8. **Ограничение максимальной задержки** - не ждем слишком долго
9. **Подробное логирование** - видно тип ошибки и задержку в логах

## 🔧 Конфигурация

В `AppConfig.cs`:
- `ProxyMaxRetries` - максимальное количество попыток (по умолчанию 30)
- `ProxyRequestTimeout` - таймаут для запроса (по умолчанию 120 секунд)
- `ProxyWaitTimeoutSeconds` - таймаут ожидания прокси из пула (по умолчанию 60 секунд)

## 🔗 Связанная документация

- [Алгоритмы Backoff](docs/BACKOFF_ALGORITHMS.md) - подробное описание всех алгоритмов
- [Обработка пустых профилей](docs/EMPTY_PROFILE.md) - как обрабатываются несуществующие профили
- [Система прокси](docs/PROXY_README.md) - как работает система прокси

## 📋 Примеры использования

### Пример 1: Обработка ошибок в SmartHttpClient

```csharp
// Создание клиента с поддержкой повторов
var httpClient = new HttpClient();
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "UserResumeScraper",
    proxyRotator: proxyRotator,
    maxRetries: 30,
    enableRetry: true
);

// Запрос с автоматическим повтором
var response = await smartClient.GetAsync("https://career.habr.com/user/resume/12345");
if (response.IsSuccessStatusCode)
{
    // Обработка успешного ответа
    Console.WriteLine($"Успешно: {response.StatusCode}");
}
else
{
    // Логика для неудачных ответов
    Console.WriteLine($"Ошибка: {response.StatusCode}");
}
```

### Пример 2: Настройка задержек

```csharp
// Настройка кастомных параметров задержки
var backoff = new ExponentialBackoff(
    serverErrorBaseDelay: TimeSpan.FromSeconds(2),
    serverErrorMaxDelay: TimeSpan.FromSeconds(60),
    proxyErrorBaseDelay: TimeSpan.FromSeconds(0.5),
    proxyErrorMaxDelay: TimeSpan.FromSeconds(10),
    jitterPercentage: 0.3f
);

// Использование в коде
var delay = backoff.CalculateServerErrorDelay(attemptNumber);
Console.WriteLine($"Задержка: {delay.TotalSeconds} секунд");
```

### Пример 3: Обработка Retry-After

```csharp
// Обработка заголовка Retry-After
if (response.StatusCode == HttpStatusCode.TooManyRequests &&
    response.Headers.RetryAfter != null)
{
    var retryAfter = response.Headers.RetryAfter;
    if (retryAfter.Date != null)
    {
        // Использовать время из заголовка
        var delay = retryAfter.Date.Value - DateTime.UtcNow;
        Console.WriteLine($"Ожидание Retry-After: {delay.TotalSeconds} секунд");
    }
    else if (retryAfter.Delta != null)
    {
        // Использовать время в секундах
        Console.WriteLine($"Ожидание Retry-After: {retryAfter.Delta.Value.TotalSeconds} секунд");
    }
}
```

## 🔍 Отладка и диагностика

### Логирование ошибок

```csharp
// Пример логирования ошибок
try
{
    var response = await smartClient.GetAsync(url);
    if (!response.IsSuccessStatusCode)
    {
        _logger.WriteLine($"Ошибка {response.StatusCode} при запросе {url}");
        _logger.WriteLine($"Попытка {attempt}/{maxRetries}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.WriteLine("Профиль не найден (404)");
        }
        else if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.WriteLine("IP заблокирован (403)");
        }
        else if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.WriteLine("Превышен лимит запросов (429)");
        }
        else if (response.StatusCode >= HttpStatusCode.InternalServerError)
        {
            _logger.WriteLine("Ошибка сервера (5xx)");
        }
    }
}
catch (Exception ex)
{
    _logger.WriteLine($"Исключение: {ex.Message}");
}
```

### Мониторинг задержек

```csharp
// Мониторинг задержек между попытками
var stopwatch = Stopwatch.StartNew();
try
{
    var response = await smartClient.GetAsync(url);
    stopwatch.Stop();
    _logger.WriteLine($"Ответ получен за {stopwatch.Elapsed.TotalSeconds} секунд");
}
catch (Exception ex)
{
    stopwatch.Stop();
    _logger.WriteLine($"Ошибка после {stopwatch.Elapsed.TotalSeconds} секунд: {ex.Message}");
}
```

## 📊 Статистика и анализ

### Сбор статистики

```csharp
// Сбор статистики по ошибкам
var errorStats = new Dictionary<HttpStatusCode, int>();
var retryStats = new Dictionary<int, int>(); // Попытка: количество

// В обработчике ошибок
if (!response.IsSuccessStatusCode)
{
    errorStats[response.StatusCode] = errorStats.GetValueOrDefault(response.StatusCode, 0) + 1;
    retryStats[attempt] = retryStats.GetValueOrDefault(attempt, 0) + 1;
}

// Вывод статистики
Console.WriteLine("\nСтатистика ошибок:");
foreach (var stat in errorStats)
{
    Console.WriteLine($"{stat.Key}: {stat.Value} раз");
}

Console.WriteLine("\nСтатистика повторов:");
foreach (var stat in retryStats.OrderBy(kvp => kvp.Key))
{
    Console.WriteLine($"Попытка {stat.Key}: {stat.Value} раз");
}
```

### Анализ успешности

```csharp
// Анализ успешности запросов
var successRate = (double)successCount / totalRequests * 100;
Console.WriteLine($"Успешность: {successRate:F1}% ({successCount}/{totalRequests})");

if (successRate < 80)
{
    Console.WriteLine("⚠️ Низкая успешность запросов! Проверьте прокси и настройки.");
}
```

## 🔄 Алгоритмы повторов

### Exponential Backoff with Jitter

Алгоритм использует экспоненциальный рост задержки с добавлением случайного jitter для предотвращения синхронизации повторов:

```csharp
// Пример реализации
public static TimeSpan CalculateServerErrorDelay(int attempt)
{
    // Базовая задержка: 2^attempt секунд
    var baseDelay = Math.Min(2 * (int)Math.Pow(2, attempt - 1), 60);

    // Jitter: ±30% от базовой задержки
    var jitter = baseDelay * 0.3f * (new Random().NextDouble() - 0.5);

    // Округление до целого числа
    return TimeSpan.FromSeconds(Math.Max(1, baseDelay + jitter));
}
```

### Параметры алгоритма

| Параметр | Значение для серверных ошибок | Значение для прокси ошибок |
|----------|-------------------------------|----------------------------|
| baseDelay | 2000мс (2 сек) | 500мс (0.5 сек) |
| maxDelay  | 60000мс (60 сек) | 10000мс (10 сек) |
| jitter    | 30% | 20% |
| maxAttempts | 30 | 30 |

## 🔧 Настройка и оптимизация

### Настройка параметров

```csharp
// Настройка кастомных параметров
var backoffConfig = new ExponentialBackoffConfig
{
    ServerErrorBaseDelay = TimeSpan.FromSeconds(2),
    ServerErrorMaxDelay = TimeSpan.FromSeconds(60),
    ProxyErrorBaseDelay = TimeSpan.FromSeconds(0.5),
    ProxyErrorMaxDelay = TimeSpan.FromSeconds(10),
    JitterPercentage = 0.3f,
    MaxAttempts = 30
};

var backoff = new ExponentialBackoff(backoffConfig);
```

### Оптимизация для разных сценариев

1. **Для высоконагруженных серверов:**
   - Увеличьте `ServerErrorBaseDelay` до 5 секунд
   - Увеличьте `ServerErrorMaxDelay` до 120 секунд

2. **Для чувствительных к задержке приложений:**
   - Уменьшите `ProxyErrorBaseDelay` до 200мс
   - Уменьшите `ProxyErrorMaxDelay` до 5 секунд

3. **Для стабильных соединений:**
   - Уменьшите `JitterPercentage` до 10%
   - Увеличьте `MaxAttempts` до 50

## 📋 Проверка и тестирование

### Тестирование алгоритма

```csharp
// Тестирование алгоритма задержек
var backoff = new ExponentialBackoff();
for (int i = 1; i <= 10; i++)
{
    var delay = backoff.CalculateServerErrorDelay(i);
    Console.WriteLine($"Попытка {i}: {delay.TotalSeconds:F1} сек");
}
```

### Тестирование с реальными ошибками

```csharp
// Симуляция ошибок для тестирования
var testCases = new[]
{
    (HttpStatusCode.InternalServerError, "Server error"),
    (HttpStatusCode.BadGateway, "Proxy error"),
    (HttpStatusCode.Forbidden, "IP blocked"),
    (HttpStatusCode.TooManyRequests, "Rate limited"),
    (HttpStatusCode.RequestTimeout, "Timeout")
};

foreach (var (statusCode, description) in testCases)
{
    Console.WriteLine($"\nТестирование {description} ({statusCode}):");

    for (int attempt = 1; attempt <= 5; attempt++)
    {
        var delay = statusCode == HttpStatusCode.InternalServerError
            ? ExponentialBackoff.CalculateServerErrorDelay(attempt)
            : ExponentialBackoff.CalculateProxyErrorDelay(attempt);

        Console.WriteLine($"Попытка {attempt}: {delay.TotalSeconds:F1} сек");
    }
}
```

## 🎯 Заключение

Стратегия повторов для HTTP ошибок значительно улучшает надежность системы:
- **Гарантирует получение данных** - страница считается обработанной только при 200 OK
- **Обрабатывает все типы ошибок** - от серверных ошибок до проблем с прокси
- **Предотвращает перегрузку сервера** - экспоненциальный рост задержки
- **Повышает устойчивость** - автоматическая смена прокси при ошибках
- **Обеспечивает подробное логирование** - для диагностики проблем

Для серьезных задач рекомендуется использовать коммерческие прокси-сервисы, которые обеспечивают более стабильное соединение и меньшую вероятность блокировок.

---

## 🏗️ Архитектура `ProxyRetryExecutor` и `ProxyHttpClientFactory` — почему часть методов `static`, а часть вызывается через `_retryExecutor`

### Где используется `ProxyHttpClientFactory`

Класс `ProxyHttpClientFactory` (`JobBoardScraper/Infrastructure/Proxy/ProxyHttpClientFactory.cs`) используется в двух местах проекта:

1. **`ProxyRetryExecutor`** — принимает экземпляр через конструктор и хранит в поле `_clientFactory`:
   ```csharp
   private readonly ProxyHttpClientFactory _clientFactory;

   public ProxyRetryExecutor(
       ProxyHttpClientFactory clientFactory,
       ConsoleLogger? logger = null, ...)
   ```

2. **`UserResumeDetailScraper`** — создаёт экземпляр и передаёт в `ProxyRetryExecutor`:
   ```csharp
   var clientFactory = new ProxyHttpClientFactory(logger: _logger);
   _retryExecutor = new ProxyRetryExecutor(clientFactory, logger: _logger);
   ```

Больше нигде в проекте `ProxyHttpClientFactory` не используется.

### Архитектура `ProxyRetryExecutor`

Класс `ProxyRetryExecutor` (`JobBoardScraper/Infrastructure/Proxy/ProxyRetryExecutor.cs`) намеренно совмещает две роли. Это не дублирование, а разделение ответственности по наличию/отсутствию внутреннего состояния.

### 1. Через поле экземпляра `_retryExecutor` — `ExecuteAsync`

`ExecuteAsync` (строки 65–208) обращается к полям экземпляра:
- `_clientFactory` — фабрика HTTP-клиентов с прокси;
- `_logger` — логгер для записей о retry/переключении прокси;
- `_maxRetriesPerProxy`, `_maxProxySwitches` — конфигурация retry, читается из `AppConfig` в конструкторе.

Все эти параметры задаются **один раз при создании скрапера** в конструкторе (например, в `UserResumeDetailScraper`):

```csharp
var clientFactory = new ProxyHttpClientFactory(logger: _logger);
_retryExecutor = new ProxyRetryExecutor(clientFactory, logger: _logger);
```

Поэтому все HTTP-запросы в `ProcessUserAsync` идут через инстанс-поле:

```csharp
var result = await _retryExecutor.ExecuteAsync(
    url: userLink,
    coordinator: _proxyCoordinator,
    fallbackSend: () => _httpClient.GetAsync(userLink, ct),
    proxySend:   client => client.GetAsync(userLink, ct),
    ct: ct).ConfigureAwait(false);
```

Здесь нужен доступ к настроенному состоянию executor'а.

### 2. Через имя класса `ProxyRetryExecutor` — статические хелперы

Следующие методы являются `static` и не используют ни `_clientFactory`, ни `_maxRetries*`:

- `ProxyRetryExecutor.ReportSuccessSafe(coordinator, proxyUrl)` — уведомить координатор об успехе прокси.
- `ProxyRetryExecutor.ReportDailyLimitSafe(coordinator, proxyUrl, logger?)` — уведомить о суточном лимите.
- `ProxyRetryExecutor.HandleDailyLimit(coordinator, proxyUrl, userLink, logger?)` — реакция на суточный лимит: уведомление координатора, запрос нового прокси, лог.

Они зависят **только от переданных аргументов** (`IProxyManager` + `ConsoleLogger`). Это чистые функции, не привязанные к жизненному циклу одного скрапера.

### 3. Почему не сделать всё instance?

| Вариант | Проблема |
|---------|----------|
| Сделать хелперы instance-методами `_retryExecutor.ReportSuccessSafe(...)` | Звучит странно: executor сам себе что-то "report'ит". Плюс пришлось бы городить пустой конструктор или передавать лишние параметры там, где состояние executor'а не нужно. |
| Сделать `ExecuteAsync` статическим | Невозможно: метод зависит от `_clientFactory`/`_logger`/`_maxRetries*` — это конфигурация, привязанная к конкретному скраперу. |

### 4. Правило для нового кода

> **Если метод работает с состоянием executor'а (фабрика клиентов, логгер, лимиты retry) — он instance и вызывается через `_retryExecutor`.**
>
> **Если метод — это чистая утилита над `IProxyManager` + `ConsoleLogger` — он `static` и вызывается через имя класса `ProxyRetryExecutor`.**

### 5. Мини-шпаргалка по сигнатурам

```csharp
// instance — есть состояние (фабрика, логгер, лимиты из AppConfig)
public async Task<ProxyRequestResult> ExecuteAsync(
    string url,
    IProxyManager? coordinator,
    Func<Task<HttpResponseMessage>> fallbackSend,
    Func<HttpClient, Task<HttpResponseMessage>> proxySend,
    CancellationToken ct);

// static — чистые хелперы, состояния не хранят
public static void ReportSuccessSafe(IProxyManager? coordinator, string? proxyUrl);
public static void ReportDailyLimitSafe(IProxyManager? coordinator, string? proxyUrl, ConsoleLogger? logger = null);
public static bool HandleDailyLimit(IProxyManager? coordinator, string? proxyUrl, string userLink, ConsoleLogger? logger = null);
```
