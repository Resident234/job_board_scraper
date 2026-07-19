# System Architecture

## Overview

JobBoardScraper is built on a modular, concurrent architecture designed for high-performance web scraping with robust error handling and proxy management. Приложение запускает до 11 параллельных скраперов, каждый работает в фоновом режиме с собственным интервалом.

## Core Components

### 1. Scraper Layer

Каждый скрапер — это независимый класс, реализующий паттерн фоновой задачи. Скраперы не имеют общего базового класса, но следуют единому шаблону: получают `SmartHttpClient`, `DatabaseClient` и необходимые колбэки/фабрики в конструкторе, а метод `StartAsync(CancellationToken)` запускает бесконечный цикл с интервалом.

```mermaid
graph LR
    subgraph "Scrapers (11 processes)"
        BFS[BruteForceUsernameScraper]
        RLP[ResumeListPageScraper]
        CLS[CompanyListScraper]
        CS[CategoryScraper]
        CFS[CompanyFollowersScraper]
        ES[ExpertsScraper]
        CDS[CompanyDetailScraper]
        UPS[UserProfileScraper]
        UFS[UserFriendsScraper]
        URDS[UserResumeDetailScraper]
        CRS[CompanyRatingScraper]
    end

    subgraph "Core Infrastructure"
        SHC[SmartHttpClient]
        DB[DatabaseClient]
        ACC[AdaptiveConcurrencyController]
        TS[TrafficStatistics]
        SS[ScraperStatistics]
    end

    subgraph "Proxy System"
        PC[ProxyCoordinator]
        PWM[ProxyWhitelistManager]
        GPM[GeneralPoolManager]
        FPLS[FreeProxyListScraper]
        PSS[ProxyScrapeScraper]
    end

    BFS --> SHC
    RLP --> SHC
    CLS --> SHC
    CS --> SHC
    CFS --> SHC
    ES --> SHC
    CDS --> SHC
    UPS --> SHC
    UFS --> SHC
    URDS --> SHC
    CRS --> SHC

    RLP --> DB
    CLS --> DB
    CFS --> DB
    ES --> DB
    CDS --> DB
    UPS --> DB
    UFS --> DB
    URDS --> DB
    CRS --> DB

    RLP --> ACC
    CFS --> ACC
    CDS --> ACC
    UPS --> ACC
    UFS --> ACC
    URDS --> ACC
    CRS --> ACC

    URDS --> PC
    PC --> PWM
    PC --> GPM
    GPM --> FPLS
    GPM --> PSS
```

### 2. Data Pipeline

```
┌──────────┐    ┌──────────┐    ┌──────────────┐    ┌────────────┐    ┌────────────┐
│  HTTP    │    │  HTML    │    │  Data        │    │  Queue     │    │  PostgreSQL│
│  Request │--->│  Parser  │--->│  Extractor   │--->│  (Buffer)  │--->│  Database  │
└──────────┘    └──────────┘    └──────────────┘    └────────────┘    └────────────┘
                                                           │
                                                           ▼
                                                  ┌────────────────┐
                                                  │  Writer Task   │
                                                  │  (Background)  │
                                                  └────────────────┘
```

### 3. Proxy Management System

```
┌─────────────────────────────────────────────────────────────────────┐
│                        ProxyCoordinator                              │
│  Выбирает источник прокси по приоритету:                            │
│  1. ProxyWhitelistManager - проверенные рабочие прокси              │
│  2. GeneralPoolManager - общий пул из бесплатных источников         │
└─────────────────────────────────────────────────────────────────────┘
         │                    │
         ▼                    ▼
┌─────────────────┐  ┌──────────────────────┐
│ ProxyWhitelist- │  │  GeneralPoolManager  │
│ Manager         │  │                      │
│ - JSON storage  │  │  - FreeProxyListScraper │
│ - Cooldown      │  │  - ProxyScrapeScraper   │
│ - Retry limit   │  │  - GeoNode API          │
│ - daily limit   │  │  - Blacklist            │
└─────────────────┘  └──────────────────────┘
```

### 4. Data Flows

#### 4.1 UserResumeDetail Data Flow

