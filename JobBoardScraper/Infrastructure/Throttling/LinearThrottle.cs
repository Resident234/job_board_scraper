namespace JobBoardScraper.Infrastructure.Throttling;

/// <summary>
/// Linear retry throttle with a fixed or linearly increasing delay between attempts.
/// </summary>
public sealed class LinearThrottle
{
    private readonly int _baseDelayMs;
    private readonly int _stepDelayMs;
    private readonly int _maxDelayMs;

    public LinearThrottle(
        int maxAttempts,
        int baseDelayMs = 2000,
        int stepDelayMs = 0,
        int maxDelayMs = 30000)
    {
        MaxAttempts = Math.Max(1, maxAttempts);
        _baseDelayMs = Math.Max(0, baseDelayMs);
        _stepDelayMs = Math.Max(0, stepDelayMs);
        _maxDelayMs = Math.Max(_baseDelayMs, maxDelayMs);
    }

    public int MaxAttempts { get; }

    public int FailedAttempts { get; private set; }

    public int CurrentAttempt => FailedAttempts + 1;

    public bool CanAttempt => FailedAttempts < MaxAttempts;

    public bool IsExhausted => !CanAttempt;

    public int RegisterFailure()
    {
        FailedAttempts++;
        return FailedAttempts;
    }

    public int CurrentDelayMs => CalculateDelay(FailedAttempts, _baseDelayMs, _stepDelayMs, _maxDelayMs);

    public Task DelayAsync(CancellationToken ct)
    {
        return Task.Delay(TimeSpan.FromMilliseconds(CurrentDelayMs), ct);
    }

    public static int CalculateDelay(
        int attempt,
        int baseDelayMs = 2000,
        int stepDelayMs = 0,
        int maxDelayMs = 30000)
    {
        if (attempt < 1) attempt = 1;

        baseDelayMs = Math.Max(0, baseDelayMs);
        stepDelayMs = Math.Max(0, stepDelayMs);
        maxDelayMs = Math.Max(baseDelayMs, maxDelayMs);

        var delayMs = (long)baseDelayMs + (long)(attempt - 1) * stepDelayMs;
        return (int)Math.Min(delayMs, maxDelayMs);
    }

    public static string GetDelayDescription(int delayMs)
    {
        return delayMs < 1000
            ? $"{delayMs}мс"
            : $"{delayMs / 1000.0:F1}с";
    }
}
