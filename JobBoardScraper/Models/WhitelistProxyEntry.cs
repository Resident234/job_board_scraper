using System.Text.Json.Serialization;

namespace JobBoardScraper.Models;

/// <summary>
/// Запись прокси в whitelist с отслеживанием состояния
/// </summary>
public class WhitelistProxyEntry
{
    [JsonPropertyName("proxyUrl")]
    public string ProxyUrl { get; set; } = string.Empty;

    [JsonPropertyName("lastUsed")]
    public DateTime LastUsed { get; set; }

    [JsonPropertyName("isFailed")]
    public bool IsFailed { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    [JsonPropertyName("failedSince")]
    public DateTime? FailedSince { get; set; }

    public bool IsCooldownPassed(TimeSpan cooldownPeriod) =>
        DateTime.UtcNow - LastUsed > cooldownPeriod;

    public bool IsAvailable(TimeSpan cooldownPeriod) =>
        IsCooldownPassed(cooldownPeriod) && (!IsFailed || IsCooldownPassed(cooldownPeriod));
}
