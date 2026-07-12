namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Data structure for CategoryRootId record type.
/// </summary>
public readonly record struct CategoryRootIdRecord(
    string CategoryId,
    string CategoryName);
