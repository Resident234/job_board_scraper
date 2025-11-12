namespace JobBoardScraper.Models;

/// <summary>
/// Структура для хранения детальных данных профиля со страницы навыков
/// </summary>
public readonly record struct ResumeProfileData(
    string Code,
    string Link,
    string Title,
    bool IsExpert,
    string? InfoTech,
    string? LevelTitle,
    int? Salary,
    List<string>? Skills
);
