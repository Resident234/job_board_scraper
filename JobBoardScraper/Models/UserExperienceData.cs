namespace JobBoardScraper.Models;

/// <summary>
/// Структура для хранения данных об опыте работы
/// </summary>
public readonly record struct UserExperienceData(
    string UserLink,
    string? CompanyCode,
    string? CompanyUrl,
    string? CompanyTitle,
    string? CompanyAbout,
    string? CompanySize,
    string? Position,
    string? Duration,
    string? Description,
    List<(int? SkillId, string SkillName)>? Skills,
    bool IsFirstRecord = false
);
