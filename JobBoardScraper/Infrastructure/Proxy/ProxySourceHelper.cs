using System.Text.RegularExpressions;

namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Helper class for managing proxy source information
/// </summary>
public static class ProxySourceHelper
{
    private const string SourceTagPrefix = "source:";
    private const string SourceTagSuffix = ":";

    /// <summary>
    /// Add source information to a proxy URL
    /// </summary>
    public static string AddSourceToProxyUrl(string proxyUrl, string sourceName)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl) || string.IsNullOrWhiteSpace(sourceName))
            return proxyUrl;

        // Check if source is already present
        if (GetProxySource(proxyUrl) != null)
            return proxyUrl;

        // Add source tag to the URL
        return $"{proxyUrl}{SourceTagPrefix}{sourceName}{SourceTagSuffix}";
    }

    /// <summary>
    /// Get source name from a proxy URL
    /// </summary>
    public static string? GetProxySource(string proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
            return null;

        var match = Regex.Match(proxyUrl, $@"{SourceTagPrefix}([^:]+){SourceTagSuffix}$");
        if (match.Success)
            return match.Groups[1].Value;

        return null;
    }

    /// <summary>
    /// Remove source information from a proxy URL
    /// </summary>
    public static string RemoveSourceFromProxyUrl(string proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
            return proxyUrl;

        var match = Regex.Match(proxyUrl, $@"{SourceTagPrefix}[^:]+{SourceTagSuffix}$");
        if (match.Success)
            return proxyUrl.Substring(0, match.Index);

        return proxyUrl;
    }

    /// <summary>
    /// Get clean proxy URL without source information
    /// </summary>
    public static string GetCleanProxyUrl(string proxyUrl)
    {
        return RemoveSourceFromProxyUrl(proxyUrl);
    }
}