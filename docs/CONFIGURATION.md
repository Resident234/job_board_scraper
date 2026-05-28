# Configuration Guide

## 📋 Configuration Overview

JobBoardScraper provides flexible configuration options through multiple layers, allowing you to customize every aspect of the scraping process.

## 🔧 Configuration Layers

### 1. App.config (Primary Configuration)

Location: `JobBoardScraper/App.config`

```xml
<configuration>
    <appSettings>
        <!-- Database Configuration -->
        <add key="Database:ConnectionString" value="Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;"/>

        <!-- Scraper Settings -->
        <add key="Experts:Enabled" value="true"/>
        <add key="Experts:Interval" value="4.00:00:00"/>
        <add key="Experts:OutputMode" value="Both"/>

        <add key="Companies:Enabled" value="false"/>
        <add key="Companies:Interval" value="1.00:00:00"/>

        <add key="Category:Enabled" value="false"/>
        <add key="Category:Interval" value="7.00:00:00"/>

        <!-- Proxy Configuration -->
        <add key="Proxy:Enabled" value="true"/>
        <add key="Proxy:List" value="http://proxy1.example.com:8080;http://proxy2.example.com:8080"/>
        <add key="Proxy:RotationInterval" value="00:05:00"/>

        <!-- Performance Settings -->
        <add key="Request:Timeout" value="00:01:30"/>
        <add key="Request:MaxRetries" value="3"/>
        <add key="Request:DelayBetweenRequests" value="00:00:02"/>

        <!-- Logging Configuration -->
        <add key="Logging:Level" value="Information"/>
        <add key="Logging:FilePath" value="logs/JobBoardScraper_{Date}.log"/>
        <add key="Logging:MaxFileSize" value="10485760"/> <!-- 10MB -->
    </appSettings>
</configuration>
```

### 2. Environment Variables

You can override any configuration setting using environment variables:

```bash
# Set database connection string
export DATABASE__CONNECTIONSTRING="Server=prod-db;Database=jobs;User Id=admin;Password=secret;"

# Enable specific scrapers
export EXPERTS__ENABLED=true
export COMPANIES__ENABLED=false

# Configure proxy
export PROXY__ENABLED=true
export PROXY__LIST="http://proxy1:8080;http://proxy2:8080"
```

### 3. Command Line Arguments

Basic settings can be overridden via command line:

```bash
dotnet run --project JobBoardScraper -- --experts-enabled true --companies-enabled false
```

## 🎛️ Scraper Configuration

### Experts Scraper

```xml
<!-- Basic configuration -->
<add key="Experts:Enabled" value="true"/>
<add key="Experts:Interval" value="4.00:00:00"/> <!-- Run every 4 days -->
<add key="Experts:OutputMode" value="Both"/> <!-- Console, File, or Both -->

<!-- Advanced settings -->
<add key="Experts:MaxPages" value="50"/>
<add key="Experts:RequestDelay" value="00:00:01"/>
<add key="Experts:UserAgent" value="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"/>
```

### Company Scraper

```xml
<add key="Companies:Enabled" value="true"/>
<add key="Companies:Interval" value="1.00:00:00"/> <!-- Run daily -->
<add key="Companies:MaxConcurrentRequests" value="2"/>
<add key="Companies:IncludeSkills" value="true"/>
<add key="Companies:IncludeRatings" value="true"/>
```

### Category Scraper

```xml
<add key="Category:Enabled" value="true"/>
<add key="Category:Interval" value="7.00:00:00"/> <!-- Run weekly -->
<add key="Category:UpdateExisting" value="true"/>
```

## 🌐 Proxy Configuration

### Basic Proxy Setup

```xml
<add key="Proxy:Enabled" value="true"/>
<add key="Proxy:List" value="http://proxy1.example.com:8080;http://proxy2.example.com:8080"/>
<add key="Proxy:RotationInterval" value="00:05:00"/> <!-- Rotate every 5 minutes -->
```

### Advanced Proxy Options

