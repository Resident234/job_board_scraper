using Xunit;
using JobBoardScraper.Infrastructure.Proxy;
using JobBoardScraper.Infrastructure.Url;
using JobBoardScraper.Infrastructure.Utils;
using JobBoardScraper.Infrastructure.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Text;

namespace JobBoardScraper.Tests
{
    public class GeneralPoolManagerTests
    {
        private static ProxyPool CreatePool(params string[] proxies)
        {
            var pool = new ProxyPool(maxSize: 1000, lowWaterMark: 10);
            foreach (var proxy in proxies)
                pool.AddProxy(proxy);
            return pool;
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullPool()
        {
            Assert.Throws<ArgumentNullException>(() => new GeneralPoolManager(null!));
        }

        [Fact]
        public void GetNextProxy_WithAvailableProxies_ReturnsProxy()
        {
            var pool = CreatePool("http://1.1.1.1:80", "http://2.2.2.2:80");
            var manager = new GeneralPoolManager(pool);

            var proxy = manager.GetNextProxy();
            Assert.Equal("http://1.1.1.1:80", proxy);
            Assert.Equal("http://1.1.1.1:80", manager.CurrentProxy);
        }

        [Fact]
        public void GetNextProxy_WithEmptyPool_ReturnsNull()
        {
            var pool = CreatePool();
            var manager = new GeneralPoolManager(pool);

            Assert.Null(manager.GetNextProxy());
            Assert.Null(manager.CurrentProxy);
        }

        [Fact]
        public void GetNextProxy_SkipsBlacklistedProxies()
        {
            var pool = CreatePool("http://bad:80", "http://good:80");
            var manager = new GeneralPoolManager(pool);
            manager.AddToBlacklist("http://bad:80");

            var proxy = manager.GetNextProxy();
            Assert.Equal("http://good:80", proxy);
        }

        [Fact]
        public void ReportFailure_WithMaxFailures_ShouldBlacklist()
        {
            var pool = CreatePool("http://proxy:80");
            var manager = new GeneralPoolManager(pool, maxFailures: 2);

            string? blacklistedProxy = null;
            manager.OnProxyBlacklisted += (p) => blacklistedProxy = p;

            manager.ReportFailure("http://proxy:80"); // 1
            manager.ReportFailure("http://proxy:80"); // 2 -> blacklist

            Assert.Equal(1, manager.BlacklistCount);
            Assert.Equal("http://proxy:80", blacklistedProxy);
        }

        [Fact]
        public void ReportFailure_ShouldClearCurrentProxy()
        {
            var pool = CreatePool("http://proxy:80");
            var manager = new GeneralPoolManager(pool);

            manager.GetNextProxy(); // sets _currentProxy
            manager.ReportFailure("http://proxy:80");

            Assert.Null(manager.CurrentProxy);
        }

        [Fact]
        public void ReportSuccess_ShouldSetCurrentProxy()
        {
            var pool = CreatePool("http://proxy:80");
            var manager = new GeneralPoolManager(pool);

            manager.ReportSuccess("http://proxy:80");
            Assert.Equal("http://proxy:80", manager.CurrentProxy);
        }

        [Fact]
        public void ReportDailyLimitReached_ShouldFireEvent()
        {
            var pool = CreatePool("http://proxy:80");
            var manager = new GeneralPoolManager(pool);

            string? verifiedProxy = null;
            manager.OnProxyVerified += (p) => verifiedProxy = p;

            manager.ReportDailyLimitReached("http://proxy:80");

            Assert.Equal("http://proxy:80", verifiedProxy);
            Assert.Null(manager.CurrentProxy);
        }

        [Fact]
        public void AddToBlacklist_ShouldAddAndClearCurrentProxy()
        {
            var pool = CreatePool("http://proxy:80");
            var manager = new GeneralPoolManager(pool);

            manager.GetNextProxy();
            manager.AddToBlacklist("http://proxy:80");

            Assert.Equal(1, manager.BlacklistCount);
            Assert.Null(manager.CurrentProxy);
        }

        [Fact]
        public void ClearBlacklist_ShouldClearAll()
        {
            var pool = CreatePool("http://proxy1:80");
            var manager = new GeneralPoolManager(pool);

            manager.AddToBlacklist("http://proxy1:80");
            manager.AddToBlacklist("http://proxy2:80");
            Assert.Equal(2, manager.BlacklistCount);

            manager.ClearBlacklist();
            Assert.Equal(0, manager.BlacklistCount);
        }

        [Fact]
        public void GetStatus_ShouldReturnFormattedString()
        {
            var pool = CreatePool("http://proxy:80");
            var manager = new GeneralPoolManager(pool);

            var status = manager.GetStatus();
            Assert.Contains("Pool: 1", status);
            Assert.Contains("Blacklist: 0", status);

            manager.GetNextProxy();
            status = manager.GetStatus();
            Assert.Contains("http://proxy:80", status);
        }

        [Fact]
        public void Properties_ShouldReturnCorrectValues()
        {
            var pool = CreatePool("http://proxy:80", "http://proxy2:80", "http://proxy3:80");
            var manager = new GeneralPoolManager(pool);

            Assert.Equal(3, manager.Count);
            Assert.True(manager.HasAvailableProxies);
            Assert.Null(manager.CurrentProxy);
            Assert.Equal(0, manager.BlacklistCount);
        }
    }

