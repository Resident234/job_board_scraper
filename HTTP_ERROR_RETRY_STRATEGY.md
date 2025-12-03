# Стратегия повторов для HTTP ошибок

## Обзор

Добавлена стратегия автоматических повторов для HTTP ошибок сервера (5xx) с использованием алгоритма **Exponential Backoff with Jitter** и троттлинга через `AdaptiveConcurrencyController`.

## Проблема

При работе с прокси часто возникают ошибки:
- **500 Internal Server Error** - внутренняя ошибка сервера
- **502 Bad Gateway** - прокси не может подключиться к серверу
- **503 Service Unavailable** - сервер временно недоступен
- **403 Forbidden** - IP заблокирован или доступ запрещён
- **429 Too Many Requests** - превышен лимит запросов (rate limiting)
- **408 Request Timeout** - таймаут запроса
- **Socket errors** - ошибки подключения к прокси

**Важно:** Страница считается обработанной ТОЛЬКО при получении ответа 200 OK. Любой другой код ответа приводит к повторной попытке со сменой прокси.

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

### Особая обработка 404 Not Found

При получении ответа 404:
- Страница считается обработанной (не повторяется)
- В поле `title` записывается "Ошибка 404"
- В поле `about` записывается "Ошибка 404"
- Это позволяет отслеживать удалённые или несуществующие профили

### 3. Примеры задержек

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

## Преимущества

1. **Страница не считается обработанной до 200 OK** - гарантия получения данных
2. **Обработка всех типов ошибок** - 5xx, 403, 429, 408 и любые другие не-200
3. **Экспоненциальный рост задержки** - дает серверу время восстановиться
4. **Jitter (рандомизация)** - предотвращает "thundering herd" (одновременные повторы)
5. **Разные параметры** - для server errors и proxy/client errors
6. **Поддержка Retry-After** - для 429 используется значение из заголовка
7. **Автоматическая смена прокси** - при каждой ошибке берётся новый прокси
8. **Ограничение максимальной задержки** - не ждем слишком долго
9. **Подробное логирование** - видно тип ошибки и задержку в логах

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
