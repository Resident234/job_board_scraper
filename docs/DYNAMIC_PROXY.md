# Динамическое обновление прокси

## 📋 Итоговая сводка: Динамические прокси

### Что было добавлено

#### 1. ProxyProvider - провайдер прокси
Класс для получения и управления списком прокси из различных источников.

**Возможности:**
- ✅ Загрузка из ProxyScrape API (бесплатный)
- ✅ Загрузка из GeoNode API (бесплатный)
- ✅ Загрузка/сохранение из файла
- ✅ Проверка работоспособности прокси
- ✅ Автоматическое удаление нерабочих прокси
- ✅ Thread-safe реализация

#### 2. DynamicProxyRotator - динамический ротатор
Ротатор с автоматическим обновлением списка прокси.

**Возможности:**
- ✅ Автоматическое обновление по расписанию
- ✅ Принудительное обновление по команде
- ✅ Интеграция с ProxyProvider
- ✅ Настраиваемый интервал обновления

#### 3. Расширение HttpClientFactory
Добавлены методы для работы с динамическими прокси:
- `CreateProxyProviderAsync()` - создание провайдера с автозагрузкой
- `CreateDynamicProxyRotatorAsync()` - создание динамического ротатора

#### 4. Документация
- **DYNAMIC_PROXY.md** - полное руководство по динамическим прокси
- **PROXY_SERVICES.md** - обзор коммерческих прокси-сервисов
- **FREE_PROXY_SOURCES.md** - бесплатные источники прокси
- **PROXY_ANONYMITY_LEVELS.md** - уровни анонимности прокси

#### 5. Примеры
- **DynamicProxyExample.cs** - 7 примеров использования

## 🌐 Источники прокси

### ⚠️ Важное предупреждение о бесплатных прокси

Бесплатные прокси имеют существенные ограничения:
- ❌ Низкая надежность (часто не работают)
- ❌ Медленная скорость
- ❌ Могут быть небезопасны (логирование трафика)
- ❌ Часто блокируются сайтами
- ❌ Нестабильная работа

