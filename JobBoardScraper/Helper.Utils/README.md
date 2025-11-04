# Helper.Utils

Вспомогательные утилиты для JobBoardScraper.

## HtmlDebug

Класс для сохранения HTML-страниц в файлы для отладки.

### Возможности

- Сохранение HTML-контента в файлы с префиксом имени скрапера
- Автоматическое создание директории для логов
- Поддержка различных кодировок
- Асинхронная и синхронная версии

### Использование

#### Асинхронное сохранение

```csharp
using JobBoardScraper.Helper.Utils;

var html = await response.Content.ReadAsStringAsync();

var savedPath = await HtmlDebug.SaveHtmlAsync(
    html: html,
    scraperName: "ExpertsScraper",
    fileName: "last_page.html",
    encoding: Encoding.UTF8,
    ct: cancellationToken
);

if (savedPath != null)
{
    Console.WriteLine($"HTML сохранён: {savedPath}");
}
```

#### Синхронное сохранение

```csharp
var savedPath = HtmlDebug.SaveHtml(
    html: html,
    scraperName: "CompanyFollowersScraper",
    fileName: "last_page.html"
);
```

### Параметры

- **html** (string, обязательный) - HTML-контент для сохранения
- **scraperName** (string, обязательный) - Название скрапера (используется как префикс)
- **fileName** (string, опциональный) - Имя файла без префикса (по умолчанию: "last_page.html")
- **outputDirectory** (string?, опциональный) - Директория для сохранения (по умолчанию: из AppConfig)
- **encoding** (Encoding?, опциональный) - Кодировка для сохранения (по умолчанию: UTF-8)
- **ct** (CancellationToken, опциональный) - Токен отмены (только для async версии)

### Формат имени файла

Файлы сохраняются с префиксом имени скрапера:

```
{scraperName}_{fileName}
```

**Примеры:**
- `ExpertsScraper_last_page.html`
- `CompanyFollowersScraper_last_page.html`
- `BruteForceUsernameScraper_error_page.html`

### Директория сохранения

По умолчанию файлы сохраняются в директорию, указанную в `AppConfig.LoggingOutputDirectory` (обычно `./logs`).

Можно указать другую директорию через параметр `outputDirectory`.

### Обработка ошибок

Методы возвращают:
- **string** - путь к сохранённому файлу при успехе
- **null** - в случае ошибки (пустой HTML, ошибка записи и т.д.)

Исключения не выбрасываются, что делает использование безопасным.

### Примеры использования в скраперах

#### ExpertsScraper

```csharp
var html = encoding.GetString(htmlBytes);

var savedPath = await HtmlDebugHelper.SaveHtmlAsync(
    html, 
    "ExpertsScraper", 
    "last_page.html",
    encoding: encoding,
    ct: ct);

if (savedPath != null)
{
    _logger.WriteLine($"HTML сохранён: {savedPath} (кодировка: {encoding.WebName})");
}
```

#### CompanyFollowersScraper

```csharp
var savedPath = await HtmlDebugHelper.SaveHtmlAsync(
    html, 
    "CompanyFollowersScraper", 
    "last_page.html",
    encoding: encoding,
    ct: ct);

if (savedPath != null)
{
    _logger.WriteLine($"HTML сохранён: {savedPath}");
}
```

### Преимущества

1. **Единообразие** - все скраперы используют один и тот же подход
2. **Безопасность** - не выбрасывает исключения
3. **Гибкость** - поддержка различных кодировок и директорий
4. **Отладка** - легко найти файлы по префиксу скрапера
5. **Простота** - минимальный код в скраперах

### Интеграция в новые скраперы

При создании нового скрапера просто добавьте:

```csharp
using JobBoardScraper.Helper.Utils;

// В методе обработки страницы:
var savedPath = await HtmlDebugHelper.SaveHtmlAsync(
    html, 
    "YourScraperName", 
    "last_page.html",
    encoding: encoding,
    ct: ct);
```

Файл будет автоматически сохранён как `YourScraperName_last_page.html` в директории логов.
