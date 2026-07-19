# Statistics System Documentation

This document describes the statistics system used in the Job Board Scraper project for monitoring database operations and performance.

## Table of Contents

- [Overview](#overview)
- [Database Statistics](#database-statistics)
- [Scraper Statistics](#scraperstatistics-class)
- [Traffic Statistics](#trafficstatistics-class)
- [Key Components](#key-components)
- [Usage Examples](#usage-examples)

## Overview

The Job Board Scraper includes three independent statistics systems:

1. **DatabaseStatistics** — per-table operation counters (inserts, updates, deletes, skips, errors)
2. **ScraperStatistics** — per-scraper performance counters (processed, success, errors, skipped, active requests)
3. **TrafficStatistics** — per-scraper HTTP traffic measurement (bytes sent/received)

## Database Statistics

### Collection Period

- **Cumulative Collection**: Statistics are collected cumulatively since the `DatabaseClient` instance is created
- **No Automatic Reset**: Data is **not reset** after reporting - it continues to accumulate throughout the client's lifetime
- **Lifetime Statistics**: Each dump shows the **total statistics since startup**, not just the period since last dump

### Reporting Frequency

- **Automatic Dumping**: Statistics are dumped to logs every time the writer task processes a queue batch
- **Interval Control**: Dump interval is controlled by `_statsDumpInterval = TimeSpan.FromMinutes(5)` in `DatabaseClient`
- **Log Format**: Summary is output in Russian through the configured logger

## Statistics Types

### 1. Insert Operations
Count of successful insert operations per table (e.g., new resumes, companies, skills).

### 2. Update Operations
Count of successful update operations per table.

### 3. Delete Operations
Count of delete operations per table (using cascading delete for experience records).

### 4. Skipped Operations
Count of operations that were skipped due to duplicates, 404 pages, validation failures, etc.

### 5. Error Operations
Count of failed operations with error details per table.

### 6. Table-Specific Metrics
Detailed counts for 13 monitored tables:
- `habr_resumes`, `habr_companies`, `habr_category_root_ids`, `habr_skills`, `habr_company_skills`
- `habr_levels`, `habr_user_skills`, `habr_user_experience`, `habr_user_experience_skills`
- `habr_resumes_universities`, `habr_resumes_educations`, `habr_universities`, `habr_company_reviews`

## Key Components

### DatabaseStatistics Class

```csharp
public class DatabaseStatistics
{
    // Инициализация всех таблиц для отображения в статистике
    public void InitializeAllTables()

    // Получить или создать статистику для таблицы
    public TableStatistics GetTableStats(string tableName)

    // Зарегистрировать INSERT операцию
    public void RecordInsert(string tableName, string? recordId = null)

    // Зарегистрировать UPDATE операцию
    public void RecordUpdate(string tableName, string? recordId = null)

    // Зарегистрировать DELETE операцию
    public void RecordDelete(string tableName, string? recordId = null)

    // Зарегистрировать пропуск (запись уже существует, без изменений)
    public void RecordSkipped(string tableName, string? recordId = null)

    // Зарегистрировать ошибку
    public void RecordError(string tableName, string? recordId = null)

    // Получить сводку по всем таблицам
    public string GetSummary()

    // Сбросить статистику
    public void Reset()
}
```

### TableStatistics Class

```csharp
public class TableStatistics
{
    public string TableName { get; }
    public int Inserts { get; }
    public int Updates { get; }
    public int Deletes { get; }
    public int Skipped { get; }
    public int Errors { get; }

    public void RecordInsert()
    public void RecordUpdate()
    public void RecordDelete()
    public void RecordSkipped()
    public void RecordError()
    public override string ToString()
}
```

### ScraperStatistics Class

`ScraperStatistics` provides per-scraper performance tracking:

```csharp
public class ScraperStatistics
{
    public int TotalFound { get; set; }
    public int TotalNotFound { get; set; }
    public int TotalItemsCollected { get; set; }
    public int Processed { get; set; }
    public int Success { get; set; }
    public int Errors { get; set; }
    public int Skipped { get; set; }
    public int ActiveRequests { get; set; }
    public DateTime StartTime { get; set; }

    // Потокобезопасное обновление счётчиков
    public void IncrementProcessed()
    public void IncrementSuccess()
    public void IncrementError()
    public void IncrementSkipped()
    public void SetActiveRequests(int count)

    // Форматированный вывод
    public override string ToString()
}
```

### TrafficStatistics Class

`TrafficStatistics` measures HTTP traffic per scraper:

```csharp
public class TrafficStatistics : IDisposable
{
    // Регистрация HTTP-запроса (размер ответа в байтах)
    public void RecordRequest(string scraperName, long bytes)

    // Получить статистику для скрапера
    public ScraperTrafficStats? GetStats(string scraperName)

    // Получить общую статистику (суммарные байты и количество запросов)
    public (long TotalBytes, long TotalRequests) GetTotalStats()

    // Сохранить отчёт в файл (публичный, вызывается в Dispose и по таймеру)
    public void SaveToFile()
}
```

Также `ScraperTrafficStats` предоставляет per-scraper статистику:

```csharp
public class ScraperTrafficStats
{
    public long TotalBytes { get; }
    public long RequestCount { get; }
    public double AverageBytesPerRequest { get; }

    public void AddRequest(long bytes)
    public string FormatBytes(long bytes)
}
```

## Example Log Output

```
=== Статистика БД (время работы: 01:23:45) ===
  habr_companies: Вставлено=34, Обновлено=12, Удалено=0, Пропущено=1, Ошибок=0
  habr_levels: Вставлено=5, Обновлено=0, Удалено=0, Пропущено=0, Ошибок=0
  habr_resumes: Вставлено=125, Обновлено=42, Удалено=0, Пропущено=8, Ошибок=3
  ...
--- ИТОГО: Вставлено=456, Обновлено=87, Удалено=0, Пропущено=15, Ошибок=5 ---
```

## Usage Examples

### Accessing Database Statistics

```csharp
var db = new DatabaseClient(connectionString, logger);
db.Statistics.RecordInsert("habr_resumes", resumeLink);
db.Statistics.RecordError("habr_companies", companyCode);

// Получить сводку
var summary = db.Statistics.GetSummary();
logger.WriteLine(summary);
```

### Accessing Scraper Statistics

```csharp
var stats = new ScraperStatistics();
stats.IncrementProcessed();
stats.IncrementSuccess();
stats.IncrementError();
stats.IncrementSkipped();
stats.SetActiveRequests(5);

Console.WriteLine(stats.ToString());
// Output: [ScraperName] Processed: 10, Success: 8, Errors: 1, Skipped: 1, Active: 5
```

### Accessing Traffic Statistics

```csharp
using var trafficStats = new TrafficStatistics(outputFile, saveInterval);
trafficStats.RecordRequest("UserResumeDetailScraper", responseBytes);
var (totalBytes, totalRequests) = trafficStats.GetTotalStats();

// Принудительно сохранить отчёт
trafficStats.SaveToFile();
```

## Best Practices

1. **Long-running Monitoring**: Statistics are ideal for monitoring long-running scraper sessions
2. **Performance Analysis**: Use statistics to identify bottlenecks and error patterns
3. **Traffic Tracking**: Monitor bandwidth usage per scraper via `TrafficStatistics`
4. **Thread Safety**: All statistics classes use thread-safe operations (`Interlocked`, `ConcurrentDictionary`)

## Integration with Other Systems

### Scraper Statistics Integration

Each scraper uses `ScraperStatistics` and integrates with `ScraperParallelLogger` for real-time progress reporting:

```csharp
// В скрапере
_stats.IncrementProcessed();
_stats.IncrementSuccess();
_parallelLogger.LogProgress(_stats, totalTasks, scraperName);
```

### Traffic Statistics Integration

Traffic statistics are integrated with `SmartHttpClient`:

```csharp
// SmartHttpClient автоматически измеряет и записывает трафик
var response = await _httpClient.GetAsync(url, cancellationToken);
// Traffic записывается после успешного ответа
```

## Troubleshooting

### Common Issues

**Statistics not appearing in logs:**
- Check that the writer task is running and processing queue batches
- Verify that database operations are calling statistics methods
- Ensure logger `OutputMode` is set correctly (not `FileOnly`)

**Incorrect statistics counts:**
- Verify that all database operations use the statistics methods
- Check for race conditions in multi-threaded scenarios
- Ensure proper initialization with `InitializeAllTables()`

**Performance impact:**
- Statistics collection has minimal overhead
- Thread-safe counters use `Interlocked` operations
- No locks or performance-heavy operations in hot paths

For more information about specific components, refer to the architecture documentation and individual module documentation.