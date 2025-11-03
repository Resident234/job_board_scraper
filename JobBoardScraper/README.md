# JobBoardScraper

Приложение для скрапинга данных с career.habr.com.

## Архитектура

Приложение запускает пять параллельных процессов:

1. **BruteForceUsernameScraper** - перебор всех возможных имен пользователей (a-z, 0-9, -, _)
2. **ResumeListPageScraper** - периодический обход страницы со списком резюме (каждые 10 минут)
3. **CompanyListScraper** - периодический обход списка компаний (раз в неделю)
4. **CategoryScraper** - периодический сбор category_root_id из select элемента (раз в неделю)
5. **CompanyFollowersScraper** - периодический обход подписчиков компаний (раз в неделю)

## Конфигурация

Все настройки находятся в файле `App.config`:

### Управление скраперами

Каждый скрапер можно включить или отключить через конфигурацию:

```xml
<!-- Включить/отключить скраперы -->
<add key="BruteForce:Enabled" value="false" />
<add key="ResumeList:Enabled" value="false" />
<add key="Companies:Enabled" value="false" />
<add key="Category:Enabled" value="false" />
<add key="CompanyFollowers:Enabled" value="true" />
```

**По умолчанию включен только `CompanyFollowersScraper`**, остальные отключены.

При запуске приложение выведет статус каждого скрапера:
```
[Program] ResumeListPageScraper: ОТКЛЮЧЕН
[Program] CompanyListScraper: ОТКЛЮЧЕН
[Program] CategoryScraper: ОТКЛЮЧЕН
[Program] CompanyFollowersScraper: ВКЛЮЧЕН
[Program] BruteForceUsernameScraper: ОТКЛЮЧЕН
```

### BruteForceUsernameScraper
- `BruteForce:Enabled` - включить/отключить скрапер (по умолчанию: `false`)
- `BruteForce:BaseUrl` - базовый URL для профилей
- `BruteForce:MinLength` - минимальная длина username
- `BruteForce:MaxLength` - максимальная длина username
- `BruteForce:MaxConcurrentRequests` - количество параллельных запросов
- `BruteForce:MaxRetries` - максимальное количество повторов при ошибке
- `BruteForce:Chars` - символы для генерации username
- `BruteForce:EnableRetry` - включить автоматические повторы (по умолчанию: `true`)
- `BruteForce:EnableTrafficMeasuring` - включить измерение трафика (по умолчанию: `true`)

### CompanyListScraper
- `Companies:Enabled` - включить/отключить скрапер (по умолчанию: `false`)
- `Companies:ListUrl` - URL списка компаний
- `Companies:BaseUrl` - базовый URL для страниц компаний
- `Companies:LinkSelector` - CSS селектор для поиска ссылок на компании
- `Companies:HrefRegex` - регулярное выражение для извлечения кода компании из href
- `Companies:NextPageSelector` - CSS селектор для поиска следующей страницы (поддерживает {0} для номера страницы)
- `Companies:OutputMode` - режим вывода: `ConsoleOnly`, `FileOnly`, `Both` (по умолчанию: `ConsoleOnly`)
- `Companies:EnableTrafficMeasuring` - включить измерение трафика (по умолчанию: `true`)

#### Логика обхода CompanyListScraper

Скрапер выполняется раз в неделю и последовательно обходит компании по следующим фильтрам:

1. **Базовый обход** - без фильтров: `https://career.habr.com/companies`
2. **По размеру компании** (sz=1 до sz=5):
   - `?sz=1`, `?sz=2`, `?sz=3`, `?sz=4`, `?sz=5`
3. **По категориям** - загружает актуальный список из таблицы `habr_category_root_ids`:
   - `?category_root_id=258822`, и т.д.
   - ⚠️ Список категорий загружается из БД **перед каждым еженедельным запуском**
4. **По дополнительным фильтрам**:
   - `?with_vacancies=1` - компании с вакансиями
   - `?with_ratings=1` - компании с рейтингами
   - `?with_habr_url=1` - компании с профилем на Habr
   - `?has_accreditation=1` - аккредитованные компании

Каждый фильтр обходится полностью (все страницы) перед переходом к следующему.

### CompanyFollowersScraper
- `CompanyFollowers:Enabled` - включить/отключить скрапер (по умолчанию: `true`)
- `CompanyFollowers:TimeoutSeconds` - таймаут HTTP-запроса в секундах (по умолчанию: `300` = 5 минут)
- `CompanyFollowers:UrlTemplate` - шаблон URL для страницы подписчиков (поддерживает {0} для кода компании)
- `CompanyFollowers:UserItemSelector` - CSS селектор для блока пользователя (`.user_friends_item`)
- `CompanyFollowers:UsernameSelector` - CSS селектор для имени пользователя (`.username`)
- `CompanyFollowers:SloganSelector` - CSS селектор для слогана/специализации (`.specialization`)
- `CompanyFollowers:NextPageSelector` - CSS селектор для поиска следующей страницы
- `CompanyFollowers:OutputMode` - режим вывода: `ConsoleOnly`, `FileOnly`, `Both`
- `CompanyFollowers:EnableTrafficMeasuring` - включить измерение трафика (по умолчанию: `true`)

#### Логика обхода CompanyFollowersScraper

Скрапер выполняется раз в неделю и обходит подписчиков всех компаний из БД:

1. **Загрузка списка компаний** - получает все `company_code` из таблицы `habr_companies`
2. **Параллельная обработка** - использует `AdaptiveConcurrencyController`:
   - Автоматически регулирует количество одновременных запросов
   - Адаптируется к скорости соединения
   - Отчёты о прогрессе и времени обработки
