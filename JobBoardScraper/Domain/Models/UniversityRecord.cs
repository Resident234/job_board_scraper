namespace JobBoardScraper.Domain.Models;

/// <summary>
/// University record type.
/// </summary>
public readonly record struct UniversityRecord(
    int HabrId,
    string Name,
    string? City = null,
    int? GraduateCount = null);
