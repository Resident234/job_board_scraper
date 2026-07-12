namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Record structure for database queue operations with specific fields for each record type.
/// </summary>
public readonly record struct DbRecord(
    DbRecordType Type,
    InsertMode Mode = InsertMode.SkipIfExists,
    ResumeRecord? Resume = null,
    CompanyRecord? Company = null,
    CategoryRootIdRecord? CategoryRootId = null,
    SkillsRecord? Skills = null,
    UserExperienceRecord? UserExperience = null,
    UniversityRecord? University = null);
