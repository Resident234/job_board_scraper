# Changelog

Все значимые изменения в проекте JobBoardScraper документируются в этом файле.

## [2.4.0] - 2024-12-11

### Добавлено

#### Парсинг участия в профсообществах (Community Participation)
- Извлечение блока "Участие в профсообществах" из профилей резюме (Хабр, GitHub и др.)
- Новая модель данных:
  - `CommunityParticipationData` - данные об участии (Name, MemberSince, Contribution, Topics)
- Новые методы в `ProfileDataExtractor`:
  - `ExtractCommunityParticipationData()` - извлечение данных об участии в сообществах
  - `ExtractSingleCommunityParticipationItem()` - парсинг одного элемента
- Новые методы в `DatabaseClient`:
  - `DatabaseUpdateUserCommunityParticipation()` - сохранение данных в JSONB поле
- Новое поле в таблице `habr_resumes`:
  - `community_participation` (JSONB) - массив объектов с данными об участии
- CSS-селекторы для участия в профсообществах в `AppConfig`
- Property-based тесты (FsCheck) для валидации JSON сериализации

#### SQL миграции
- `sql/alter_resumes_add_community_participation.sql` - добавление поля community_participation

### Изменено

#### UserResumeDetailScraper
- Интеграция извлечения участия в профсообществах в основной процесс обхода
- Автоматическое сохранение данных в JSONB поле
- Логирование количества записей участия в сообществах

#### DatabaseClient
- Добавлен новый тип `DbRecordType.UserCommunityParticipation`
- Расширена структура `DbRecord` полем `CommunityParticipation`
- Обновлён метод `EnqueueUserResumeDetail()` с параметром `communityParticipation`

---

## [2.3.0] - 2024-12-10

### Добавлено

#### Парсинг высшего образования (University Education Scraper)
- Извлечение блока "Высшее образование" из профилей резюме
- Новые модели данных:
  - `UniversityData` - данные университета (HabrId, Name, City, GraduateCount)
  - `CourseData` - данные курса с JSON-сериализацией
  - `UserUniversityData` - связь пользователь-университет
  - `UniversityEducationData` - комбинированная модель
- Новые методы в `ProfileDataExtractor`:
  - `ExtractEducationData()` - извлечение данных об образовании
  - `ParseCoursePeriod()` - парсинг периода курса
  - `ExtractUniversityIdFromUrl()` - извлечение ID из URL
- Новые методы в `DatabaseClient`:
  - `EnqueueUniversity()` / `FlushUniversityQueue()` - очередь университетов
  - `EnqueueUserUniversity()` / `FlushUserUniversityQueue()` - очередь связей
- Новые таблицы БД:
  - `habr_universities` - справочник университетов
  - `habr_resumes_universities` - связь резюме с университетами (курсы в JSONB)
- CSS-селекторы для образования в `AppConfig`
- Property-based тесты (FsCheck) для валидации

#### Документация
- [UNIVERSITY_EDUCATION_SCRAPER.md](docs/UNIVERSITY_EDUCATION_SCRAPER.md) - полная документация
- Обновлён [USER_RESUME_DETAIL_SCRAPER.md](docs/USER_RESUME_DETAIL_SCRAPER.md)

#### SQL миграции
- `sql/create_universities_table.sql` - таблица университетов
- `sql/create_resumes_universities_table.sql` - связь резюме-университеты

### Изменено

#### UserResumeDetailScraper
- Интеграция извлечения образования в основной процесс обхода
- Автоматическое сохранение университетов и связей
- Логирование количества записей образования

---

## [2.2.0] - 2024-12-03

### Добавлено

#### ExponentialBackoff - умная стратегия повторов
- Класс `ExponentialBackoff` в `Helper.Utils/ExponentialBackoff.cs`
- Алгоритм Exponential Backoff with Jitter для HTTP ошибок
- Специализированные методы для разных типов ошибок:
  - `CalculateServerErrorDelay()` - для ошибок сервера (5xx): baseDelay=2с, maxDelay=60с
  - `CalculateProxyErrorDelay()` - для ошибок прокси/сети: baseDelay=0.5с, maxDelay=10с
