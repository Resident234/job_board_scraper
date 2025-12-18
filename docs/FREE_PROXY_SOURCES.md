# Бесплатные источники прокси

## ⚠️ Важное предупреждение

Бесплатные прокси имеют существенные ограничения:
- ❌ Низкая надежность (часто не работают)
- ❌ Медленная скорость
- ❌ Могут быть небезопасны (логирование трафика)
- ❌ Часто блокируются сайтами
- ❌ Нестабильная работа

**Рекомендуется использовать только для тестирования!**

Для продакшена используйте [коммерческие сервисы](PROXY_SERVICES.md).

---

## Реализованные парсеры в проекте

В проекте реализованы два автоматических парсера для получения бесплатных прокси:

### FreeProxyListScraper

**Источник:** https://free-proxy-list.net/

**Класс:** `JobBoardScraper/Proxy/FreeProxyListScraper.cs`

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

---

### ProxyScrapeScraper

**Источник:** https://api.proxyscrape.com/

**Класс:** `JobBoardScraper/Proxy/ProxyScrapeScraper.cs`

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

### DynamicProxyRotator

**Класс:** `JobBoardScraper/Proxy/DynamicProxyRotator.cs`

Агрегирует прокси из обоих источников и обеспечивает:
- Автоматическое обновление списка (по умолчанию каждый час)
- Ротацию прокси при запросах
- Удаление нерабочих прокси
- Принудительное обновление через `ForceUpdateAsync()`

---

## Автоматические источники (API)

### 1. ProxyScrape API ⭐⭐⭐⭐

**URL:** https://api.proxyscrape.com/

**Использование в коде:**
```csharp
var provider = new ProxyProvider();
await provider.LoadFromProxyScrapeAsync(timeout: 10000, country: "all");
```

**Прямой запрос:**
```
https://api.proxyscrape.com/v2/?request=get&protocol=http&timeout=10000&country=all&ssl=all&anonymity=all
```

**Параметры:**
- `timeout` - таймаут в миллисекундах (5000-10000)
- `country` - код страны (us, ru, all)
- `ssl` - yes/no/all
- `anonymity` - elite/anonymous/transparent/all

**Особенности:**
- Бесплатный
- Обновляется регулярно
- Простой API
- Без регистрации

---

### 2. GeoNode API ⭐⭐⭐⭐

**URL:** https://proxylist.geonode.com/

**Использование в коде:**
```csharp
var provider = new ProxyProvider();
await provider.LoadFromGeoNodeAsync(limit: 100);
```

**Прямой запрос:**
```
https://proxylist.geonode.com/api/proxy-list?limit=100&page=1&sort_by=lastChecked&sort_type=desc
```

**Параметры:**
- `limit` - количество прокси (1-500)
- `page` - номер страницы
- `sort_by` - lastChecked/speed/upTime
- `sort_type` - asc/desc

**Особенности:**
- Бесплатный
- JSON API
- Информация о последней проверке
- Без регистрации

---

### 3. ProxyList+ API ⭐⭐⭐

**URL:** https://list.proxylistplus.com/

**Прямой запрос:**
```
https://list.proxylistplus.com/Fresh-HTTP-Proxy-List-1
```

**Особенности:**
- Бесплатный
- Обновляется ежедневно
- Простой формат (IP:PORT)

---

## Ручные источники (веб-сайты)

### 1. Free Proxy List ⭐⭐⭐⭐
- **URL:** https://free-proxy-list.net/
- Обновляется каждые 10 минут
- Фильтрация по стране, анонимности, HTTPS
- Экспорт в TXT

### 2. HideMy.name ⭐⭐⭐⭐
- **URL:** https://hidemy.name/ru/proxy-list/
- Большой список
- Фильтрация по параметрам
- Проверка скорости

### 3. ProxyScan ⭐⭐⭐
- **URL:** https://www.proxyscan.io/
- API доступен
- Фильтрация по типу
- Проверка работоспособности

