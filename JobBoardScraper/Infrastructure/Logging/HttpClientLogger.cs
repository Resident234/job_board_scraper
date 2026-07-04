namespace JobBoardScraper.Infrastructure.Logging;

/// <summary>
/// Formatting helpers for HTTP retry and proxy-client messages.
/// </summary>
public static class HttpClientLogger
{
    private const string RetryIcon = "↻";
    private const string ErrorIcon = "✖";
    private const string SkipIcon = "⏭";

    public static void LogThrottleRetry(
        ConsoleLogger? logger,
        int failedAttempt,
        int nextAttempt,
        int maxAttempts,
        string context,
        int delayMs,
        string? reason = null)
    {
        WriteLine(logger, FormatThrottleRetry(failedAttempt, nextAttempt, maxAttempts, context, delayMs, reason));
    }

    public static string FormatThrottleRetry(
        int failedAttempt,
        int nextAttempt,
        int maxAttempts,
        string context,
        int delayMs,
        string? reason = null)
    {
        var message = $"{RetryIcon} Ошибка на попытке {failedAttempt}/{maxAttempts}";
        if (!string.IsNullOrWhiteSpace(reason))
            message += $": {reason}";

        message += $". Повторная попытка {nextAttempt}/{maxAttempts} через {FormatDelay(delayMs)}: {context}";
        return message;
    }

    public static void LogError(ConsoleLogger? logger, string context, Exception ex)
    {
        WriteLine(logger, $"{ErrorIcon} {context}: {ex.Message}");
    }

    public static void LogSkip(ConsoleLogger? logger, string reason)
    {
        WriteLine(logger, $"{SkipIcon} {reason}");
    }

    private static string FormatDelay(int delayMs)
    {
        return delayMs < 1000
            ? $"{delayMs}мс"
            : $"{delayMs / 1000.0:F1}с";
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
