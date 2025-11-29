# Примеры конфигурации прокси

## Базовая конфигурация (один прокси)

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy.example.com:8080" />
```

## Несколько прокси с ротацией

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1.example.com:8080;http://proxy2.example.com:8080;http://proxy3.example.com:8080" />
```

## Прокси с аутентификацией

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://username:password@proxy.example.com:8080" />
```

## Несколько прокси с разной аутентификацией

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://user1:pass1@proxy1.example.com:8080;http://user2:pass2@proxy2.example.com:8080" />
```

## SOCKS5 прокси

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="socks5://proxy.example.com:1080" />
```

## Локальный прокси для отладки (Fiddler)

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://localhost:8888" />
```

## Локальный прокси для отладки (Charles)

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://localhost:8888" />
```

## Коммерческие прокси-сервисы

### BrightData (Luminati)

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

## Ротирующиеся прокси (Rotating Proxies)

```xml
<add key="Proxy:Enabled" value="true" />
<!-- Каждый запрос автоматически получает новый IP -->
<add key="Proxy:List" value="http://username:password@rotating.proxy.com:8080" />
```

## Несколько типов прокси

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1.example.com:8080;socks5://proxy2.example.com:1080;http://user:pass@proxy3.example.com:3128" />
```

## Прокси через запятую (альтернативный формат)

```xml
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1.example.com:8080, http://proxy2.example.com:8080, http://proxy3.example.com:8080" />
```

## Отключение прокси

```xml
<add key="Proxy:Enabled" value="false" />
<add key="Proxy:List" value="" />
```

## Рекомендации по выбору конфигурации

### Для разработки и тестирования
- Используйте локальный прокси (Fiddler/Charles) для отладки
- Или отключите прокси полностью

### Для легкого скрапинга
- 1-2 прокси достаточно
- Можно использовать бесплатные прокси (но они ненадежны)

### Для агрессивного скрапинга
- 5-10 прокси для ротации
- Используйте коммерческие сервисы (BrightData, Smartproxy)
- Включите ручную ротацию в коде

### Для массового скрапинга
- Ротирующиеся прокси (автоматическая смена IP)
- Или большой пул прокси (20+)
- Мониторинг статуса прокси

## Проверка конфигурации

После настройки запустите приложение и проверьте логи:

```
✓ Прокси включены: 3 серверов
Proxy 1/3
```

Если прокси не работают, проверьте:
1. Доступность прокси-серверов
2. Правильность учетных данных
3. Формат URL (должен начинаться с http:// или socks5://)
4. Порт прокси-сервера
