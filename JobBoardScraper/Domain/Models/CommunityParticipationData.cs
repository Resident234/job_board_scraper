namespace JobBoardScraper.Domain.Models;

/// <summary>
/// Данные об участии в профсообществе (Хабр, GitHub и др.)
/// Секция "Участие в профсообществах" в профиле резюме
/// </summary>
public class CommunityParticipationData
{
    /// <summary>
    /// Название сообщества (Хабр, GitHub)
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Дата начала участия (например, "c мая 2009 (16 лет и 7 месяцев)")
    /// </summary>
    public string? MemberSince { get; set; }
    
    /// <summary>
    /// Вклад в сообщество (например, "2 публикации, 93 комментария" или "4229 вкладов в 7 репозиториев")
    /// </summary>
    public string? Contribution { get; set; }
    
    /// <summary>
    /// Темы/языки (например, "Управление e-commerce • Компьютерное железо" или "PHP • CSS")
    /// </summary>
    public string? Topics { get; set; }
}
