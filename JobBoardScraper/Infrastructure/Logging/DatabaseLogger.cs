using System.Reflection;

namespace JobBoardScraper.Infrastructure.Logging;

/// <summary>
/// Обёртка для типовых сообщений операций с БД.
/// Инкапсулирует форматирование INSERT/UPDATE/SKIP/DELETE и очереди записи в едином стиле с иконками.
/// </summary>
public sealed class DatabaseLogger
{
    private const string InsertIcon = "✅";
    private const string UpdateIcon = "↻";
    private const string ErrorIcon = "❌";
    private const string SkipIcon = "⏭";
    private const string InfoIcon = "ℹ";
    private const string DeleteIcon = "🗑";
    private const int MaxRecordLogDepth = 3;

    private readonly ConsoleLogger _logger;

    public DatabaseLogger(ConsoleLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Log(string message)
    {
        _logger.WriteLine(message);
    }

    public void LogError(string entity, string entityName, string errorText)
    {
        if (string.IsNullOrEmpty(entityName))
            Log($"[DB] {entity}: {ErrorIcon} ERROR - {errorText}");
        else
            Log($"[DB] {entity} {entityName}: {ErrorIcon} ERROR - {errorText}");
    }

    public void LogInsert(string entity, string entityName, string id)
    {
        if (string.IsNullOrEmpty(entityName))
            Log($"[DB] {entity}: {InsertIcon} INSERT (id={id})");
        else
            Log($"[DB] {entity} {entityName}: {InsertIcon} INSERT (id={id})");
    }

    public void LogUpdate(string entity, string entityName, string id)
    {
        if (string.IsNullOrEmpty(entityName))
            Log($"[DB] {entity}: {UpdateIcon} UPDATE (id={id})");
        else
            Log($"[DB] {entity} {entityName}: {UpdateIcon} UPDATE (id={id})");
    }

    public void LogEnqueue(string recordType, object? record)
    {
        Log($"[DB Queue] {recordType}: {FormatRecord(record)}");
    }

    /// <summary>
    /// Логирует факт удаления записей в едином формате с иконкой 🗑.
    /// Все формирование сообщения (entityLabel, сущность, описание удалённого и количество)
    /// выполняется внутри обёртки. Снаружи передаётся заголовок сущности,
    /// текстовое описание того, что удалено, и количество удалённых записей.
    /// </summary>
    /// <param name="entityLabel">Текст после префикса "[DB] " и до двоеточия, например "UserSkills habr_user" или "Дополнительное образование".</param>
    /// <param name="deletedDescription">Краткое описание того, что удалено (например "старых связей", "старых записей", "записей").</param>
    /// <param name="count">Количество удалённых записей.</param>
    /// <param name="fields">Пары (имя поля, значение). Поля со значением null пропускаются.</param>
    public void LogDelete(string entityLabel, string deletedDescription, int count, params (string Name, object? Value)[] fields)
    {
        var parts = new List<string> { $"[DB] {entityLabel}: {DeleteIcon} удалено {deletedDescription}={count}" };

        foreach (var (name, value) in fields)
        {
            if (value is null)
                continue;

            parts.Add(FormatLogField(name, value));
        }

        Log(string.Join(" | ", parts));
    }

    /// <summary>
    /// Логирует количественный результат загрузки/обработки в едином формате.
    /// Например: "Загружено N company_id из БД" или "Пропущено N связей ...".
    /// </summary>
    /// <param name="action">Действие в прошедшем времени (например, "Загружено", "Пропущено", "Добавлено").</param>
    /// <param name="count">Количество элементов.</param>
    /// <param name="entityLabel">Описание того, что считается (например, "company_id", "компаний").</param>
    /// <param name="suffix">Опциональный суффикс сообщения (например, " из БД", " пользователей").</param>
    public void LogCount(string action, int count, string entityLabel, string suffix = "")
    {
        Log($"[DB] {action} {count} {entityLabel}{suffix}");
    }

    /// <summary>
    /// Логирует события фоновой задачи записи в БД в едином формате с иконкой ℹ.
    /// </summary>
    public void LogWriter(string eventName, string description, params (string Name, object? Value)[] fields)
    {
        var parts = new List<string> { $"[DB Writer] {eventName}: {InfoIcon} {description}" };

        foreach (var (name, value) in fields)
        {
            if (value is null)
                continue;

            parts.Add(FormatLogField(name, value));
        }

        Log(string.Join(" | ", parts));
    }

    /// <summary>
    /// Логирует SKIP-операцию (пропуск записи) с подробным списком непустых полей.
    /// Все проверки на null и форматирование значений выполняются внутри обёртки.
    /// Снаружи передаётся только заголовок сущности, причина пропуска и пары (имя поля, значение).
    /// </summary>
    /// <param name="entityLabel">Текст после префикса "[DB] " и до двоеточия, например "Resume habr_user".</param>
    /// <param name="reason">Краткое описание причины пропуска (например "404 страница", "уже существует").</param>
    /// <param name="fields">Пары (имя поля, значение). Поля со значением null пропускаются.</param>
    public void LogSkip(string entityLabel, string reason, params (string Name, object? Value)[] fields)
    {
        var parts = new List<string> { $"[DB] {entityLabel}: {SkipIcon} SKIP ({reason})" };

        foreach (var (name, value) in fields)
        {
            if (value is null)
                continue;

            parts.Add(FormatLogField(name, value));
        }

        Log(string.Join(" | ", parts));
    }

    /// <summary>
    /// Логирует INSERT/UPDATE операцию с подробным списком непустых полей.
    /// Все проверки на null, форматирование значений и выбор иконки INSERT/UPDATE
    /// выполняются внутри обёртки. Снаружи передаётся только заголовок сущности,
    /// флаг isInsert и пары (имя поля, значение).
    /// </summary>
    /// <param name="entityLabel">Текст после префикса "[DB] " и до двоеточия, например "Компания habr_company".</param>
    /// <param name="isInsert">true для INSERT (иконка ✅), false для UPDATE (иконка ↻).</param>
    /// <param name="fields">Пары (имя поля, значение). Поля со значением null пропускаются.</param>
    public void LogParts(string entityLabel, bool isInsert, params (string Name, object? Value)[] fields)
    {
        var parts = new List<string> { $"[DB] {entityLabel}:" };

        foreach (var (name, value) in fields)
        {
            if (value is null)
                continue;

            parts.Add(FormatLogField(name, value));
        }

        parts.Add(isInsert ? $"{InsertIcon} INSERT" : $"{UpdateIcon} UPDATE");

        Log(string.Join(" | ", parts));
    }

    /// <summary>
    /// Форматирует одно поле для логирования. Ожидается, что value уже не null.
    /// Поддерживает: ICollection (логируется количество элементов),
    /// decimal/double/float (формат F2), string (с обрезкой до 50 символов),
    /// остальные типы — через ToString().
    /// </summary>
    private static string FormatLogField(string name, object value)
    {
        if (value is System.Collections.ICollection collection)
            return $"{name}={collection.Count}";

        if (value is decimal d)
            return $"{name}={d.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";

        if (value is double db)
            return $"{name}={db.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";

        if (value is float f)
            return $"{name}={f.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";

        if (value is string s)
        {
            var preview = s.Length > 50 ? s.Substring(0, 50) + "..." : s;
            return $"{name}={preview}";
        }

        return $"{name}={value}";
    }

    private static string FormatRecord(object? record)
    {
        if (record is null)
            return "null";

        return FormatValue(record, depth: 0);
    }

    private static string FormatValue(object? value, int depth)
    {
        if (value == null)
            return "<null>";

        if (depth > MaxRecordLogDepth)
            return value.ToString() ?? "<value>";

        var type = value.GetType();

        if (type.IsEnum)
            return value.ToString() ?? "<enum>";

        if (type == typeof(string))
            return string.IsNullOrWhiteSpace((string)value) ? "<empty>" : EscapeLogValue((string)value);

        if (IsScalarType(type))
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "<value>";

        if (value is System.Collections.IEnumerable enumerable && type != typeof(string))
            return FormatEnumerable(enumerable, depth);

        return FormatObjectProperties(value, depth);
    }

    private static string FormatObjectProperties(object value, int depth)
    {
        var properties = value.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.MetadataToken)
            .Select(property =>
            {
                try
                {
                    return $"{property.Name}={FormatValue(property.GetValue(value), depth + 1)}";
                }
                catch (Exception ex)
                {
                    return $"{property.Name}=<error reading property: {EscapeLogValue(ex.Message)}>";
                }
            });

        return "{" + string.Join(", ", properties) + "}";
    }

    private static string FormatEnumerable(System.Collections.IEnumerable enumerable, int depth)
    {
        var items = new List<string>();

        foreach (var item in enumerable)
        {
            items.Add(FormatValue(item, depth + 1));
        }

        return items.Count == 0 ? "[]" : "[" + string.Join("; ", items) + "]";
    }

    private static bool IsScalarType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return type.IsPrimitive
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid);
    }

    private static string EscapeLogValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}
