# JobBoardScraper

JobBoardScraper - .NET 9 консольное приложение для сбора данных с career.habr.com. Проект умеет обходить резюме, профили пользователей, компании, рейтинги компаний, списки экспертов, категории и связанные страницы, сохранять результат в PostgreSQL и работать через прокси с ретраями, лимитами и статистикой.

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-12+-336791)](https://www.postgresql.org/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE.md)

## 📚 Документация

- **[Coding Standards](docs/CODING_STANDARDS.md)** - Руководство по форматированию кода и соглашениям о наименовании
- **[Architecture](docs/ARCHITECTURE.md)** - Общее описание архитектуры проекта
- **[Configuration](docs/CONFIGURATION.md)** - Настройка параметров и конфигурации
- **[Quickstart](docs/QUICKSTART.md)** - Быстрый старт и основные команды
- **[DB Schema](docs/DB_SCHEMA.md)** - ER-диаграмма базы данных (Mermaid)

Основные документы:

- [STATISTICS.md](docs/STATISTICS.md) - система статистики и мониторинга операций с базой данных.
- [ARCHITECTURE.md](docs/ARCHITECTURE.md) - архитектура.
- [QUICKSTART.md](docs/QUICKSTART.md) - быстрый старт.
- [CONFIGURATION.md](docs/CONFIGURATION.md) - конфигурация.
- [BUILD.md](docs/BUILD.md) - сборка и публикация.
- [EXAMPLES.md](docs/EXAMPLES.md) - практические примеры.
- [TESTING_AND_DEPLOYMENT.md](docs/TESTING_AND_DEPLOYMENT.md) - тестирование и деплой.
- [DB_SCHEMA.md](docs/DB_SCHEMA.md) - модель данных PostgreSQL (ER-диаграмма в формате Mermaid).

Модули:

- [USER_RESUME_DETAIL_SCRAPER.md](docs/USER_RESUME_DETAIL_SCRAPER.md)
- [USER_PROFILE_SCRAPER.md](docs/USER_PROFILE_SCRAPER.md)
- [UNIVERSITY_EDUCATION_SCRAPER.md](docs/UNIVERSITY_EDUCATION_SCRAPER.md)
- [COMPANY_DETAIL_SCRAPER.md](docs/COMPANY_DETAIL_SCRAPER.md)
- [COMPANY_RATING_SCRAPER.md](docs/COMPANY_RATING_SCRAPER.md)
- [EMPTY_PROFILE.md](docs/EMPTY_PROFILE.md)

Инфраструктура:

- [HTTP_ERROR_RETRY_STRATEGY.md](docs/HTTP_ERROR_RETRY_STRATEGY.md)
- [BACKOFF_ALGORITHMS.md](docs/BACKOFF_ALGORITHMS.md)
- [TRAFFIC_OPTIMIZATION.md](docs/TRAFFIC_OPTIMIZATION.md)
- [DYNAMIC_PROXY.md](docs/DYNAMIC_PROXY.md)
- [USERRESUME_WITH_PROXY.md](docs/USERRESUME_WITH_PROXY.md)
- [PROGRESS_TRACKING.md](docs/PROGRESS_TRACKING.md) - потокобезопасное отслеживание и логирование прогресса скрапинга.

## Реализованная Функциональность

| Модуль | Назначение | Основные артефакты |
| --- | --- | --- |
| **Скраперы** | | |
| `ResumeListPageScraper` | Сбор ссылок на резюме, навыков, уровней, зарплат и фильтров из списков резюме | `Scrapers/ResumeListPageScraper.cs` |
| `UserResumeDetailScraper` | Детальные данные резюме: о себе, навыки, опыт, образование, возраст, гражданство, удалёнка, профсообщества | `Scrapers/UserResumeDetailScraper.cs`, `Parsing/ProfileDataExtractor.cs` |
| `BruteForceUsernameScraper` | Перебор username по алфавиту для поиска новых профилей | `Scrapers/BruteForceUsernameScraper.cs` |
| `CompanyListScraper` | Сбор кодов компаний, company_id и названий из списка компаний | `Scrapers/CompanyListScraper.cs` |
| `CompanyDetailScraper` | Детальная информация о компании: описание, сайт, рейтинг, сотрудники, навыки, представители | `Scrapers/CompanyDetailScraper.cs` |
| `CompanyRatingScraper` | Рейтинги компаний, награды, средняя оценка, отзывы с dedup по hash | `Scrapers/CompanyRatingScraper.cs`, `Infrastructure/Utils/HashUtils.cs`, `sql/alter_companies_add_rating_fields.sql`, `sql/create_company_reviews_table.sql` |
| `CompanyFollowersScraper` | Подписчики компаний, username и слоган | `Scrapers/CompanyFollowersScraper.cs` |
| `ExpertsScraper` | Профили экспертов с флагом `expert = true` | `Scrapers/ExpertsScraper.cs` |
| `UserProfileScraper` | Публичные профили пользователей (имя, опыт, последний визит) | `Scrapers/UserProfileScraper.cs` |
| `UserFriendsScraper` | Списки друзей пользователей, пополнение очереди резюме | `Scrapers/UserFriendsScraper.cs` |
| `CategoryScraper` | Справочник корневых категорий для фильтрации резюме | `Scrapers/CategoryScraper.cs` |
| **Образование** | | |
| Высшее образование | Парсинг и нормализованное хранение ВУЗов, курсов, связей резюме-ВУЗ | `Parsing/ProfileDataExtractor.cs`, `sql/create_universities_table.sql`, `sql/create_resumes_universities_table.sql` |
| Дополнительное образование | Парсинг курсов, тренингов и периодов обучения | `sql/create_resumes_educations_table.sql` |
| **Прокси и отказоустойчивость** | | |
| SmartHttpClient | HTTP-клиент с retry, обработкой временных ошибок и сбором статистики трафика | `Infrastructure/Http/SmartHttpClient.cs`, `Infrastructure/Http/HttpClientFactory.cs` |
| Free proxy pool | Автоматический сбор бесплатных прокси из трёх источников (free-proxy-list, ProxyScrape, GeoNode) | `Infrastructure/Proxy/FreeProxyListScraper.cs`, `Infrastructure/Proxy/ProxyScrapeScraper.cs`, `Infrastructure/Proxy/ProxyCoordinator.cs` |
| Proxy whitelist | Whitelist рабочих прокси с cooldown при суточном лимите, retry и JSON-хранилище | `Infrastructure/Proxy/ProxyWhitelistManager.cs`, `Infrastructure/Proxy/JsonWhitelistStorage.cs`, `Infrastructure/Proxy/GeneralPoolManager.cs` |
| Proxy retry | Специализированные повторные попытки с прокси-специфичной стратегией | `Infrastructure/Proxy/ProxyRetryExecutor.cs` |
| Dynamic proxy | Создание HttpClient с динамической сменой прокси | `Infrastructure/Proxy/ProxyHttpClientFactory.cs` |
| **Статистика и логи** | | |
| Статистика скраперов | ScraperStatistics: обработано, успешно, ошибок, пропущено, время работы | `Infrastructure/Statistics/ScraperStatistics.cs` |
| Статистика трафика | TrafficStatistics: общий и per-scraper расход трафика с сохранением в файл | `Infrastructure/Statistics/TrafficStatistics.cs` |
| Статистика БД | DatabaseStatistics: вставки, обновления, ошибки записи | `Infrastructure/Statistics/DatabaseStatistics.cs` |
| Логирование | ConsoleLogger, ScraperLogger, ScraperProgressLogger, ScraperParallelLogger с режимом `OutputMode` | `Infrastructure/Logging/` |
| **Адаптивность** | | |
| AdaptiveConcurrencyController | Адаптивный контроль конкурентности запросов на основе успешности | `Core/AdaptiveConcurrencyController.cs` |
| LinearThrottle / ExponentialBackoff | Троттлинг и экспоненциальная задержка при ошибках | `Infrastructure/Throttling/` |
| **Архитектура** | | |
| Структура проекта | Namespace-ы: `Data/`, `Domain/Models/`, `Parsing/`, `Scrapers/`, `Infrastructure/*` | `Data/`, `Domain/Models/`, `Parsing/`, `Scrapers/`, `Infrastructure/*` |
| **Другое** | | |
| Empty Profile Detection | Логика определения пустых профилей и повторного обхода | `Scrapers/UserResumeDetailScraper.cs`, `sql/alter_resumes_add_empty_profile_field.sql` |
| Progress Tracking | Потокобезопасное отслеживание прогресса параллельных скраперов | `Infrastructure/Logging/ScraperProgressLogger.cs`, `Infrastructure/Logging/ScraperParallelLogger.cs` |

## Возможности

### Сбор данных

Приложение включает 11 скраперов, каждый из которых отвечает за свой источник данных. Полное описание всех скраперов с деталями реализации и интервалами обхода — в разделе [Скраперы приложения](#скраперы-приложения).

Краткий перечень:

- **ResumeListPageScraper** — сбор ссылок на резюме и фильтров из списков.
- **UserResumeDetailScraper** — детальные данные резюме (о себе, навыки, опыт, образование и др.).
- **CompanyListScraper** + **CompanyDetailScraper** — компании и их детальная информация.
- **CompanyRatingScraper** — рейтинги компаний, награды и отзывы.
- **CategoryScraper** — справочник корневых категорий.
- **CompanyFollowersScraper** — подписчики компаний.
- **ExpertsScraper** — профили экспертов.
- **UserProfileScraper** — публичные профили пользователей.
- **UserFriendsScraper** — списки друзей пользователей.
- **BruteForceUsernameScraper** — перебор имён пользователей.

### Хранение данных

Данные сохраняются в PostgreSQL через `Data/DatabaseClient.cs`. Запись идет через очереди и фоновые writer-задачи, чтобы скраперы не блокировались на каждой операции БД.

Все таблицы базы данных:

- `habr_resumes` — резюме и профильные поля (основная таблица пользователей).
- `habr_companies` — компании и агрегированные поля.
- `habr_company_reviews` — отзывы о компаниях с уникальным `review_hash`.
- `habr_universities` — справочник ВУЗов career.habr.com.
- `habr_resumes_universities` — связь резюме с ВУЗами, курсами и периодами обучения.
- `habr_resumes_educations` — дополнительное образование (курсы, тренинги).
- `habr_user_experience` — опыт работы пользователя.
- `habr_user_experience_skills` — навыки, связанные с конкретной записью опыта.
- `habr_user_skills` — навыки пользователя.
- `habr_skills` — справочник навыков.
- `habr_company_skills` — навыки, связанные с компанией.
- `habr_levels` — уровни специалистов.
- `habr_category_root_ids` — корневые категории для фильтрации резюме.

### Прокси и отказоустойчивость

- `SmartHttpClient` добавляет retry, обработку временных HTTP-ошибок и сбор статистики трафика.
- `FreeProxyListScraper` и `ProxyScrapeScraper` пополняют общий пул бесплатных прокси.
- `ProxyCoordinator` выбирает источник прокси: сначала whitelist, затем общий пул.
- `ProxyWhitelistManager` сохраняет рабочие прокси в `./data/proxy_whitelist.json`, учитывает 24-часовой cooldown после суточного лимита career.habr.com и удаляет нерабочие прокси после настраиваемого числа ошибок.
- `UserResumeDetailScraper` распознает текст лимита `Вы исчерпали суточный лимит на просмотр профилей специалистов` и сообщает координатору прокси о необходимости переключения.
- `ProxyRetryExecutor` обрабатывает повторные попытки с прокси-специфичной стратегией.
- `ProxyHttpClientFactory` создает `HttpClient` с поддержкой динамической смены прокси.

### Статистика и логи

- Каждый основной скрапер использует `ScraperStatistics`: обработано, успешно, ошибок, пропущено, активные запросы и время работы.
- `TrafficStatistics` пишет общий и per-scraper расход трафика.
- `DatabaseStatistics` отслеживает операции записи в БД (вставки, обновления, ошибки).
- `ConsoleLogger`, `ScraperProgressLogger` и `ScraperParallelLogger` дают единый формат логов.
- Режим вывода задается через `OutputMode`: `ConsoleOnly`, `FileOnly`, `Both`.

## Архитектура и структура проекта

```text
JobBoardScraper/
  App.config                 # Runtime-конфигурация
  AppConfig.cs               # Типизированный доступ к настройкам
  Program.cs                 # Композиция скраперов и фоновых задач (11 процессов)
  Core/                      # AdaptiveConcurrencyController
  Data/                      # PostgreSQL-клиент и очереди записи
  Domain/Models/             # DTO, статистика, модели прокси и данных
  Parsing/                   # HTML extraction helpers (ProfileDataExtractor, CompanyDataExtractor, UserDataExtractor, HtmlParser)
  Scrapers/                  # Скраперы career.habr.com (11 скраперов)
  Infrastructure/
    Http/                    # SmartHttpClient, HttpClientFactory, HttpClientLogger
    Logging/                 # ConsoleLogger, ScraperLogger, ScraperProgressLogger, ScraperParallelLogger
    Proxy/                   # ProxyCoordinator, ProxyWhitelistManager, GeneralPoolManager, 
                             # FreeProxyListScraper, ProxyScrapeScraper, ProxyInfo,
                             # ProxyHttpClientFactory, ProxyRetryExecutor, ProxySourceHelper,
                             # ProxySourceStatistics, ProxyScraper, ProxyScraperLauncher
    Statistics/              # ScraperStatistics, TrafficStatistics, DatabaseStatistics
    Throttling/              # LinearThrottle, ExponentialBackoff
    Utils/                   # StringUtils, HashUtils, HtmlDebug
    Url/                     # UrlManager
```

## Быстрый Старт

### 1. Требования

- .NET 9 SDK
- PostgreSQL 12+
- Доступ к `career.habr.com`

### 2. Установка зависимостей

```bash
cd job_board_scraper
dotnet restore JobBoardScraper.sln
```

### 3. Инициализация БД

Создайте базу `jobs` или поменяйте строку подключения в `JobBoardScraper/App.config`.

Полный набор SQL-скриптов для создания актуальной схемы базы данных с нуля:

**Создание таблиц (обязательно):**

```bash
psql -U postgres -d jobs -f sql/create_resumes_table.sql
psql -U postgres -d jobs -f sql/create_companies_table.sql
psql -U postgres -d jobs -f sql/create_skills_table.sql
psql -U postgres -d jobs -f sql/create_levels_table.sql
psql -U postgres -d jobs -f sql/create_category_root_ids_table.sql
psql -U postgres -d jobs -f sql/create_user_skills_table.sql
psql -U postgres -d jobs -f sql/create_user_experience_table.sql
psql -U postgres -d jobs -f sql/create_user_experience_skills_table.sql
psql -U postgres -d jobs -f sql/create_universities_table.sql
psql -U postgres -d jobs -f sql/create_resumes_universities_table.sql
psql -U postgres -d jobs -f sql/create_company_reviews_table.sql
psql -U postgres -d jobs -f sql/create_resumes_educations_table.sql
```

**Миграции (добавление полей в существующие таблицы):**

```bash
psql -U postgres -d jobs -f sql/alter_companies_add_rating_fields.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_additional_fields.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_community_participation.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_job_search_status.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_empty_profile_field.sql
psql -U postgres -d jobs -f sql/alter_add_timestamps.sql
psql -U postgres -d jobs -f sql/add_is_deleted_column.sql
psql -U postgres -d jobs -f sql/add_expert_columns.sql
psql -U postgres -d jobs -f sql/add_company_details_columns.sql
psql -U postgres -d jobs -f sql/add_user_profile_columns.sql
psql -U postgres -d jobs -f sql/add_unique_link_constraint.sql
psql -U postgres -d jobs -f sql/add_slogan_column.sql
psql -U postgres -d jobs -f sql/rename_companies_columns.sql
```

### 4. Конфигурация

Основной файл: `JobBoardScraper/App.config`.

#### Вариант 1: Только UserResumeDetailScraper с бесплатными прокси (рекомендуемый старт)

```xml
<!-- Основной скрапер резюме -->
<add key="UserResumeDetail:Enabled" value="true" />
<add key="UserResumeDetail:TimeoutSeconds" value="60" />
<add key="UserResumeDetail:EnableRetry" value="true" />
<add key="UserResumeDetail:EnableTrafficMeasuring" value="true" />
<add key="UserResumeDetail:OutputMode" value="Both" />

<!-- Бесплатные прокси -->
<add key="FreeProxy:Enabled" value="true" />
<add key="FreeProxy:RefreshIntervalMinutes" value="10" />
<add key="FreeProxy:PoolMaxSize" value="10000" />
<add key="FreeProxy:WaitTimeoutSeconds" value="30" />
<add key="FreeProxy:RequestTimeoutSeconds" value="420" />
<add key="FreeProxy:MaxRetries" value="2" />
<add key="FreeProxy:MaxSwitches" value="3000" />

<!-- Whitelist прокси -->
<add key="ProxyWhitelist:Enabled" value="true" />
<add key="ProxyWhitelist:StorageType" value="file" />
<add key="ProxyWhitelist:FilePath" value="./data/proxy_whitelist.json" />
<add key="ProxyWhitelist:CooldownHours" value="24" />
<add key="ProxyWhitelist:MaxRetryAttempts" value="5" />
<add key="ProxyWhitelist:DailyLimitMessage" value="Вы исчерпали суточный лимит на просмотр профилей специалистов" />
```

#### Вариант 2: UserResumeDetailScraper без прокси (прямое соединение)

```xml
<add key="UserResumeDetail:Enabled" value="true" />
<add key="UserResumeDetail:EnableRetry" value="true" />
<add key="UserResumeDetail:EnableTrafficMeasuring" value="true" />
<add key="UserResumeDetail:OutputMode" value="Both" />

<add key="FreeProxy:Enabled" value="false" />
<add key="ProxyWhitelist:Enabled" value="false" />
```

#### Вариант 3: Полный сбор данных (все скраперы)

```xml
<!-- Все скраперы включены -->
<add key="ResumeList:Enabled" value="true" />
<add key="Companies:Enabled" value="true" />
<add key="Category:Enabled" value="true" />
<add key="CompanyFollowers:Enabled" value="true" />
<add key="Experts:Enabled" value="true" />
<add key="CompanyDetail:Enabled" value="true" />
<add key="UserProfile:Enabled" value="true" />
<add key="UserFriends:Enabled" value="true" />
<add key="UserResumeDetail:Enabled" value="true" />
<add key="CompanyRating:Enabled" value="true" />

<!-- Настройки таймаутов -->
<add key="ResumeList:IntervalMinutes" value="10" />
<add key="UserResumeDetail:TimeoutSeconds" value="60" />
<add key="CompanyDetail:TimeoutSeconds" value="60" />
<add key="UserProfile:TimeoutSeconds" value="60" />
<add key="UserFriends:TimeoutSeconds" value="60" />
<add key="CompanyRating:TimeoutSeconds" value="60" />
<add key="Experts:TimeoutSeconds" value="600" />
<add key="CompanyFollowers:TimeoutSeconds" value="300" />

<!-- Все с ретраями и замером трафика -->
<add key="ResumeList:EnableTrafficMeasuring" value="true" />
<add key="Companies:EnableTrafficMeasuring" value="true" />
<add key="Category:EnableTrafficMeasuring" value="true" />
<add key="CompanyFollowers:EnableTrafficMeasuring" value="true" />
<add key="Experts:EnableTrafficMeasuring" value="true" />
<add key="CompanyDetail:EnableTrafficMeasuring" value="true" />
<add key="UserProfile:EnableTrafficMeasuring" value="true" />
<add key="UserFriends:EnableTrafficMeasuring" value="true" />
<add key="UserResumeDetail:EnableTrafficMeasuring" value="true" />
<add key="CompanyRating:EnableTrafficMeasuring" value="true" />
```

#### Вариант 4: Собственные (коммерческие) прокси

```xml
<add key="UserResumeDetail:Enabled" value="true" />
<add key="UserResumeDetail:EnableRetry" value="true" />

<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1:8080;http://user:pass@proxy2:8080;socks5://proxy3:1080" />
<add key="Proxy:RotationIntervalSeconds" value="0" />
<add key="Proxy:AutoRotate" value="false" />

<add key="FreeProxy:Enabled" value="false" />
<add key="ProxyWhitelist:Enabled" value="false" />
```

#### Вариант 5: BruteForce + ResumeList (поиск новых профилей)

```xml
<add key="BruteForce:Enabled" value="true" />
<add key="BruteForce:MinLength" value="5" />
<add key="BruteForce:MaxLength" value="5" />
<add key="BruteForce:MaxConcurrentRequests" value="5" />
<add key="BruteForce:MaxRetries" value="200" />
<add key="BruteForce:Chars" value="abcdefghijklmnopqrstuvwxyz0123456789-_" />
<add key="BruteForce:EnableRetry" value="true" />
<add key="BruteForce:EnableTrafficMeasuring" value="true" />

<add key="ResumeList:Enabled" value="true" />
<add key="ResumeList:SkillsEnumerationEnabled" value="true" />
<add key="ResumeList:SkillsStartId" value="1" />
<add key="ResumeList:SkillsEndId" value="10000" />
<add key="ResumeList:WorkStatesEnabled" value="true" />
<add key="ResumeList:ExperiencesEnabled" value="true" />
<add key="ResumeList:QidsEnabled" value="true" />
<add key="ResumeList:CompanyIdsEnabled" value="true" />
<add key="ResumeList:UniversityIdsEnabled" value="true" />
```

#### Вариант 6: Только аналитика компаний

```xml
<add key="Companies:Enabled" value="true" />
<add key="CompanyDetail:Enabled" value="true" />
<add key="CompanyRating:Enabled" value="true" />
<add key="CompanyFollowers:Enabled" value="true" />

<add key="Companies:OutputMode" value="Both" />
<add key="CompanyDetail:OutputMode" value="Both" />
<add key="CompanyRating:OutputMode" value="Both" />
<add key="CompanyFollowers:OutputMode" value="Both" />
```

### 5. Запуск

```bash
dotnet run --project JobBoardScraper/JobBoardScraper.csproj
```

Сборка release:

```bash
dotnet build JobBoardScraper/JobBoardScraper.csproj -c Release
```

Публикация self-contained под Windows:

```bash
dotnet publish JobBoardScraper/JobBoardScraper.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

## Детали Реализованных Модулей

### Free proxy pool

Автоматический сбор бесплатных прокси работает в фоне, если включены настройки `FreeProxy:*`. Реализация поддерживает:

- загрузку с `free-proxy-list.net` (HTML-парсинг);
- загрузку из ProxyScrape API;
- загрузку из GeoNode API;
- фильтрацию и добавление в общий пул;
- ограничение размера пула (по умолчанию 10000);
- ожидание появления прокси при пустом пуле;
- подробное логирование источников и статистики.

Подробнее: [DYNAMIC_PROXY.md](docs/DYNAMIC_PROXY.md), [USERRESUME_WITH_PROXY.md](docs/USERRESUME_WITH_PROXY.md)

### Whitelist прокси

Whitelist нужен для профилей резюме, где один рабочий IP лучше использовать до суточного лимита, а не менять прокси на каждый запрос. Состояние сохраняется в JSON-файл и включает URL прокси, время последнего использования, флаг ошибки и счетчик retries.

Алгоритм выбора:

1. Взять пригодный whitelist-прокси, у которого прошел cooldown.
2. Если whitelist пуст или все записи на cooldown, взять прокси из общего пула.
3. При успешном использовании добавить прокси в whitelist.
4. При суточном лимите отметить прокси как рабочий, но отправить на cooldown.
5. При сетевых ошибках увеличить `retry_count`; после лимита ошибок удалить запись.

Подробнее: [CHANGELOG.md](CHANGELOG.md)

### Скраперы приложения

Ниже приведены краткие описания всех скраперов, входящих в состав приложения.

#### BruteForceUsernameScraper

Перебирает все возможные имена пользователей на основе заданного алфавита (`BruteForce:Chars`) и диапазона длин (`MinLength`/`MaxLength`). Для каждого сгенерированного username делает HTTP-запрос к `https://career.habr.com/{username}`. Если страница существует (не 404), сохраняет ссылку в очередь резюме. Поддерживает retry, замер трафика и адаптивный контроль конкурентности.

#### ResumeListPageScraper

Периодически обходит страницу списка резюме (`/resumes?order=last_visited`) и собирает ссылки на профили, навыки, уровни, зарплаты и статусы поиска работы. Поддерживает фильтрацию по компаниям (`company_ids[]`), ВУЗам (`university_ids[]`), навыкам (`skills[]`), стажу работы (`experiences[]`), статусу поиска (`work_states[]`), категориям (`qids[]`) и сортировке (`order`). Интервал обхода: 10 минут.

#### CompanyListScraper

Периодически обходит страницу списка компаний (`/companies`) и извлекает код компании из URL, числовой `company_id` из атрибута `data-company-id` и название. Поддерживает перебор комбинаций фильтров (размер + категория + дополнительный фильтр). Интервал обхода: 7 дней.

#### CategoryScraper

Периодически собирает список корневых категорий (`category_root_id`) со страницы фильтрации резюме. Извлекает option-элементы из `<select id="category_root_id">` и сохраняет ID + название каждой категории. Интервал обхода: 7 дней.

#### CompanyFollowersScraper

Обходит страницы подписчиков компаний (`/companies/{code}/followers`) и собирает ссылки на профили пользователей, username и слоган. Поддерживает пагинацию. Интервал обхода: 5 дней.

#### ExpertsScraper

Обходит `https://career.habr.com/experts?order=lastActive` и извлекает данные экспертов: имя, ссылку на профиль, стаж работы, привязанные компании. Устанавливает флаг `expert = true` в БД. Интервал обхода: 4 дня.

#### CompanyDetailScraper

Для каждой компании из БД загружает детальную страницу и извлекает: описание, сайт, рейтинг, количество сотрудников (текущие/все), подписчиков, навыки, признак ведения блога на Хабре, публичных представителей компании и числовой `company_id` (из ID кнопки избранного или из ссылок фильтрации). Интервал обхода: 30 дней.

#### UserProfileScraper

Загружает страницу профиля пользователя (`/{username}`) и извлекает: имя, опыт работы, последний визит, sidebar-метаданные. Сохраняет данные в таблицу `habr_resumes`. Интервал обхода: 30 дней.

#### UserFriendsScraper

Обходит страницы друзей пользователей (`/{username}/friends`) и собирает ссылки на связанные профили. Поддерживает фильтрацию только публичных профилей (`UserFriends:OnlyPublic`). Полученные ссылки добавляются в очередь резюме для дальнейшего обхода другими скраперами. Интервал обхода: 30 дней.

#### UserResumeDetailScraper

Центральный скрапер, который для каждого пользователя без детальных данных (определяется через `ResumesGetUserLinksWithoutData`) загружает страницу резюме и извлекает: «О себе», навыки, опыт работы (должности, компании, даты, описание, теги), уровень специалиста, зарплату, статус поиска работы, имя пользователя, техническую информацию, возраст, гражданство, готовность к удалённой работе, дату последнего визита, дату регистрации, участие в профсообществах, высшее и дополнительное образование.

В рамках парсинга высшего образования вызывает `ProfileDataExtractor.ExtractEducationData()` и сохраняет:
- ВУЗ: `habr_id`, название, город, количество выпускников.
- Связь резюме-ВУЗ: `user_id`, `university_id`.
- Курсы: JSON-массив с `name`, `start_date`, `end_date`, `duration`, `is_current`.
- Описание записи образования.

Данные пишутся через `DatabaseClient.EnqueueUniversity()` и `DatabaseClient.EnqueueUserUniversity()`.

Подробнее: [UNIVERSITY_EDUCATION_SCRAPER.md](docs/UNIVERSITY_EDUCATION_SCRAPER.md)

Интегрирован с системой прокси — получает прокси через `ProxyCoordinator` и распознаёт сообщение о суточном лимите career.habr.com для автоматической смены IP. Интервал обхода: 20 минут.

#### CompanyRatingScraper

Генерирует URL для рейтингов компаний по параметрам размера и года, проходит пагинацию и извлекает данные из карточек рейтинга:

- код компании и URL;
- название, город, описание;
- рейтинг и средняя оценка;
- список наград из `alt` изображений;
- текст отзыва без HTML.

Компания обновляется или создается по `code`. Отзывы сохраняются отдельно в `habr_company_reviews`, дубликаты отсекаются по `review_hash` (MD5 от текста). Интервал обхода: 30 дней.

Подробнее: [COMPANY_RATING_SCRAPER.md](docs/COMPANY_RATING_SCRAPER.md)

### Единая статистика

`ScraperStatistics` используется как общий формат статистики для скраперов. Параллельные скраперы обновляют активные запросы и прогресс через `ScraperParallelLogger`, а итоговый вывод идет в одном формате. Дополнительно:

- `TrafficStatistics` - замер трафика per-scraper с сохранением в файл.
- `DatabaseStatistics` - счётчики операций записи в БД.


## Процессы приложения

Приложение запускает до 11 параллельных процессов:

1. **BruteForceUsernameScraper** - перебор всех возможных имён пользователей
2. **ResumeListPageScraper** - периодический обход страницы со списком резюме (каждые 10 минут)
3. **CompanyListScraper** - периодический обход списка компаний (раз в неделю)
4. **CategoryScraper** - периодический сбор category_root_id (раз в неделю)
5. **CompanyFollowersScraper** - периодический обход подписчиков компаний (каждые 5 дней)
6. **ExpertsScraper** - периодический обход экспертов (каждые 4 дня)
7. **CompanyDetailScraper** - периодический обход детальных страниц компаний (раз в месяц)
8. **UserProfileScraper** - периодический обход профилей пользователей (раз в месяц)
9. **UserFriendsScraper** - периодический обход списков друзей (раз в месяц)
10. **UserResumeDetailScraper** - периодический обход резюме (каждые 20 минут)
11. **CompanyRatingScraper** - периодический обход рейтингов компаний (раз в месяц)

## Полезные SQL-Запросы

### Аналитика профилей и образования
```sql
-- Профили, у которых есть высшее образование
SELECT r.id, r.link, r.title
FROM habr_resumes r
WHERE EXISTS (
  SELECT 1 FROM habr_resumes_universities ru WHERE ru.user_id = r.id
)
ORDER BY r.updated_at DESC;

-- Топ ВУЗов по количеству связанных резюме
SELECT u.name, u.city, COUNT(*) AS resume_count
FROM habr_resumes_universities ru
JOIN habr_universities u ON u.id = ru.university_id
GROUP BY u.name, u.city
ORDER BY resume_count DESC;

-- Поиск специалистов по гражданству и готовности к удаленке
SELECT link, title, citizenship, remote_work
FROM habr_resumes
WHERE citizenship = 'Россия' AND remote_work = true
LIMIT 20;
```

### Аналитика компаний и рейтингов
```sql
-- Рейтинг компаний по количеству отзывов
SELECT c.code, c.title, COUNT(r.id) AS review_count
FROM habr_companies c
LEFT JOIN habr_company_reviews r ON r.company_id = c.id
GROUP BY c.code, c.title
ORDER BY review_count DESC;

-- Компании с наивысшим средним баллом (scores)
SELECT title, scores, city
FROM habr_companies
WHERE scores IS NOT NULL
ORDER BY scores DESC
LIMIT 10;
```

### Проверка качества данных (Пустые профили)
```sql
-- Найти профили, помеченные как пустые, но имеющие данные (валидация логики)
SELECT r.id, r.link, r.title
FROM habr_resumes r
WHERE r.is_empty = TRUE
  AND (
      EXISTS (SELECT 1 FROM habr_user_experience ue WHERE ue.user_id = r.id)
      OR EXISTS (SELECT 1 FROM habr_resumes_universities ru WHERE ru.user_id = r.id)
      OR (r.community_participation IS NOT NULL AND jsonb_array_length(r.community_participation) > 0)
  );

-- Статистика заполненности профилей
SELECT 
    COUNT(*) FILTER (WHERE is_empty = FALSE) as filled,
    COUNT(*) FILTER (WHERE is_empty = TRUE) as empty,
    COUNT(*) as total
FROM habr_resumes;
```

Заполненность профилей и детальный экспорт также можно посмотреть с помощью готовых скриптов в папке `sql/`:

**Аналитика и отчеты:**
- `sql/count_filled_profiles.sql` — общая статистика заполненности
- `sql/count_profiles_by_type.sql` — статистика по типам профилей (пустые/заполненные)
- `sql/list_filled_profiles_detailed.sql` — детальный текстовый отчет по всем заполненным профилям
- `sql/get_resumes_with_slogan.sql` — поиск резюме с заполненным слоганом
- `sql/get_user_experience.sql` — выгрузка опыта работы пользователей

**Проверка качества данных:**
- `sql/select_resumes_without_data.sql` — поиск "дыр" в данных (профили без ключевой информации)
- `sql/verify_empty_profile_logic.sql` — проверка корректности маркировки пустых профилей
- `sql/select_empty_profiles.sql` — список всех помеченных пустых профилей

**Обслуживание БД:**
- `sql/remove_doubles.sql` — удаление дубликатов записей
- `sql/cleanup_404_pages.sql` — очистка ссылок, приведших к 404 ошибке
- `sql/add_is_deleted_column.sql` — добавление флага мягкого удаления

## Логи и Артефакты

- Логи пишутся в `./logs`.
- Статистика трафика по умолчанию пишется в `./logs/traffic_stats.txt` (интервал сохранения: 5 минут).
- Статистика прокси по умолчанию пишется в `./logs/proxy_stats.txt` (интервал сохранения: 10 минут).
- Whitelist прокси по умолчанию хранится в `./data/proxy_whitelist.json`.
- HTML-дампы включаются отдельными `SaveHtml` настройками конкретных скраперов.
- При работе с прокси создаётся `ProxyCoordinator` с периодическим отчётом о статусе (каждые 5 минут).

## Лицензия

MIT. См. [LICENSE.md](LICENSE.md).