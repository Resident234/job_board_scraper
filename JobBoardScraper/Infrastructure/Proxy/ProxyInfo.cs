namespace JobBoardScraper.Infrastructure.Proxy;

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
    /// </summary>
    public bool IsTransparent() => Anonymity.Contains("transparent", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Check if proxy was recently checked (within last 5 minutes)
    /// </summary>
    public bool IsRecentlyChecked()
    {
        var lower = LastChecked.ToLowerInvariant();
        if (lower.Contains("sec")) return true;
        if (lower.Contains("min"))
        {
            var parts = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
                if (int.TryParse(part, out var minutes))
                    return minutes <= 5;
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Check if IP address is valid
    /// </summary>
    public bool IsValidIp()
    {
        if (string.IsNullOrWhiteSpace(IpAddress)) return false;
        if (IpAddress == "0.0.0.0") return false;
        if (IpAddress.StartsWith("127.0.0.")) return false;
        return true;
    }
    
    /// <summary>
    /// Get quality score for sorting (higher is better)
    /// </summary>
    public int GetQualityScore()
    {
        if (IsElite()) return 3;
        if (IsAnonymous()) return 2;
        if (IsTransparent()) return 1;
        return 0;
    }

    /// <summary>
    /// Filter proxies, removing transparent and invalid ones, sorted by quality
    /// </summary>
    public static List<ProxyInfo> FilterProxies(List<ProxyInfo> proxies) =>
        proxies.Where(p => !p.IsTransparent() && p.IsValidIp() && (p.IsElite() || p.IsAnonymous()))
               .OrderByDescending(p => p.GetQualityScore())
               .ThenByDescending(p => p.IsRecentlyChecked())
               .ToList();

    /// <summary>
    /// Check if a string is in valid ip:port format
    /// </summary>
    public static bool IsValidProxyFormat(string proxy)
    {
        var parts = proxy.Split(':');
        if (parts.Length != 2)
            return false;

        var ip = parts[0];
        var portStr = parts[1];

        // Проверка IP
        if (string.IsNullOrWhiteSpace(ip) || ip == "0.0.0.0" || ip.StartsWith("127."))
            return false;

        // Проверка порта
        if (!int.TryParse(portStr, out var port) || port < 1 || port > 65535)
            return false;

        return true;
    }

    /// <summary>
    /// Convert ip:port string to a proxy URL with the specified protocol.
    /// Returns null if the format is invalid.
    /// </summary>
    public static string? ToProxyUrl(string ipPort, string protocol = "http")
    {
        if (string.IsNullOrWhiteSpace(ipPort))
            return null;
        if (!IsValidProxyFormat(ipPort))
            return null;
        return $"{protocol}://{ipPort}";
    }

    /// <summary>
    /// Build proxy URL from ip, port and protocol
    /// </summary>
    public static string BuildProxyUrl(string ip, string port, string protocol = "http")
    {
        return $"{protocol}://{ip}:{port}";
    }

    /// <summary>
    /// Add source prefix to proxy URL (for tracking proxy origin in the pool)
    /// </summary>
    public static string AddSourceToProxyUrl(string proxyUrl, string sourceNamePrefix)
    {
        return ProxySourceHelper.AddSourceToProxyUrl(proxyUrl, sourceNamePrefix);
    }
}