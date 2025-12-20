using System.Text.Json.Serialization;

namespace JobBoardScraper.Domain.Models;

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
