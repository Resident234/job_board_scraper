# Configuration Guide

## 📋 Configuration Overview

JobBoardScraper uses a single XML configuration file (`App.config`) with typed access via `AppConfig.cs`. Все настройки читаются из секции `<appSettings>`.

## 🔧 Configuration File

Location: `JobBoardScraper/App.config`

### Database Configuration

```xml
<add key="Database:ConnectionString" value="Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;"/>
```

### DatabaseClient Settings

```xml
<add key="DatabaseClient:OutputMode" value="Both" />
<!-- ConsoleOnly, FileOnly, или Both -->
```

### Scraper Management

Каждый скрапер включается/отключается отдельной настройкой:

```xml
<!-- BruteForceUsernameScraper -->
<add key="BruteForce:Enabled" value="false" />
<add key="BruteForce:BaseUrl" value="http://career.habr.com/" />
<add key="BruteForce:MinLength" value="5" />
<add key="BruteForce:MaxLength" value="5" />
<add key="BruteForce:MaxConcurrentRequests" value="5" />
<add key="BruteForce:MaxRetries" value="200" />
<add key="BruteForce:Chars" value="abcdefghijklmnopqrstuvwxyz0123456789-_" />
<add key="BruteForce:EnableRetry" value="true" />
<add key="BruteForce:EnableTrafficMeasuring" value="true" />
<add key="BruteForce:OutputMode" value="Both" />

<!-- CompanyListScraper -->
<add key="Companies:Enabled" value="false" />
<add key="Companies:ListUrl" value="https://career.habr.com/companies" />
<add key="Companies:ItemSelector" value=".companies-item" />
<add key="Companies:OutputMode" value="Both" />
<add key="Companies:UseFilterCombinations" value="false" />

<!-- CompanyFollowersScraper -->
<add key="CompanyFollowers:Enabled" value="false" />
<add key="CompanyFollowers:TimeoutSeconds" value="300" />
<add key="CompanyFollowers:OutputMode" value="Both" />

<!-- ResumeListPageScraper -->
<add key="ResumeList:Enabled" value="false" />
<add key="ResumeList:IntervalMinutes" value="300" />
<add key="ResumeList:PageUrl" value="https://career.habr.com/resumes?order=last_visited" />
<add key="ResumeList:OutputMode" value="Both" />
<!-- Фильтрация по навыкам, компаниям, ВУЗам, стажу, статусу поиска -->
<add key="ResumeList:SkillsEnumerationEnabled" value="true" />
<add key="ResumeList:WorkStatesEnabled" value="true" />
<add key="ResumeList:ExperiencesEnabled" value="true" />
<add key="ResumeList:QidsEnabled" value="true" />
<add key="ResumeList:OrderEnabled" value="true" />
<add key="ResumeList:CompanyIdsEnabled" value="true" />
<add key="ResumeList:UniversityIdsEnabled" value="true" />

<!-- CategoryScraper -->
<add key="Category:Enabled" value="false" />
<add key="Category:EnableTrafficMeasuring" value="true" />
<add key="Category:OutputMode" value="Both" />

<!-- ExpertsScraper -->
<add key="Experts:Enabled" value="false" />
<add key="Experts:ListUrl" value="https://career.habr.com/experts?order=lastActive" />
<add key="Experts:TimeoutSeconds" value="600" />
<add key="Experts:EnableRetry" value="true" />
<add key="Experts:OutputMode" value="Both" />
<add key="Experts:SaveHtml" value="false" />

<!-- CompanyDetailScraper -->
<add key="CompanyDetail:Enabled" value="false" />
<add key="CompanyDetail:TimeoutSeconds" value="60" />
<add key="CompanyDetail:OutputMode" value="Both" />

<!-- UserProfileScraper -->
<add key="UserProfile:Enabled" value="false" />
<add key="UserProfile:TimeoutSeconds" value="60" />
<add key="UserProfile:OutputMode" value="Both" />

<!-- UserFriendsScraper -->
<add key="UserFriends:Enabled" value="false" />
<add key="UserFriends:TimeoutSeconds" value="60" />
<add key="UserFriends:OnlyPublic" value="true" />
<add key="UserFriends:OutputMode" value="Both" />

<!-- UserResumeDetailScraper (включен по умолчанию) -->
<add key="UserResumeDetail:Enabled" value="true" />
<add key="UserResumeDetail:TimeoutSeconds" value="60" />
<add key="UserResumeDetail:EnableRetry" value="true" />
<add key="UserResumeDetail:EnableTrafficMeasuring" value="true" />
<add key="UserResumeDetail:OutputMode" value="Both" />

<!-- CompanyRatingScraper -->
<add key="CompanyRating:Enabled" value="false" />
<add key="CompanyRating:TimeoutSeconds" value="60" />
<add key="CompanyRating:EnableRetry" value="true" />
<add key="CompanyRating:EnableTrafficMeasuring" value="true" />
<add key="CompanyRating:OutputMode" value="Both" />

<!-- Program / AdaptiveConcurrency / FreeProxy logging -->
<add key="Program:OutputMode" value="Both" />
<add key="AdaptiveConcurrency:OutputMode" value="Both" />
<add key="FreeProxy:OutputMode" value="Both" />
```

### Proxy Configuration

#### Static Proxy Rotation

```xml
<add key="Proxy:Enabled" value="false" />
<add key="Proxy:List" value="" />
<!-- Формат: http://host:port;http://user:pass@host:port;socks5://host:1080 -->
<add key="Proxy:RotationIntervalSeconds" value="0" />
<add key="Proxy:AutoRotate" value="false" />
```

#### Free Proxy Pool

