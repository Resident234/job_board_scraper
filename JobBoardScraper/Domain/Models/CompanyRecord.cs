namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Data structure for Company record type.
/// </summary>
public readonly record struct CompanyRecord(
    string CompanyCode,
    string CompanyUrl,
    string? CompanyTitle = null,
    long? CompanyId = null,
    string? About = null,
    string? Description = null,
    string? Site = null,
    decimal? Rating = null,
    int? CurrentEmployees = null,
    int? PastEmployees = null,
    int? Followers = null,
    int? WantWork = null,
    string? EmployeesCount = null,
    bool? Habr = null,
    string? City = null,
    List<string>? Awards = null,
    decimal? Scores = null,
    List<CompanyReviewRecord>? ReviewRecords = null,
    List<SkillsRecord>? Skills = null);
