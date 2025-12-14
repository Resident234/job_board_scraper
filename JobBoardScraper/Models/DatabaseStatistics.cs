using System.Collections.Concurrent;

namespace JobBoardScraper.Models;

/// <summary>
/// Статистика операций с базой данных по таблицам
/// </summary>
public class DatabaseStatistics
{
    private readonly ConcurrentDictionary<string, TableStatistics> _tableStats = new();
    private readonly DateTime _startTime = DateTime.Now;

    /// <summary>
    /// Получить или создать статистику для таблицы
    /// </summary>
    public TableStatistics GetTableStats(string tableName)
    {
        return _tableStats.GetOrAdd(tableName, _ => new TableStatistics(tableName));
    }

    /// <summary>
    /// Зарегистрировать INSERT операцию
    /// </summary>
    public void RecordInsert(string tableName, string? recordId = null)
    {
        GetTableStats(tableName).RecordInsert();
    }

    /// <summary>
    /// Зарегистрировать UPDATE операцию
    /// </summary>
    public void RecordUpdate(string tableName, string? recordId = null)
    {
        GetTableStats(tableName).RecordUpdate();
    }

    /// <summary>
    /// Зарегистрировать пропуск (запись уже существует, без изменений)
    /// </summary>
    public void RecordSkipped(string tableName, string? recordId = null)
    {
        GetTableStats(tableName).RecordSkipped();
    }

    /// <summary>
    /// Зарегистрировать ошибку
    /// </summary>
    public void RecordError(string tableName, string? recordId = null)
    {
        GetTableStats(tableName).RecordError();
    }

    /// <summary>
    /// Получить сводку по всем таблицам
    /// </summary>
    public string GetSummary()
    {
        var elapsed = DateTime.Now - _startTime;
        var lines = new List<string>
        {
            $"=== Статистика БД (время работы: {elapsed:hh\\:mm\\:ss}) ==="
        };

        foreach (var kvp in _tableStats.OrderBy(x => x.Key))
        {
            lines.Add(kvp.Value.ToString());
        }

        var totalInserts = _tableStats.Values.Sum(t => t.Inserts);
        var totalUpdates = _tableStats.Values.Sum(t => t.Updates);
        var totalSkipped = _tableStats.Values.Sum(t => t.Skipped);
        var totalErrors = _tableStats.Values.Sum(t => t.Errors);

        lines.Add($"--- ИТОГО: Вставлено={totalInserts}, Обновлено={totalUpdates}, Пропущено={totalSkipped}, Ошибок={totalErrors} ---");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Сбросить статистику
    /// </summary>
    public void Reset()
    {
        _tableStats.Clear();
    }
}

/// <summary>
/// Статистика операций для одной таблицы
/// </summary>
public class TableStatistics
{
    public string TableName { get; }
    public int Inserts => _inserts;
    public int Updates => _updates;
    public int Skipped => _skipped;
    public int Errors => _errors;

    private int _inserts;
    private int _updates;
    private int _skipped;
    private int _errors;

    public TableStatistics(string tableName)
    {
        TableName = tableName;
    }

    public void RecordInsert() => Interlocked.Increment(ref _inserts);
    public void RecordUpdate() => Interlocked.Increment(ref _updates);
    public void RecordSkipped() => Interlocked.Increment(ref _skipped);
    public void RecordError() => Interlocked.Increment(ref _errors);

    public override string ToString()
    {
        return $"  {TableName}: Вставлено={_inserts}, Обновлено={_updates}, Пропущено={_skipped}, Ошибок={_errors}";
    }
}
