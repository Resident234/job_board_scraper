namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Структура для хранения детальных данных компании
/// </summary>
public readonly record struct CompanyDetailsData(
    string Url,
    long? CompanyId,
    string? Title,
    string? About,
    string? Description,
    string? Site,
    decimal? Rating,
    int? CurrentEmployees,
    int? PastEmployees,
    int? Followers,
    int? WantWork,
    string? EmployeesCount,
    bool? Habr
);
