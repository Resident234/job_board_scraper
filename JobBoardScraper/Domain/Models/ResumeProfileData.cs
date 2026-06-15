namespace JobBoardScraper.Domain.Models;

using JobBoardScraper.Data;

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
    List<SkillsRecord>? Skills
);
