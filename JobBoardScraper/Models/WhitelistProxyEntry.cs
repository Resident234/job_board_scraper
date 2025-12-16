using System.Text.Json.Serialization;

namespace JobBoardScraper.Models;

/// <summary>
/// Запись прокси в whitelist с отслеживанием состояния
/// </summary>
public class WhitelistProxyEntry
{
    /// <summary>
    /// URL прокси (например, http://1.2.3.4:8080)
    /// </summary>
    [JsonPropertyName("proxyUrl")]
    public string ProxyUrl { get; set; } = string.Empty;

    /// <summary>
    /// Дата и время последнего использования прокси
    /// </summary>
    [JsonPropertyName("lastUsed")]
    public DateTime LastUsed { get; set; }

    /// <summary>
    /// Флаг, указывающий что прокси не работает
    /// </summary>
    [JsonPropertyName("isFailed")]
    public bool IsFailed { get; set; }

    /// <summary>
    /// Количество неудачных попыток использования
    /// </summary>
    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    /// <summary>
    /// Дата и время, когда прокси впервые перестал работать
    /// </summary>
    [JsonPropertyName("failedSince")]
    public DateTime? FailedSince { get; set; }

    /// <summary>
    /// Проверяет, прошёл ли cooldown период с момента последнего использования
    /// </summary>
    public bool IsCooldownPassed(TimeSpan cooldownPeriod)
    {
        return DateTime.UtcNow - LastUsed > cooldownPeriod;
    }

    /// <summary>
    /// Проверяет, доступен ли прокси для использования
    /// </summary>
    public bool IsAvailable(TimeSpan cooldownPeriod)
    {
        // Прокси доступен если:
        // 1. Прошёл cooldown период И
        // 2. Либо не помечен как failed, либо прошёл cooldown (даёт шанс восстановиться)
        return IsCooldownPassed(cooldownPeriod) && (!IsFailed || IsCooldownPassed(cooldownPeriod));
    }
}
