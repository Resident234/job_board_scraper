# Стратегия повторов для HTTP ошибок

## Обзор

Добавлена стратегия автоматических повторов для HTTP ошибок сервера (5xx) с использованием алгоритма **Exponential Backoff with Jitter** и троттлинга через `AdaptiveConcurrencyController`.

## Проблема

При работе с прокси часто возникают ошибки:
- **503 Service Unavailable** - сервер временно недоступен
- **500 Internal Server Error** - внутренняя ошибка сервера
- **502 Bad Gateway** - прокси не может подключиться к серверу
- **Socket errors** - ошибки подключения к прокси

## Решение

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

### 2. Примеры задержек

**Для ошибок сервера (503, 500, 502):**
```
Попытка 1:  2.0 ± 0.6 сек  (1.4 - 2.6 сек)
Попытка 2:  4.0 ± 1.2 сек  (2.8 - 5.2 сек)
Попытка 3:  8.0 ± 2.4 сек  (5.6 - 10.4 сек)
Попытка 4: 16.0 ± 4.8 сек  (11.2 - 20.8 сек)
Попытка 5: 32.0 ± 9.6 сек  (22.4 - 41.6 сек)
Попытка 6+: 60.0 сек max   (ограничено maxDelay)
```

**Для ошибок прокси/сети:**
```
Попытка 1: 0.5 ± 0.1 сек  (0.4 - 0.6 сек)
Попытка 2: 1.0 ± 0.2 сек  (0.8 - 1.2 сек)
Попытка 3: 2.0 ± 0.4 сек  (1.6 - 2.4 сек)
Попытка 4: 4.0 ± 0.8 сек  (3.2 - 4.8 сек)
Попытка 5+: 10.0 сек max  (ограничено maxDelay)
```

### 3. Троттлинг

Используется `AdaptiveConcurrencyController` для управления нагрузкой:

```csharp
sw.Stop();
_controller.ReportLatency(sw.Elapsed);
```

`AdaptiveConcurrencyController` автоматически:
- Уменьшает параллелизм при высокой задержке
- Увеличивает параллелизм при низкой задержке
- Предотвращает перегрузку сервера

## Поведение

### До изменений:
```
[UserResumeDetailScraper] HTTP запрос: 503 Service Unavailable
[UserResumeDetailScraper] Skipping user (non-success status code)
```

### После изменений:
```
[UserResumeDetailScraper] Using proxy: http://213.157.6.50:80 (attempt 16/30)
[UserResumeDetailScraper] Server error 503 (attempt 16/30). Backoff delay: 2.1с
[UserResumeDetailScraper] Retrying with next proxy after delay...
[UserResumeDetailScraper] Using proxy: http://77.76.189.189:8092 (attempt 17/30)
[UserResumeDetailScraper] HTTP запрос: 200 OK
```

## Преимущества

1. **Экспоненциальный рост задержки** - дает серверу время восстановиться
2. **Jitter (рандомизация)** - предотвращает "thundering herd" (одновременные повторы)
3. **Разные параметры** - для server errors и proxy errors
4. **Ограничение максимальной задержки** - не ждем слишком долго
5. **Подробное логирование** - видно задержку в логах

## Конфигурация

В `AppConfig.cs`:
- `ProxyMaxRetries` - максимальное количество попыток (по умолчанию 30)
- `ProxyRequestTimeout` - таймаут для запроса (по умолчанию 120 секунд)
- `ProxyWaitTimeoutSeconds` - таймаут ожидания прокси из пула (по умолчанию 60 секунд)

## Связанная документация

- [Алгоритмы Backoff](docs/BACKOFF_ALGORITHMS.md) - подробное описание всех алгоритмов

## Проверка

Код успешно компилируется:
```
dotnet build
Сборка успешно выполнено с предупреждениями (6) через 6,4 с
```
