namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Полные данные об образовании пользователя в одном университете
/// Результат парсинга блока "Высшее образование"
/// </summary>
public class UniversityEducationData
{
    public UniversityData University { get; set; }
    public List<CourseData> Courses { get; set; } = new();
    public string? Description { get; set; }
}