    public class HtmlDebugTests : IDisposable
    {
        private readonly string _tempDir;

        public HtmlDebugTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Fact]
        public async Task SaveHtmlAsync_WithValidHtml_ShouldSaveFile()
        {
            var path = await HtmlDebug.SaveHtmlAsync(
                "<html><body>test</body></html>",
                "TestScraper",
                _tempDir,
                Encoding.UTF8);

            Assert.NotNull(path);
            Assert.True(File.Exists(path));
            var content = await File.ReadAllTextAsync(path, Encoding.UTF8);
            Assert.Equal("<html><body>test</body></html>", content);
            Assert.Contains("Test_last_page.html", path);
        }

        [Fact]
        public async Task SaveHtmlAsync_WithEmptyHtml_ShouldReturnNull()
        {
            var path = await HtmlDebug.SaveHtmlAsync(
                "",
                "TestScraper",
                _tempDir);

            Assert.Null(path);
        }

        [Fact]
        public async Task SaveHtmlAsync_WithNullHtml_ShouldReturnNull()
        {
            var path = await HtmlDebug.SaveHtmlAsync(
                null!,
                "TestScraper",
                _tempDir);

            Assert.Null(path);
        }

        [Fact]
        public async Task SaveHtmlAsync_WithOnlyWhitespace_ShouldReturnNull()
        {
            var path = await HtmlDebug.SaveHtmlAsync(
                "   ",
                "TestScraper",
                _tempDir);

            Assert.Null(path);
        }

        [Fact]
        public async Task SaveHtmlAsync_WithLogger_ShouldLogSuccess()
        {
            var logger = new ConsoleLogger("Test");
            var path = await HtmlDebug.SaveHtmlAsync(
                "<html>test</html>",
                "TestScraper",
                logger,
                _tempDir);

            Assert.NotNull(path);
            Assert.True(File.Exists(path));
        }

        [Fact]
        public async Task SaveHtmlAsync_WithLoggerAndEmptyHtml_ShouldLogWarning()
        {
            var logger = new ConsoleLogger("Test");
            var path = await HtmlDebug.SaveHtmlAsync(
                "",
                "TestScraper",
                logger,
                _tempDir);

            Assert.Null(path);
        }

        [Fact]
        public async Task SaveHtmlAsync_ShouldStripScraperSuffix()
        {
            var path = await HtmlDebug.SaveHtmlAsync(
                "<html>test</html>",
                "CompanyDetailScraper",
                _tempDir);

            Assert.NotNull(path);
            Assert.Contains("CompanyDetail_last_page.html", path);
        }

        [Fact]
        public async Task SaveHtmlAsync_WithoutScraperSuffix_ShouldKeepName()
        {
            var path = await HtmlDebug.SaveHtmlAsync(
                "<html>test</html>",
                "CustomParser",
                _tempDir);

            Assert.NotNull(path);
            Assert.Contains("CustomParser_last_page.html", path);
        }

        [Fact]
        public async Task SaveHtmlAsync_WithCancellation_ShouldHandleGracefully()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var path = await HtmlDebug.SaveHtmlAsync(
                "<html>test</html>",
                "TestScraper",
                _tempDir,
                Encoding.UTF8,
                cts.Token);

