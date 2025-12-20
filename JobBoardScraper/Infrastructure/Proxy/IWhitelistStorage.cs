using JobBoardScraper.Domain.Models;

namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Интерфейс для персистентного хранения whitelist прокси
/// </summary>
public interface IWhitelistStorage
{
    Task<List<WhitelistProxyEntry>> LoadAsync();
    Task SaveAsync(List<WhitelistProxyEntry> entries);
    Task AddOrUpdateAsync(WhitelistProxyEntry entry);
    Task RemoveAsync(string proxyUrl);
}