```xml
<!-- Authentication -->
<add key="Proxy:List" value="http://user:pass@proxy1.example.com:8080;socks5://user:pass@proxy2.example.com:1080"/>

<!-- Health check settings -->
<add key="Proxy:HealthCheckInterval" value="00:01:00"/>
<add key="Proxy:MaxFailuresBeforeRemoval" value="3"/>
<add key="Proxy:HealthCheckUrl" value="https://www.google.com"/>

<!-- Rotation strategy -->
<add key="Proxy:RotationStrategy" value="RoundRobin"/> <!-- RoundRobin or Random -->
<add key="Proxy:MinProxiesForRotation" value="2"/>
```

### Proxy Formats Supported

- `http://host:port`
- `https://host:port`
- `socks4://host:port`
- `socks5://host:port`
- `http://username:password@host:port` (with authentication)

## 📊 Performance Configuration

### Request Settings

```xml
<!-- Timeout settings -->
<add key="Request:Timeout" value="00:01:30"/> <!-- 90 seconds -->
<add key="Request:ConnectTimeout" value="00:00:15"/> <!-- 15 seconds -->

<!-- Retry policy -->
<add key="Request:MaxRetries" value="3"/>
<add key="Request:RetryDelay" value="00:00:05"/> <!-- 5 seconds -->
<add key="Request:RetryBackoffMultiplier" value="2.0"/> <!-- Exponential backoff -->

<!-- Rate limiting -->
<add key="Request:DelayBetweenRequests" value="00:00:02"/> <!-- 2 seconds -->
<add key="Request:RandomDelayVariation" value="00:00:01"/> <!-- ±1 second randomness -->
```

### Concurrency Settings

```xml
<!-- Global concurrency -->
<add key="Concurrency:MaxParallelScrapers" value="3"/>
<add key="Concurrency:MaxRequestsPerDomain" value="2"/>

<!-- Database connection pool -->
<add key="Database:MaxPoolSize" value="20"/>
<add key="Database:CommandTimeout" value="30"/>
```

## 📁 Logging Configuration

### Basic Logging

```xml
<add key="Logging:Level" value="Information"/> <!-- Debug, Information, Warning, Error, Critical -->
<add key="Logging:FilePath" value="logs/JobBoardScraper_{Date}.log"/>
<add key="Logging:MaxFileSize" value="10485760"/> <!-- 10MB -->
<add key="Logging:MaxRetainedFiles" value="30"/>
```

### Advanced Logging

```xml
<!-- Console logging -->
<add key="Logging:ConsoleEnabled" value="true"/>
<add key="Logging:ConsoleLevel" value="Information"/>

<!-- File logging -->
<add key="Logging:FileEnabled" value="true"/>
<add key="Logging:FileLevel" value="Debug"/>

<!-- Structured logging -->
<add key="Logging:UseJsonFormat" value="true"/>
<add key="Logging:IncludeScopes" value="true"/>
```

## 🔧 Database Configuration

### Connection Strings

```xml
<!-- Basic connection -->
<add key="Database:ConnectionString" value="Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;"/>

<!-- With additional parameters -->
<add key="Database:ConnectionString" value="Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;Pooling=true;MinPoolSize=5;MaxPoolSize=20;Timeout=30;"/>
```

### Database Schema Settings

```xml
<add key="Database:AutoCreateTables" value="false"/>
<add key="Database:AutoUpdateSchema" value="false"/>
<add key="Database:EnableMigrations" value="true"/>
```

## 🛡️ Security Configuration

### API Security

```xml
<add key="Security:RequireApiKey" value="true"/>
<add key="Security:ApiKey" value="your-secure-api-key-here"/>
<add key="Security:AllowedIps" value="127.0.0.1;192.168.1.0/24"/>
```

### Rate Limiting

```xml
<add key="Security:EnableRateLimiting" value="true"/>
<add key="Security:MaxRequestsPerMinute" value="60"/>
<add key="Security:RateLimitWindow" value="00:01:00"/>
```

## 📋 Current Scraper Configuration (Текущая конфигурация скраперов)

### ✅ Enabled Scrapers (Включенные скраперы)

#### UserResumeDetailScraper
- **Status:** ✅ Enabled (Включен)
- **Setting:** `UserResumeDetail:Enabled = true`
- **Description:** Extracts detailed information from user resumes (Извлечение детальной информации из резюме пользователей)
- **Features:**
  - "About me" text (Текст "О себе")
  - Skills list (Список навыков)
  - Work experience with companies (Опыт работы с компаниями)
  - Positions and skills by roles (Должности и навыки по позициям)

