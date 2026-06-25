# JobBoardScraper

JobBoardScraper - .NET 9 консольное приложение для сбора данных с Habr Career. Проект умеет обходить резюме, профили пользователей, компании, рейтинги компаний, списки экспертов, категории и связанные страницы, сохранять результат в PostgreSQL и работать через прокси с ретраями, лимитами и статистикой.

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

## Реализованная Функциональность

| Модуль | Документация | Основные артефакты |
| --- | --- | --- |
| Высшее образование | Парсинг блока образования из резюме, нормализованное хранение университетов и связей резюме-ВУЗ | `Parsing/ProfileDataExtractor.cs`, `Scrapers/UserResumeDetailScraper.cs`, `Data/DatabaseClient.cs`, `sql/create_universities_table.sql`, `sql/create_resumes_universities_table.sql` |
| `CompanyRatingScraper` | Рейтинги компаний, награды, оценки, отзывы, dedup по hash | `Scrapers/CompanyRatingScraper.cs`, `Domain/Models/CompanyRatingData.cs`, `sql/alter_companies_add_rating_fields.sql`, `sql/create_company_reviews_table.sql` |
| Free proxy pool | Автоматический сбор и ротация бесплатных прокси | `Infrastructure/Proxy/FreeProxyListScraper.cs`, `FreeProxyPool.cs`, `ProxyScrapeScraper.cs`, `ProxyCoordinator.cs` |
| Статистика | Единый сбор статистики скраперов | `Domain/Models/ScraperStatistics.cs`, `Infrastructure/Logging/ParallelScraperLogger.cs`, `Scrapers/*.cs` |
| Архитектура | Рефакторинг структуры проекта и namespace-ов | `Data/`, `Domain/Models/`, `Parsing/`, `Scrapers/`, `Infrastructure/*` |
| Proxy whitelist | Whitelist рабочих прокси, cooldown при суточном лимите, retry и JSON-хранилище | `Infrastructure/Proxy/ProxyWhitelistManager.cs`, `JsonWhitelistStorage.cs`, `GeneralPoolManager.cs`, `ProxyCoordinator.cs` |

## Возможности

### Сбор данных

- `ResumeListPageScraper` собирает ссылки на резюме, навыки, уровни, зарплаты и статусы поиска работы из списков резюме.
- `UserResumeDetailScraper` дополняет резюме детальными данными: about, навыки, опыт, компании, должности, возраст, гражданство, удаленная работа, посещаемость, участие в сообществах и высшее образование.
- `CompanyListScraper` и `CompanyDetailScraper` собирают компании, числовой `company_id`, описание, сайт, рейтинг, сотрудников, подписчиков, признаки блога на Habr, навыки и публичных представителей.
- `CompanyRatingScraper` обходит `https://career.habr.com/companies/ratings`, перебирает размеры компаний и годы, обрабатывает пагинацию, сохраняет рейтинг, город, описание, награды, среднюю оценку и отзывы.
- `ExpertsScraper` собирает профили экспертов.
- `UserProfileScraper` и `UserFriendsScraper` обрабатывают публичные пользовательские профили и связи.
- `CategoryScraper` сохраняет справочник корневых категорий.
- `BruteForceUsernameScraper` поддерживает перебор пользовательских URL с retry и замером трафика.

### Хранение данных

Данные сохраняются в PostgreSQL через `Data/DatabaseClient.cs`. Запись идет через очереди и фоновые writer-задачи, чтобы скраперы не блокировались на каждой операции БД.

Ключевые таблицы:

- `habr_resumes` - резюме и профильные поля.
- `habr_companies` - компании и агрегированные поля по компаниям.
- `habr_company_reviews` - отзывы о компаниях с уникальным `review_hash`.
- `habr_universities` - справочник ВУЗов Habr Career.
- `habr_resumes_universities` - связь резюме с ВУЗами, курсами и периодами обучения.
- `habr_user_experience`, `habr_user_experience_skills`, `habr_user_skills` - опыт и навыки пользователя.
- `habr_skills`, `habr_company_skills`, `habr_levels`, `habr_category_root_ids` - справочники и связи.