```mermaid
flowchart TD
    A[UserResumeDetailScraper] --> B[Fetch userLink from DB]
    B --> C{HTTP status}
    C -->|404| D[Skip without DB write]
    C -->|non-2xx| E[Fail]
    C -->|2xx| F[UserDataExtractor checks]
    F --> G{Deleted?}
    G -->|Yes| H[EnqueueResume isDeleted]
    G -->|No| I{Private?}
    I -->|Yes| J[EnqueueResume isPublic=false]
    I -->|No| K{Daily limit?}
    K -->|Yes| L[Proxy rotate / skip]
    K -->|No| M[Extract about, skills, experience, education, communities]
    M --> N[EnqueueResume UpdateIfExists]
    N --> O[Writer Task → habr_resumes + related tables]
```

#### 4.2 University Education Data Flow

Часть пайплайна `UserResumeDetailScraper` (не отдельный скрапер):

```mermaid
flowchart TD
    A[UserResumeDetailScraper] --> B[UserDataExtractor.ExtractEducation]
    B --> C{Universities found?}
    C -->|Yes| D[Parse courses for each university]
    C -->|No| E[EnqueueResume without universities]
    D --> F[EnqueueResume with userUniversities]
    F --> G[Writer: ResumesUniversitiesInsert]
    G --> H[habr_universities]
    G --> I[habr_resumes_universities]
```

#### 4.3 UserProfile Data Flow

```mermaid
flowchart TD
    A[UserProfileScraper] --> B[Fetch userLink/friends]
    B --> C{HTTP 2xx?}
    C -->|No| D[Skip]
    C -->|Yes| E[UserDataExtractor.IsPublicProfile]
    E --> F{Public?}
    F -->|No| G[EnqueueResume isPublic=false]
    F -->|Yes| H[Extract level, salary, experience, lastVisit]
    H --> I[EnqueueResume UpdateIfExists]
    I --> J[habr_resumes / habr_levels]
```

#### 4.4 UserFriends Data Flow

```mermaid
flowchart TD
    A[UserFriendsScraper] --> B[Fetch userLink/friends?page=N]
    B --> C{HTTP 2xx?}
    C -->|No| D[Stop paging for user]
    C -->|Yes| E[UserDataExtractor.ExtractFriends]
    E --> F{friendsOnPage == 0?}
    F -->|Yes| D
    F -->|No| G[EnqueueResume UpdateIfExists]
    G --> H[Next page]
    H --> B
```

#### 4.5 ResumeListPage Data Flow

```mermaid
flowchart TD
    A[ResumeListPageScraper] --> B[Fetch /resumes with filters]
    B --> C{HTTP OK?}
    C -->|No| D[Continue next URL]
    C -->|Yes| E[UserDataExtractor.ParseProfilesFromPage]
    E --> F[EnqueueResume for each profile]
    F --> G{Skills mode?}
    G -->|Yes| H[EnqueueSkill]
    G -->|No| I[Next page / filter]
    H --> I
```

#### 4.6 BruteForceUsername Data Flow

```mermaid
flowchart TD
    A[BruteForceUsernameScraper] --> B[Build username URL]
    B --> C{404?}
    C -->|Yes| D[Skip]
    C -->|No| E[HtmlParser.ExtractTitle]
    E --> F[EnqueueResume SkipIfExists]
    F --> G[habr_resumes]
```

#### 4.7 Experts Data Flow

```mermaid
flowchart TD
    A[ExpertsScraper] --> B[Fetch /experts?page=N]
    B --> C{HTTP 2xx?}
    C -->|No| D[End crawl]
    C -->|Yes| E[UserDataExtractor.ParseExpertsFromPage]
    E --> F{expertsOnPage == 0?}
    F -->|Yes| D
    F -->|No| G[EnqueueResume]
    G --> H[EnqueueCompany if present]
    H --> I[Next page]
    I --> B
```

#### 4.8 CompanyList Data Flow

```mermaid
flowchart TD
    A[CompanyListScraper] --> B[Fetch /companies with filters]
    B --> C{HTTP 2xx?}
    C -->|No| D[Stop filter pagination]
    C -->|Yes| E[CompanyDataExtractor.ExtractCompanies]
    E --> F[EnqueueCompany code/url/id]
    F --> G{HasNextPage?}
    G -->|Yes| H[Next page]
    G -->|No| D
    H --> B
```

