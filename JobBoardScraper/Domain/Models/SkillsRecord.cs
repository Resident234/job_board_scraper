namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Data structure for Skills record type.
/// </summary>
public readonly record struct SkillsRecord(
    int? SkillId = null,
    string? SkillTitle = null);
