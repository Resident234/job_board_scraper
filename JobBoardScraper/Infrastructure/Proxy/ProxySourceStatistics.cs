using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Statistics tracker for individual proxy sources
/// </summary>
public class ProxySourceStatistics
{
    [JsonPropertyName("sourceName")]
    public string SourceName { get; set; }

    [JsonPropertyName("totalProxiesScraped")]
    public int TotalProxiesScraped { get; set; }

    [JsonPropertyName("workingProxies")]
    public int WorkingProxies { get; set; }

    [JsonPropertyName("failedProxies")]
    public int FailedProxies { get; set; }

    [JsonPropertyName("whitelistedProxies")]
    public int WhitelistedProxies { get; set; }

    [JsonPropertyName("responseCodeCounts")]
    public ConcurrentDictionary<int, int> ResponseCodeCounts { get; set; } = new();

    [JsonPropertyName("lastScrapeTime")]
    public DateTime? LastScrapeTime { get; set; }

    public ProxySourceStatistics(string sourceName)
    {
        SourceName = sourceName;
    }

    /// <summary>
    /// Record a successful proxy scrape
    /// </summary>
    public void RecordProxyScraped()
    {
        TotalProxiesScraped++;
        LastScrapeTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Record a working proxy
    /// </summary>
    public void RecordWorkingProxy()
    {
        WorkingProxies++;
    }

    /// <summary>
    /// Record a failed proxy
    /// </summary>
    public void RecordFailedProxy()
    {
        FailedProxies++;
    }

    /// <summary>
    /// Record a proxy that was added to whitelist
    /// </summary>
    public void RecordWhitelistedProxy()
    {
        WhitelistedProxies++;
    }

    /// <summary>
    /// Record a response code
    /// </summary>
    public void RecordResponseCode(int statusCode)
    {
        ResponseCodeCounts.AddOrUpdate(statusCode, 1, (key, oldValue) => oldValue + 1);
    }

    /// <summary>
    /// Get summary statistics
    /// </summary>
    public string GetSummary()
    {
        return $"{SourceName}: Scraped={TotalProxiesScraped}, Working={WorkingProxies}, " +
               $"Failed={FailedProxies}, Whitelisted={WhitelistedProxies}, " +
               $"LastScrape={LastScrapeTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}";
    }

    /// <summary>
    /// Get detailed statistics including response codes
    /// </summary>
    public string GetDetailedStats()
    {
        var responseCodeSummary = string.Join(", ",
            ResponseCodeCounts.OrderBy(kv => kv.Key)
                .Select(kv => $"{kv.Key}:{kv.Value}"));

        return $"{SourceName} Statistics:\n" +
               $"  Total Scraped: {TotalProxiesScraped}\n" +
               $"  Working Proxies: {WorkingProxies}\n" +
               $"  Failed Proxies: {FailedProxies}\n" +
               $"  Whitelisted Proxies: {WhitelistedProxies}\n" +
               $"  Last Scrape: {LastScrapeTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}\n" +
               $"  Response Codes: {responseCodeSummary}";
    }
}