3. **Обход подписчиков** - для каждой компании:
   - Открывает `https://career.habr.com/companies/{code}/followers`
   - Обходит все страницы пагинации (`?page=1`, `?page=2`, и т.д.)
   - Извлекает для каждого пользователя:
     - **username** - текст из `.username`
     - **ссылка** - атрибут `href` из `a` (преобразуется в полный URL)
     - **slogan** - текст из `.specialization` (опционально)
4. **Сохранение в БД** - записывает в таблицу `habr_resumes` с полями `link`, `title` (username), `slogan`
   - Использует режим `UpdateIfExists` для обновления существующих записей

### CategoryScraper
- `Category:Enabled` - включить/отключить скрапер (по умолчанию: `false`)
- `Category:EnableTrafficMeasuring` - включить измерение трафика (по умолчанию: `true`)
- Использует те же настройки `Companies:*` для доступа к странице
- Собирает все значения из `<select id="category_root_id">` и сохраняет в таблицу `habr_category_root_ids`

### ResumeListPageScraper
- `ResumeList:Enabled` - включить/отключить скрапер (по умолчанию: `false`)
- `ResumeList:EnableTrafficMeasuring` - включить измерение трафика (по умолчанию: `true`)

### Logging
- `Logging:OutputDirectory` - директория для лог-файлов (по умолчанию: `./logs`)

### Database
- `Database:ConnectionString` - строка подключения к PostgreSQL

## База данных

Перед запуском необходимо создать таблицы:

```bash
# Таблица для компаний
psql -U postgres -d jobs -f sql/create_companies_table.sql

# Таблица для категорий
psql -U postgres -d jobs -f sql/create_category_root_ids_table.sql

# Индексы для резюме
psql -U postgres -d jobs -f sql/create_index.sql
```

### Структура таблиц

**habr_companies**
- `company_code` - уникальный код компании
- `company_url` - полный URL компании
- `created_at`, `updated_at` - временные метки

**habr_category_root_ids**
- `category_id` - уникальный идентификатор категории
- `category_name` - название категории
- `created_at`, `updated_at` - временные метки

Подробнее см. [sql/README.md](../sql/README.md)

## SmartHttpClient - Умная обёртка над HttpClient

`SmartHttpClient` - это универсальная обёртка, которая добавляет к стандартному `HttpClient` две ключевые возможности:

### Возможности

1. **Автоматические повторы (Retry)**
   - Экспоненциальная задержка между попытками
   - Обработка транзиентных ошибок (408, 429, 500, 502, 503, 504)
   - Учёт заголовка `Retry-After`
   - Настраиваемое количество попыток и задержек

2. **Измерение трафика**
   - Автоматический подсчёт размера каждого HTTP-ответа
   - Статистика по каждому скраперу отдельно
   - Общая статистика по всем скраперам
   - Сохранение в файл с настраиваемым интервалом

### Настройка через конфигурацию

Каждый скрапер имеет индивидуальные настройки:

```xml
<!-- BruteForceUsernameScraper: повторы + измерение трафика -->
<add key="BruteForce:EnableRetry" value="true" />
<add key="BruteForce:EnableTrafficMeasuring" value="true" />

<!-- Остальные скраперы: только измерение трафика -->
<add key="Companies:EnableTrafficMeasuring" value="true" />
<add key="CompanyFollowers:EnableTrafficMeasuring" value="true" />
<add key="ResumeList:EnableTrafficMeasuring" value="true" />
<add key="Category:EnableTrafficMeasuring" value="true" />

<!-- Настройки статистики трафика -->
<add key="Traffic:OutputFile" value="./logs/traffic_stats.txt" />
<add key="Traffic:SaveIntervalMinutes" value="5" />
```

### Пример использования

```csharp
// Создание SmartHttpClient для скрапера с повторами и измерением трафика
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "BruteForceUsernameScraper",
    trafficStats: trafficStats,
    enableRetry: true,
    enableTrafficMeasuring: true,
    maxRetries: 200,
    baseDelay: TimeSpan.FromMilliseconds(400),
    maxDelay: TimeSpan.FromSeconds(30)
);

// Использование в скрапере
var result = await smartClient.FetchAsync(url, infoLog: Console.WriteLine);
```

### Статистика трафика

Статистика автоматически сохраняется в файл (по умолчанию каждые 5 минут):

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

  CompanyListScraper:
    Requests:     234
    Total:        8.2 MB
    Avg/Request:  35.04 KB
...
```

## Логирование

Каждый процесс может иметь свой режим вывода:

1. **ConsoleOnly** - вывод только в консоль (по умолчанию)
2. **FileOnly** - вывод только в файл
3. **Both** - вывод одновременно в консоль и файл

Лог-файлы создаются в формате: `{ProcessName}_{yyyyMMdd_HHmmss}.log`

Пример: `CompanyListScraper_20251029_213000.log`

## Сборка и запуск

### Запуск через .NET CLI
```bash
dotnet run --project JobBoardScraper
```

### Сборка для продакшена
```bash
# Release сборка
dotnet build JobBoardScraper/JobBoardScraper.csproj -c Release

# Публикация самодостаточного приложения (Windows)
dotnet publish JobBoardScraper/JobBoardScraper.csproj -c Release -r win-x64 --self-contained true -o ./publish

# Однофайловая публикация
dotnet publish JobBoardScraper/JobBoardScraper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

Подробнее см. [BUILD_README.md](BUILD_README.md)

## Требования

- .NET 9.0 SDK
- PostgreSQL 12+
