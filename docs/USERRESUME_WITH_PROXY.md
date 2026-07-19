# UserResumeDetailScraper с ротацией прокси

## Описание

UserResumeDetailScraper теперь поддерживает автоматическую ротацию прокси для каждой страницы резюме. Это помогает обойти ограничение career.habr.com на просмотр профилей.

## ⚠️ Проблема

career.habr.com ограничивает количество просматриваемых профилей в день:
> "Вы исчерпали суточный лимит на просмотр профилей специалистов. Зарегистрируйтесь или войдите в свой аккаунт, чтобы увидеть больше профилей."

## ✅ Решение

Использование ротации прокси для каждой страницы:
- Каждый запрос идет через новый прокси
- Обход ограничений по IP
- Автоматическая загрузка прокси из публичных источников

## 🚀 Настройка

### 1. Включить прокси в App.config

```xml
<!-- Включить прокси -->
<add key="Proxy:Enabled" value="true" />

<!-- Оставить список пустым для автоматической загрузки -->
<add key="Proxy:List" value="" />
```

### 2. Или указать свои прокси

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1:8080;http://proxy2:8080;http://proxy3:8080" />
```

### 3. Включить UserResumeDetailScraper

```xml
<add key="UserResumeDetail:Enabled" value="true" />
```

## 📝 Как это работает

### Автоматическая загрузка прокси

Если `Proxy:List` пуст, система автоматически:

1. Загружает прокси из **ProxyScrape API**
2. Загружает прокси из **GeoNode API**
3. Создает пул прокси для ротации

```
[HttpClientFactory] Список прокси пуст. Загрузка из публичных источников...
[HttpClientFactory] ProxyScrape: загружено 50 прокси
[HttpClientFactory] GeoNode: загружено 100 прокси
[HttpClientFactory] ✓ Загружено 100 прокси
```

### Ротация для каждой страницы

Перед каждым запросом к странице резюме:

```csharp
// Автоматическая ротация прокси
_httpClient.RotateProxy();

// Запрос через новый прокси
var response = await _httpClient.GetAsync(userLink, ct);
```

### Логи

```
[Program] UserResumeDetailScraper: ВКЛЮЧЕН
[Program] UserResumeDetailScraper: Прокси ВКЛЮЧЕНЫ (100 серверов)
[Program] UserResumeDetailScraper: Ротация прокси для каждой страницы
```

## 🔧 Ручная настройка прокси

### Использовать свои прокси

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1.example.com:8080;http://proxy2.example.com:8080" />
```

### С аутентификацией

```xml
<add key="Proxy:List" value="http://user:pass@proxy1.example.com:8080;http://user:pass@proxy2.example.com:8080" />
```

### Коммерческие прокси

```xml
<!-- Smartproxy -->
<add key="Proxy:List" value="http://username:password@gate.smartproxy.com:7000" />

<!-- BrightData -->
<add key="Proxy:List" value="http://username-session-random123:password@zproxy.lum-superproxy.io:22225" />
```

## 📊 Мониторинг

### Проверка статуса прокси

При запуске приложение выведет:

```
[Program] UserResumeDetailScraper: Прокси ВКЛЮЧЕНЫ (100 серверов)
[Program] UserResumeDetailScraper: Ротация прокси для каждой страницы
```

### Логи обработки

```
[UserResumeDetailScraper] Обработка: https://career.habr.com/username
[UserResumeDetailScraper] Proxy: 1/100
[UserResumeDetailScraper] Status: 200 OK
```

## ⚡ Производительность

### Без прокси
- ❌ Ограничение: ~50-100 профилей в день
- ❌ Блокировка по IP

### С ротацией прокси
- ✅ Ограничение: зависит от количества прокси
- ✅ 100 прокси = ~5000-10000 профилей в день
- ✅ Обход блокировок

## 🎯 Рекомендации

### Для тестирования
1. Используйте автоматическую загрузку прокси
2. Или 5-10 своих прокси
3. Проверьте работоспособность

### Для продакшена
1. Купите коммерческие прокси
2. Используйте 50-100 прокси
3. Настройте мониторинг

### Оптимальная конфигурация

