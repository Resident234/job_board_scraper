namespace JobBoardScraper.Domain.Models;

/// <summary>
/// User-university relation record type.
/// </summary>
public readonly record struct UserUniversityRecord(
    string UserLink,
    UniversityRecord University,
    List<CourseData>? Courses = null,
    string? Description = null);
