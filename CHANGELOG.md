# Changelog

## 2025-10-29 - Logging System Implementation

### Добавлено
- **Helper.ConsoleLogger** - система управления выводом в консоль и файлы
  - `OutputMode` enum с тремя режимами: ConsoleOnly, FileOnly, Both
  - `ConsoleLogger` класс для управления выводом процессов
  - `DualWriter` для одновременной записи в консоль и файл
  
- **CompanyListScraper** - новый скрапер для обхода списка компаний
  - Периодический обход (раз в неделю)
  - Извлечение кодов компаний из career.habr.com/companies
  - Сохранение в таблицу `habr_companies`
  - Интеграция с ConsoleLogger (режим Both по умолчанию)

- **Конфигурация через App.config**
  - Все настройки вынесены в XML конфигурацию
  - CSS селекторы и regex паттерны настраиваются без перекомпиляции
  - Режимы вывода для каждого процесса

### Изменено
- **AppConfig.cs** - переход с констант на ConfigurationManager
  - Чтение настроек из App.config
  - Значения по умолчанию для всех параметров
  
- **DatabaseClient.cs** - поддержка разных типов записей
  - Универсальная очередь через `DbRecord` struct
  - Методы `EnqueueResume()` и `EnqueueCompany()`
  - Обработка разных типов в фоновой задаче

- **Структура проекта**
  - Переименование `job_board_scraper` → `JobBoardScraper`
  - SQL скрипты перемещены в папку `sql/`
  - Добавлена папка `Helper/` для утилит

### SQL
- `sql/create_companies_table.sql` - таблица `habr_companies`
- Индексы на `company_code` и `created_at`

### Документация
- `JobBoardScraper/README.md` - основная документация
- `JobBoardScraper/Helper/README.md` - документация по логированию
- `sql/README.md` - документация по SQL скриптам

## Архитектура

### Три параллельных процесса:
1. **BruteForceUsernameScraper** - перебор username (a-z, 0-9, -, _)
2. **ResumeListPageScraper** - обход /resumes?order=last_visited (каждые 10 минут)
3. **CompanyListScraper** - обход /companies (раз в неделю)

### Общая инфраструктура:
- HttpClient с retry механизмом
- AdaptiveConcurrencyController для динамической регулировки нагрузки
- DatabaseClient с асинхронной очередью записи
- ConsoleLogger для управления выводом
