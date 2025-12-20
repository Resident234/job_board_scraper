# Proxy Whitelist Manager

Умная система управления прокси с белым списком для обхода суточных лимитов Habr Career.

## Проблема

Habr Career ограничивает количество просмотров профилей в сутки для одного IP. При достижении лимита выводится сообщение:
> "Вы исчерпали суточный лимит на просмотр профилей специалистов"

## Архитектура (v2.0)

Система разделена на три компонента с чёткими зонами ответственности:

```
┌─────────────────────────────────────────────────────────┐
│              ProxyCoordinator                           │
│  - Выбирает из какого менеджера брать прокси            │
│  - Маршрутизирует события (success/failure/limit)       │
│  - Переключает между пулами                             │
└─────────────────────────────────────────────────────────┘
           │                           │
           ▼                           ▼
┌─────────────────────┐    ┌─────────────────────────────┐
│ WhitelistManager    │    │ GeneralPoolManager          │
│                     │    │                             │
│ - Проверенные прокси│◄───│ - Свежие прокси             │
│ - JSON persistence  │    │ - Blacklist                 │
│ - Cooldown логика   │    │ - События верификации       │
└─────────────────────┘    └─────────────────────────────┘
           │                           │
           │      OnProxyVerified      │
           └───────────────────────────┘
```

### Компоненты

| Компонент | Ответственность |
|-----------|-----------------|
| `ProxyCoordinator` | Координация между whitelist и general pool, маршрутизация событий |
| `ProxyWhitelistManager` | Управление проверенными прокси, cooldown, persistence |
| `GeneralPoolManager` | Управление свежими прокси, blacklist, события верификации |

### Обмен данными

Когда прокси из general pool достигает суточного лимита (значит работает):
1. `GeneralPoolManager` вызывает событие `OnProxyVerified`
2. `ProxyCoordinator` получает событие и вызывает `WhitelistManager.ReportDailyLimitReached`
3. Прокси автоматически добавляется в whitelist

## Конфигурация (App.config)

```xml
<!-- Proxy Whitelist Settings -->
<add key="ProxyWhitelist:Enabled" value="true" />
<add key="ProxyWhitelist:StorageType" value="file" />
<add key="ProxyWhitelist:FilePath" value="./data/proxy_whitelist.json" />
<add key="ProxyWhitelist:CooldownHours" value="24" />
<add key="ProxyWhitelist:RecheckIntervalMinutes" value="60" />
<add key="ProxyWhitelist:MaxRetryAttempts" value="5" />
<add key="ProxyWhitelist:AutosaveIntervalMinutes" value="20" />
<add key="ProxyWhitelist:DailyLimitMessage" value="Вы исчерпали суточный лимит..." />
```

| Параметр | Описание | По умолчанию |
|----------|----------|--------------|
| `Enabled` | Включить белый список | `true` |
| `StorageType` | Тип хранилища (`file`) | `file` |
| `FilePath` | Путь к JSON файлу | `./data/proxy_whitelist.json` |
| `CooldownHours` | Период блокировки IP (часы) | `24` |
| `RecheckIntervalMinutes` | Интервал перепроверки белого списка | `60` |
| `MaxRetryAttempts` | Макс. попыток до удаления из списка | `5` |
| `AutosaveIntervalMinutes` | Интервал автосохранения | `20` |

## Алгоритм работы

```
1. ProxyCoordinator.GetNextProxy():
   a. Если есть текущий рабочий прокси → вернуть его
   b. Попробовать WhitelistManager (проверенные прокси)
   c. Если whitelist исчерпан → переключиться на GeneralPoolManager
   d. Через RecheckInterval → вернуться к whitelist

2. При успехе (ReportSuccess):
   - Прокси остаётся текущим для повторного использования
   - Сбрасывается счётчик ошибок

3. При ошибке (ReportFailure):
   - Увеличивается счётчик попыток
   - После MaxRetryAttempts → удаление из whitelist / добавление в blacklist
   - Текущий прокси сбрасывается → следующий вызов GetNextProxy вернёт другой

4. При суточном лимите (ReportDailyLimitReached):
   - Прокси добавляется в whitelist (если из general pool)
   - Текущий прокси сбрасывается → переключение на следующий
```