- Метод `GetDelayDescription()` для форматированного вывода задержки
- Потокобезопасная реализация с использованием `lock`

#### Поле job_search_status
- Новое поле `job_search_status` в таблице `habr_resumes`
- SQL миграция `sql/alter_resumes_add_job_search_status.sql`
- Поддержка значений: "Ищу работу", "Не ищу работу", "Рассматриваю предложения"
- Индекс для быстрого поиска по статусу

#### Документация
- [BACKOFF_ALGORITHMS.md](docs/BACKOFF_ALGORITHMS.md) - полное описание алгоритмов задержки
- [HTTP_ERROR_RETRY_STRATEGY.md](HTTP_ERROR_RETRY_STRATEGY.md) - стратегия повторов для HTTP ошибок
- [REFACTORING_PROFILE_EXTRACTOR.md](REFACTORING_PROFILE_EXTRACTOR.md) - рефакторинг извлечения данных
- [SAVE_EXTRACTED_DATA_TO_DB.md](SAVE_EXTRACTED_DATA_TO_DB.md) - сохранение данных в БД
- [JOB_SEARCH_STATUS_FIELD_SUMMARY.md](JOB_SEARCH_STATUS_FIELD_SUMMARY.md) - поле статуса поиска работы

### Изменено

#### Рефакторинг ProfileDataExtractor
- Перенос кода извлечения данных из `ResumeListPageScraper` в `ProfileDataExtractor`
- Новые методы:
  - `ExtractNameInfoTechAndLevel()` - имя, должности и уровень из списка резюме
  - `ExtractSalaryFromSection()` - зарплата из секции профиля
  - `ExtractJobSearchStatusFromSection()` - статус поиска работы
- Устранено дублирование кода между скраперами

#### Улучшение UserResumeDetailScraper
- Интеграция ExponentialBackoff для HTTP ошибок
- Разные параметры задержки для server errors и proxy errors
- Улучшенное логирование с отображением задержки

#### Обновление DatabaseClient
- Метод `EnqueueUserResumeDetail` принимает параметр `jobSearchStatus`
- Метод `DatabaseInsert` поддерживает поле `job_search_status`
- Обновлены SQL запросы INSERT и UPDATE

#### Исправление ParallelScraperLogger
- Удалены дублирующиеся префиксы имени класса в логах

### Исправлено

- Дублирование кода извлечения данных между скраперами
- Фиксированная задержка при HTTP ошибках (заменена на экспоненциальную)
- Дублирование имени класса в логах ParallelScraperLogger

## [2.1.0] - 2024-11-28

### Добавлено

#### ProxyRotator - система ротации прокси-серверов
- Автоматическая ротация прокси-серверов для распределения нагрузки
- Поддержка множественных прокси с циклическим переключением
- Поддержка различных типов прокси: HTTP, HTTPS, SOCKS5
- Поддержка аутентификации (username:password)
- Ручная и автоматическая ротация
- Опциональное использование (можно отключить для конкретных скраперов)
- Настройки в `App.config`:
  - `Proxy:Enabled` - включить/выключить прокси
  - `Proxy:List` - список прокси-серверов (через ; или ,)
  - `Proxy:RotationIntervalSeconds` - интервал автоматической ротации
  - `Proxy:AutoRotate` - автоматическая ротация при каждом запросе

#### HttpClientFactory - фабрика для создания HttpClient
- Метод `CreateHttpClient()` - создание HttpClient с опциональной поддержкой прокси
- Метод `CreateProxyRotator()` - создание ProxyRotator из конфигурации
- Метод `CreateDefaultClient()` - создание HttpClient по умолчанию (обратная совместимость)
- Автоматическая настройка compression (GZip, Deflate)

