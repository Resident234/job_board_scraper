using System.Text.Json.Serialization;

namespace JobBoardScraper.Proxy;

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

/// <summary>
/// Контейнер для сериализации whitelist в JSON
/// </summary>
public class WhitelistData
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }

    [JsonPropertyName("entries")]
    public List<WhitelistProxyEntry> Entries { get; set; } = new();
}
