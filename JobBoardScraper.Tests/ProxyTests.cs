using Xunit;
using JobBoardScraper.Infrastructure.Proxy;
using System;
using System.Collections.Generic;
using System.Net;

namespace JobBoardScraper.Tests
{
    public class ProxyInfoTests
    {
        [Fact]
        public void ProxyInfo_Constructor_ShouldStoreDataCorrectly()
        {
            var info = new ProxyInfo(
                "1.2.3.4",
                "8080",
                "US",
                "elite proxy",
                true,
                "12 sec ago"
            );

            Assert.Equal("1.2.3.4", info.IpAddress);
            Assert.Equal("8080", info.Port);
            Assert.Equal("US", info.CountryCode);
            Assert.Equal("elite proxy", info.Anonymity);
            Assert.True(info.HttpsSupport);
            Assert.Equal("12 sec ago", info.LastChecked);
        }

        [Fact]
        public void ToProxyUrl_WithHttpsSupport_ReturnsHttpsUrl()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "elite", true, "1 min ago");
            var url = info.ToProxyUrl();
            Assert.Equal("https://1.2.3.4:8080", url);
        }

        [Fact]
        public void ToProxyUrl_WithoutHttpsSupport_ReturnsHttpUrl()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "anonymous", false, "1 min ago");
            var url = info.ToProxyUrl();
            Assert.Equal("http://1.2.3.4:8080", url);
        }

        [Fact]
        public void IsElite_WithEliteKeyword_ReturnsTrue()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "elite proxy", true, "1 min ago");
            Assert.True(info.IsElite());
        }

        [Fact]
        public void IsElite_WithAnonymous_ReturnsFalse()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "anonymous", true, "1 min ago");
            Assert.False(info.IsElite());
        }

        [Fact]
        public void IsAnonymous_WithAnonymousKeyword_ReturnsTrue()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "anonymous", true, "1 min ago");
            Assert.True(info.IsAnonymous());
        }

        [Fact]
        public void IsTransparent_WithTransparentKeyword_ReturnsTrue()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "transparent", true, "1 min ago");
            Assert.True(info.IsTransparent());
        }

        [Fact]
        public void IsTransparent_WithElite_ReturnsFalse()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "elite proxy", true, "1 min ago");
            Assert.False(info.IsTransparent());
        }

        [Fact]
        public void IsRecentlyChecked_WithSeconds_ReturnsTrue()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "elite", true, "30 sec ago");
            Assert.True(info.IsRecentlyChecked());
        }

        [Fact]
        public void IsRecentlyChecked_WithLessThan5Minutes_ReturnsTrue()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "elite", true, "3 min ago");
            Assert.True(info.IsRecentlyChecked());
        }

        [Fact]
        public void IsRecentlyChecked_WithMoreThan5Minutes_ReturnsFalse()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "elite", true, "10 min ago");
            Assert.False(info.IsRecentlyChecked());
        }

        [Fact]
        public void IsValidIp_WithValidIp_ReturnsTrue()
        {
            var info = new ProxyInfo("8.8.8.8", "8080", "US", "elite", true, "1 min ago");
            Assert.True(info.IsValidIp());
        }

        [Fact]
        public void IsValidIp_WithLocalhost_ReturnsFalse()
        {
            var info = new ProxyInfo("127.0.0.1", "8080", "US", "elite", true, "1 min ago");
            Assert.False(info.IsValidIp());
        }

        [Fact]
        public void IsValidIp_WithZeroIp_ReturnsFalse()
        {
            var info = new ProxyInfo("0.0.0.0", "8080", "US", "elite", true, "1 min ago");
            Assert.False(info.IsValidIp());
        }

        [Fact]
        public void IsValidIp_WithEmptyIp_ReturnsFalse()
        {
            var info = new ProxyInfo("", "8080", "US", "elite", true, "1 min ago");
            Assert.False(info.IsValidIp());
        }

        [Fact]
        public void GetQualityScore_Elite_Returns3()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "elite proxy", true, "1 min ago");
            Assert.Equal(3, info.GetQualityScore());
        }

        [Fact]
        public void GetQualityScore_Anonymous_Returns2()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "anonymous", true, "1 min ago");
            Assert.Equal(2, info.GetQualityScore());
        }

        [Fact]
        public void GetQualityScore_Transparent_Returns1()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "transparent", true, "1 min ago");
            Assert.Equal(1, info.GetQualityScore());
        }

        [Fact]
        public void GetQualityScore_Unknown_Returns0()
        {
            var info = new ProxyInfo("1.2.3.4", "8080", "US", "unknown", true, "1 min ago");
            Assert.Equal(0, info.GetQualityScore());
        }

        [Fact]
        public void FilterProxies_RemovesTransparentAndInvalid_SortsByQuality()
        {
            var proxies = new List<ProxyInfo>
            {
                new("1.1.1.1", "80", "US", "transparent", true, "1 min ago"),
                new("2.2.2.2", "80", "US", "elite proxy", true, "1 min ago"),
                new("3.3.3.3", "80", "US", "anonymous", true, "1 min ago"),
                new("0.0.0.0", "80", "US", "elite proxy", true, "1 min ago"),
                new("127.0.0.1", "80", "US", "anonymous", true, "1 min ago"),
            };

            var filtered = ProxyInfo.FilterProxies(proxies);

            Assert.Equal(2, filtered.Count);
            Assert.Equal("2.2.2.2", filtered[0].IpAddress); // elite first
            Assert.Equal("3.3.3.3", filtered[1].IpAddress); // anonymous second
        }

        [Fact]
        public void IsValidProxyFormat_ValidFormat_ReturnsTrue()
        {
            Assert.True(ProxyInfo.IsValidProxyFormat("1.2.3.4:8080"));
        }

        [Fact]
        public void IsValidProxyFormat_InvalidFormat_ReturnsFalse()
        {
            Assert.False(ProxyInfo.IsValidProxyFormat("invalid"));
            Assert.False(ProxyInfo.IsValidProxyFormat("1.2.3.4:0"));
            Assert.False(ProxyInfo.IsValidProxyFormat("1.2.3.4:99999"));
            Assert.False(ProxyInfo.IsValidProxyFormat("0.0.0.0:8080"));
            Assert.False(ProxyInfo.IsValidProxyFormat("127.0.0.1:8080"));
        }

        [Fact]
        public void ToProxyUrl_Static_ValidInput_ReturnsUrl()
        {
            var url = ProxyInfo.ToProxyUrl("1.2.3.4:8080");
            Assert.Equal("http://1.2.3.4:8080", url);
        }

        [Fact]
        public void ToProxyUrl_Static_WithHttps_ReturnsHttpsUrl()
        {
            var url = ProxyInfo.ToProxyUrl("1.2.3.4:8080", "https");
            Assert.Equal("https://1.2.3.4:8080", url);
        }

        [Fact]
        public void ToProxyUrl_Static_InvalidInput_ReturnsNull()
        {
            Assert.Null(ProxyInfo.ToProxyUrl(""));
            Assert.Null(ProxyInfo.ToProxyUrl("invalid"));
            Assert.Null(ProxyInfo.ToProxyUrl("   "));
        }

        [Fact]
        public void BuildProxyUrl_ReturnsCorrectUrl()
        {
            var url = ProxyInfo.BuildProxyUrl("1.2.3.4", "8080");
            Assert.Equal("http://1.2.3.4:8080", url);
        }

        [Fact]
        public void BuildProxyUrl_WithHttps_ReturnsHttpsUrl()
        {
            var url = ProxyInfo.BuildProxyUrl("1.2.3.4", "8080", "https");
            Assert.Equal("https://1.2.3.4:8080", url);
        }
    }

    public class ProxyRotatorTests
    {
        [Fact]
        public void GetNextProxy_ShouldRotateCorrectly()
        {
            var proxies = new List<string> { "http://proxy1:8080", "http://proxy2:8080", "http://proxy3:8080" };
            var rotator = new ProxyRotator(proxies);

            var first = rotator.GetNextProxy();
            var second = rotator.GetNextProxy();
            var third = rotator.GetNextProxy();
            var fourth = rotator.GetNextProxy();

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotNull(third);
            Assert.NotNull(fourth);

            // Verify round-robin by checking Host property
            Assert.Equal("proxy1", ((WebProxy)first).Address.Host);
            Assert.Equal("proxy2", ((WebProxy)second).Address.Host);
            Assert.Equal("proxy3", ((WebProxy)third).Address.Host);
            Assert.Equal("proxy1", ((WebProxy)fourth).Address.Host); // Cycle back
        }

        [Fact]
        public void GetNextProxy_EmptyList_ShouldReturnNull()
        {
            var rotator = new ProxyRotator(new List<string>());
            Assert.Null(rotator.GetNextProxy());
        }

        [Fact]
        public void GetNextProxy_NullList_ShouldReturnNull()
        {
            var rotator = new ProxyRotator(null);
            Assert.Null(rotator.GetNextProxy());
        }

        [Fact]
        public void IsEnabled_WithNonEmptyList_ReturnsTrue()
        {
            var rotator = new ProxyRotator(new List<string> { "http://proxy1:8080" });
            Assert.True(rotator.IsEnabled);
        }

        [Fact]
        public void IsEnabled_WithEmptyList_ReturnsFalse()
        {
            var rotator = new ProxyRotator(new List<string>());
            Assert.False(rotator.IsEnabled);
        }

        [Fact]
        public void ProxyCount_ReturnsCorrectCount()
        {
            var proxies = new List<string> { "http://p1:80", "http://p2:80", "http://p3:80" };
            var rotator = new ProxyRotator(proxies);
            Assert.Equal(3, rotator.ProxyCount);
        }

        [Fact]
        public void ProxyCount_WithEmptyList_ReturnsZero()
        {
            var rotator = new ProxyRotator(new List<string>());
            Assert.Equal(0, rotator.ProxyCount);
        }

        [Fact]
        public void GetCurrentProxy_ShouldReturnCurrentWithoutAdvancing()
        {
            var proxies = new List<string> { "http://proxy1:8080", "http://proxy2:8080" };
            var rotator = new ProxyRotator(proxies);

            var current = rotator.GetCurrentProxy();
            var sameCurrent = rotator.GetCurrentProxy();

            Assert.Equal(((WebProxy)current).Address.Host, ((WebProxy)sameCurrent).Address.Host);
        }

        [Fact]
        public void GetStatus_WithProxies_ReturnsStatusString()
        {
            var proxies = new List<string> { "http://p1:80", "http://p2:80" };
            var rotator = new ProxyRotator(proxies);

            var status = rotator.GetStatus();

            Assert.Contains("Proxy", status);
            Assert.Contains("1/2", status);
        }

        [Fact]
        public void GetStatus_WithEmptyList_ReturnsDisabledMessage()
        {
            var rotator = new ProxyRotator(new List<string>());
            Assert.Equal("Proxy rotation disabled", rotator.GetStatus());
        }

        [Fact]
        public void GetStatus_WithNullList_ReturnsDisabledMessage()
        {
            var rotator = new ProxyRotator(null);
            Assert.Equal("Proxy rotation disabled", rotator.GetStatus());
        }

        [Fact]
        public void GetNextProxy_ShouldCreateWebProxyWithCorrectCredentials()
        {
            var proxies = new List<string> { "http://user:pass@proxy1:8080" };
            var rotator = new ProxyRotator(proxies);

            var proxy = (WebProxy)rotator.GetNextProxy();

            Assert.Equal("proxy1", proxy.Address.Host);
            Assert.Equal(8080, proxy.Address.Port);
            Assert.NotNull(proxy.Credentials);
        }
    }

    public class ProxySourceStatisticsTests
    {
        [Fact]
        public void Constructor_ShouldSetSourceName()
        {
            var stats = new ProxySourceStatistics("SourceA");
            Assert.Equal("SourceA", stats.SourceName);
        }

        [Fact]
        public void RecordProxyScraped_ShouldIncrementTotal()
        {
            var stats = new ProxySourceStatistics("SourceA");
            stats.RecordProxyScraped();
            stats.RecordProxyScraped();

            Assert.Equal(2, stats.TotalProxiesScraped);
        }

        [Fact]
        public void RecordProxyScraped_ShouldSetLastScrapeTime()
        {
            var stats = new ProxySourceStatistics("SourceA");
            stats.RecordProxyScraped();

            Assert.NotNull(stats.LastScrapeTime);
            Assert.True((DateTime.UtcNow - stats.LastScrapeTime.Value).TotalSeconds < 5);
        }

        [Fact]
        public void RecordWorkingProxy_ShouldIncrementWorkingCount()
        {
            var stats = new ProxySourceStatistics("SourceA");
            stats.RecordWorkingProxy();
            stats.RecordWorkingProxy();
            stats.RecordWorkingProxy();

            Assert.Equal(3, stats.WorkingProxies);
        }

        [Fact]
        public void RecordFailedProxy_ShouldIncrementFailedCount()
        {
            var stats = new ProxySourceStatistics("SourceA");
            stats.RecordFailedProxy();

            Assert.Equal(1, stats.FailedProxies);
        }

        [Fact]
        public void RecordWhitelistedProxy_ShouldIncrementWhitelistedCount()
        {
            var stats = new ProxySourceStatistics("SourceA");
            stats.RecordWhitelistedProxy();
            stats.RecordWhitelistedProxy();

            Assert.Equal(2, stats.WhitelistedProxies);
        }

        [Fact]
        public void RecordResponseCode_ShouldTrackCounts()
        {
            var stats = new ProxySourceStatistics("SourceA");
            stats.RecordResponseCode(200);
            stats.RecordResponseCode(200);
            stats.RecordResponseCode(404);

            Assert.Equal(2, stats.ResponseCodeCounts[200]);
            Assert.Equal(1, stats.ResponseCodeCounts[404]);
        }

        [Fact]
        public void GetSummary_ShouldIncludeAllStats()
        {
            var stats = new ProxySourceStatistics("SourceA");
            stats.RecordProxyScraped();
            stats.RecordWorkingProxy();
            stats.RecordFailedProxy();
            stats.RecordWhitelistedProxy();

            var summary = stats.GetSummary();

            Assert.Contains("SourceA", summary);
            Assert.Contains("Scraped=1", summary);
            Assert.Contains("Working=1", summary);
            Assert.Contains("Failed=1", summary);
            Assert.Contains("Whitelisted=1", summary);
        }

        [Fact]
        public void GetSummary_WhenNeverScraped_ShowsNever()
        {
            var stats = new ProxySourceStatistics("SourceB");
            var summary = stats.GetSummary();

            Assert.Contains("LastScrape=Never", summary);
        }

        [Fact]
        public void GetDetailedStats_ShouldIncludeResponseCodes()
        {
            var stats = new ProxySourceStatistics("SourceA");
            stats.RecordProxyScraped();
            stats.RecordWorkingProxy();
            stats.RecordResponseCode(200);
            stats.RecordResponseCode(404);

            var detailed = stats.GetDetailedStats();

            Assert.Contains("SourceA Statistics", detailed);
            Assert.Contains("Total Scraped: 1", detailed);
            Assert.Contains("Working Proxies: 1", detailed);
            Assert.Contains("Response Codes: 200:1, 404:1", detailed);
        }

        [Fact]
        public void GetDetailedStats_WithNoResponseCodes_ShowsEmptyCodes()
        {
            var stats = new ProxySourceStatistics("SourceA");

            var detailed = stats.GetDetailedStats();

            Assert.Contains("Response Codes:", detailed);
        }
    }

    public class ProxySourceHelperTests
    {
        [Fact]
        public void AddSourceToProxyUrl_ShouldAddSourceSuffix()
        {
            var result = ProxySourceHelper.AddSourceToProxyUrl("http://1.2.3.4:8080", "SourceA");
            Assert.Equal("http://1.2.3.4:8080source:SourceA:", result);
        }

        [Fact]
        public void AddSourceToProxyUrl_WithEmptySource_ReturnsOriginal()
        {
            var result = ProxySourceHelper.AddSourceToProxyUrl("http://1.2.3.4:8080", "");
            Assert.Equal("http://1.2.3.4:8080", result);
        }

        [Fact]
        public void AddSourceToProxyUrl_WithEmptyUrl_ReturnsEmpty()
        {
            var result = ProxySourceHelper.AddSourceToProxyUrl("", "SourceA");
            Assert.Equal("", result);
        }

        [Fact]
        public void AddSourceToProxyUrl_WhenSourceAlreadyPresent_ReturnsOriginal()
        {
            var urlWithSource = "http://1.2.3.4:8080source:SourceA:";
            var result = ProxySourceHelper.AddSourceToProxyUrl(urlWithSource, "SourceB");
            Assert.Equal(urlWithSource, result);
        }

        [Fact]
        public void GetProxySource_WithSource_ReturnsSourceName()
        {
            var result = ProxySourceHelper.GetProxySource("http://1.2.3.4:8080source:SourceA:");
            Assert.Equal("SourceA", result);
        }

        [Fact]
        public void GetProxySource_WithoutSource_ReturnsNull()
        {
            var result = ProxySourceHelper.GetProxySource("http://1.2.3.4:8080");
            Assert.Null(result);
        }

        [Fact]
        public void GetProxySource_WithEmptyUrl_ReturnsNull()
        {
            var result = ProxySourceHelper.GetProxySource("");
            Assert.Null(result);
        }

        [Fact]
        public void GetProxySource_WithNullUrl_ReturnsNull()
        {
            var result = ProxySourceHelper.GetProxySource(null);
            Assert.Null(result);
        }

        [Fact]
        public void RemoveSourceFromProxyUrl_WithSource_RemovesSuffix()
        {
            var result = ProxySourceHelper.RemoveSourceFromProxyUrl("http://1.2.3.4:8080source:SourceA:");
            Assert.Equal("http://1.2.3.4:8080", result);
        }

        [Fact]
        public void RemoveSourceFromProxyUrl_WithoutSource_ReturnsOriginal()
        {
            var result = ProxySourceHelper.RemoveSourceFromProxyUrl("http://1.2.3.4:8080");
            Assert.Equal("http://1.2.3.4:8080", result);
        }

        [Fact]
        public void GetCleanProxyUrl_WithSource_ReturnsCleanUrl()
        {
            var result = ProxySourceHelper.GetCleanProxyUrl("http://1.2.3.4:8080source:SourceA:");
            Assert.Equal("http://1.2.3.4:8080", result);
        }

        [Fact]
        public void GetCleanProxyUrl_WithoutSource_ReturnsOriginal()
        {
            var result = ProxySourceHelper.GetCleanProxyUrl("http://1.2.3.4:8080");
            Assert.Equal("http://1.2.3.4:8080", result);
        }
    }
}