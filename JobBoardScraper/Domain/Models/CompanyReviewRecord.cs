namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Data structure for Company Review record type.
/// </summary>
public readonly record struct CompanyReviewRecord(
    string CompanyCode,
    string ReviewHash,
    string ReviewText);