### 4. Spys.one ⭐⭐⭐
- **URL:** https://spys.one/en/
- Большая база
- Детальная информация
- Обновляется часто

---

## Использование в проекте

### Автоматическая загрузка

```csharp
// Создать провайдер
var provider = new ProxyProvider();

// Загрузить из ProxyScrape
await provider.LoadFromProxyScrapeAsync();
Console.WriteLine($"ProxyScrape: {provider.GetProxies().Count} прокси");

// Загрузить из GeoNode
await provider.LoadFromGeoNodeAsync(limit: 50);
Console.WriteLine($"GeoNode: {provider.GetProxies().Count} прокси");

// Проверить работоспособность
var removed = await provider.RemoveDeadProxiesAsync();
Console.WriteLine($"Удалено нерабочих: {removed}");
Console.WriteLine($"Рабочих: {provider.GetProxies().Count}");

// Сохранить рабочие в файл
await provider.SaveToFileAsync("working_proxies.txt");
```

### Загрузка из файла

Создайте файл `proxies.txt`:
```
http://123.45.67.89:8080
http://98.76.54.32:3128
http://11.22.33.44:80
```

Загрузите в код:
```csharp
var provider = new ProxyProvider();
await provider.LoadFromFileAsync("proxies.txt");
```

### Динамическое обновление

```csharp
// Создать динамический ротатор с автообновлением
var dynamicRotator = await HttpClientFactory.CreateDynamicProxyRotatorAsync(
    updateInterval: TimeSpan.FromHours(1),
    autoUpdate: true
);

// Использовать
var httpClient = HttpClientFactory.CreateHttpClient(dynamicRotator);
var smartClient = new SmartHttpClient(httpClient, "MyScraper");
```

---

## Проверка работоспособности

### Проверить один прокси

```csharp
var provider = new ProxyProvider();
var isAlive = await provider.TestProxyAsync("http://123.45.67.89:8080");
Console.WriteLine($"Прокси работает: {isAlive}");
```

### Проверить все прокси

```csharp
var provider = new ProxyProvider();
await provider.LoadFromProxyScrapeAsync();

var proxies = provider.GetProxies();
foreach (var proxy in proxies)
{
    var isAlive = await provider.TestProxyAsync(proxy);
    Console.WriteLine($"{proxy}: {(isAlive ? "✓" : "✗")}");
}
```

### Удалить нерабочие

```csharp
var provider = new ProxyProvider();
await provider.LoadFromProxyScrapeAsync();

// Удалить все нерабочие прокси
var removed = await provider.RemoveDeadProxiesAsync();
Console.WriteLine($"Удалено: {removed}");

// Сохранить только рабочие
await provider.SaveToFileAsync("working_proxies.txt");
```

---

## Рекомендации

### Для тестирования:
1. Загрузите 20-50 прокси из ProxyScrape
2. Проверьте работоспособность
3. Сохраните рабочие в файл
4. Используйте этот файл для тестов

### Для разработки:
1. Используйте локальный прокси (Fiddler, Charles)
2. Или 2-3 проверенных бесплатных прокси
3. Не полагайтесь на стабильность

### Для продакшена:
1. **НЕ используйте бесплатные прокси!**
2. Купите коммерческий сервис
3. См. [PROXY_SERVICES.md](PROXY_SERVICES.md)

---

## Альтернативы

### Бесплатные пробные периоды коммерческих сервисов:

1. **Smartproxy** - 3 дня бесплатно
2. **Webshare** - 10 прокси бесплатно навсегда
3. **ProxyMesh** - 100 запросов бесплатно

### Дешевые варианты:

1. **ProxyMesh** - $10/месяц
2. **IPRoyal** - $1.75/GB
3. **Webshare** - $2.99/месяц

---

## Полный пример

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

## Заключение

Бесплатные прокси подходят только для:
- ✅ Тестирования
- ✅ Разработки
- ✅ Обучения
- ✅ Небольших экспериментов

Для серьезных задач используйте [коммерческие сервисы](PROXY_SERVICES.md).
