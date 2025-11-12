namespace JobBoardScraper.Models;

/// <summary>
/// Структура для хранения данных профиля пользователя
/// </summary>
public readonly record struct UserProfileData(
    string? UserCode,
    string? UserName,
    bool? IsExpert,
    string? LevelTitle,
    string? InfoTech,
    int? Salary,
    string? WorkExperience,
    string? LastVisit,
    bool? IsPublic
);