### ❌ Disabled Scrapers (Отключенные скраперы)

| Scraper | Setting | Description |
|---------|-----------|----------|
| BruteForceUsernameScraper | `BruteForce:Enabled = false` | Username enumeration (Перебор имен пользователей) |
| CompanyListScraper | `Companies:Enabled = false` | Company list (Список компаний) |
| CompanyFollowersScraper | `CompanyFollowers:Enabled = false` | Company followers (Подписчики компаний) |
| ResumeListPageScraper | `ResumeList:Enabled = false` | Resume list (Список резюме) |
| CategoryScraper | `Category:Enabled = false` | Categories (Категории) |
| ExpertsScraper | `Experts:Enabled = false` | Experts (Эксперты) |
| CompanyDetailScraper | `CompanyDetail:Enabled = false` | Company details (Детали компаний) |
| UserProfileScraper | `UserProfile:Enabled = false` | User profiles (Профили пользователей) |
| UserFriendsScraper | `UserFriends:Enabled = false` | User friends (Друзья пользователей) |
| CompanyRatingScraper | `CompanyRating:Enabled = false` | Company ratings (Рейтинги компаний) |

### UserResumeDetailScraper Settings (Настройки UserResumeDetailScraper)

```xml
<add key="UserResumeDetail:Enabled" value="true" />
<add key="UserResumeDetail:TimeoutSeconds" value="60" />
<add key="UserResumeDetail:EnableRetry" value="true" />
<add key="UserResumeDetail:EnableTrafficMeasuring" value="true" />
<add key="UserResumeDetail:OutputMode" value="Both" />
```

### How to Change Configuration (Как изменить конфигурацию)

#### Enable another scraper:
1. Open `JobBoardScraper/App.config`
2. Find the desired scraper (e.g., `CompanyRating:Enabled`)
3. Change `value="false"` to `value="true"`
4. Save the file

#### Disable UserResumeDetailScraper:
```xml
<add key="UserResumeDetail:Enabled" value="false" />
```

#### Enable multiple scrapers simultaneously:
```xml
<add key="UserResumeDetail:Enabled" value="true" />
<add key="UserProfile:Enabled" value="true" />
<add key="CompanyDetail:Enabled" value="true" />
```

### Running (Запуск)

After changing the configuration, simply run the application:

```bash
dotnet run --project JobBoardScraper
```

The application will automatically start only the enabled scrapers.

### Configuration Check (Проверка конфигурации)

On startup, the application will display information about enabled scrapers:

```
[Program] UserResumeDetailScraper: Enabled
[Program] CompanyRatingScraper: Disabled
[Program] ExpertsScraper: Disabled
...
```

### Additional Information (Дополнительная информация)

- [USER_RESUME_DETAIL_SCRAPER.md](docs/USER_RESUME_DETAIL_SCRAPER.md) - UserResumeDetailScraper documentation
- [App.config](JobBoardScraper/App.config) - Configuration file

## 🚀 Advanced Configuration

### Custom Headers

```xml
<add key="Request:CustomHeaders" value="Accept-Language=en-US;Accept-Encoding=gzip;X-Requested-With=XMLHttpRequest"/>
```

### User Agent Rotation

```xml
<add key="Request:UserAgentRotation" value="true"/>
<add key="Request:UserAgentList" value="
    Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36,
    Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36,
    Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36
"/>
```

### Adaptive Scraping

```xml
<add key="Adaptive:EnableSmartThrottling" value="true"/>
<add key="Adaptive:SuccessRateTarget" value="0.95"/> <!-- 95% success rate -->
<add key="Adaptive:MinDelay" value="00:00:01"/> <!-- 1 second -->
<add key="Adaptive:MaxDelay" value="00:00:10"/> <!-- 10 seconds -->
```

## 📊 Configuration Examples

### Production Configuration

