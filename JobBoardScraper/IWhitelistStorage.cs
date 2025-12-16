using JobBoardScraper.Models;

namespace JobBoardScraper;

/// <summary>
/// Интерфейс для персистентного хранения whitelist прокси
/// </summary>
public interface IWhitelistStorage
{
    /// <summary>
    /// Загрузить все записи whitelist
    /// </summary>
    Task<List<WhitelistProxyEntry>> LoadAsync();

    /// <summary>
    /// Сохранить все записи whitelist
    /// </summary>
    Task SaveAsync(List<WhitelistProxyEntry> entries);

    /// <summary>
    /// Добавить или обновить запись прокси
    /// </summary>
    Task AddOrUpdateAsync(WhitelistProxyEntry entry);

    /// <summary>
    /// Удалить прокси из whitelist
    /// </summary>
    Task RemoveAsync(string proxyUrl);
}
