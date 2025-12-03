# Алгоритмы Backoff (задержки между повторами)

## Обзор

При работе со скрапингом и прокси часто возникают ошибки, требующие повторных попыток. Правильный выбор алгоритма задержки между повторами критически важен для:
- Предотвращения перегрузки сервера
- Эффективного использования ресурсов
- Избежания блокировок

## Алгоритмы

### 1. Fixed Delay (Фиксированная задержка)

**Формула:** `delay = constant`

**Пример:**
```
Попытка 1: 2 сек
Попытка 2: 2 сек
Попытка 3: 2 сек
...
```

**Плюсы:**
- Простота реализации
- Предсказуемое поведение

**Минусы:**
- Не адаптируется к ситуации
- При серьезных проблемах может быть недостаточной
- При легких проблемах - избыточной

**Когда использовать:** Простые сценарии с редкими ошибками

---

### 2. Linear Backoff (Линейный рост)

**Формула:** `delay = baseDelay * attempt`

**Пример (baseDelay = 1 сек):**
```
Попытка 1: 1 сек
Попытка 2: 2 сек
Попытка 3: 3 сек
Попытка 4: 4 сек
...
```

**Плюсы:**
- Постепенно увеличивает задержку
- Простая реализация

**Минусы:**
- Медленный рост
- Может быть недостаточным при серьезных проблемах

**Когда использовать:** Сценарии с умеренной нагрузкой

---

### 3. Exponential Backoff (Экспоненциальный рост) ⭐

**Формула:** `delay = baseDelay * 2^(attempt-1)`

**Пример (baseDelay = 1 сек):**
```
Попытка 1: 1 сек
Попытка 2: 2 сек
Попытка 3: 4 сек
Попытка 4: 8 сек
Попытка 5: 16 сек
Попытка 6: 32 сек
...
```

**Плюсы:**
- Быстро увеличивает задержку при повторных ошибках
- Дает серверу время восстановиться
- Широко используется в индустрии

**Минусы:**
- Может стать слишком большой без ограничения
- Все клиенты могут повторять одновременно ("thundering herd")

**Когда использовать:** Большинство сценариев с повторами

---

### 4. Exponential Backoff with Jitter (с рандомизацией) ⭐⭐

**Формула:** `delay = baseDelay * 2^(attempt-1) + random(-jitter, +jitter)`

**Пример (baseDelay = 1 сек, jitter = 30%):**
```
Попытка 1: 1000 ± 300 мс  (0.7 - 1.3 сек)
Попытка 2: 2000 ± 600 мс  (1.4 - 2.6 сек)
Попытка 3: 4000 ± 1200 мс (2.8 - 5.2 сек)
Попытка 4: 8000 ± 2400 мс (5.6 - 10.4 сек)
...
```

**Плюсы:**
- Все преимущества экспоненциального роста
- Предотвращает "thundering herd" (когда все клиенты повторяют одновременно)
- Лучшее распределение нагрузки

**Минусы:**
- Немного сложнее реализовать
- Менее предсказуемое поведение

**Когда использовать:** Параллельные запросы, высоконагруженные системы

---

### 5. Decorrelated Jitter (рекомендация AWS) ⭐⭐⭐

**Формула:** `delay = min(maxDelay, random(baseDelay, previousDelay * 3))`

**Пример:**
```
Попытка 1: random(1, 3) сек
Попытка 2: random(1, previousDelay * 3) сек
Попытка 3: random(1, previousDelay * 3) сек
...
```

**Плюсы:**
- Лучшее распределение нагрузки
- Рекомендуется AWS для их сервисов
- Хорошо работает при высокой конкуренции

**Минусы:**
- Сложнее реализовать
- Требует хранения предыдущей задержки

**Когда использовать:** Высоконагруженные распределенные системы

---

### 6. Adaptive Backoff (Адаптивный)

**Формула:** Динамически корректируется на основе частоты ошибок

**Пример:**
```
Если ошибок < 10%: delay = baseDelay
Если ошибок 10-30%: delay = baseDelay * 2
Если ошибок 30-50%: delay = baseDelay * 4
Если ошибок > 50%: delay = baseDelay * 8
```

**Плюсы:**
- Самый умный подход
- Автоматически адаптируется к ситуации

**Минусы:**
- Сложная реализация
- Требует отслеживания статистики

**Когда использовать:** Долгоживущие сервисы с переменной нагрузкой

---

## Реализация в проекте

В проекте используется **Exponential Backoff with Jitter** с ограничением максимальной задержки.

### Класс ExponentialBackoff

Расположение: `JobBoardScraper/Helper.Utils/ExponentialBackoff.cs`

#### Основной метод

```csharp
public static int CalculateDelay(
    int attempt,           // Номер попытки (начиная с 1)
    int baseDelayMs = 1000,    // Базовая задержка в мс
    int maxDelayMs = 30000,    // Максимальная задержка в мс
    double jitterFactor = 0.3) // Фактор рандомизации (±30%)
```

**Алгоритм:**
1. Рассчитывает экспоненциальную задержку: `baseDelay * 2^(attempt-1)`
2. Ограничивает максимальной задержкой: `Math.Min(exponentialDelay, maxDelayMs)`
3. Добавляет jitter: `cappedDelay + random(-jitter, +jitter)`
4. Гарантирует минимум 100мс

