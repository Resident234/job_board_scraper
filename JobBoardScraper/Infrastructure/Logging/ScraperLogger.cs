using System.Collections.Generic;
using System.Text;

namespace JobBoardScraper.Infrastructure.Logging;

/// <summary>
/// Универсальный класс-обёртка для типовых сообщений скраперов.
/// Инкапсулирует форматирование частых паттернов (старт/завершение цикла, загрузка страницы,
/// постановка в очередь, сохранение HTML, ошибки, ретраи, счётчики) в едином стиле с иконками.
/// Не предназначен для покрытия всех возможных сообщений: уникальные/специфичные логи
/// по-прежнему пишутся через прямой вызов <c>_logger.WriteLine(...)</c>.
/// </summary>
public static class ScraperLogger
{
    private const string StartIcon = "▶";
    private const string EndIcon = "■";
    private const string PageIcon = "↓";
    private const string RetryIcon = "↻";
    private const string EnqueueIcon = "⇪";
    private const string SkipIcon = "⏭";
    private const string CountIcon = "Σ";
    private const string HtmlIcon = "💾";
    private const string ErrorIcon = "✖";
    private const string WarnIcon = "⚠";

    /// <summary>
    /// Логирует начало цикла обхода/сбора.
    /// Пример: "▶ Начало обхода списка компаний..."
    /// </summary>
    public static void LogStart(ConsoleLogger? logger, string description)
    {
        WriteLine(logger, $"{StartIcon} {description}");
    }

    /// <summary>
    /// Логирует начало обхода конкретной страницы/URL.
    /// Пример: "↓ Загрузка страницы: {url}"
    /// </summary>
    public static void LogPage(ConsoleLogger? logger, string url)
    {
        WriteLine(logger, $"{PageIcon} Загрузка страницы: {url}");
    }

    /// <summary>
    /// Логирует начало обхода конкретной страницы по её номеру.
    /// Пример: "↓ Обработка страницы 5: {url}"
    /// </summary>
    public static void LogPage(ConsoleLogger? logger, int page, string url)
    {
        WriteLine(logger, $"{PageIcon} Обработка страницы {page}: {url}");
    }

    /// <summary>
    /// Логирует завершение цикла обхода/сбора с произвольной причиной.
    /// Пример: "■ Сбор завершён."
    /// </summary>
    public static void LogEnd(ConsoleLogger? logger, string reason)
    {
        WriteLine(logger, $"{EndIcon} {reason}");
    }

    /// <summary>
    /// Логирует завершение цикла обхода/сбора с финальной статистикой.
    /// Пример: "■ Обход завершён. {statistics}"
    /// </summary>
    public static void LogEnd(ConsoleLogger? logger, object statistics)
    {
        WriteLine(logger, $"{EndIcon} Обход завершён. {statistics}");
    }

    /// <summary>
    /// Логирует завершение, вызванное неуспешным HTTP-кодом.
    /// Пример: "■ Страница вернула код 404. Завершение."
    /// </summary>
    public static void LogEnd(ConsoleLogger? logger, int statusCode, string? suffix = null)
    {
        var msg = $"{EndIcon} Страница вернула код {statusCode}. Завершение.";
        if (!string.IsNullOrEmpty(suffix))
            msg += $" {suffix}";
        WriteLine(logger, msg);
    }

    /// <summary>
    /// Логирует ошибку — текстовое описание без исключения.
    /// Пример: "✖ Не найден элемент select#category_root_id"
    /// </summary>
    public static void LogError(ConsoleLogger? logger, string description)
    {
        WriteLine(logger, $"{ErrorIcon} {description}");
    }

    /// <summary>
    /// Логирует ошибку с контекстом и исключением.
    /// Пример: "✖ Ошибка при сборе category_root_id: {ex.Message}"
    /// </summary>
    public static void LogError(ConsoleLogger? logger, string context, Exception ex)
    {
        WriteLine(logger, $"{ErrorIcon} {context}: {ex.Message}");
    }

    /// <summary>
    /// Логирует простое исключение без контекста.
    /// Пример: "✖ {ex.Message}"
    /// </summary>
    public static void LogError(ConsoleLogger? logger, Exception ex)
    {
        WriteLine(logger, $"{ErrorIcon} {ex.Message}");
    }

    /// <summary>
    /// Логирует предупреждение (не критическая ситуация).
    /// Пример: "⚠ Достигнута последняя страница (5)."
    /// </summary>
    public static void LogWarning(ConsoleLogger? logger, string description)
    {
        WriteLine(logger, $"{WarnIcon} {description}");
    }

    /// <summary>
    /// Логирует попытку повтора (ретрай).
    /// Пример: "↻ Повторная попытка 2/3 для страницы 5: {url}"
    /// </summary>
    public static void LogRetry(ConsoleLogger? logger, int attempt, int maxAttempts, string context)
    {
        WriteLine(logger, $"{RetryIcon} Повторная попытка {attempt}/{maxAttempts}: {context}");
    }