**Рекомендуется использовать только для тестирования!**
Для продакшена используйте [коммерческие сервисы](#коммерческие-для-производства).

---

### Бесплатные (для тестирования):

#### 1. ProxyScrape API ⭐⭐⭐⭐
- **URL:** https://api.proxyscrape.com/
- **API URL:** `https://api.proxyscrape.com/v2/?request=get&protocol=http&timeout=10000&country=all&ssl=all&anonymity=all`
- **Параметры:** `timeout`, `country`, `ssl`, `anonymity`
- **Особенности:** Бесплатный, регулярно обновляется, простой API, без регистрации

#### 2. GeoNode API ⭐⭐⭐⭐
- **URL:** https://proxylist.geonode.com/
- **API URL:** `https://proxylist.geonode.com/api/proxy-list?limit=100&page=1&sort_by=lastChecked&sort_type=desc`
- **Параметры:** `limit`, `page`, `sort_by`, `sort_type`
- **Особенности:** Бесплатный, JSON API, информация о последней проверке, без регистрации

#### 3. Free Proxy List
- **URL:** https://free-proxy-list.net/
- **Особенности:** Обновляется каждые 10 минут, фильтрация по стране, анонимности, HTTPS

#### 4. HideMy.name
- **URL:** https://hidemy.name/ru/proxy-list/
- **Особенности:** Большой список, фильтрация по параметрам, проверка скорости

#### 5. ProxyScan
- **URL:** https://www.proxyscan.io/
- **Особенности:** API доступен, фильтрация по типу, проверка работоспособности

#### 6. Spys.one
- **URL:** https://spys.one/en/
- **Особенности:** Большая база, детальная информация, часто обновляется

#### 7. Локальный файл
- **Формат:** Один прокси на строку, поддержка комментариев (#)
  ```
  http://123.45.67.89:8080
  http://98.76.54.32:3128
  # Быстрые прокси
  http://11.22.33.44:80
  ```

### Коммерческие (для производства):
1. **BrightData** - $500+/месяц, 72M+ IP
2. **Smartproxy** - $75+/месяц, 40M+ IP
3. **Oxylabs** - $300+/месяц, 100M+ IP
4. **ProxyMesh** - $10+/месяц
5. **IPRoyal** - $1.75/GB
6. **Webshare** - Бесплатно (10 прокси)

### Альтернативы с бесплатным пробным периодом:
1. **Smartproxy** - 3 дня бесплатно
2. **Webshare** - 10 прокси бесплатно навсегда
3. **ProxyMesh** - 100 запросов бесплатно

---

## 📋 Реализованные парсеры в проекте

В проекте реализованы автоматические парсеры для получения бесплатных прокси:

### FreeProxyListScraper

**Источник:** https://free-proxy-list.net/

**Класс:** `JobBoardScraper/Infrastructure/Proxy/FreeProxyListScraper.cs`

**Особенности:**
- Парсит HTML-таблицу с прокси через AngleSharp
- Фильтрует только анонимные и элитные прокси (исключает transparent)
- Сортирует по качеству и времени последней проверки
- Автоматическое обновление каждые 10 минут (настраивается)
- Если в пуле меньше 100 прокси — немедленное обновление

**Извлекаемые данные:**
- IP-адрес и порт
- Страна
- Уровень анонимности
- Поддержка HTTPS
- Время последней проверки

### ProxyScrapeScraper

**Источник:** https://api.proxyscrape.com/

**Класс:** `JobBoardScraper/Infrastructure/Proxy/ProxyScrapeScraper.cs`

**Особенности:**
- Простой GET-запрос к API
- Возвращает список в формате `ip:port`
- Автоматическое обновление каждые 10 минут (настраивается)
- Валидация формата IP и порта

**API URL:**
```
https://api.proxyscrape.com/v4/free-proxy-list/get?request=displayproxies&protocol=http&timeout=10000&country=all&ssl=all&anonymity=all
```

---

## 🚀 Быстрый старт

### Базовый пример

```csharp
// 1. Создать провайдер и загрузить прокси
var provider = new ProxyProvider();
await provider.LoadFromProxyScrapeAsync();
await provider.LoadFromGeoNodeAsync(50);

// 2. Проверить и удалить нерабочие
var removed = await provider.RemoveDeadProxiesAsync();
Console.WriteLine($"Удалено нерабочих: {removed}");

// 3. Создать ротатор
var proxies = provider.GetProxies();
var rotator = new ProxyRotator(proxies);

// 4. Использовать
var httpClient = HttpClientFactory.CreateHttpClient(rotator);
var smartClient = new SmartHttpClient(httpClient, "MyScraper", proxyRotator: rotator);
```

### С автообновлением

```csharp
// Создать динамический ротатор с автообновлением каждый час
var provider = await HttpClientFactory.CreateProxyProviderAsync();
var dynamicRotator = new DynamicProxyRotator(
    provider,
    updateInterval: TimeSpan.FromHours(1),
    autoUpdate: true
);

// Получить текущие прокси и создать ротатор
var proxies = dynamicRotator.GetProxies();
var rotator = new ProxyRotator(proxies);

// Использовать
var httpClient = HttpClientFactory.CreateHttpClient(rotator);
var smartClient = new SmartHttpClient(httpClient, "MyScraper", proxyRotator: rotator);
```

### Упрощенный вариант

```csharp
// Одной строкой
var dynamicRotator = await HttpClientFactory.CreateDynamicProxyRotatorAsync(
    updateInterval: TimeSpan.FromHours(1),
    autoUpdate: true
);
```

---

## 📋 Описание
Система поддерживает автоматическое получение и обновление списка прокси из публичных источников.

## Использование

### Базовое использование

```csharp
// Создать провайдер прокси
var provider = new ProxyProvider();

// Загрузить из ProxyScrape
await provider.LoadFromProxyScrapeAsync();

// Загрузить из GeoNode
await provider.LoadFromGeoNodeAsync(limit: 100);

// Загрузить из файла
await provider.LoadFromFileAsync("proxies.txt");

// Получить список
var proxies = provider.GetProxies();
Console.WriteLine($"Загружено {proxies.Count} прокси");

// Создать ротатор
var rotator = new ProxyRotator(proxies);
```

### Автоматическое обновление

```csharp
// Создать динамический ротатор с автообновлением каждый час
var provider = await HttpClientFactory.CreateProxyProviderAsync();
var dynamicRotator = new DynamicProxyRotator(
    provider,
    updateInterval: TimeSpan.FromHours(1),
    autoUpdate: true
);

// Использовать как обычный ротатор
var httpClient = HttpClientFactory.CreateHttpClient(dynamicRotator);
var smartClient = new SmartHttpClient(httpClient, "MyScraper");

// Принудительно обновить список
await dynamicRotator.ForceUpdateAsync();
```

### Проверка работоспособности прокси

```csharp
var provider = new ProxyProvider();
await provider.LoadFromProxyScrapeAsync();

// Проверить один прокси
var isAlive = await provider.TestProxyAsync("http://proxy.example.com:8080");
Console.WriteLine($"Прокси работает: {isAlive}");

// Удалить все нерабочие прокси
var removed = await provider.RemoveDeadProxiesAsync();
Console.WriteLine($"Удалено {removed} нерабочих прокси");
```

### Сохранение и загрузка из файла

```csharp
var provider = new ProxyProvider();

// Загрузить прокси из разных источников
await provider.LoadFromProxyScrapeAsync();
await provider.LoadFromGeoNodeAsync();

// Сохранить в файл
await provider.SaveToFileAsync("proxies.txt");

// Загрузить из файла
var provider2 = new ProxyProvider();
await provider2.LoadFromFileAsync("proxies.txt");
```

---

## 🛠️ Использование бесплатных прокси

### Автоматическая загрузка и проверка

```csharp
// Создать провайдер
var provider = new ProxyProvider();

// Загрузить из разных источников
Console.WriteLine("Загрузка прокси...");
await provider.LoadFromProxyScrapeAsync();
await provider.LoadFromGeoNodeAsync(50);
Console.WriteLine($"Загружено: {provider.GetProxies().Count}");

// Проверить работоспособность
Console.WriteLine("Проверка работоспособности...");
var removed = await provider.RemoveDeadProxiesAsync(maxConcurrent: 20);
Console.WriteLine($"Удалено нерабочих: {removed}");
Console.WriteLine($"Рабочих: {provider.GetProxies().Count}");

// Сохранить рабочие
await provider.SaveToFileAsync("working_proxies.txt");
Console.WriteLine("Рабочие прокси сохранены в working_proxies.txt");
```

### Проверка работоспособности прокси

```csharp
// Проверить один прокси
var provider = new ProxyProvider();
var isAlive = await provider.TestProxyAsync("http://123.45.67.89:8080");
Console.WriteLine($"Прокси работает: {isAlive}");
```

---

## 📋 Рекомендации по использованию

### Для тестирования:
1. Загрузите 20-50 прокси из ProxyScrape или GeoNode
2. Проверьте работоспособность
3. Сохраните рабочие в файл

### Для разработки:
1. Используйте статический список из конфигурации
2. Или локальный прокси (Fiddler, Charles)
3. Не полагайтесь на стабильность

### Для продакшена:
1. **НЕ используйте бесплатные прокси!**
2. Купите коммерческий прокси-сервис
3. См. раздел [Коммерческие (для производства)](#коммерческие-для-производства)

---

## 🎯 Полный пример настройки бесплатных прокси

```csharp
public async Task SetupFreeProxiesAsync()
{
    var provider = new ProxyProvider();

    // 1. Загрузить из разных источников
    Console.WriteLine("Загрузка прокси...");
    await provider.LoadFromProxyScrapeAsync();
    await provider.LoadFromGeoNodeAsync(50);
    Console.WriteLine($"Загружено: {provider.GetProxies().Count}");

    // 2. Проверить работоспособность
    Console.WriteLine("Проверка работоспособности...");
    var removed = await provider.RemoveDeadProxiesAsync(maxConcurrent: 20);
    Console.WriteLine($"Удалено нерабочих: {removed}");
    Console.WriteLine($"Рабочих: {provider.GetProxies().Count}");

    // 3. Сохранить рабочие
    await provider.SaveToFileAsync("working_proxies.txt");
    Console.WriteLine("Рабочие прокси сохранены в working_proxies.txt");

    // 4. Создать ротатор
    var proxies = provider.GetProxies();
    if (proxies.Count > 0)
    {
        var rotator = new ProxyRotator(proxies);
        Console.WriteLine($"Ротатор создан с {rotator.ProxyCount} прокси");
        return rotator;
    }
    else
    {
        Console.WriteLine("⚠️ Не найдено рабочих прокси!");
        return null;
    }
}
```

---

## 📋 Формат файла с прокси

```
# Комментарии начинаются с #
http://proxy1.example.com:8080
http://user:pass@proxy2.example.com:8080
socks5://proxy3.example.com:1080

# Можно группировать
# Быстрые прокси
http://fast1.example.com:8080
http://fast2.example.com:8080

# Медленные прокси
http://slow1.example.com:8080
```

---

## Рекомендации

### Для разработки
- Используйте статический список из конфигурации
- Или локальный прокси (Fiddler)

### Для тестирования
- Загрузите 10-20 прокси из публичных источников
- Проверьте работоспособность
- Сохраните рабочие в файл

### Для продакшена
- Используйте коммерческие прокси-сервисы
- Или комбинируйте: коммерческие + бесплатные
- Настройте автообновление каждые 1-2 часа
- Регулярно проверяйте работоспособность

---

## Ограничения бесплатных прокси

⚠️ **Важно:**
- Бесплатные прокси часто нестабильны
- Низкая скорость
- Могут быть небезопасны
- Часто блокируются сайтами

Для серьезных задач рекомендуется использовать коммерческие прокси-сервисы:
- BrightData (Luminati)
- Smartproxy
- Oxylabs
- ProxyMesh

---

## Мониторинг

```csharp
// Периодически выводить статистику
var timer = new System.Timers.Timer(60000); // каждую минуту
timer.Elapsed += (s, e) =>
{
    var proxies = provider.GetProxies();
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Доступно прокси: {proxies.Count}");
};
timer.Start();
```

---

## Отладка

```csharp
// Включить подробное логирование
var provider = new ProxyProvider();

// Загрузить с логированием
var count1 = await provider.LoadFromProxyScrapeAsync();
Console.WriteLine($"ProxyScrape: {count1} прокси");

var count2 = await provider.LoadFromGeoNodeAsync();
Console.WriteLine($"GeoNode: {count2} прокси");

// Проверить каждый прокси
var proxies = provider.GetProxies();
foreach (var proxy in proxies)
{
    var isAlive = await provider.TestProxyAsync(proxy);
    Console.WriteLine($"{proxy}: {(isAlive ? "✓" : "✗")}");
}
```

---

## 🎉 Заключение

Бесплатные прокси подходят только для:
- ✅ Тестирования
- ✅ Разработки
- ✅ Обучения
- ✅ Небольших экспериментов

Для серьезных задач используйте [коммерческие прокси-сервисы](#коммерческие-для-производства).
Все изменения обратно совместимы и не требуют модификации существующего кода.

---

### Коммерческие прокси-сервисы

#### 1. BrightData (Luminati) ⭐⭐⭐⭐⭐
**Лучший выбор для профессионального скрапинга**

- **Сайт:** https://brightdata.com/
- **Цена:** от $500/месяц
- **Прокси:** 72M+ IP адресов
- **Типы:** Residential, Datacenter, Mobile
- **Ротация:** Автоматическая
- **Геолокация:** 195 стран

**Пример использования:**
```xml
<add key="Proxy:List" value="http://username-session-random123:password@zproxy.lum-superproxy.io:22225" />
```

#### 2. Smartproxy ⭐⭐⭐⭐⭐
**Лучшее соотношение цена/качество**

- **Сайт:** https://smartproxy.com/
- **Цена:** от $75/месяц (8GB)
- **Прокси:** 40M+ IP адресов
- **Типы:** Residential, Datacenter
- **Ротация:** Автоматическая
- **Геолокация:** 195+ стран

**Пример использования:**
```xml
<add key="Proxy:List" value="http://username:password@gate.smartproxy.com:7000" />
```

#### 3. Oxylabs ⭐⭐⭐⭐⭐
**Для корпоративного использования**

- **Сайт:** https://oxylabs.io/
- **Цена:** от $300/месяц
- **Прокси:** 100M+ IP адресовopen
- **Типы:** Residential, Datacenter, Mobile
- **Ротация:** Автоматическая
- **Геолокация:** 195 стран

**Пример использования:**
```xml
<add key="Proxy:List" value="http://username:password@pr.oxylabs.io:7777" />
```

#### 4. ProxyMesh ⭐⭐⭐⭐
**Для небольших проектов**

- **Сайт:** https://proxymesh.com/
- **Цена:** от $10/месяц
- **Прокси:** Datacenter
- **Ротация:** Автоматическая
- **Геолокация:** США, Европа

**Пример использования:**
```xml
<add key="Proxy:List" value="http://username:password@us-wa.proxymesh.com:31280" />
```

#### 5. IPRoyal ⭐⭐⭐⭐
**Бюджетный вариант**

- **Сайт:** https://iproyal.com/
- **Цена:** от $1.75/GB
- **Прокси:** Residential, Datacenter
- **Ротация:** Автоматическая
- **Геолокация:** 195+ стран

**Пример использования:**
```xml
<add key="Proxy:List" value="http://username:password@geo.iproyal.com:12321" />
```

#### 6. Webshare ⭐⭐⭐⭐
**Бесплатный план**

- **Сайт:** https://www.webshare.io/
- **Цена:** Бесплатно (10 прокси) или от $2.99/месяц
- **Прокси:** Datacenter
- **Ротация:** Ручная
- **Геолокация:** США, Европа

**Пример использования:**
```xml
<add key="Proxy:List" value="http://username:password@proxy.webshare.io:80" />
```

---

## 📋 Чеклист: Настройка и использование прокси

### ✅ Шаг 1: Настройка конфигурации

- [ ] Открыть `JobBoardScraper/App.config`
- [ ] Найти секцию `<!-- Proxy Settings -->`
- [ ] Установить `Proxy:Enabled` в `true`
- [ ] Добавить список прокси в `Proxy:List`
- [ ] Сохранить файл

### ✅ Шаг 2: Проверка формата прокси

- [ ] HTTP: `http://proxy.example.com:8080`
- [ ] С аутентификацией: `http://user:pass@proxy.example.com:8080`
- [ ] SOCKS5: `socks5://proxy.example.com:1080`
- [ ] Разделитель: точка с запятой (`;`) или запятая (`,`)

### ✅ Шаг 3: Обновление кода (если нужно)

```csharp
var proxyRotator = HttpClientFactory.CreateProxyRotator();
var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator);
var smartClient = new SmartHttpClient(httpClient, "MyScraper", proxyRotator: proxyRotator);
```

### ✅ Шаг 4: Сборка проекта

- [ ] Открыть терминал
- [ ] Выполнить: `dotnet build JobBoardScraper/JobBoardScraper.csproj`
- [ ] Убедиться, что сборка прошла успешно

### ✅ Шаг 5: Запуск и проверка

- [ ] Запустить приложение: `dotnet run --project JobBoardScraper`
- [ ] Проверить логи на наличие сообщения: `✓ Прокси включены: N серверов`
- [ ] Убедиться, что запросы проходят через прокси

### ✅ Шаг 6: Тестирование

- [ ] Проверить доступность прокси-серверов
- [ ] Убедиться, что HTTP-запросы выполняются успешно
- [ ] Проверить ротацию (если настроена)
- [ ] Проверить статистику трафика

### ✅ Шаг 7: Мониторинг

- [ ] Следить за логами на наличие ошибок
- [ ] Проверять статус прокси: `smartClient.GetProxyStatus()`
- [ ] Мониторить производительность

---

## 📚 Примеры конфигурации прокси

### Базовая конфигурация (один прокси)

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy.example.com:8080" />
```

### Несколько прокси с ротацией

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1.example.com:8080;http://proxy2.example.com:8080;http://proxy3.example.com:8080" />
```

### Прокси с аутентификацией

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://username:password@proxy.example.com:8080" />
```

### Несколько прокси с разной аутентификацией

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://user1:pass1@proxy1.example.com:8080;http://user2:pass2@proxy2.example.com:8080" />
```

### SOCKS5 прокси

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="socks5://proxy.example.com:1080" />
```

### Локальный прокси для отладки (Fiddler)

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://localhost:8888" />
```

### Коммерческие прокси-сервисы

#### BrightData (Luminati)

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://username-session-random123:password@zproxy.lum-superproxy.io:22225" />
```

#### Smartproxy

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://username:password@gate.smartproxy.com:7000" />
```

#### Oxylabs

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://username:password@pr.oxylabs.io:7777" />
```

#### Ротирующиеся прокси (Rotating Proxies)

```xml
<add key="Proxy:Enabled" value="true" />
<!-- Каждый запрос автоматически получает новый IP -->
<add key="Proxy:List" value="http://username:password@rotating.proxy.com:8080" />
```

#### Несколько типов прокси

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1.example.com:8080;socks5://proxy2.example.com:1080;http://user:pass@proxy3.example.com:3128" />
```

#### Прокси через запятую (альтернативный формат)

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1.example.com:8080, http://proxy2.example.com:8080, http://proxy3.example.com:8080" />
```

#### Отключение прокси

```xml
<add key="Proxy:Enabled" value="false" />
<add key="Proxy:List" value="" />
```

---

## 📋 Интеграция прокси в существующие скраперы

### CompanyRatingScraper

```csharp
var proxyRotator = HttpClientFactory.CreateProxyRotator();
var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator, AppConfig.CompanyRatingTimeout);
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "CompanyRatingScraper",
    trafficStats: trafficStats,
    enableRetry: AppConfig.CompanyRatingEnableRetry,
    enableTrafficMeasuring: AppConfig.CompanyRatingEnableTrafficMeasuring,
    timeout: AppConfig.CompanyRatingTimeout,
    proxyRotator: proxyRotator
);
```

### UserProfileScraper

```csharp
var proxyRotator = HttpClientFactory.CreateProxyRotator();
var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator, AppConfig.UserProfileTimeout);
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "UserProfileScraper",
    trafficStats: trafficStats,
    enableRetry: AppConfig.UserProfileEnableRetry,
    enableTrafficMeasuring: AppConfig.UserProfileEnableTrafficMeasuring,
    timeout: AppConfig.UserProfileTimeout,
    proxyRotator: proxyRotator
);
```

### BruteForceUsernameScraper

```csharp
var proxyRotator = HttpClientFactory.CreateProxyRotator();
var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator);
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "BruteForceUsernameScraper",
    trafficStats: trafficStats,
    enableRetry: AppConfig.BruteForceEnableRetry,
    enableTrafficMeasuring: AppConfig.BruteForceEnableTrafficMeasuring,
    maxRetries: AppConfig.MaxRetries,
    proxyRotator: proxyRotator
);
```

### Отключение прокси для конкретного скрапера

```csharp
var httpClient = HttpClientFactory.CreateHttpClient(null);
var smartClient = new SmartHttpClient(httpClient, "MyScraper");
```

---

## 📋 Proxy Whitelist Manager

Умная система управления прокси с белым списком для обхода суточных лимитов Habr Career.

### Проблема

Habr Career ограничивает количество просмотров профилей в сутки для одного IP. При достижении лимита выводится сообщение:
> "Вы исчерпали суточный лимит на просмотр профилей специалистов"

### Архитектура (v2.0)

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

### Конфигурация (App.config)

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

### Алгоритм работы

1. `ProxyCoordinator.GetNextProxy()` выбирает текущий рабочий прокси, проверяет whitelist, при необходимости переключается на общий пул.
2. При успехе прокси остаётся текущим.
3. При ошибке увеличивается счётчик попыток; после превышения `MaxRetryAttempts` прокси удаляется из whitelist.
4. При достижении суточного лимита прокси добавляется в whitelist.

### Интеграция

```csharp
var whitelistStorage = new JsonWhitelistStorage(AppConfig.ProxyWhitelistFilePath);
var whitelistManager = new ProxyWhitelistManager(whitelistStorage);
await whitelistManager.LoadStateAsync();

