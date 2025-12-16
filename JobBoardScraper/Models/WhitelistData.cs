using System.Text.Json.Serialization;

namespace JobBoardScraper.Models;

/// <summary>
/// Контейнер для хранения whitelist прокси в JSON файле
/// </summary>
public class WhitelistData
{
    /// <summary>
    /// Версия формата данных
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Дата и время последнего обновления whitelist
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Список записей прокси в whitelist
    /// </summary>
    [JsonPropertyName("entries")]
    public List<WhitelistProxyEntry> Entries { get; set; } = new();
}
