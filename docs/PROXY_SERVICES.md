# Коммерческие прокси-сервисы

## Рекомендуемые сервисы

### 1. BrightData (Luminati) ⭐⭐⭐⭐⭐
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

**Особенности:**
- Самая большая сеть прокси
- Высокая скорость и надежность
- Автоматическая ротация IP
- Поддержка 24/7

---

### 2. Smartproxy ⭐⭐⭐⭐⭐
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

**Особенности:**
- Доступная цена
- Простая настройка
- Хорошая документация
- Бесплатный пробный период

---

### 3. Oxylabs ⭐⭐⭐⭐⭐
**Для корпоративного использования**

- **Сайт:** https://oxylabs.io/
- **Цена:** от $300/месяц
- **Прокси:** 100M+ IP адресов
- **Типы:** Residential, Datacenter, Mobile
- **Ротация:** Автоматическая
- **Геолокация:** 195 стран

**Пример использования:**
```xml
<add key="Proxy:List" value="http://username:password@pr.oxylabs.io:7777" />
```

**Особенности:**
- Корпоративный уровень
- Высокая надежность
- Dedicated account manager
- API для управления

---

### 4. ProxyMesh ⭐⭐⭐⭐
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

**Особенности:**
- Низкая цена
- Простая настройка
- Подходит для начинающих
- Бесплатный пробный период

---

### 5. IPRoyal ⭐⭐⭐⭐
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

**Особенности:**
- Очень низкая цена
- Pay-as-you-go модель
- Хорошее качество
- Быстрая поддержка

---

### 6. Webshare ⭐⭐⭐⭐
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

**Особенности:**
- Бесплатный план (10 прокси)
- Простой API
- Хорошая документация
- Подходит для тестирования

---

## Сравнительная таблица

| Сервис | Цена/месяц | IP адресов | Типы | Ротация | Рейтинг |
|--------|-----------|-----------|------|---------|---------|
| BrightData | $500+ | 72M+ | Res/DC/Mobile | Авто | ⭐⭐⭐⭐⭐ |
| Smartproxy | $75+ | 40M+ | Res/DC | Авто | ⭐⭐⭐⭐⭐ |
| Oxylabs | $300+ | 100M+ | Res/DC/Mobile | Авто | ⭐⭐⭐⭐⭐ |
| ProxyMesh | $10+ | - | DC | Авто | ⭐⭐⭐⭐ |
| IPRoyal | $1.75/GB | - | Res/DC | Авто | ⭐⭐⭐⭐ |
| Webshare | Бесплатно | - | DC | Ручная | ⭐⭐⭐⭐ |

**Легенда:**
- Res = Residential (домашние IP)
- DC = Datacenter (серверные IP)
- Mobile = Мобильные IP

---

## Рекомендации по выбору

### Для разработки и тестирования:
- **Webshare** (бесплатный план)
- **ProxyMesh** ($10/месяц)

### Для небольших проектов:
- **Smartproxy** ($75/месяц)
- **IPRoyal** (pay-as-you-go)

### Для серьезного скрапинга:
- **BrightData** (лучшее качество)
- **Oxylabs** (корпоративный уровень)

### Для бюджетных проектов:
- **IPRoyal** (самая низкая цена)
- **ProxyMesh** (простота использования)

---

## Настройка в проекте

### BrightData
```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://username-session-random123:password@zproxy.lum-superproxy.io:22225" />
```

### Smartproxy
```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://username:password@gate.smartproxy.com:7000" />
```

### Oxylabs
```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://username:password@pr.oxylabs.io:7777" />
```

### ProxyMesh
```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://username:password@us-wa.proxymesh.com:31280;http://username:password@us-ca.proxymesh.com:31280" />
```

---

## Бесплатные источники

### Для тестирования можно использовать:

1. **ProxyScrape API**
   - Бесплатный
   - Обновляется регулярно
   - Низкое качество

2. **GeoNode API**
   - Бесплатный
   - JSON API
   - Средне качество

3. **Free-Proxy-List**
   - Бесплатный
   - Большой список
   - Низкая надежность

**Использование:**
```csharp
var provider = new ProxyProvider();
await provider.LoadFromProxyScrapeAsync();
await provider.LoadFromGeoNodeAsync();
```

---

## Важные замечания

⚠️ **Бесплатные прокси:**
- Нестабильны
- Медленные
- Могут быть небезопасны
- Часто блокируются

✅ **Коммерческие прокси:**
- Стабильны
- Быстрые
- Безопасны
- Редко блокируются

---

## Дополнительные ресурсы

- [DYNAMIC_PROXY.md](DYNAMIC_PROXY.md) - Динамическое обновление прокси
- [PROXY_ROTATION.md](PROXY_ROTATION.md) - Ротация прокси
- [PROXY_CONFIG_EXAMPLES.md](PROXY_CONFIG_EXAMPLES.md) - Примеры конфигураций
