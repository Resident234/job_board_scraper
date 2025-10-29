# JobBoardScraper

Приложение для скрапинга данных с career.habr.com.

## Архитектура

Приложение запускает три параллельных процесса:

1. **BruteForceUsernameScraper** - перебор всех возможных имен пользователей (a-z, 0-9, -, _)
2. **ResumeListPageScraper** - периодический обход страницы со списком резюме (каждые 10 минут)
3. **CompanyListScraper** - периодический обход списка компаний (раз в неделю)

## Конфигурация

Все настройки находятся в файле `App.config`:

### BruteForceUsernameScraper
- `BruteForce:BaseUrl` - базовый URL для профилей
- `BruteForce:MinLength` - минимальная длина username
- `BruteForce:MaxLength` - максимальная длина username
- `BruteForce:MaxConcurrentRequests` - количество параллельных запросов
- `BruteForce:MaxRetries` - максимальное количество повторов при ошибке
- `BruteForce:Chars` - символы для генерации username

### CompanyListScraper
- `Companies:ListUrl` - URL списка компаний
- `Companies:BaseUrl` - базовый URL для страниц компаний
- `Companies:LinkSelector` - CSS селектор для поиска ссылок на компании
- `Companies:HrefRegex` - регулярное выражение для извлечения кода компании из href
- `Companies:NextPageSelector` - CSS селектор для поиска следующей страницы (поддерживает {0} для номера страницы)
- `Companies:OutputMode` - режим вывода: `ConsoleOnly`, `FileOnly`, `Both` (по умолчанию: `ConsoleOnly`)

### Logging
- `Logging:OutputDirectory` - директория для лог-файлов (по умолчанию: `./logs`)

### Database
- `Database:ConnectionString` - строка подключения к PostgreSQL

## База данных

Перед запуском необходимо создать таблицы:

```bash
psql -U postgres -d jobs -f sql/create_companies_table.sql
```

## Логирование

Каждый процесс может иметь свой режим вывода:

1. **ConsoleOnly** - вывод только в консоль (по умолчанию)
2. **FileOnly** - вывод только в файл
3. **Both** - вывод одновременно в консоль и файл

Лог-файлы создаются в формате: `{ProcessName}_{yyyyMMdd_HHmmss}.log`

Пример: `CompanyListScraper_20251029_213000.log`

## Запуск

```bash
dotnet run --project JobBoardScraper
```

## Требования

- .NET 9.0 SDK
- PostgreSQL 12+