#### 4.9 Category Data Flow

```mermaid
flowchart TD
    A[CategoryScraper] --> B[Fetch /companies]
    B --> C{HTTP 2xx?}
    C -->|No| D[Abort run]
    C -->|Yes| E[CompanyDataExtractor.ExtractCategories]
    E --> F[EnqueueCategoryRootId]
    F --> G[habr_category_root_ids]
```

#### 4.10 CompanyDetail Data Flow

```mermaid
flowchart TD
    A[CompanyDetailScraper] --> B[Fetch company URL from DB]
    B --> C{HTTP 2xx and company_id?}
    C -->|No| D[Skip]
    C -->|Yes| E[CompanyDataExtractor: title, about, skills, employees...]
    E --> F[EnqueueCompany companyRecord]
    E --> G[EnqueueCompany related]
    E --> H[EnqueueResume employees SkipIfExists]
    E --> I[EnqueueResume members UpdateIfExists]
    F --> J[habr_companies / skills / resumes]
```

#### 4.11 CompanyFollowers Data Flow

```mermaid
flowchart TD
    A[CompanyFollowersScraper] --> B[Fetch /companies/code/followers?page=N]
    B --> C{HTTP 2xx?}
    C -->|No| D[Stop company]
    C -->|Yes| E[CompanyDataExtractor.ExtractFollowersUsers]
    E --> F{users empty?}
    F -->|Yes| D
    F -->|No| G[EnqueueResume UpdateIfExists]
    G --> H{HasNextFollowersPage?}
    H -->|Yes| I[Next page]
    H -->|No| D
    I --> B
```

#### 4.12 CompanyRating Data Flow

```mermaid
flowchart TD
    A[CompanyRatingScraper] --> B[Fetch /companies/ratings sz/y/page]
    B --> C{HTTP 2xx?}
    C -->|No| D[Break pagination]
    C -->|Yes| E[CompanyDataExtractor.ParseCompaniesFromPage]
    E --> F{companiesCount == 0?}
    F -->|Yes| D
    F -->|No| G[EnqueueCompany with rating/awards/reviews]
    G --> H[habr_companies / habr_company_reviews]
    H --> I[Next page]
    I --> B
```

### 5. Proxy Whitelist Management

```mermaid
graph TB
    subgraph "Proxy Management"
        PWM[ProxyWhitelistManager]
        WL[Whitelist Storage<br/>./data/proxy_whitelist.json]
        GPM[GeneralPoolManager]
    end

    subgraph "Scraper"
        URDS[UserResumeDetailScraper]
    end

    subgraph "Free Proxy Sources"
        FPLS[FreeProxyListScraper]
        PSS[ProxyScrapeScraper]
        GN[GeoNode API]
    end

    URDS -->|GetNextProxy| PWM
    URDS -->|ReportSuccess/Failure| PWM
    URDS -->|ReportDailyLimitReached| PWM

    PWM -->|Priority 1| WL
    PWM -->|Priority 2| GPM

    GPM --> FPLS
    GPM --> PSS
    GPM --> GN
```

### 6. Progress Tracking System

The system uses a custom thread-safe progress tracking mechanism to report scraping execution statistics in multi-threaded environments. It ensures accurate, atomic updates of items processed and displays real-time progress.

For more details, see [Progress Tracking System](PROGRESS_TRACKING.md).

## Technical Stack

### Backend
- **.NET 9.0** - Core framework
- **C# 12** - Programming language
- **AngleSharp** - HTML DOM parsing

### Database
- **PostgreSQL 12+** - Primary data store
- **Npgsql** - .NET PostgreSQL driver

### Infrastructure
- **System.Net.Http** - HTTP client with custom retry logic
- **System.Text.Json** - JSON serialization for whitelist storage
- **System.Security.Cryptography** - MD5 hashing for review dedup

## Project Structure