#### Расширение SmartHttpClient
- Поддержка ProxyRotator в конструкторе
- Метод `GetProxyStatus()` - получение информации о текущем прокси
- Метод `RotateProxy()` - ручная ротация прокси
- Полная обратная совместимость (прокси опциональны)

#### ProxyProvider - динамическое получение прокси
- Автоматическая загрузка прокси из публичных источников
- Поддержка ProxyScrape API
- Поддержка GeoNode API
- Загрузка/сохранение из файла
- Проверка работоспособности прокси
- Автоматическое удаление нерабочих прокси

#### DynamicProxyRotator - автоматическое обновление
- Автоматическое обновление списка прокси по расписанию
- Принудительное обновление по команде
- Интеграция с ProxyProvider
- Настраиваемый интервал обновления

#### Документация по прокси
- [PROXY_ROTATION.md](docs/PROXY_ROTATION.md) - полная документация
- [PROXY_USAGE_EXAMPLE.md](docs/PROXY_USAGE_EXAMPLE.md) - примеры использования
- [PROXY_QUICKSTART.md](docs/PROXY_QUICKSTART.md) - быстрый старт
- [PROXY_CONFIG_EXAMPLES.md](docs/PROXY_CONFIG_EXAMPLES.md) - примеры конфигураций
- [DYNAMIC_PROXY.md](docs/DYNAMIC_PROXY.md) - динамическое обновление прокси
- [PROXY_SERVICES.md](docs/PROXY_SERVICES.md) - коммерческие прокси-сервисы
- [FREE_PROXY_SOURCES.md](docs/FREE_PROXY_SOURCES.md) - бесплатные источники прокси

## [2.0.0] - 2024-11-04

### Добавлено

#### ExpertsScraper - новый скрапер для обхода экспертов
- Обход страниц экспертов с `https://career.habr.com/experts`
- Извлечение данных эксперта:
  - Имя и ссылка на профиль
  - Код пользователя из URL
  - Стаж работы (например, "9 лет и 9 месяцев")
  - Флаг эксперта (`expert = true`)
- Извлечение компаний из карточек экспертов
- Автоматическая пагинация
- Режим `UpdateIfExists` для обновления существующих записей
- Настраиваемый интервал обхода (по умолчанию: 4 дня)
- Поддержка всех режимов вывода (`ConsoleOnly`, `FileOnly`, `Both`)

#### Расширение структуры базы данных
- Новый столбец `code` - код пользователя из URL профиля
- Новый столбец `expert` - флаг эксперта (boolean)
- Новый столбец `work_experience` - стаж работы (text)
- Индексы для быстрого поиска по `code` и `expert`
- SQL-скрипт `add_expert_columns.sql` для миграции

#### SmartHttpClient - универсальная обёртка над HttpClient
- Объединяет функциональность `HttpRetry` и `TrafficMeasuringHttpClient`
- Автоматические повторы с экспоненциальной задержкой
- Измерение HTTP-трафика для каждого скрапера
- Настраиваемый timeout для каждого скрапера
- Поддержка заголовка `Retry-After`
- Индивидуальные настройки для каждого скрапера

#### Система измерения HTTP-трафика
- Класс `TrafficStatistics` для сбора статистики
- Автоматический подсчёт размера каждого HTTP-ответа
- Статистика по каждому скраперу отдельно
- Общая статистика по всем скраперам
- Сохранение в файл с настраиваемым интервалом (по умолчанию: 5 минут)
- Настройки в `App.config`:
  - `Traffic:OutputFile` - путь к файлу статистики
  - `Traffic:SaveIntervalMinutes` - интервал сохранения

#### Управление скраперами через конфигурацию
- Каждый скрапер можно включить/отключить через `App.config`
- Настройки `{Scraper}:Enabled` для всех скраперов:
  - `BruteForce:Enabled`
  - `ResumeList:Enabled`
  - `Companies:Enabled`
  - `Category:Enabled`
  - `CompanyFollowers:Enabled`
  - `Experts:Enabled`
