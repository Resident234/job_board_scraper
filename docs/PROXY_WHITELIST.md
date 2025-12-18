# Proxy Whitelist Manager

Умная система управления прокси с белым списком для обхода суточных лимитов Habr Career.

## Проблема

Habr Career ограничивает количество просмотров профилей в сутки для одного IP. При достижении лимита выводится сообщение:
> "Вы исчерпали суточный лимит на просмотр профилей специалистов"

## Решение

`ProxyWhitelistManager` реализует умную ротацию прокси:
- Рабочие прокси сохраняются в белый список
- Приоритет отдаётся проверенным прокси из белого списка
- Автоматическое переключение при достижении лимита
- Удаление нерабочих прокси после N неудачных попыток

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
1. Получить прокси из белого списка (если cooldown прошёл)
2. Использовать прокси пока работает
3. При сообщении "суточный лимит":
   - Добавить прокси в белый список (если его там нет)
   - Переключиться на следующий прокси
4. При ошибке соединения → увеличить счётчик попыток
5. После MaxRetryAttempts неудач → удалить из белого списка
6. Если белый список исчерпан → использовать общий пул
7. Через RecheckInterval → вернуться к белому списку
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

Все события логируются с префиксом `[WHITELIST]`:

| Событие | Формат лога |
|---------|-------------|
| Прокси добавлен в белый список | `★ Прокси перемещён в белый список: {url}` |
| Прокси взят в использование | `→ Прокси из белого списка взят в использование: {url}` |
| Прокси не отвечает | `⚠ Прокси из белого списка не отвечает, попытка #N/M: {url}` |
| Прокси удалён | `✗ Прокси удалён из белого списка после N попыток: {url}` |
| Суточный лимит | `Суточный лимит исчерпан для прокси: {url}` |
| Переключение пула | `Белый список исчерпан, переключение на общий пул` |
| Возврат к белому списку | `Интервал перепроверки прошёл, возврат к белому списку` |
| Автосохранение | `Дамп в JSON файл выполнен (N прокси)` |
| Статистика | `Активный пул: X | Белый список: N | Общий пул: M` |

## Интеграция

`ProxyWhitelistManager` интегрирован в `UserResumeDetailScraper`:

```csharp
// Создание менеджера
var storage = new JsonWhitelistStorage(AppConfig.ProxyWhitelistFilePath);
var whitelistManager = new ProxyWhitelistManager(storage, proxyPool);
await whitelistManager.LoadStateAsync();

// Использование в скрапере
var scraper = new UserResumeDetailScraper(
    httpClient, db, getUserCodes, controller,
    proxyPool: proxyPool,
    proxyWhitelistManager: whitelistManager
);
```

## Файлы

| Файл | Описание |
|------|----------|
| `ProxyWhitelistManager.cs` | Основной менеджер |
| `JsonWhitelistStorage.cs` | Хранилище в JSON |
| `IWhitelistStorage.cs` | Интерфейс хранилища |
| `WhitelistProxyEntry.cs` | Модель записи |
| `./data/proxy_whitelist.json` | Файл белого списка |
