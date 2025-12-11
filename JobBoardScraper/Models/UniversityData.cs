namespace JobBoardScraper.Models;

/// <summary>
/// Данные об университете
/// </summary>
public readonly record struct UniversityData(
    int HabrId,
    string Name,
    string? City = null,
    int? GraduateCount = null
);
