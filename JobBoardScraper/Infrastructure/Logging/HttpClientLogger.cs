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
        ConsoleLogger logger,
        int failedAttempt,
        int nextAttempt,
        int maxAttempts,
        string context,
        int delayMs,
        string? reason = null)
    {
        logger.WriteLine(FormatThrottleRetry(failedAttempt, nextAttempt, maxAttempts, context, delayMs, reason));
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

    public static void LogError(ConsoleLogger logger, string context, Exception ex)
    {
        logger.WriteLine($"{ErrorIcon} {context}: {ex.Message}");
    }

    public static void LogSkip(ConsoleLogger logger, string reason)
    {
        logger.WriteLine($"{SkipIcon} {reason}");
    }

    /// <summary>
    /// Логирует информационное сообщение.
    /// </summary>
    public static void LogInfo(ConsoleLogger logger, string message)
    {
        logger.WriteLine(message);
    }

    private static string FormatDelay(int delayMs)
    {
        return delayMs < 1000
            ? $"{delayMs}мс"
            : $"{delayMs / 1000.0:F1}с";
    }
}
