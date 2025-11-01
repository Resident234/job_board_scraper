# JobBoardScraper

Приложение для скрапинга данных с career.habr.com.

## Архитектура

Приложение запускает четыре параллельных процесса:

1. **BruteForceUsernameScraper** - перебор всех возможных имен пользователей (a-z, 0-9, -, _)
2. **ResumeListPageScraper** - периодический обход страницы со списком резюме (каждые 10 минут)
3. **CompanyListScraper** - периодический обход списка компаний (раз в неделю)
4. **CategoryScraper** - периодический сбор category_root_id из select элемента (раз в неделю)

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

### CategoryScraper
- Использует те же настройки `Companies:*` для доступа к странице
- Собирает все значения из `<select id="category_root_id">` и сохраняет в таблицу `habr_category_root_ids`

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
