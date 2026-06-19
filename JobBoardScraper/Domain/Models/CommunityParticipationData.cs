using System.Text.Json.Serialization;

namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Данные об участии пользователя в профессиональных сообществах
/// </summary>
public class CommunityParticipationData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("member_since")]
    public string? MemberSince { get; set; }

    [JsonPropertyName("contribution")]
    public string? Contribution { get; set; }

    [JsonPropertyName("topics")]
    public string? Topics { get; set; }
}