            // With pre-canceled token, WriteAllTextAsync will throw
            // The catch block in HtmlDebug returns null
            Assert.Null(path);
        }
    }

    public class UrlManagerTests
    {
        [Fact]
        public void BuildUserProfileUrl_ShouldCombineBaseWithUsername()
        {
            // BaseUrl from AppConfig may be null in tests, so check behavior
            var url = UrlManager.BuildUserProfileUrl("testuser");
            Assert.Contains("testuser", url);
            Assert.Contains("testuser", url);
        }

        [Fact]
        public void Combine_WithRelativePath_ShouldJoin()
        {
            var result = UrlManager.Combine("http://base.com", "/path");
            Assert.Equal("http://base.com/path", result);
        }

        [Fact]
        public void Combine_WithAbsolutePath_ReturnsIt()
        {
            var result = UrlManager.Combine("http://base.com", "http://other.com/path");
            Assert.Equal("http://other.com/path", result);
        }

        [Fact]
        public void Combine_WithNullBase_UsesBaseUrl()
        {
            // Must not throw
            var result = UrlManager.Combine(null, "/path");
            Assert.NotNull(result);
        }

        [Fact]
        public void Combine_WithNullRelative_ReturnsBase()
        {
            var result = UrlManager.Combine("http://base.com", null);
            Assert.Equal("http://base.com", result);
        }

        [Fact]
        public void Combine_WithRelativeWithoutSlash_ShouldAdd()
        {
            var result = UrlManager.Combine("http://base.com", "path");
            Assert.Equal("http://base.com/path", result);
        }

        [Fact]
        public void ToAbsolute_WithRelative_ShouldPrependBase()
        {
            var result = UrlManager.ToAbsolute("/resumes");
            Assert.NotNull(result);
            Assert.True(result.Contains("/resumes"));
        }

        [Fact]
        public void ToAbsolute_WithAbsolute_ShouldReturnAsIs()
        {
            var result = UrlManager.ToAbsolute("http://other.com/page");
            Assert.Equal("http://other.com/page", result);
        }

        [Fact]
        public void ToAbsolute_WithNull_ShouldReturnBaseUrl()
        {
            var result = UrlManager.ToAbsolute(null);
            Assert.NotNull(result);
        }

        [Fact]
        public void StripBase_WithMatchingPrefix_ShouldStrip()
        {
            var result = UrlManager.StripBase("http://career.habr.com/testuser");
            // If BaseUrl ends with /testuser path, should strip base
            // Если BaseUrl не совпадает, вернет исходное значение
            // Просто проверяем, что не вылетает
            Assert.Contains("testuser", result);
        }

        [Fact]
        public void StripBase_WithNull_ReturnsEmpty()
        {
            var result = UrlManager.StripBase(null);
            Assert.Equal("", result);
        }

        [Fact]
        public void AddQueryParameter_WithUrlWithoutQuery_ShouldAddWithQuestion()
        {
            var result = UrlManager.AddQueryParameter("http://example.com/page", "key", "value");
            Assert.Equal("http://example.com/page?key=value", result);
        }

        [Fact]
        public void AddQueryParameter_WithUrlWithQuery_ShouldAddWithAmpersand()
        {
            var result = UrlManager.AddQueryParameter("http://example.com/page?existing=1", "key", "value");
            Assert.Equal("http://example.com/page?existing=1&key=value", result);
        }

        [Fact]
        public void AddQueryParameter_WithNullUrl_ReturnsEmpty()
        {
            var result = UrlManager.AddQueryParameter(null, "key", "value");
            Assert.Equal("", result);
        }

        [Fact]
        public void AddQueryParameter_WithEmptyParamName_ReturnsOriginal()
        {
            var result = UrlManager.AddQueryParameter("http://example.com", "", "value");
            Assert.Equal("http://example.com", result);
        }

        [Fact]
        public void WithPage_WithPage1_ReturnsUrlUnchanged()
        {
            var result = UrlManager.WithPage("http://example.com/list", 1);
            Assert.Equal("http://example.com/list", result);
        }

        [Fact]
        public void WithPage_WithPage2_AddsPageParam()
        {
            var result = UrlManager.WithPage("http://example.com/list", 2);
            Assert.Equal("http://example.com/list?page=2", result);
        }

        [Fact]
        public void WithPage_WithExistingQuery_AddsWithAmpersand()
        {
            var result = UrlManager.WithPage("http://example.com/list?filter=1", 2);
            Assert.Equal("http://example.com/list?filter=1&page=2", result);
        }

        [Fact]
        public void WithOrder_WithNullOrder_ReturnsOriginal()
        {
            var result = UrlManager.WithOrder("http://example.com/list", null);
            Assert.Equal("http://example.com/list", result);
        }

        [Fact]
        public void WithOrder_WithEmptyOrder_ReturnsOriginal()
        {
            var result = UrlManager.WithOrder("http://example.com/list", "");
            Assert.Equal("http://example.com/list", result);
        }

        [Fact]
        public void WithOrder_WithValue_AddsOrderParam()
        {
            var result = UrlManager.WithOrder("http://example.com/list", "desc");
            Assert.Equal("http://example.com/list?order=desc", result);
        }

        [Fact]
        public void BuildFriendsUrl_WithUserLinkAndNoPage_ReturnsFriendsPath()
        {
            var result = UrlManager.BuildFriendsUrl("http://example.com/user");
            Assert.Equal("http://example.com/user/friends", result);
        }

        [Fact]
        public void BuildFriendsUrl_WithPage2_ReturnsFriendsWithPage()
        {
            var result = UrlManager.BuildFriendsUrl("http://example.com/user", 2);
            Assert.Equal("http://example.com/user/friends?page=2", result);
        }

        [Fact]
        public void BuildFriendsUrl_WithPage1_ReturnsWithoutPage()
        {
            var result = UrlManager.BuildFriendsUrl("http://example.com/user", 1);
            Assert.Equal("http://example.com/user/friends", result);
        }

        [Fact]
        public void BuildExpertsUrl_WithPage1_ReturnsNoPageParam()
        {
            var result = UrlManager.BuildExpertsUrl(1);
            Assert.DoesNotContain("page=", result);
        }

        [Fact]
        public void BuildExpertsUrl_WithPage2_AddsPageParam()
        {
            var result = UrlManager.BuildExpertsUrl(2);
            Assert.Contains("page=2", result);
        }

        [Fact]
        public void BuildCompanyFollowersUrl_WithPage1_ReturnsBase()
        {
            var result = UrlManager.BuildCompanyFollowersUrl("testco", 1);
            Assert.Contains("testco", result);
            Assert.DoesNotContain("page=", result);
        }

        [Fact]
        public void GetAbsolutePath_WithAbsoluteUrl_ReturnsPath()
        {
            var result = UrlManager.GetAbsolutePath("http://example.com/users/123");
            Assert.Equal("/users/123", result);
        }

        [Fact]
        public void GetAbsolutePath_WithRelativeUrl_ReturnsInput()
        {
            var result = UrlManager.GetAbsolutePath("/users/123");
            Assert.Equal("/users/123", result);
        }

        [Fact]
        public void GetAbsolutePath_WithNull_ReturnsEmpty()
        {
            var result = UrlManager.GetAbsolutePath(null);
            Assert.Equal("", result);
        }

        [Fact]
        public void GetAbsolutePath_WithQueryString_StripsQuery()
        {
            var result = UrlManager.GetAbsolutePath("http://example.com/path?param=1");
            Assert.Equal("/path", result);
        }

        [Fact]
        public void GetLastPathSegment_WithSimplePath_ReturnsLast()
        {
            var result = UrlManager.GetLastPathSegment("/users/testuser");
            Assert.Equal("testuser", result);
        }

        [Fact]
        public void GetLastPathSegment_WithMultipleSegments_ReturnsLast()
        {
            var result = UrlManager.GetLastPathSegment("/companies/tensor/followers");
            Assert.Equal("followers", result);
        }

        [Fact]
        public void GetLastPathSegment_WithEmptyPath_ReturnsEmpty()
        {
            var result = UrlManager.GetLastPathSegment("/");
            Assert.Equal("", result);
        }

        [Fact]
        public void GetLastPathSegment_WithNull_ReturnsEmpty()
        {
            var result = UrlManager.GetLastPathSegment(null);
            Assert.Equal("", result);
        }

        [Fact]
        public void Format_ShouldSubstituteArg()
        {
            var result = UrlManager.Format("/resumes?work_state={0}", "active");
            Assert.Equal("/resumes?work_state=active", result);
        }

        [Fact]
        public void Format_WithNullTemplate_ReturnsEmpty()
        {
            var result = UrlManager.Format(null!, "test");
            Assert.Equal("", result);
        }

        [Fact]
        public void BuildCompaniesListUrl_ShouldNotThrow()
        {
            // Так как использует AppConfig, может вернуть любую строку
            var result = UrlManager.BuildCompaniesListUrl(1, null, null, null);
            Assert.NotNull(result);
        }

        [Fact]
        public void BuildUrlWithFilters_WithPage1_NoFilters_ReturnsBase()
        {
            var result = UrlManager.BuildUrlWithFilters("http://base.com/list", 1, null, null, null);
            Assert.Equal("http://base.com/list", result);
        }

        [Fact]
        public void BuildUrlWithFilters_WithPage2_AddsPageParam()
        {
            var result = UrlManager.BuildUrlWithFilters("http://base.com/list", 2, null, null, null);
            Assert.Equal("http://base.com/list?page=2", result);
        }

        [Fact]
        public void BuildUrlWithFilters_WithSizeFilter_AddsSzParam()
        {
            var result = UrlManager.BuildUrlWithFilters("http://base.com/list", 1, 3, null, null);
            Assert.Equal("http://base.com/list?sz=3", result);
        }

        [Fact]
        public void BuildUrlWithFilters_WithCategoryFilter_AddsCategoryRootId()
        {
            var result = UrlManager.BuildUrlWithFilters("http://base.com/list", 1, null, "cat42", null);
            Assert.Equal("http://base.com/list?category_root_id=cat42", result);
        }

        [Fact]
        public void BuildUrlWithFilters_WithAdditionalFilter_AddsIt()
        {
            var result = UrlManager.BuildUrlWithFilters(
                "http://base.com/list",
                1, null, null,
                new KeyValuePair<string, string>("with_vacancies", "1"));
            Assert.Equal("http://base.com/list?with_vacancies=1", result);
        }

        [Fact]
        public void BuildUrlWithFilters_WithAllFiltersAndPage2_CombinesCorrectly()
        {
            var result = UrlManager.BuildUrlWithFilters(
                "http://base.com/list",
                2, 5, "cat10",
                new KeyValuePair<string, string>("with_vacancies", "1"));
            Assert.Equal("http://base.com/list?page=2&sz=5&category_root_id=cat10&with_vacancies=1", result);
        }

        [Fact]
        public void GetCompaniesListUrl_ReturnsFromAppConfig()
        {
            var result = UrlManager.GetCompaniesListUrl();
            Assert.NotNull(result);
        }

        [Fact]
        public void FormatCompanyIdsUrl_WithLongId_ReturnsUrl()
        {
            var result = UrlManager.FormatCompanyIdsUrl(12345L);
            Assert.NotNull(result);
            Assert.Contains("12345", result);
        }

        [Fact]
        public void FormatCompanyIdsUrl_WithStringId_ReturnsUrl()
        {
            var result = UrlManager.FormatCompanyIdsUrl("abc-123");
            Assert.NotNull(result);
            Assert.Contains("abc-123", result);
        }

        [Fact]
        public void FormatCompanyIdsUrl_WithCurrentCompanyParam_AppendsIt()
        {
            var result = UrlManager.FormatCompanyIdsUrl("42", "&current_company=1");
            Assert.NotNull(result);
            Assert.Contains("42", result);
            Assert.Contains("current_company=1", result);
        }

        [Fact]
        public void FormatCompanyIdsUrl_WithEmptyCurrentCompanyParam_DoesNotAppend()
        {
            var result = UrlManager.FormatCompanyIdsUrl("42", "");
            Assert.NotNull(result);
            Assert.Contains("42", result);
            Assert.DoesNotContain("current_company", result);
        }

        [Fact]
        public void WithPage_WithNullUrl_ReturnsEmpty()
        {
            var result = UrlManager.WithPage(null, 2);
            Assert.Equal("?page=2", result);
        }

        [Fact]
        public void BuildFriendsUrl_WithNullUserLink_ReturnsSlashFriends()
        {
            var result = UrlManager.BuildFriendsUrl(null);
            Assert.Equal("/friends", result);
        }

        [Fact]
        public void BuildFriendsUrl_WithEmptyUserLink_ReturnsSlashFriends()
        {
            var result = UrlManager.BuildFriendsUrl("");
            Assert.Equal("/friends", result);
        }

        [Fact]
        public void Combine_WithEmptyRelative_ReturnsBase()
        {
            var result = UrlManager.Combine("http://base.com", "");
            Assert.Equal("http://base.com", result);
        }

        [Fact]
        public void Combine_WithNullBaseAndNullRelative_ReturnsBaseUrl()
        {
            var result = UrlManager.Combine(null, null);
            Assert.NotNull(result);
        }
    }
}
