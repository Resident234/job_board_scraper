using System.Text.Json.Serialization;

namespace JobBoardScraper.Models;

/// <summary>
/// Данные о курсе/факультете в университете
/// </summary>
public class CourseData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("start_date")]
    public string? StartDate { get; set; }
    
    [JsonPropertyName("end_date")]
    public string? EndDate { get; set; }
    
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }
    
    [JsonPropertyName("is_current")]
    public bool IsCurrent { get; set; }
}