```xml
<add key="FreeProxy:Enabled" value="true" />
<add key="FreeProxy:RefreshIntervalMinutes" value="10" />
<add key="FreeProxy:PoolMaxSize" value="10000" />
<add key="FreeProxy:ListUrl" value="https://free-proxy-list.net/ru/" />
<add key="FreeProxy:ProxyScrapeApiUrl" value="https://api.proxyscrape.com/v4/..." />
<add key="FreeProxy:ProxyScrapeEnabled" value="true" />
<add key="FreeProxy:GeoNodeEnabled" value="true" />
<add key="FreeProxy:GeoNodeApiUrl" value="https://proxylist.geonode.com/api/proxy-list?..." />
<add key="FreeProxy:WaitTimeoutSeconds" value="30" />
<add key="FreeProxy:RequestTimeoutSeconds" value="420" />
<add key="FreeProxy:MaxRetries" value="2" />
<add key="FreeProxy:MaxSwitches" value="3000" />
```

#### Proxy Whitelist

```xml
<add key="ProxyWhitelist:Enabled" value="true" />
<add key="ProxyWhitelist:StorageType" value="file" /><!-- file или database -->
<add key="ProxyWhitelist:FilePath" value="./data/proxy_whitelist.json" />
<add key="ProxyWhitelist:CooldownHours" value="24" />
<add key="ProxyWhitelist:RecheckIntervalMinutes" value="60" />
<add key="ProxyWhitelist:MaxRetryAttempts" value="5" />
<add key="ProxyWhitelist:DailyLimitMessage" value="Вы исчерпали суточный лимит на просмотр профилей специалистов" />
<add key="ProxyWhitelist:AutosaveIntervalMinutes" value="20" />
```

### Traffic Statistics

```xml
<add key="Traffic:OutputFile" value="./logs/traffic_stats.txt" />
<add key="Traffic:SaveIntervalMinutes" value="5" />
```

### Proxy Statistics

```xml
<add key="ProxyStats:OutputFile" value="./logs/proxy_stats.txt" />
<add key="ProxyStats:SaveIntervalMinutes" value="10" />
```

### Logging

```xml
<add key="Logging:OutputDirectory" value="./logs" />
```

### Level Validation

```xml
<add key="Levels:ValidTitles" value="Стажёр (Intern),Младший (Junior),Средний (Middle),Старший (Senior),Ведущий (Lead),..." />
```

### Education Parsing

```xml
<add key="Education:SectionTitleText" value="Высшее образование" />
<add key="Education:SectionSelector" value=".content-section" />
<add key="Education:ItemSelector" value=".resume-education-item" />
<add key="Education:UniversityLinkSelector" value="a[href*='/universities/']" />
<add key="Education:UniversityIdRegex" value="/universities/(\d+)" />

<add key="AdditionalEducation:SectionTitleText" value="Дополнительное образование" />
<add key="AdditionalEducation:ContainerSelector" value=".resume-educations" />
<add key="AdditionalEducation:ItemSelector" value=".resume-educations__item" />
```

## 🌐 Proxy Formats Supported

- `http://host:port`
- `https://host:port`
- `socks4://host:port`
- `socks5://host:port`
- `http://username:password@host:port` (with authentication)

## 📋 Current Scraper Status

| Scraper | Setting | Default |
|---------|---------|---------|
| UserResumeDetailScraper | `UserResumeDetail:Enabled` | `true` |
| BruteForceUsernameScraper | `BruteForce:Enabled` | `false` |
| CompanyListScraper | `Companies:Enabled` | `false` |
| CompanyFollowersScraper | `CompanyFollowers:Enabled` | `false` |
| ResumeListPageScraper | `ResumeList:Enabled` | `false` |
| CategoryScraper | `Category:Enabled` | `false` |
| ExpertsScraper | `Experts:Enabled` | `false` |
| CompanyDetailScraper | `CompanyDetail:Enabled` | `false` |
| UserProfileScraper | `UserProfile:Enabled` | `false` |
| UserFriendsScraper | `UserFriends:Enabled` | `false` |
| CompanyRatingScraper | `CompanyRating:Enabled` | `false` |

## 🔍 Configuration Validation

The system reads configuration on startup via `AppConfig.cs`. Missing keys will return default values (typically `false` for enabled flags, empty for strings). No automatic validation is performed — invalid values may cause runtime errors.

## Примеры конфигурации

### Только UserResumeDetailScraper с прокси

```xml
<add key="UserResumeDetail:Enabled" value="true" />
<add key="FreeProxy:Enabled" value="true" />
<add key="FreeProxy:RefreshIntervalMinutes" value="10" />
<add key="ProxyWhitelist:Enabled" value="true" />
```

### Полный сбор данных (все скраперы)

```xml
<add key="ResumeList:Enabled" value="true" />
<add key="Companies:Enabled" value="true" />
<add key="CompanyFollowers:Enabled" value="true" />
<add key="Category:Enabled" value="true" />
<add key="Experts:Enabled" value="true" />
<add key="CompanyDetail:Enabled" value="true" />
<add key="UserProfile:Enabled" value="true" />
<add key="UserFriends:Enabled" value="true" />
<add key="UserResumeDetail:Enabled" value="true" />
<add key="CompanyRating:Enabled" value="true" />
```

## Дополнительная информация

- [USER_RESUME_DETAIL_SCRAPER.md](USER_RESUME_DETAIL_SCRAPER.md) - UserResumeDetailScraper documentation
- [DYNAMIC_PROXY.md](DYNAMIC_PROXY.md) - Free proxy pool
- [USERRESUME_WITH_PROXY.md](USERRESUME_WITH_PROXY.md) - Proxy usage with resume scraper
- [App.config](../JobBoardScraper/App.config) - Configuration file