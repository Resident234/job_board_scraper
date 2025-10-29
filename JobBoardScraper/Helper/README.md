# Helper.ConsoleLogger

Утилиты для управления выводом в консоль и файлы.

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

### DualWriter
TextWriter, который пишет одновременно в два потока (консоль + файл).
Используется внутри ConsoleLogger, обычно не требует прямого использования.

## Конфигурация

В `App.config`:

```xml
<add key="Logging:OutputDirectory" value="./logs" />
<add key="Companies:OutputMode" value="Both" />
```

В `AppConfig.cs`:

```csharp
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

- Автоматическое создание директории для логов
- AutoFlush для файлов (немедленная запись)
- Корректное освобождение ресурсов через Dispose
- Сохранение оригинального Console.Out для восстановления
- Потокобезопасность через стандартные TextWriter механизмы
