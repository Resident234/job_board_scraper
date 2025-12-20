namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Общий интерфейс для менеджеров прокси
/// </summary>
public interface IProxyManager
{
    /// <summary>
    /// Получить следующий доступный прокси
    /// </summary>
    string? GetNextProxy();
    
    /// <summary>
    /// Сообщить об успешном использовании прокси
    /// </summary>
    void ReportSuccess(string proxyUrl);
    
    /// <summary>
    /// Сообщить об ошибке прокси
    /// </summary>
    void ReportFailure(string proxyUrl);
    
    /// <summary>
    /// Сообщить о достижении суточного лимита
    /// </summary>
    void ReportDailyLimitReached(string proxyUrl);
    
    /// <summary>
    /// Количество прокси в пуле
    /// </summary>
    int Count { get; }
    
    /// <summary>
    /// Есть ли доступные прокси
    /// </summary>
    bool HasAvailableProxies { get; }
    
    /// <summary>
    /// Текущий активный прокси (если есть)
    /// </summary>
    string? CurrentProxy { get; }
}