- Вывод статуса каждого скрапера при запуске

#### Независимое логирование для каждого скрапера
- Класс `ConsoleLogger` для управления выводом
- Три режима вывода: `ConsoleOnly`, `FileOnly`, `Both`
- Отдельные лог-файлы для каждого скрапера
- Формат имени файла: `{ScraperName}_{timestamp}.log`
- Настройка режима через `App.config`:
  - `Companies:OutputMode`
  - `CompanyFollowers:OutputMode`
  - `Experts:OutputMode`

#### Документация
- `MIGRATION_GUIDE.md` - руководство по миграции с версии 1.x
- `QUICKSTART.md` - быстрый старт для новых пользователей
- `CHANGELOG.md` - история изменений
- Обновлён `README.md` с описанием ExpertsScraper
- Обновлён `sql/README.md` с примерами запросов для экспертов
- `docs/TRAFFIC_OPTIMIZATION.md` - оптимизация трафика

### Изменено

#### Рефакторинг системы логирования
- Удалён глобальный `Console.SetOut()`
- Каждый скрапер использует свой экземпляр `ConsoleLogger`
- Логи больше не смешиваются между скраперами
- Улучшена читаемость логов

#### Рефакторинг HTTP-клиентов
- Удалён класс `HttpRetry` (заменён на `SmartHttpClient`)
- Удалён класс `TrafficMeasuringHttpClient` (заменён на `SmartHttpClient`)
- Удалён класс `DualWriter` (заменён на `ConsoleLogger`)
- Все скраперы используют `SmartHttpClient`

#### Улучшение DatabaseClient
- Добавлена поддержка новых полей (`code`, `expert`, `work_experience`)
- Метод `EnqueueResume` принимает дополнительные параметры
- Метод `DatabaseInsert` поддерживает новые поля
- Улучшена обработка режима `UpdateIfExists`

#### Обновление Program.cs
- Инициализация `TrafficStatistics`
- Создание `SmartHttpClient` для каждого скрапера
- Вывод статуса каждого скрапера при запуске
- Добавлен ExpertsScraper в список процессов

### Удалено

- Класс `HttpRetry` (заменён на `SmartHttpClient`)
- Класс `TrafficMeasuringHttpClient` (заменён на `SmartHttpClient`)
- Класс `DualWriter` (заменён на `ConsoleLogger`)
- Глобальное перенаправление `Console.SetOut()`

### Исправлено

- Проблема смешанных логов от разных скраперов
- Дублирование кода HTTP-запросов
- Отсутствие контроля трафика
- Невозможность отключить отдельные скраперы
- Проблемы с кодировкой HTML (правильное определение из заголовков)

## [1.0.0] - 2024-10-29

### Добавлено

- Первоначальный релиз
- BruteForceUsernameScraper - перебор имён пользователей
- ResumeListPageScraper - обход списка резюме
- CompanyListScraper - обход списка компаний
- CategoryScraper - сбор категорий
- CompanyFollowersScraper - обход подписчиков компаний
- AdaptiveConcurrencyController - адаптивное управление параллелизмом
- DatabaseClient - работа с PostgreSQL
- Базовая система логирования
- Конфигурация через App.config

---

## Формат

Этот changelog следует принципам [Keep a Changelog](https://keepachangelog.com/ru/1.0.0/),
и проект придерживается [Semantic Versioning](https://semver.org/lang/ru/).

### Типы изменений

- **Добавлено** - для новой функциональности
- **Изменено** - для изменений в существующей функциональности
- **Устарело** - для функциональности, которая скоро будет удалена
- **Удалено** - для удалённой функциональности
- **Исправлено** - для исправления багов
- **Безопасность** - для изменений, связанных с безопасностью
