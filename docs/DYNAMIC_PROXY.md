# Free Proxy Pool - динамическое управление прокси

## 📋 Обзор

В проекте реализована система автоматического сбора, ротации и управления бесплатными прокси-серверами. Система состоит из нескольких компонентов, работающих совместно для обеспечения бесперебойного сбора данных с Habr Career.

## 🏗️ Архитектура

```
┌─────────────────────────────────────────────────────────────────────┐
│                        ProxyCoordinator                              │
│  Выбирает источник прокси по приоритету:                            │
│  1. ProxyWhitelistManager - проверенные рабочие прокси              │
│  2. GeneralPoolManager - общий пул из бесплатных источников         │
└─────────────────────────────────────────────────────────────────────┘
         │                    │
         ▼                    ▼
┌─────────────────┐  ┌──────────────────────────────────┐
│ ProxyWhitelist- │  │     GeneralPoolManager            │
│ Manager         │  │  (управляет пулом FreeProxyPool)   │
│ - JSON storage  │  │                                  │
│ - Cooldown      │  │  ┌──────────────────────────┐    │
│ - Retry limit   │  │  │     FreeProxyPool         │    │
│ - daily limit   │  │  │  (ConcurrentQueue<Proxy>) │    │
└─────────────────┘  │  └──────────────────────────┘    │
                     │           │                      │
                     │           ▼                      │
                     │  ┌──────────────────────────┐    │
                     │  │  ProxyScraperLauncher     │    │
                     │  │  Запускает фоновые задачи:│    │
                     │  │  - FreeProxyListScraper   │    │
                     │  │  - ProxyScrapeScraper     │    │
                     │  └──────────────────────────┘    │
                     └──────────────────────────────────┘
```

## 📦 Компоненты

### 1. FreeProxyPool

Потокобезопасная очередь прокси-серверов. Хранит до `PoolMaxSize` (по умолчанию 10000) прокси.

**Класс:** `JobBoardScraper/Infrastructure/Proxy/ProxyInfo.cs` (модель данных)

**Методы:**
- `TryTake(out ProxyInfo)` — атомарно извлечь прокси из очереди
- `Add(ProxyInfo)` — добавить прокси
- `Count` — текущее количество
- Ограничение максимального размера пула

### 2. FreeProxyListScraper

Парсит HTML-таблицу с `https://free-proxy-list.net/` через AngleSharp.

**Класс:** `JobBoardScraper/Infrastructure/Proxy/FreeProxyListScraper.cs`

**Особенности:**
- Парсит HTML-таблицу с прокси
- Фильтрует только анонимные и элитные прокси (исключает transparent)
- Автоматическое обновление каждые N минут (настраивается через `FreeProxy:RefreshIntervalMinutes`)
- Если в пуле меньше 100 прокси — немедленное обновление

### 3. ProxyScrapeScraper

Загружает прокси из ProxyScrape API.

**Класс:** `JobBoardScraper/Infrastructure/Proxy/ProxyScrapeScraper.cs`

**API URL:**
```
https://api.proxyscrape.com/v4/free-proxy-list/get?request=displayproxies&protocol=http&timeout=10000&country=all&ssl=all&anonymity=all&skip=0&limit=2000
```

**Особенности:**
- Простой GET-запрос к API
- Возвращает список в формате `ip:port`
- Валидация формата IP и порта
- Автоматическое обновление по расписанию

### 4. GeoNode API (опционально)

Дополнительный источник прокси через GeoNode API.

**API URL:**
```
https://proxylist.geonode.com/api/proxy-list?limit=200&page=1&sort_by=lastChecked&sort_type=desc
```

**Настройка:**
```xml
<add key="FreeProxy:GeoNodeEnabled" value="true" />
<add key="FreeProxy:GeoNodeApiUrl" value="https://proxylist.geonode.com/api/proxy-list?..." />
```

### 5. ProxyScraperLauncher

Запускает все прокси-скраперы в фоновом режиме.

**Класс:** `JobBoardScraper/Infrastructure/Proxy/ProxyScraperLauncher.cs`

**Методы:**
- `LaunchAll(...)` — запускает все настроенные источники прокси
- `RegisterStatistics(IProxyManager)` — регистрирует статистику
- `Dispose()` — остановка всех фоновых задач

### 6. ProxyCoordinator

Координатор между whitelist и общим пулом прокси.

**Класс:** `JobBoardScraper/Infrastructure/Proxy/ProxyCoordinator.cs`

**Алгоритм работы:**
1. `GetNextProxy()` — пытается получить прокси из whitelist (приоритет 1)
2. Если whitelist пуст или все на cooldown — берёт из общего пула (приоритет 2)
3. `ReportSuccess(uri)` — прокси отработал успешно, добавляется в whitelist
4. `ReportDailyLimit(uri)` — прокси упёрся в суточный лимит, отправляется на cooldown
5. `ReportFailure(uri)` — ошибка, увеличивается счётчик; после `MaxRetryAttempts` удаляется

### 7. ProxyWhitelistManager

Управляет whitelist'ом проверенных прокси.

**Класс:** `JobBoardScraper/Infrastructure/Proxy/ProxyWhitelistManager.cs`

