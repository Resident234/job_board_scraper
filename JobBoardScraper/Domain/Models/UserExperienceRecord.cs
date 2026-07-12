namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Data structure for UserExperience record type.
/// </summary>
public readonly record struct UserExperienceRecord(
    string UserLink,
    CompanyRecord Company,
    string? Position = null,
    string? Duration = null,
    string? Description = null,
    List<SkillsRecord>? Skills = null,
    bool IsFirstRecord = false);
