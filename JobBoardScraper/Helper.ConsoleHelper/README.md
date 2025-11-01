# Helper.ConsoleHelper

Утилиты для управления выводом в консоль и файлы.

**Namespace:** `JobBoardScraper.Helper.ConsoleHelper`  
**Папка:** `JobBoardScraper/Helper.ConsoleHelper/`

## Архитектура логирования

### Принцип работы

`ConsoleLogger` реализует паттерн **независимого логирования** для каждого компонента системы. Вместо глобального перенаправления потока вывода (`Console.SetOut()`), каждый экземпляр логгера управляет своим собственным файлом и решает, куда писать сообщения.

### Ключевые особенности архитектуры

1. **Изоляция логгеров**
   - Каждый `ConsoleLogger` создаёт свой собственный `StreamWriter`
   - Логгеры не влияют друг на друга и на глобальный `Console.Out`
   - Один процесс может иметь несколько независимых логгеров

2. **Гибкие режимы вывода**
   - `ConsoleOnly` - вывод только в консоль (по умолчанию)
   - `FileOnly` - вывод только в файл (консоль остаётся чистой)
   - `Both` - дублирование в консоль и файл одновременно

3. **Потокобезопасность**
   - Все операции записи защищены `lock(_lock)`
   - Безопасная работа в многопоточной среде
   - Корректное закрытие файлов при `Dispose()`

4. **Автоматическое управление файлами**
   - Файлы создаются с уникальными именами: `{ProcessName}_{Timestamp}.log`
   - Автоматическое создание директории для логов
   - `AutoFlush = true` для немедленной записи на диск

### Схема работы

```
┌─────────────────────────────────────────────────────────────┐
│                      Приложение                              │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────────┐  ┌──────────────────┐  ┌────────────┐ │
│  │ CategoryScraper  │  │CompanyListScraper│  │ DB Queue   │ │
│  │                  │  │                  │  │            │ │
│  │  ConsoleLogger   │  │  ConsoleLogger   │  │ConsoleLogger│
│  │  "CategoryScraper"  │  "CompanyListScraper" "DB Queue"│ │
│  └────────┬─────────┘  └────────┬─────────┘  └──────┬─────┘ │
│           │                     │                    │       │
│           │ WriteLine()         │ WriteLine()        │       │
│           ▼                     ▼                    ▼       │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              lock + режим вывода                       │ │
│  └────────────────────────────────────────────────────────┘ │
│           │                     │                    │       │
│     ┌─────┴─────┐         ┌─────┴─────┐        ┌─────┴────┐│
│     ▼           ▼         ▼           ▼        ▼          ▼││
│  Console  CategoryScraper_ CompanyList_ Console  DBQueue_ │ │
│   .Out    20251101.log    20251101.log  .Out   20251101.log││
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

### Жизненный цикл логгера

```csharp
// 1. Создание
var logger = new ConsoleLogger("ProcessName");
// Состояние: режим ConsoleOnly, файл не создан

// 2. Настройка режима
logger.SetOutputMode(OutputMode.Both);
// Состояние: создан файл ProcessName_20251101_120000.log
//            StreamWriter открыт с AutoFlush=true

// 3. Использование
logger.WriteLine("Сообщение");
// Действие: запись в Console.Out И в файл (если режим Both)

// 4. Смена режима (опционально)
logger.SetOutputMode(OutputMode.FileOnly);
// Действие: старый файл закрыт, создан новый файл

// 5. Освобождение ресурсов
logger.Dispose();
// Действие: файл закрыт, StreamWriter освобождён
```

### Преимущества подхода

✅ **Независимость** - каждый компонент имеет свой лог-файл  
✅ **Читаемость** - легко найти логи конкретного процесса  
✅ **Масштабируемость** - можно добавлять новые логгеры без конфликтов  
✅ **Гибкость** - разные компоненты могут иметь разные режимы вывода  
✅ **Безопасность** - потокобезопасная работа в асинхронной среде  

### Сравнение с глобальным перенаправлением

| Аспект | Глобальное `Console.SetOut()` | Независимые логгеры |
|--------|-------------------------------|---------------------|
| Файлов на процесс | 1 общий | 1 на каждый логгер |
| Конфликты | Да, перезаписывают друг друга | Нет |
| Читаемость логов | Смешанные сообщения | Чистые, разделённые |
| Потокобезопасность | Требует синхронизации | Встроенная |
| Гибкость настройки | Одна для всех | Индивидуальная |

## Компоненты

### OutputMode (enum)
Режимы вывода:
- `ConsoleOnly` - только в консоль
- `FileOnly` - только в файл
- `Both` - одновременно в консоль и файл

### ConsoleLogger
Управление выводом для конкретного процесса.

#### Использование

```csharp
using JobBoardScraper.Helper.ConsoleHelper;

// Создание логгера
var logger = new ConsoleLogger("MyProcess");

// Установка режима вывода
logger.SetOutputMode(OutputMode.Both); // консоль + файл
logger.SetOutputMode(OutputMode.FileOnly); // только файл
logger.SetOutputMode(OutputMode.ConsoleOnly); // только консоль

// Вывод сообщений
logger.WriteLine("Сообщение"); // [MyProcess] Сообщение
logger.WriteLineWithTime("Сообщение"); // [2025-10-29 21:30:00] [MyProcess] Сообщение

// Восстановление консоли
logger.RestoreConsole();

// Освобождение ресурсов
logger.Dispose();
```

#### Пример интеграции в скрапер

```csharp
using JobBoardScraper.Helper.ConsoleHelper;

public sealed class MyScraper : IDisposable
{
    private readonly ConsoleLogger _logger;

    public MyScraper(OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _logger = new ConsoleLogger("MyScraper");
        _logger.SetOutputMode(outputMode);
    }

    public async Task RunAsync()
    {
        _logger.WriteLine("Начало работы...");
        
        try
        {
            // Ваш код
            _logger.WriteLine("Обработано 100 элементов");
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"Ошибка: {ex.Message}");
        }
        
        _logger.WriteLine("Завершено");
    }

    public void Dispose()
    {
        _logger?.Dispose();
    }
}
```



## Конфигурация

В `App.config`:

```xml
<add key="Logging:OutputDirectory" value="./logs" />
<add key="Companies:OutputMode" value="Both" />
```

В `AppConfig.cs`:

```csharp
using JobBoardScraper.Helper.ConsoleHelper;

public static string LoggingOutputDirectory => 
    ConfigurationManager.AppSettings["Logging:OutputDirectory"] ?? "./logs";

public static OutputMode CompaniesOutputMode
{
    get
    {
        var value = ConfigurationManager.AppSettings["Companies:OutputMode"];
        return Enum.TryParse<OutputMode>(value, out var mode) 
            ? mode 
            : OutputMode.ConsoleOnly;
    }
}
```

## Формат лог-файлов

Файлы создаются автоматически в формате:
```
{ProcessName}_{yyyyMMdd_HHmmss}.log
```

Примеры:
- `CompanyListScraper_20251029_213000.log`
- `BruteForceUsernameScraper_20251029_214500.log`

## Особенности

- **Отдельный файл для каждого логгера** - каждый экземпляр `ConsoleLogger` пишет в свой файл
- Автоматическое создание директории для логов
- AutoFlush для файлов (немедленная запись)
- Корректное освобождение ресурсов через Dispose
- Потокобезопасность через lock механизм
- Не использует глобальное перенаправление `Console.SetOut()` - каждый логгер независим