**Особенности:**
- Хранение в JSON-файле (`./data/proxy_whitelist.json`)
- Cooldown 24 часа после суточного лимита
- Автоматическое удаление после N ошибок
- Автосохранение каждые 20 минут

### 8. GeneralPoolManager

Управляет общим пулом прокси с blacklist'ом и событиями верификации.

**Класс:** `JobBoardScraper/Infrastructure/Proxy/GeneralPoolManager.cs`

**События:**
- `OnProxyVerified` — прокси подтверждён как рабочий (передаётся в whitelist)
- `OnProxyBlacklisted` — прокси забанен после превышения лимита ошибок

## ⚙️ Конфигурация

### Free Proxy Pool

```xml
<!-- Включение бесплатных прокси -->
<add key="FreeProxy:Enabled" value="true" />
<add key="FreeProxy:RefreshIntervalMinutes" value="10" />
<add key="FreeProxy:PoolMaxSize" value="10000" />
<add key="FreeProxy:ListUrl" value="https://free-proxy-list.net/ru/" />

<!-- ProxyScrape API -->
<add key="FreeProxy:ProxyScrapeApiUrl" value="https://api.proxyscrape.com/v4/free-proxy-list/get?request=displayproxies&protocol=http&timeout=10000&country=all&ssl=all&anonymity=all&skip=0&limit=2000" />
<add key="FreeProxy:ProxyScrapeEnabled" value="true" />

<!-- GeoNode API -->
<add key="FreeProxy:GeoNodeEnabled" value="true" />
<add key="FreeProxy:GeoNodeApiUrl" value="https://proxylist.geonode.com/api/proxy-list?limit=200&page=1&sort_by=lastChecked&sort_type=desc" />

<!-- Таймауты и лимиты -->
<add key="FreeProxy:WaitTimeoutSeconds" value="30" />
<add key="FreeProxy:RequestTimeoutSeconds" value="420" />
<add key="FreeProxy:MaxRetries" value="2" />
<add key="FreeProxy:MaxSwitches" value="3000" />
```

### Proxy Whitelist

```xml
<add key="ProxyWhitelist:Enabled" value="true" />
<add key="ProxyWhitelist:StorageType" value="file" />
<add key="ProxyWhitelist:FilePath" value="./data/proxy_whitelist.json" />
<add key="ProxyWhitelist:CooldownHours" value="24" />
<add key="ProxyWhitelist:RecheckIntervalMinutes" value="60" />
<add key="ProxyWhitelist:MaxRetryAttempts" value="5" />
<add key="ProxyWhitelist:DailyLimitMessage" value="Вы исчерпали суточный лимит на просмотр профилей специалистов" />
<add key="ProxyWhitelist:AutosaveIntervalMinutes" value="20" />
```

## 🔌 Интеграция с UserResumeDetailScraper

```csharp
// В Program.cs создаётся ProxyCoordinator если включены прокси
if (proxyScraperLauncher != null)
{
    var whitelistStorage = new JsonWhitelistStorage(AppConfig.ProxyWhitelistFilePath);
    var whitelistManager = new ProxyWhitelistManager(whitelistStorage);
    await whitelistManager.LoadStateAsync();

    var generalPoolManager = new GeneralPoolManager(proxyScraperLauncher.Pool);
    proxyCoordinator = new ProxyCoordinator(whitelistManager, generalPoolManager);
    proxyScraperLauncher.RegisterStatistics(proxyCoordinator);
    proxyCoordinator.StartPeriodicStatsReporting(cts.Token, TimeSpan.FromMinutes(5));
}

// Передаётся в UserResumeDetailScraper
var scraper = new UserResumeDetailScraper(
    httpClient, db, getUserCodes, controller,
    proxyCoordinator: proxyCoordinator,
    interval: TimeSpan.FromMinutes(20),
    outputMode: ...
);
```

## 📊 Статистика источников

`ProxySourceStatistics` собирает информацию о работе каждого источника прокси:

- Количество загруженных прокси
- Количество успешных/неудачных использований
- Время последнего обновления
- Процент успешных прокси

Статистика выводится раз в 5 минут через `ProxyCoordinator.StartPeriodicStatsReporting()`.

## 📋 ProxySourceStatistics

**Класс:** `JobBoardScraper/Infrastructure/Proxy/ProxySourceStatistics.cs`

Отслеживает:
- Имя источника (FreeProxyList, ProxyScrape, GeoNode)
- Количество попыток использования
- Количество успешных запросов
- Количество неудачных запросов
- Последняя ошибка
- Процент успеха

## 📌 Proxy Formats Supported

- `http://host:port`
- `https://host:port`
- `socks4://host:port`
- `socks5://host:port`
- `http://username:password@host:port` (with authentication)

## ⚠️ Важно

- Бесплатные прокси имеют существенные ограничения: низкая надёжность, медленная скорость, частая блокировка
- Рекомендуется для тестирования и небольших объёмов
- Для production-сценариев рекомендованы коммерческие прокси (настройка через `Proxy:List`)

## Связанные документы

- [USERRESUME_WITH_PROXY.md](USERRESUME_WITH_PROXY.md) - использование прокси со скрапером резюме
- [ARCHITECTURE.md](ARCHITECTURE.md) - общая архитектура
- [CONFIGURATION.md](CONFIGURATION.md) - настройки конфигурации