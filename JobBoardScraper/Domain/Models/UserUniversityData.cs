namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Данные о связи пользователя с университетом
/// </summary>
public class UserUniversityData
{
    public string UserLink { get; set; } = string.Empty;
    public int UniversityHabrId { get; set; }
    public List<CourseData> Courses { get; set; } = new();
    public string? Description { get; set; }
}
