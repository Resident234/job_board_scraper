namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Data structure for Resume record type.
/// </summary>
public readonly record struct ResumeRecord(
    InsertMode Mode = InsertMode.SkipIfExists,
    string Link = "",
    string? Title = null,
    string? Slogan = null,
    string? Code = null,
    bool? Expert = null,
    string? WorkExperience = null,
    string? UserCode = null,
    string? UserName = null,
    bool? IsExpert = null,
    string? LevelTitle = null,
    string? InfoTech = null,
    int? Salary = null,
    string? LastVisit = null,
    string? Age = null,
    string? Registration = null,
    string? Citizenship = null,
    bool? RemoteWork = null,
    bool? IsPublic = null,
    string? JobSearchStatus = null,
    bool? IsEmpty = null,
    List<SkillsRecord>? Skills = null,
    List<CommunityParticipationData>? CommunityParticipation = null,
    List<UserExperienceRecord>? UserExperience = null,
    List<UserUniversityRecord>? UserUniversities = null,
    List<AdditionalEducationRecord>? AdditionalEducations = null,
    bool? IsDeleted = null,
    string? About = null);
