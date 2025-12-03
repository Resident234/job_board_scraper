namespace JobBoardScraper.Helper.Utils;

/// <summary>
/// Реализация алгоритма Exponential Backoff with Jitter для расчета задержки между повторами
/// </summary>
public static class ExponentialBackoff
{
    private static readonly Random _random = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Рассчитывает задержку по алгоритму Exponential Backoff with Jitter
    /// </summary>
    /// <param name="attempt">Номер попытки (начиная с 1)</param>
    /// <param name="baseDelayMs">Базовая задержка в миллисекундах (по умолчанию 1000)</param>
    /// <param name="maxDelayMs">Максимальная задержка в миллисекундах (по умолчанию 30000)</param>
    /// <param name="jitterFactor">Фактор рандомизации от 0 до 1 (по умолчанию 0.3 = ±30%)</param>
    /// <returns>Задержка в миллисекундах</returns>
    public static int CalculateDelay(
        int attempt,
        int baseDelayMs = 1000,
        int maxDelayMs = 30000,
        double jitterFactor = 0.3)
    {
        if (attempt < 1) attempt = 1;
        
        // Экспоненциальный рост: baseDelay * 2^(attempt-1)
        // Используем Math.Min для предотвращения переполнения
        int exponentialDelay;
        if (attempt > 20)
        {
            exponentialDelay = maxDelayMs;
        }
        else
        {
            exponentialDelay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
        }
        
        // Ограничиваем максимальной задержкой
        int cappedDelay = Math.Min(exponentialDelay, maxDelayMs);
        
        // Добавляем jitter (рандомизацию)
        double randomValue;
        lock (_lock)
        {
            randomValue = _random.NextDouble() * 2 - 1; // от -1 до +1
        }
        int jitter = (int)(cappedDelay * jitterFactor * randomValue);
        
        // Финальная задержка (минимум 100мс)
        return Math.Max(100, cappedDelay + jitter);
    }

    /// <summary>
    /// Рассчитывает задержку для ошибок сервера (5xx)
    /// Использует более агрессивные параметры
    /// </summary>
    public static int CalculateServerErrorDelay(int attempt)
    {
        return CalculateDelay(
            attempt: attempt,
            baseDelayMs: 2000,    // Начинаем с 2 секунд
            maxDelayMs: 60000,    // Максимум 60 секунд
            jitterFactor: 0.3
        );
    }

    /// <summary>
    /// Рассчитывает задержку для ошибок прокси/сети
    /// Использует менее агрессивные параметры
    /// </summary>
    public static int CalculateProxyErrorDelay(int attempt)
    {
        return CalculateDelay(
            attempt: attempt,
            baseDelayMs: 500,     // Начинаем с 0.5 секунды
            maxDelayMs: 10000,    // Максимум 10 секунд
            jitterFactor: 0.2
        );
    }

    /// <summary>
    /// Возвращает описание задержки для логирования
    /// </summary>
    public static string GetDelayDescription(int delayMs)
    {
        if (delayMs < 1000)
            return $"{delayMs}мс";
        else
            return $"{delayMs / 1000.0:F1}с";
    }
}
