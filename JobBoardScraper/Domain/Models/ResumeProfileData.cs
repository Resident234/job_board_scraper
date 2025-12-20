namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Данные профиля резюме со страницы списка резюме
/// </summary>
public record ResumeProfileData(
    string Code,
    string Link,
    string Title,
    bool IsExpert,
    string? InfoTech,
    string? LevelTitle,
    int? Salary,
    List<string>? Skills
);