var generalPoolManager = new GeneralPoolManager(freeProxyPool);
var proxyCoordinator = new ProxyCoordinator(whitelistManager, generalPoolManager);

var scraper = new UserResumeDetailScraper(
    httpClient, db, getUserCodes, controller,
    proxyCoordinator: proxyCoordinator
);
```

---

## 📋 Пример использования прокси в скраперах

```csharp
// Создать ProxyRotator из конфигурации
var proxyRotator = HttpClientFactory.CreateProxyRotator();

if (proxyRotator?.IsEnabled == true)
{
    Console.WriteLine($"✓ Прокси включены: {proxyRotator.ProxyCount} серверов");
}
else
{
    Console.WriteLine("○ Прокси отключены");
}

// Создать HttpClient с прокси
var httpClient = HttpClientFactory.CreateHttpClient(
    proxyRotator, 
    timeout: AppConfig.CompanyRatingTimeout
);

// Создать SmartHttpClient с прокси
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "CompanyRatingScraper",
    trafficStats: trafficStats,
    enableRetry: AppConfig.CompanyRatingEnableRetry,
    enableTrafficMeasuring: AppConfig.CompanyRatingEnableTrafficMeasuring,
    timeout: AppConfig.CompanyRatingTimeout,
    proxyRotator: proxyRotator
);
```

### Ручная ротация прокси

```csharp
for (int i = 0; i < pages.Count; i++)
{
    var response = await smartClient.GetAsync(pages[i]);

    // Переключить прокси каждые 10 страниц
    if (i > 0 && i % 10 == 0)
    {
        smartClient.RotateProxy();
        Console.WriteLine($"Прокси переключен: {smartClient.GetProxyStatus()}");
    }
}
```

---

## 📋 Прочие документы

- **PROXY_CHECKLIST.md** – чеклист настройки и использования прокси
- **PROXY_CHECKLIST_1.md** – альтернативный чеклист
- **PROXY_CONFIG_EXAMPLES.md** – примеры конфигураций
- **PROXY_FEATURE_SUMMARY.md** – сводка возможностей
- **PROXY_FEATURE_SUMMARY_1.md** – альтернативная сводка
- **PROXY_INTEGRATION_GUIDE.md** – руководство по интеграции
- **PROXY_ROTATION.md** – описание ротации прокси
- **PROXY_QUICKSTART.md** – быстрый старт
- **PROXY_README.md** – поддержка прокси в проекте
- **PROXY_SERVICES.md** – коммерческие сервисы
- **PROXY_USAGE_EXAMPLE.md** – примеры использования
- **PROXY_WHITELIST.md** – менеджер белого списка

--- 

## 📌 Заключение

Все перечисленные документы успешно объединены в один файл **DYNAMIC_PROXY.md**. Остальные файлы `PROXY_*.md` могут быть удалены, оставив только этот файл.