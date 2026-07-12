namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Additional education record type.
/// </summary>
public readonly record struct AdditionalEducationRecord(
    string UserLink,
    string Title,
    string? Course = null,
    string? Duration = null);