### Прокси и отказоустойчивость

- `SmartHttpClient` добавляет retry, обработку временных HTTP-ошибок и сбор статистики трафика.
- `FreeProxyListScraper` и `ProxyScrapeScraper` пополняют общий пул бесплатных прокси.
- `ProxyCoordinator` выбирает источник прокси: сначала whitelist, затем общий пул.
- `ProxyWhitelistManager` сохраняет рабочие прокси в `./data/proxy_whitelist.json`, учитывает 24-часовой cooldown после суточного лимита Habr Career и удаляет нерабочие прокси после настраиваемого числа ошибок.
- `UserResumeDetailScraper` распознает текст лимита `Вы исчерпали суточный лимит на просмотр профилей специалистов` и сообщает координатору прокси о необходимости переключения.

### Статистика и логи

- Каждый основной скрапер использует `ScraperStatistics`: обработано, успешно, ошибок, пропущено, активные запросы и время работы.
- `TrafficStatistics` пишет общий и per-scraper расход трафика.
- `ConsoleLogger`, `ScraperProgressLogger` и `ParallelScraperLogger` дают единый формат логов.
- Режим вывода задается через `OutputMode`: `ConsoleOnly`, `FileOnly`, `Both`.

## Архитектура

```text
JobBoardScraper/
  App.config                 # Runtime-конфигурация
  AppConfig.cs               # Типизированный доступ к настройкам
  Program.cs                 # Композиция скраперов и фоновых задач
  Data/                      # PostgreSQL-клиент и очереди записи
  Domain/Models/             # DTO, статистика, модели прокси и данных
  Parsing/                   # DOM/HTML extraction helpers
  Scrapers/                  # Скраперы Habr Career
  Infrastructure/
    Http/                    # SmartHttpClient, HttpClientFactory
    Logging/                 # Логирование и прогресс
    Proxy/                   # Ротация, пул, whitelist, источники прокси
    Statistics/              # Статистика трафика
    Throttling/              # Backoff
    Utils/                   # Общие утилиты
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

Минимальный набор SQL-скриптов для актуальной схемы:

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
psql -U postgres -d jobs -f sql/alter_companies_add_rating_fields.sql
psql -U postgres -d jobs -f sql/create_company_reviews_table.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_additional_fields.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_community_participation.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_job_search_status.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_empty_profile_field.sql
psql -U postgres -d jobs -f sql/alter_add_timestamps.sql
```

### 4. Конфигурация

Основной файл: `JobBoardScraper/App.config`.

Пример включения детального скрапера резюме с бесплатными прокси и whitelist:

```xml
<add key="UserResumeDetail:Enabled" value="true" />
<add key="UserResumeDetail:EnableRetry" value="true" />
<add key="UserResumeDetail:EnableTrafficMeasuring" value="true" />

<add key="FreeProxy:Enabled" value="true" />
<add key="FreeProxy:RefreshIntervalMinutes" value="10" />
<add key="FreeProxy:WaitTimeoutSeconds" value="30" />

<add key="ProxyWhitelist:Enabled" value="true" />
<add key="ProxyWhitelist:StorageType" value="file" />
<add key="ProxyWhitelist:FilePath" value="./data/proxy_whitelist.json" />
<add key="ProxyWhitelist:CooldownHours" value="24" />
<add key="ProxyWhitelist:MaxRetryAttempts" value="5" />
```

Пример включения рейтингов компаний:

