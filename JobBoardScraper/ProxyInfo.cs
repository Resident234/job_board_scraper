namespace JobBoardScraper;

/// <summary>
/// Data structure for proxy information parsed from free-proxy-list.net
/// 
/// Proxy Anonymity Levels:
/// - Elite: Maximum anonymity, doesn't reveal real IP or proxy usage (best for scraping)
/// - Anonymous: Hides real IP but identifies as proxy (good for scraping)
/// - Transparent: Reveals real IP via X-Forwarded-For header (useless for scraping)
/// 
/// See docs/PROXY_ANONYMITY_LEVELS.md for detailed explanation
/// </summary>
public record ProxyInfo(
    string IpAddress,
    string Port,
    string CountryCode,
    string Anonymity,
    bool HttpsSupport,
    string LastChecked)
{
    /// <summary>
    /// Convert proxy info to URL string
    /// </summary>
    public string ToProxyUrl() => HttpsSupport 
        ? $"https://{IpAddress}:{Port}" 
        : $"http://{IpAddress}:{Port}";
    
    /// <summary>
    /// Check if proxy is elite (highest anonymity)
    /// </summary>
    public bool IsElite() => Anonymity.Contains("elite", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Check if proxy is anonymous
    /// </summary>
    public bool IsAnonymous() => Anonymity.Contains("anonymous", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Check if proxy is transparent (lowest anonymity, should be excluded)
    /// Transparent proxies reveal your real IP via X-Forwarded-For header - useless for scraping!
    /// </summary>
    public bool IsTransparent() => Anonymity.Contains("transparent", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Check if proxy was recently checked (within last 5 minutes)
    /// </summary>
    public bool IsRecentlyChecked()
    {
        var lower = LastChecked.ToLowerInvariant();
        
        // Check for seconds
        if (lower.Contains("sec"))
            return true;
        
        // Check for minutes
        if (lower.Contains("min"))
        {
            // Extract number of minutes
            var parts = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (int.TryParse(part, out var minutes))
                {
                    return minutes <= 5;
                }
            }
            // If we can't parse, assume it's recent
            return true;
        }
        
        // Hours, days, etc. are not recent
        return false;
    }
    
    /// <summary>
    /// Check if IP address is valid (not localhost or 0.0.0.0)
    /// </summary>
    public bool IsValidIp()
    {
        if (string.IsNullOrWhiteSpace(IpAddress))
            return false;
        
        // Exclude 0.0.0.0
        if (IpAddress == "0.0.0.0")
            return false;
        
        // Exclude localhost range 127.0.0.x
        if (IpAddress.StartsWith("127.0.0."))
            return false;
        
        return true;
    }
    
    /// <summary>
    /// Get quality score for sorting (higher is better)
    /// Elite = 3, Anonymous = 2, Transparent = 1
    /// </summary>
    public int GetQualityScore()
    {
        if (IsElite()) return 3;
        if (IsAnonymous()) return 2;
        if (IsTransparent()) return 1;
        return 0;
    }
}