    /// <summary>
    /// Простая перегрузка LogEnqueue для обратной совместимости.
    /// Логирует постановку записи в очередь (enqueue) с произвольной строкой extra.
    /// Пример: "⇪ В очередь: {key} -> {url}"
    /// </summary>
    public static void LogEnqueue(ConsoleLogger? logger, string key, string url, string? extra = null)
    {
        var msg = $"{EnqueueIcon} В очередь: {key} -> {url}";
        if (!string.IsNullOrEmpty(extra))
            msg += $" {extra}";
        WriteLine(logger, msg);
    }

    /// <summary>
    /// Расширенная перегрузка LogEnqueue: принимает тип сущности,
    /// её идентификатор и список пар "имя поля" → "значение", которые были поставлены в очередь.
    /// Формирует единое форматированное сообщение, заменяющее множественный verbose-вывод
    /// через <c>_logger.WriteLine(...)</c>.
    /// Пример:
    /// "⇪ В очередь: Resume[userLink] { Name = 'Иван', Level = 'Senior', Skills = '12 шт.', Experience = '3 записей', ... }"
    /// </summary>
    /// <param name="logger">Логгер (может быть null — тогда пишется в Console).</param>
    /// <param name="entityType">Название сущности (например, "Resume", "Company", "UserProfile").</param>
    /// <param name="entityId">Идентификатор сущности (например, userLink, companyCode).</param>
    /// <param name="fields">Пары (имя, значение) для каждого поля, добавленного в очередь.</param>
    public static void LogEnqueue(
        ConsoleLogger? logger,
        string entityType,
        string entityId,
        params (string Name, object? Value)[] fields)
    {
        var header = $"{EnqueueIcon} В очередь: {entityType}[{entityId}]";
        var body = FormatFields(fields);
        var msg = string.IsNullOrEmpty(body) ? header : $"{header} {body}";
        WriteLine(logger, msg);
    }

    /// <summary>
    /// Логирует пропуск записи.
    /// Пример: "⏭ Пропуск: {reason}"
    /// </summary>
    public static void LogSkip(ConsoleLogger? logger, string reason)
    {
        WriteLine(logger, $"{SkipIcon} {reason}");
    }

    /// <summary>
    /// Логирует сохранение HTML-файла для отладки.
    /// Пример: "💾 HTML сохранён: {path} (кодировка: utf-8)"
    /// </summary>
    public static void LogHtmlSaved(ConsoleLogger? logger, string path, string? encodingName = null)
    {
        var msg = $"{HtmlIcon} HTML сохранён: {path}";
        if (!string.IsNullOrEmpty(encodingName))
            msg += $" (кодировка: {encodingName})";
        WriteLine(logger, msg);
    }

    /// <summary>
    /// Логирует счётчик загруженных/обработанных элементов.
    /// Пример: "Σ Загружено 42 пользователей из БД."
    /// </summary>
    public static void LogCount(ConsoleLogger? logger, string action, int count, string entity, string? suffix = null)
    {
        var msg = $"{CountIcon} {action} {count} {entity}";
        if (!string.IsNullOrEmpty(suffix))
            msg += suffix;
        WriteLine(logger, msg);
    }

    /// <summary>
    /// Форматирует набор пар (Name, Value) в читаемую строку: "{ Name = '...', Age = '...', ... }".
    /// Значение null выводится как "null". Значения-строки оборачиваются в кавычки.
    /// Коллекции выводятся как "N шт.".
    /// </summary>
    private static string FormatFields((string Name, object? Value)[] fields)
    {
        if (fields == null || fields.Length == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append("{ ");
        for (int i = 0; i < fields.Length; i++)
        {
            var (name, value) = fields[i];
            sb.Append(name).Append(" = ").Append(FormatValue(value));
            if (i < fields.Length - 1)
                sb.Append(", ");
        }
        sb.Append(" }");
        return sb.ToString();
    }

    /// <summary>
    /// Форматирует одно значение поля для LogEnqueue.
    /// </summary>
    private static string FormatValue(object? value)
    {
        if (value == null)
            return "null";

        // Коллекции (исключая строки) выводим как "N шт."
        if (value is string s)
        {
            // Ограничиваем длину строки, чтобы не засорять лог
            const int maxLen = 100;
            if (s.Length > maxLen)
                s = s.Substring(0, maxLen) + "…";
            return $"'{s}'";
        }

        if (value is System.Collections.ICollection col)
        {
            return $"{col.Count} шт.";
        }

        return value.ToString() ?? "null";
    }

    private static void WriteLine(ConsoleLogger? logger, string message)
    {
        if (logger != null)
        {
            logger.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }
}