```xml
<add key="CompanyRating:Enabled" value="true" />
<add key="CompanyRating:OutputMode" value="Both" />
<add key="CompanyRating:TimeoutSeconds" value="60" />
<add key="CompanyRating:EnableRetry" value="true" />
<add key="CompanyRating:EnableTrafficMeasuring" value="true" />
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

### Высшее образование

`UserResumeDetailScraper` вызывает `ProfileDataExtractor.ExtractEducationData()` и извлекает блок `Высшее образование`, если он есть в профиле. Из блока сохраняются:

- ВУЗ: `habr_id`, название, город, количество выпускников.
- Связь резюме-ВУЗ: `user_id`, `university_id`.
- Курсы: JSON-массив с `name`, `start_date`, `end_date`, `duration`, `is_current`.
- Описание записи образования.

Данные пишутся через `DatabaseClient.EnqueueUniversity()` и `DatabaseClient.EnqueueUserUniversity()`.

Подробнее: [UNIVERSITY_EDUCATION_SCRAPER.md](docs/UNIVERSITY_EDUCATION_SCRAPER.md)

### Рейтинги компаний

`CompanyRatingScraper` генерирует URL для рейтингов компаний по параметрам размера и года, проходит пагинацию и извлекает данные из карточек рейтинга:

- код компании и URL;
- название, город, описание;
- рейтинг и средняя оценка;
- список наград из `alt` изображений;
- текст отзыва без HTML.

Компания обновляется или создается по `code`. Отзывы сохраняются отдельно в `habr_company_reviews`, а дубликаты отсекаются по `review_hash`.

Подробнее: [COMPANY_RATING_SCRAPER.md](docs/COMPANY_RATING_SCRAPER.md)

### Free proxy pool

Автоматический сбор бесплатных прокси работает в фоне, если включены настройки `FreeProxy:*`. Реализация поддерживает:

- загрузку с `free-proxy-list.net`;
- загрузку из ProxyScrape API;
- фильтрацию и добавление в общий пул;
- ограничение размера пула;
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

Подробнее: [CHANGELOG.md](CHANGELOG.md#unreleased)

### Единая статистика

`ScraperStatistics` используется как общий формат статистики для скраперов. Параллельные скраперы обновляют активные запросы и прогресс через `ParallelScraperLogger`, а итоговый вывод идет в одном формате.

### Namespace-структура

Текущая структура уже приведена к отдельным слоям:

- `Data` - работа с БД.
- `Domain/Models` - модели предметной области.
- `Parsing` - HTML/DOM extraction.
- `Scrapers` - прикладная логика обхода страниц.
- `Infrastructure/Http`, `Infrastructure/Logging`, `Infrastructure/Proxy`, `Infrastructure/Statistics`, `Infrastructure/Throttling`, `Infrastructure/Utils` - техническая инфраструктура.

## Полезные SQL-Запросы

Профили, у которых уже есть высшее образование:

```sql
SELECT r.id, r.link, r.title
FROM habr_resumes r
WHERE EXISTS (
  SELECT 1 FROM habr_resumes_universities ru WHERE ru.user_id = r.id
)
ORDER BY r.updated_at DESC;
```

Топ ВУЗов по количеству связанных резюме:

```sql
SELECT u.name, u.city, COUNT(*) AS resume_count
FROM habr_resumes_universities ru
JOIN habr_universities u ON u.id = ru.university_id
GROUP BY u.name, u.city
ORDER BY resume_count DESC;
```

Отзывы по компаниям:

```sql
SELECT c.code, c.title, COUNT(r.id) AS review_count
FROM habr_companies c
LEFT JOIN habr_company_reviews r ON r.company_id = c.id
GROUP BY c.code, c.title
ORDER BY review_count DESC;
```

Заполненность профилей можно смотреть готовыми скриптами:

- `sql/count_filled_profiles.sql`
- `sql/list_filled_profiles_detailed.sql`
- `sql/select_resumes_without_data.sql`
- `sql/verify_empty_profile_logic.sql`

## Логи и Артефакты

- Логи пишутся в `./logs`.
- Статистика трафика по умолчанию пишется в `./logs/traffic_stats.txt`.
- Whitelist прокси по умолчанию хранится в `./data/proxy_whitelist.json`.
- HTML-дампы включаются отдельными `SaveHtml` настройками конкретных скраперов.

## Лицензия

MIT. См. [LICENSE.md](LICENSE.md).