```
JobBoardScraper/
  App.config                 # Runtime-конфигурация (XML)
  AppConfig.cs               # Типизированный доступ к настройкам
  Program.cs                 # Композиция скраперов и фоновых задач
  Core/
    AdaptiveConcurrencyController.cs  # Адаптивное управление параллелизмом
  Data/
    DatabaseClient.cs        # PostgreSQL-клиент с очередями записи
  Domain/
    Models/                  # DTO, модели данных, статистики
      CompanyRatingData.cs
      CommunityParticipationData.cs
      CourseData.cs
      UniversityData.cs
      UserProfileData.cs
      WhitelistProxyEntry.cs
      ...
  Parsing/
    UserDataExtractor.cs     # Извлечение данных пользователей и резюме
    CompanyDataExtractor.cs  # Извлечение данных компаний
    HtmlParser.cs            # Базовые HTML-утилиты
  Scrapers/
    BruteForceUsernameScraper.cs
    ResumeListPageScraper.cs
    CompanyListScraper.cs
    CategoryScraper.cs
    CompanyFollowersScraper.cs
    ExpertsScraper.cs
    CompanyDetailScraper.cs
    UserProfileScraper.cs
    UserFriendsScraper.cs
    UserResumeDetailScraper.cs
    CompanyRatingScraper.cs
  Infrastructure/
    Http/
      SmartHttpClient.cs     # HTTP-клиент с retry и статистикой
      HttpClientFactory.cs   # Фабрика HttpClient
      HttpClientLogger.cs    # Логирование HTTP-запросов
    Logging/
      ConsoleLogger.cs       # Логирование (Console/File/Both)
      ScraperLogger.cs       # Логирование скраперов
      ScraperProgressLogger.cs  # Прогресс-логирование
      ScraperParallelLogger.cs  # Потокобезопасное логирование
    Proxy/
      ProxyCoordinator.cs    # Координатор источников прокси
      ProxyWhitelistManager.cs  # Управление whitelist прокси
      GeneralPoolManager.cs  # Управление общим пулом
      FreeProxyListScraper.cs  # Сбор бесплатных прокси
      ProxyScrapeScraper.cs  # Сбор из ProxyScrape API
      ProxyInfo.cs           # Модель прокси
      ProxyHttpClientFactory.cs  # HTTP-клиент с прокси
      ProxyRetryExecutor.cs  # Retry для прокси
      ProxySourceHelper.cs   # Helper для источников
      ProxySourceStatistics.cs   # Статистика источников
      ProxyScraper.cs        # Базовый скрапер прокси
      ProxyScraperLauncher.cs   # Запуск прокси-скраперов
      JsonWhitelistStorage.cs   # JSON-хранилище whitelist
    Statistics/
      ScraperStatistics.cs   # Статистика скраперов
      TrafficStatistics.cs   # Статистика трафика
      DatabaseStatistics.cs  # Статистика БД
    Throttling/
      LinearThrottle.cs      # Линейный throttle
      ExponentialBackoff.cs  # Экспоненциальный backoff
    Utils/
      StringUtils.cs         # Строковые утилиты
      HashUtils.cs           # MD5 хэширование
      HtmlDebug.cs           # HTML-отладка
    Url/
      UrlManager.cs          # Управление URL
```

## Performance Characteristics

### Throughput
- **Single node**: 50-100 requests/minute (with rate limiting)
- **Proxy rotation**: 1-5 seconds per rotation

### Resource Usage
- **Memory**: 100-300MB per worker process
- **CPU**: 10-30% average utilization
- **Bandwidth**: 1-5 Mbps depending on scrape intensity

## Best Practices

### Performance Optimization
- Use appropriate rate limiting via `AdaptiveConcurrencyController`
- Implement proper caching of HTTP connections
- Optimize database queries with batch writes
- Monitor and tune proxy rotation

### Reliability
- Implement comprehensive error handling with retries
- Use `SmartHttpClient` for transient fault handling
- Monitor system health continuously via statistics
- Implement proper logging with `OutputMode`

### Security
- Keep database credentials secure in `App.config`
- Use HTTPS for all communications
- Regularly update dependencies
- Monitor for suspicious activity via traffic stats

## Future Architecture Evolution

### Planned Enhancements
- **API integration**: Use career.habr.com API (`https://career.habr.com/info/api#q1.7`) instead of HTML parsing
- **Enhanced monitoring** with Prometheus/Grafana
- **Improved proxy management** with machine learning
- **Better data processing** pipeline with streaming

This architecture provides a solid foundation for building a robust, scalable web scraping system that can handle the demands of modern data extraction while maintaining reliability and performance.