# Оптимизация трафика в JobBoardScraper

## Как работает загрузка страниц через HttpClient

### ✅ Что загружается
- **HTML-код страницы** - основной контент
- **Встроенные стили** (inline CSS в HTML)
- **Встроенные скрипты** (inline JavaScript в HTML)

### ❌ Что НЕ загружается автоматически
- **Изображения** (`<img src="...">`)
- **Внешние CSS** (`<link rel="stylesheet">`)
- **Внешние JavaScript** (`<script src="...">`)
- **Шрифты** (`@font-face`)
- **Видео/аудио** (`<video>`, `<audio>`)
- **Фоновые изображения** (CSS `background-image`)

## Почему HttpClient экономит трафик

`HttpClient` получает только **исходный HTML-код** страницы, не рендерит её и не загружает внешние ресурсы. Это означает:

```
Браузер (Chrome/Firefox):
  HTML (10 KB) + CSS (50 KB) + JS (200 KB) + Images (500 KB) = 760 KB

HttpClient:
  HTML (10 KB) = 10 KB

Экономия: ~98.7%
```

## Реализованные оптимизации

### 1. Сжатие контента (gzip, deflate, br)

```csharp
client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
```

**Эффект:** Сжатие HTML в 5-10 раз
- HTML без сжатия: 50 KB
- HTML с gzip: 5-10 KB

### 2. Явное указание типов контента

```csharp
client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
```

**Эффект:** Сервер знает, что нужен только HTML, не отправляет лишние данные

### 3. Измерение трафика через SmartHttpClient

Все HTTP-запросы автоматически измеряются:
- Размер каждого ответа
- Статистика по скраперам
- Общая статистика

## Дополнительные возможности оптимизации

### Если нужно ещё больше сэкономить

#### 1. Использовать HEAD-запросы для проверки существования

```csharp
// Вместо GET для проверки 404
var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
if (response.StatusCode == HttpStatusCode.NotFound)
    return; // Страница не существует, не загружаем контент
```

**Экономия:** HEAD возвращает только заголовки без тела ответа

#### 2. Кэширование результатов

```csharp
// Сохранять уже обработанные URL в памяти или БД
if (cache.Contains(url))
    return; // Не загружаем повторно
```

#### 3. Условные запросы (If-Modified-Since)

```csharp
client.DefaultRequestHeaders.IfModifiedSince = lastCheckDate;
// Сервер вернёт 304 Not Modified, если контент не изменился
```

## Текущая статистика трафика

Статистика автоматически сохраняется в `./logs/traffic_stats.txt`:

```
================================================================================
Traffic Statistics Report - 2025-11-02 12:30:00
================================================================================

OVERALL STATISTICS:
  Total Requests: 1,234
  Total Traffic:  45.67 MB
  Average/Request: 37.92 KB

PER-SCRAPER STATISTICS:
--------------------------------------------------------------------------------
  BruteForceUsernameScraper:
    Requests:     500
    Total:        18.5 MB
    Avg/Request:  37.92 KB
```

## Настройка через конфигурацию

```xml
<!-- Включить/отключить измерение трафика для каждого скрапера -->
<add key="BruteForce:EnableTrafficMeasuring" value="true" />
<add key="Companies:EnableTrafficMeasuring" value="true" />
<add key="CompanyFollowers:EnableTrafficMeasuring" value="true" />
<add key="ResumeList:EnableTrafficMeasuring" value="true" />
<add key="Category:EnableTrafficMeasuring" value="true" />

<!-- Настройки сохранения статистики -->
<add key="Traffic:OutputFile" value="./logs/traffic_stats.txt" />
<add key="Traffic:SaveIntervalMinutes" value="5" />
```

## Выводы

✅ **HttpClient уже оптимален** - не загружает изображения и внешние ресурсы  
✅ **Сжатие включено** - gzip/deflate/br экономят 80-90% трафика  
✅ **Измерение работает** - видим реальное потребление трафика  
✅ **Настройка гибкая** - можно включать/отключать для каждого скрапера  

**Итого:** При скрапинге HTML-страниц через HttpClient вы уже экономите ~98% трафика по сравнению с браузером!
