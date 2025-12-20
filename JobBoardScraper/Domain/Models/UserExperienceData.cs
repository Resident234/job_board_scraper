namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Данные об опыте работы пользователя
/// </summary>
public record UserExperienceData(
    string UserLink,
    string? CompanyCode,
    string? CompanyUrl,
    string? CompanyTitle,
    string? CompanyAbout,
    string? CompanySize,
    string? Position,
    string? Duration,
    string? Description,
    List<(int? SkillId, string SkillName)> Skills,
    bool IsFirstRecord
);
