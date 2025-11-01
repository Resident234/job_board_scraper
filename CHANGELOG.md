# Changelog

## 2025-11-01 - Company Followers Scraper

### Добавлено
- **CompanyFollowersScraper** - новый скрапер для обхода подписчиков компаний
  - Загружает список компаний из БД
  - Обходит страницы `/companies/{code}/followers` с пагинацией
  - Извлекает username, ссылку и слоган пользователей
  - Интеграция с ConsoleLogger
  
- **Поддержка слогана в DatabaseClient**
  - Добавлен параметр `slogan` в `DatabaseInsert()`
  - Обновлён `EnqueueResume()` для поддержки слогана
  - Расширен `DbRecord` с полем `TertiaryValue`
  
- **SQL скрипт add_slogan_column.sql**
  - Добавление столбца `slogan TEXT` в таблицу `habr_resumes`
  
- **Метод GetAllCompanyCodes() в DatabaseClient**
  - Получение списка всех company_code из БД
  
- **Настройки в App.config**
  - `CompanyFollowers:UrlTemplate`
  - `CompanyFollowers:UserItemSelector`
  - `CompanyFollowers:UsernameSelector`
  - `CompanyFollowers:SloganSelector`
  - `CompanyFollowers:NextPageSelector`
  - `CompanyFollowers:OutputMode`

## 2025-11-01 - Logging Architecture Refactoring

### Изменено
- **Helper.ConsoleHelper** - рефакторинг системы логирования
  - Вынесено в отдельный namespace `JobBoardScraper.Helper.ConsoleHelper`
  - Папка переименована в `Helper.ConsoleHelper/` (аналогично AliExpressScraper)
  - Убрано глобальное перенаправление `Console.SetOut()`
  - Каждый логгер теперь пишет в свой собственный файл
  - Добавлена потокобезопасность через `lock`
  - Удалён класс `DualWriter` (больше не нужен)
  - Обновлена документация с описанием архитектуры

## 2025-10-29 - Logging System Implementation

### Добавлено
- **Helper.ConsoleHelper** - система управления выводом в консоль и файлы
  - `OutputMode` enum с тремя режимами: ConsoleOnly, FileOnly, Both
  - `ConsoleLogger` класс для управления выводом процессов
  
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
  - Добавлена папка `Helper.ConsoleHelper/` для утилит логирования

### SQL
- `sql/create_companies_table.sql` - таблица `habr_companies`
- Индексы на `company_code` и `created_at`

### Документация
- `JobBoardScraper/README.md` - основная документация
- `JobBoardScraper/Helper.ConsoleHelper/README.md` - документация по логированию
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
