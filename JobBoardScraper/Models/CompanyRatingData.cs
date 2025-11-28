namespace JobBoardScraper.Models;

/// <summary>
/// Данные о рейтинге компании, извлеченные со страницы рейтингов
/// </summary>
public sealed record CompanyRatingData(
    string Code,
    string Url,
    string? Title,
    decimal? Rating,
    string? About,
    string? City,
    List<string>? Awards,
    decimal? Scores,
    string? ReviewText
);
