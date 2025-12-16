using System.Text.Json;
using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Models;

namespace JobBoardScraper;

/// <summary>
/// Реализация хранения whitelist прокси в JSON файле
/// </summary>
public class JsonWhitelistStorage : IWhitelistStorage
{
    private readonly string _filePath;
    private readonly ConsoleLogger? _logger;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public JsonWhitelistStorage(string filePath, ConsoleLogger? logger = null)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _logger = logger;
        EnsureDirectoryExists();
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger?.WriteLine($"Created directory: {directory}");
        }
    }

    public async Task<List<WhitelistProxyEntry>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger?.WriteLine($"Whitelist file not found, creating empty: {_filePath}");
                return new List<WhitelistProxyEntry>();
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var data = JsonSerializer.Deserialize<WhitelistData>(json);
            
            if (data?.Entries == null)
            {
                _logger?.WriteLine("Whitelist file is empty or invalid, returning empty list");
                return new List<WhitelistProxyEntry>();
            }

            _logger?.WriteLine($"Loaded {data.Entries.Count} proxies from whitelist");
            return data.Entries;
        }
        catch (Exception ex)
        {
            _logger?.WriteLine($"Error loading whitelist: {ex.Message}");
            return new List<WhitelistProxyEntry>();
        }
    }


    public async Task SaveAsync(List<WhitelistProxyEntry> entries)
    {
        try
        {
            var data = new WhitelistData
            {
                Version = 1,
                LastUpdated = DateTime.UtcNow,
                Entries = entries ?? new List<WhitelistProxyEntry>()
            };

            var json = JsonSerializer.Serialize(data, JsonOptions);
            
            lock (_lock)
            {
                File.WriteAllText(_filePath, json);
            }
            
            _logger?.WriteLine($"Saved {entries?.Count ?? 0} proxies to whitelist");
        }
        catch (Exception ex)
        {
            _logger?.WriteLine($"Error saving whitelist: {ex.Message}");
        }
    }

    public async Task AddOrUpdateAsync(WhitelistProxyEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.ProxyUrl))
            return;

        var entries = await LoadAsync();
        var existing = entries.FirstOrDefault(e => e.ProxyUrl == entry.ProxyUrl);
        
        if (existing != null)
        {
            existing.LastUsed = entry.LastUsed;
            existing.IsFailed = entry.IsFailed;
            existing.RetryCount = entry.RetryCount;
            existing.FailedSince = entry.FailedSince;
        }
        else
        {
            entries.Add(entry);
        }

        await SaveAsync(entries);
    }

    public async Task RemoveAsync(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl))
            return;

        var entries = await LoadAsync();
        var removed = entries.RemoveAll(e => e.ProxyUrl == proxyUrl);
        
        if (removed > 0)
        {
            await SaveAsync(entries);
            _logger?.WriteLine($"Removed proxy from whitelist: {proxyUrl}");
        }
    }
}