```xml
<!-- Включить прокси -->
<add key="Proxy:Enabled" value="true" />

<!-- Использовать коммерческие прокси -->
<add key="Proxy:List" value="http://username:password@gate.smartproxy.com:7000" />

<!-- Увеличенный таймаут для прокси (по умолчанию 120 секунд) -->
<add key="FreeProxy:RequestTimeoutSeconds" value="120" />

<!-- Автоматические повторные попытки с разными прокси -->
<add key="FreeProxy:MaxRetries" value="3" />

<!-- Включить UserResumeDetailScraper -->
<add key="UserResumeDetail:Enabled" value="true" />
<add key="UserResumeDetail:EnableRetry" value="true" />
<add key="UserResumeDetail:TimeoutSeconds" value="60" />
```

## ⏱️ Настройка таймаута и повторных попыток

### Увеличенный таймаут для прокси

Прокси-серверы обычно работают медленнее прямого соединения. По умолчанию для запросов через прокси используется увеличенный таймаут:

```xml
<!-- Таймаут для HTTP-запросов через прокси (по умолчанию 120 секунд) -->
<add key="FreeProxy:RequestTimeoutSeconds" value="120" />
```

### Автоматические повторные попытки

Если прокси не работает (таймаут, ошибка соединения), система автоматически пробует следующий прокси:

```xml
<!-- Максимальное количество попыток с разными прокси (по умолчанию 3) -->
<add key="FreeProxy:MaxRetries" value="3" />
```

**Как это работает:**
1. Берется прокси из пула
2. Делается попытка запроса
3. Если ошибка - берется следующий прокси
4. Повторяется до успеха или достижения лимита попыток
5. Только после успешного ответа переходим к следующей странице

**Логи:**
```
[UserResumeDetailScraper] Using proxy: http://123.30.154.171:7777 (attempt 1/3)
[UserResumeDetailScraper] Proxy error (attempt 1/3): The proxy tunnel request to proxy 'http://84.39.112.144:3128/' failed with status code '400'
[UserResumeDetailScraper] Trying next proxy...
[UserResumeDetailScraper] Using proxy: http://133.18.234.13:80 (attempt 2/3)
[UserResumeDetailScraper] Status: 200 OK
```

### Рекомендации по таймауту и повторным попыткам

**Бесплатные прокси:**
- Таймаут: 120-180 секунд (медленные и нестабильные)
- Повторные попытки: 3-5 (много нерабочих прокси)

**Коммерческие прокси:**
- Таймаут: 60-90 секунд (быстрые и стабильные)
- Повторные попытки: 2-3 (редко ломаются)

**Без прокси:**
- Таймаут: 60 секунд (стандартный)
- Повторные попытки: 1 (не используются)

### Пример конфигурации

```xml
<!-- Для бесплатных прокси -->
<add key="FreeProxy:RequestTimeoutSeconds" value="180" />
<add key="FreeProxy:MaxRetries" value="5" />

<!-- Для коммерческих прокси -->
<add key="FreeProxy:RequestTimeoutSeconds" value="90" />
<add key="FreeProxy:MaxRetries" value="2" />

<!-- Для прямого соединения -->
<add key="UserResumeDetail:TimeoutSeconds" value="60" />
```

## 🔍 Отладка

### Прокси не работают

Проверьте логи:

```
[HttpClientFactory] ⚠️ Не удалось загрузить прокси из публичных источников
```

**Решение:** Укажите прокси вручную в `Proxy:List`

### Медленная работа

Бесплатные прокси могут быть медленными.

**Решение:** Используйте коммерческие прокси

### Ошибки 403/429

Прокси заблокированы сайтом.

**Решение:** 
1. Обновите список прокси
2. Используйте residential прокси
3. Увеличьте задержку между запросами

## 📚 Дополнительная информация

- [DYNAMIC_PROXY.md](DYNAMIC_PROXY.md) - Динамическое обновление прокси

## 🚀 Быстрый старт

```bash
# 1. Включить прокси в App.config
# Proxy:Enabled = true
# Proxy:List = "" (для автозагрузки)

# 2. Включить UserResumeDetailScraper
# UserResumeDetail:Enabled = true

# 3. Запустить
dotnet run --project JobBoardScraper
```

## ✅ Готово!

UserResumeDetailScraper теперь использует ротацию прокси для каждой страницы, что позволяет обойти ограничения career.habr.com и собирать больше данных.
