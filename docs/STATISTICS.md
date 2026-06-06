# Statistics System Documentation

This document describes the statistics system used in the Job Board Scraper project for monitoring database operations and performance.

## Table of Contents

- [Overview](#overview)
- [Statistics Collection](#statistics-collection)
- [Statistics Reporting](#statistics-reporting)
- [Statistics Types](#statistics-types)
- [Key Components](#key-components)
- [Usage Examples](#usage-examples)
- [Best Practices](#best-practices)

## Overview

The Job Board Scraper includes a comprehensive statistics system that tracks and reports on all database operations. This system provides valuable insights into the performance, reliability, and efficiency of the scraping process.

## Statistics Collection

### Collection Period

- **Cumulative Collection**: Statistics are collected cumulatively since the `DatabaseClient` instance is created
- **No Automatic Reset**: Data is **not reset** after reporting - it continues to accumulate throughout the client's lifetime
- **Lifetime Statistics**: Each dump shows the **total statistics since startup**, not just the period since last dump

### What This Means

```csharp
// At 10:00: Create DatabaseClient
var client = new DatabaseClient(connectionString, logger);

// At 10:05: First statistics dump (5 minutes of data)
// At 10:10: Second statistics dump (10 minutes of data)
// At 10:15: Third statistics dump (15 minutes of data)
// ...
```

## Statistics Reporting

### Reporting Frequency

- **Automatic Dumping**: Statistics are dumped to logs every **5 minutes** via the `TryDumpStatistics()` method
- **Interval Control**: The dump interval is controlled by `_statsDumpInterval = TimeSpan.FromMinutes(5)`
- **Trigger Mechanism**: `TryDumpStatistics()` is called after major database operations

### Reporting Behavior

- **Non-Intrusive**: Statistics dumping doesn't interfere with normal operations
- **Log-Based**: Output is written to the configured logger
- **No Data Loss**: Counters are not reset after logging

## Statistics Types

The system collects several types of operational statistics:

### 1. Insert Operations
- Count of successful insert operations per table
- Examples: New resumes, companies, skills, etc.

### 2. Update Operations
- Count of successful update operations per table
- Examples: Profile updates, company details, etc.

### 3. Error Operations
- Count of failed operations with error details per table
- Helps identify problematic areas

### 4. Skipped Operations
- Count of operations that were skipped
- Reasons: Duplicates, 404 pages, validation failures, etc.

### 5. Table-Specific Metrics
- Detailed counts for each database table
- Helps analyze distribution of operations

## Key Components

### DatabaseStatistics Class

The core class that maintains all statistical counters:

```csharp
public class DatabaseStatistics
{
    // Maintains counters for each table and operation type
    private Dictionary<string, TableStatistics> _tableStats = new();

    // Initializes tracking for all known tables
    public void InitializeAllTables()

    // Records an insert operation for a table
    public void RecordInsert(string tableName, string key)

    // Records an update operation for a table
    public void RecordUpdate(string tableName, string key)

    // Records an error operation for a table
    public void RecordError(string tableName, string key)

    // Records a skipped operation for a table
    public void RecordSkipped(string tableName, string key)

    // Gets a formatted summary of all statistics
    public string GetSummary()
}
```

### TryDumpStatistics() Method

Controls when statistics are logged:

```csharp
private void TryDumpStatistics()
{
    if (DateTime.Now - _lastStatsDump >= _statsDumpInterval)
    {
        Log(_statistics.GetSummary());
        _lastStatsDump = DateTime.Now;
    }
}
```

### Statistics Property

Provides access to statistics for external monitoring:

```csharp
public DatabaseStatistics Statistics => _statistics;
```

## Usage Examples

### Basic Usage

```csharp
// Statistics are automatically collected during normal operations
var client = new DatabaseClient(connectionString, logger);

// Access current statistics programmatically
var stats = client.Statistics;
Console.WriteLine($"Total inserts: {stats.GetTotalInserts()}");
Console.WriteLine($"Total errors: {stats.GetTotalErrors()}");
```

### Example Log Output

```
[DB] Statistics Summary:
    habr_resumes: 125 inserts, 42 updates, 3 errors, 8 skips
    habr_companies: 34 inserts, 12 updates, 0 errors, 1 skip
    habr_skills: 456 inserts, 23 updates, 1 error, 0 skips
    Total operations: 214 successful, 3 errors
```

### Programmatic Access

```csharp
// Get statistics for specific tables
var resumeStats = client.Statistics.GetTableStatistics("habr_resumes");
Console.WriteLine($"Resume inserts: {resumeStats.InsertCount}");
Console.WriteLine($"Resume errors: {resumeStats.ErrorCount}");

// Get overall statistics
Console.WriteLine($"Total operations: {client.Statistics.GetTotalOperations()}");
Console.WriteLine($"Success rate: {client.Statistics.GetSuccessRate():P}");
```

## Best Practices

### 1. Long-running Monitoring

Statistics are ideal for monitoring long-running scraper sessions:

```csharp
// Start scraper
var scraper = new ResumeScraper(client);
scraper.Start();

// Monitor statistics periodically
while (scraper.IsRunning)
{
    var stats = client.Statistics;
    Console.WriteLine($"Progress: {stats.GetTotalInserts()} resumes processed");
    Thread.Sleep(TimeSpan.FromMinutes(1));
}
```

### 2. Performance Analysis

Use statistics to identify bottlenecks and error patterns:

```csharp
// Analyze error rates
var errorRate = (double)stats.GetTotalErrors() / stats.GetTotalOperations();
if (errorRate > 0.05) // 5% error threshold
{
    Console.WriteLine($"High error rate detected: {errorRate:P}");
    // Trigger alerts or corrective actions
}
```

### 3. Periodic Reset

For time-based analysis, create new `DatabaseClient` instances:

```csharp
// Hourly statistics
while (keepRunning)
{
    var hourlyClient = new DatabaseClient(connectionString, logger);
    RunScraperForOneHour(hourlyClient);
    var hourlyStats = hourlyClient.Statistics.GetSummary();
    SaveHourlyReport(hourlyStats);
    hourlyClient.Dispose();
}
```

### 4. External Integration

Export statistics to monitoring systems:

```csharp
// Integrate with monitoring systems
var stats = client.Statistics;
monitoringSystem.RecordMetrics([
    new Metric("db.inserts", stats.GetTotalInserts()),
    new Metric("db.updates", stats.GetTotalUpdates()),
    new Metric("db.errors", stats.GetTotalErrors()),
    new Metric("db.success_rate", stats.GetSuccessRate())
]);
```

### 5. Alerting

Set up alerts based on statistics thresholds:

```csharp
// Monitor for issues
if (stats.GetErrorRate("habr_resumes") > 0.1) // 10% error rate
{
    alertSystem.Trigger("High resume error rate");
}

if (stats.GetSkippedCount("habr_companies") > 100)
{
    alertSystem.Trigger("Many company operations skipped");
}
```

## Advanced Usage

### Custom Statistics

Extend the system with custom metrics:

```csharp
// Add custom statistics tracking
client.Statistics.TrackCustomMetric("scraping.speed", resumesPerMinute);
client.Statistics.TrackCustomMetric("proxy.success_rate", successfulProxyAttempts / totalAttempts);

// Access custom metrics
var speed = client.Statistics.GetCustomMetric("scraping.speed");
var proxySuccess = client.Statistics.GetCustomMetric("proxy.success_rate");
```

### Statistics Export

Export statistics for external analysis:

```csharp
// Export to JSON
var statsJson = client.Statistics.ExportToJson();
File.WriteAllText("stats.json", statsJson);

// Export to CSV
var statsCsv = client.Statistics.ExportToCsv();
File.WriteAllText("stats.csv", statsCsv);
```

## Troubleshooting

### Common Issues

**Statistics not appearing in logs:**
- Check that `_statsDumpInterval` is set correctly
- Verify that `TryDumpStatistics()` is being called
- Ensure logger is properly configured

**Incorrect statistics counts:**
- Verify that all database operations use the statistics methods
- Check for race conditions in multi-threaded scenarios
- Ensure proper initialization with `InitializeAllTables()`

**Performance impact:**
- Statistics collection has minimal overhead
- If needed, increase the dump interval
- Consider sampling for very high-volume operations

## Integration with Other Systems

### Monitoring Dashboards

```csharp
// Prometheus integration example
var registry = new CollectorRegistry();
var insertCounter = Metrics.CreateCounter("jobboard_inserts_total", "Total inserts", new CounterConfiguration
{
    LabelNames = new[] { "table" }
});

client.Statistics.OnInsertRecorded += (table, key) => {
    insertCounter.WithLabels(table).Inc();
};
```

### Logging Systems

```csharp
// Structured logging integration
client.Statistics.OnSummaryGenerated += summary => {
    logger.LogInformation("Statistics Summary: {Summary}", summary);
    // Can be parsed by log analysis tools
};
```

## Conclusion

The statistics system provides comprehensive monitoring capabilities for the Job Board Scraper. By understanding how statistics are collected, reported, and analyzed, you can effectively monitor performance, identify issues, and optimize the scraping process.

For more information about specific components, refer to the architecture documentation and individual module documentation.