#### Специализированные методы

```csharp
// Для ошибок сервера (5xx) - более агрессивные параметры
var delay = ExponentialBackoff.CalculateServerErrorDelay(attempt);
// baseDelay=2000мс, maxDelay=60000мс, jitter=30%

// Для ошибок прокси/сети - менее агрессивные параметры
var delay = ExponentialBackoff.CalculateProxyErrorDelay(attempt);
// baseDelay=500мс, maxDelay=10000мс, jitter=20%

// Для логирования
var description = ExponentialBackoff.GetDelayDescription(delayMs);
// Возвращает "500мс" или "2.5с"
```

### Примеры задержек

**Для ошибок сервера (503, 500, 502):**
```
Попытка 1:  2.0 ± 0.6 сек  (1.4 - 2.6 сек)
Попытка 2:  4.0 ± 1.2 сек  (2.8 - 5.2 сек)
Попытка 3:  8.0 ± 2.4 сек  (5.6 - 10.4 сек)
Попытка 4: 16.0 ± 4.8 сек  (11.2 - 20.8 сек)
Попытка 5: 32.0 ± 9.6 сек  (22.4 - 41.6 сек)
Попытка 6+: 60.0 ± 18 сек  (42 - 78 сек) - ограничено maxDelay
```

**Для ошибок прокси/сети:**
```
Попытка 1: 0.5 ± 0.1 сек  (0.4 - 0.6 сек)
Попытка 2: 1.0 ± 0.2 сек  (0.8 - 1.2 сек)
Попытка 3: 2.0 ± 0.4 сек  (1.6 - 2.4 сек)
Попытка 4: 4.0 ± 0.8 сек  (3.2 - 4.8 сек)
Попытка 5+: 10.0 ± 2 сек  (8 - 12 сек) - ограничено maxDelay
```

### Использование в UserResumeDetailScraper

```csharp
// При ошибке сервера (5xx)
if (response != null && (int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
{
    var serverErrorDelay = ExponentialBackoff.CalculateServerErrorDelay(attempt);
    _logger.WriteLine($"Server error {(int)response.StatusCode} (attempt {attempt}/{maxRetries}). " +
        $"Backoff delay: {ExponentialBackoff.GetDelayDescription(serverErrorDelay)}");
    
    if (attempt < maxRetries && _proxyPool != null)
    {
        _logger.WriteLine($"Retrying with next proxy after delay...");
        response.Dispose();
        response = null;
        await Task.Delay(serverErrorDelay, ct);
        continue;
    }
}

// При ошибке прокси/сети
catch (Exception ex) when (attempt < maxRetries && _proxyPool != null)
{
    var proxyErrorDelay = ExponentialBackoff.CalculateProxyErrorDelay(attempt);
    
    _logger.WriteLine($"Proxy error (attempt {attempt}/{maxRetries}): {ex.Message}. " +
        $"Backoff delay: {ExponentialBackoff.GetDelayDescription(proxyErrorDelay)}");
    
    if (attempt < maxRetries)
    {
        _logger.WriteLine($"Trying next proxy after delay...");
        await Task.Delay(proxyErrorDelay, ct);
    }
}
```

### Пример логов

```
[UserResumeDetailScraper] Using proxy: http://213.157.6.50:80 (attempt 1/30)
[UserResumeDetailScraper] Server error 503 (attempt 1/30). Backoff delay: 2.1с
[UserResumeDetailScraper] Retrying with next proxy after delay...
[UserResumeDetailScraper] Using proxy: http://77.76.189.189:8092 (attempt 2/30)
[UserResumeDetailScraper] Server error 503 (attempt 2/30). Backoff delay: 4.3с
[UserResumeDetailScraper] Retrying with next proxy after delay...
[UserResumeDetailScraper] Using proxy: http://189.202.188.149:80 (attempt 3/30)
[UserResumeDetailScraper] HTTP запрос: 200 OK
```

### Потокобезопасность

Класс использует `lock` для генерации случайных чисел, что обеспечивает потокобезопасность при параллельных запросах:

```csharp
private static readonly Random _random = new();
private static readonly object _lock = new();

// В методе CalculateDelay:
double randomValue;
lock (_lock)
{
    randomValue = _random.NextDouble() * 2 - 1; // от -1 до +1
}
```

---

## Сравнительная таблица

| Алгоритм | Сложность | Адаптивность | Thundering Herd | Рекомендация |
|----------|-----------|--------------|-----------------|--------------|
| Fixed | Низкая | Нет | Да | Простые случаи |
| Linear | Низкая | Частичная | Да | Умеренная нагрузка |
| Exponential | Средняя | Да | Да | Большинство случаев |
| Exponential + Jitter | Средняя | Да | Нет | **Рекомендуется** |
| Decorrelated Jitter | Высокая | Да | Нет | AWS сервисы |
| Adaptive | Высокая | Полная | Нет | Сложные системы |

---

## Ссылки

- [AWS Architecture Blog: Exponential Backoff And Jitter](https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/)
- [Google Cloud: Retry Strategy](https://cloud.google.com/storage/docs/retry-strategy)
- [Microsoft: Transient fault handling](https://docs.microsoft.com/en-us/azure/architecture/best-practices/transient-faults)