```xml
<configuration>
    <appSettings>
        <!-- Production database -->
        <add key="Database:ConnectionString" value="Server=prod-db.example.com:5432;User Id=scraper;Password=secure-password;Database=jobs_prod;SSL Mode=Require;"/>

        <!-- All scrapers enabled -->
        <add key="Experts:Enabled" value="true"/>
        <add key="Companies:Enabled" value="true"/>
        <add key="Category:Enabled" value="true"/>

        <!-- Aggressive proxy rotation -->
        <add key="Proxy:Enabled" value="true"/>
        <add key="Proxy:List" value="http://premium-proxy1:8080;http://premium-proxy2:8080;http://premium-proxy3:8080"/>
        <add key="Proxy:RotationInterval" value="00:01:00"/>

        <!-- Conservative rate limiting -->
        <add key="Request:DelayBetweenRequests" value="00:00:03"/>
        <add key="Request:MaxRetries" value="5"/>

        <!-- Comprehensive logging -->
        <add key="Logging:Level" value="Information"/>
        <add key="Logging:FilePath" value="logs/production/JobBoardScraper_{Date}.log"/>
    </appSettings>
</configuration>
```

### Development Configuration

```xml
<configuration>
    <appSettings>
        <!-- Local development database -->
        <add key="Database:ConnectionString" value="Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs_dev;"/>

        <!-- Only experts scraper for testing -->
        <add key="Experts:Enabled" value="true"/>
        <add key="Experts:Interval" value="00:10:00"/> <!-- Every 10 minutes for testing -->
        <add key="Experts:MaxPages" value="5"/> <!-- Limit pages for development -->

        <!-- No proxy for local testing -->
        <add key="Proxy:Enabled" value="false"/>

        <!-- Fast requests for development -->
        <add key="Request:DelayBetweenRequests" value="00:00:00"/>
        <add key="Request:MaxRetries" value="1"/>

        <!-- Detailed logging for debugging -->
        <add key="Logging:Level" value="Debug"/>
        <add key="Logging:UseJsonFormat" value="false"/>
    </appSettings>
</configuration>
```

## 🔍 Configuration Validation

The system automatically validates configuration on startup:

- **Required fields**: Ensures all mandatory settings are present
- **Format validation**: Validates URLs, time spans, etc.
- **Range checking**: Ensures values are within acceptable ranges
- **Dependency checking**: Validates that dependent configurations are consistent

### Common Validation Errors

| Error | Solution |
|-------|----------|
| `Missing required configuration: Database:ConnectionString` | Add the missing configuration key |
| `Invalid timespan format: Experts:Interval` | Use format `DD.HH:MM:SS` |
| `Proxy list cannot be empty when Proxy:Enabled=true` | Provide at least one proxy or disable proxy |
| `MaxRetries must be between 1 and 10` | Adjust the value to be within range |

## 🛠️ Configuration Management

### Best Practices

1. **Use environment-specific configurations**
   - Separate configs for development, testing, production
   - Use environment variables for sensitive data

2. **Version control**
   - Store configuration templates in version control
   - Exclude files with secrets using `.gitignore`

3. **Security**
   - Never commit passwords or API keys
   - Use secret management systems in production
   - Rotate credentials regularly

4. **Documentation**
   - Document non-obvious configuration choices
   - Maintain a configuration change log
   - Include examples for complex settings

### Configuration Files

- `App.config` - Main configuration (committed to repo)
- `App.secrets.config` - Secret configuration (excluded from repo)
- `App.Development.config` - Development overrides
- `App.Production.config` - Production overrides

## 📚 Reference

### Configuration Keys Reference

| Category | Key | Type | Default | Description |
|----------|-----|------|---------|-------------|
| **Database** | `Database:ConnectionString` | string | - | PostgreSQL connection string |
| **Experts** | `Experts:Enabled` | bool | false | Enable experts scraper |
| **Experts** | `Experts:Interval` | TimeSpan | 4.00:00:00 | Scraping interval |
| **Proxy** | `Proxy:Enabled` | bool | false | Enable proxy rotation |
| **Proxy** | `Proxy:List` | string | "" | Semicolon-separated proxy list |
| **Request** | `Request:Timeout` | TimeSpan | 00:01:30 | Request timeout |
| **Logging** | `Logging:Level` | string | "Information" | Logging level |

### TimeSpan Format

All time-based configurations use the format: `DD.HH:MM:SS`

- `00:00:05` - 5 seconds
- `00:01:30` - 1 minute 30 seconds
- `01:00:00` - 1 hour
- `1.00:00:00` - 1 day

This comprehensive configuration system provides the flexibility needed to adapt JobBoardScraper to various environments and requirements while maintaining robust defaults for quick deployment.