**Важно:** Прокси добавляется в белый список только при достижении суточного лимита, а не при первом успешном запросе. Это гарантирует, что в белый список попадают только проверенные прокси, которые отработали полный цикл до лимита.

## Формат хранения (JSON)

```json
[
  {
    "ProxyUrl": "http://proxy1:8080",
    "LastUsed": "2024-12-17T10:30:00Z",
    "IsFailed": false,
    "RetryCount": 0,
    "FailedSince": null
  }
]
```

## Логирование

События логируются с префиксами по компонентам:

### [COORDINATOR]
| Событие | Формат лога |
|---------|-------------|
| Прокси из whitelist | `Whitelist → {url}` |
| Прокси из pool | `General Pool → {url}` |
| Переключение на pool | `Whitelist исчерпан → General Pool` |
| Возврат к whitelist | `Возврат к whitelist` |
| Прокси верифицирован | `★ Прокси верифицирован, добавляем в whitelist: {url}` |

### [WHITELIST]
| Событие | Формат лога |
|---------|-------------|
| Прокси добавлен | `★ Добавлен в whitelist: {url}` |
| Прокси OK | `✓ Прокси OK: {url}` |
| Ошибка | `⚠ Ошибка #N/M: {url}` |
| Удалён | `✗ Удалён после N попыток: {url}` |
| Автосохранение | `Дамп в JSON (N прокси)` |

### [GENERAL]
| Событие | Формат лога |
|---------|-------------|
| Прокси выбран | `→ Прокси: {url}` |
| Прокси OK | `✓ Прокси OK: {url}` |
| Ошибка | `⚠ Ошибка #N/M: {url}` |
| В blacklist | `✗ В blacklist: {url}` |
| Верифицирован | `★ Прокси достиг лимита (работает!): {url}` |

## Интеграция

`ProxyCoordinator` интегрирован в `UserResumeDetailScraper`:

```csharp
// Создание компонентов
var whitelistStorage = new JsonWhitelistStorage(AppConfig.ProxyWhitelistFilePath);
var whitelistManager = new ProxyWhitelistManager(whitelistStorage);
await whitelistManager.LoadStateAsync();

var generalPoolManager = new GeneralPoolManager(freeProxyPool);
var proxyCoordinator = new ProxyCoordinator(whitelistManager, generalPoolManager);

// Использование в скрапере
var scraper = new UserResumeDetailScraper(
    httpClient, db, getUserCodes, controller,
    proxyCoordinator: proxyCoordinator
);
```

## Файлы

| Файл | Описание |
|------|----------|
| `IProxyManager.cs` | Общий интерфейс для менеджеров |
| `ProxyCoordinator.cs` | Координатор между пулами |
| `ProxyWhitelistManager.cs` | Менеджер белого списка |
| `GeneralPoolManager.cs` | Менеджер общего пула |
| `JsonWhitelistStorage.cs` | Хранилище в JSON |
| `IWhitelistStorage.cs` | Интерфейс хранилища |
| `WhitelistProxyEntry.cs` | Модель записи |
| `./data/proxy_whitelist.json` | Файл белого списка |

## Миграция с v1.0

Если вы использовали старую версию с `ProxyWhitelistManager(storage, pool)`:

```csharp
// Было (v1.0):
var manager = new ProxyWhitelistManager(storage, pool);

// Стало (v2.0):
var whitelistManager = new ProxyWhitelistManager(storage);
var generalPoolManager = new GeneralPoolManager(pool);
var coordinator = new ProxyCoordinator(whitelistManager, generalPoolManager);
```

Формат JSON файла белого списка не изменился — миграция данных не требуется.
