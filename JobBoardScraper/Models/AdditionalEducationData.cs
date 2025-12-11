namespace JobBoardScraper.Models;

/// <summary>
/// Данные о дополнительном образовании (курсы, тренинги)
/// Секция "Дополнительное образование" в профиле резюме
/// </summary>
public class AdditionalEducationData
{
    /// <summary>
    /// Ссылка на профиль пользователя
    /// </summary>
    public string UserLink { get; set; } = string.Empty;
    
    /// <summary>
    /// Название организации/платформы (например, index-tech)
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Название курса (например, Рекрутмент)
    /// </summary>
    public string? Course { get; set; }
    
    /// <summary>
    /// Период обучения (например, Март 2024 — Март 2024 (1 месяц))
    /// </summary>
    public string? Duration { get; set; }
}
