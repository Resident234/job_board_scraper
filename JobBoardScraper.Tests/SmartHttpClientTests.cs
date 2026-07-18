using Xunit;
using JobBoardScraper.Domain.Models;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Proxy;
using JobBoardScraper.Infrastructure.Statistics;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace JobBoardScraper.Tests
{
    /// <summary>
    /// Mock handler that returns a predefined response for any request.
    /// </summary>
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
        private int _callCount;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string? content = null)
        {
            _responseFactory = _ => new HttpResponseMessage(statusCode)
            {
                Content = content != null ? new StringContent(content) : null
            };
        }

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int CallCount => _callCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(_responseFactory(request));
        }
    }

    public class SmartHttpClientTests
    {
        private static SmartHttpClient CreateClient(
            HttpMessageHandler handler,
            string scraperName = "test-scraper",
            bool enableRetry = false,
            TrafficStatistics? trafficStats = null,
            int maxRetries = 3,
            TimeSpan? baseDelay = null,
            TimeSpan? maxDelay = null,
            TimeSpan? timeout = null)
        {
            var httpClient = new HttpClient(handler);
            return new SmartHttpClient(
                httpClient,
                scraperName,
                trafficStats: trafficStats,
                enableRetry: enableRetry,
                enableTrafficMeasuring: false,
                maxRetries: maxRetries,
                baseDelay: baseDelay ?? TimeSpan.FromMilliseconds(10),
                maxDelay: maxDelay ?? TimeSpan.FromMilliseconds(100),
                timeout: timeout ?? TimeSpan.FromSeconds(30));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullHttpClient()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SmartHttpClient(null!, "test"));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullScraperName()
        {
            using var client = new HttpClient();
            Assert.Throws<ArgumentNullException>(() =>
                new SmartHttpClient(client, null!));
        }

        [Fact]
        public async Task GetAsync_WithoutRetry_ShouldReturnResponse()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "response body");
            var smartClient = CreateClient(handler);

            var response = await smartClient.GetAsync("https://example.com");

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task GetAsync_WithoutRetry_ShouldNotRetryOnError()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError);
            var smartClient = CreateClient(handler);

            var response = await smartClient.GetAsync("https://example.com");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task GetAsync_WithRetry_ShouldRetryOnTransientError()
        {
            var callCount = 0;
            var handler = new MockHttpMessageHandler(_ =>
            {
                Interlocked.Increment(ref callCount);
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            });
            var smartClient = CreateClient(handler, enableRetry: true, maxRetries: 3);

            var response = await smartClient.GetAsync("https://example.com");

            // Should return the last response after exhausting retries
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(3, callCount);
        }

        [Fact]
        public async Task GetAsync_WithRetry_ShouldSucceedAfterRetry()
        {
            var callCount = 0;
            var handler = new MockHttpMessageHandler(_ =>
            {
                var count = Interlocked.Increment(ref callCount);
                return count < 3
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("success") };
            });
            var smartClient = CreateClient(handler, enableRetry: true, maxRetries: 3);

            var response = await smartClient.GetAsync("https://example.com");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3, callCount);
        }

        [Fact]
        public async Task GetAsync_WithRetry_ShouldNotRetryOnNonTransientError()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.NotFound);
            var smartClient = CreateClient(handler, enableRetry: true);

            var response = await smartClient.GetAsync("https://example.com");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public void GetProxyStatus_WithoutProxy_ShouldReturnNoProxy()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
            var smartClient = CreateClient(handler);

            var status = smartClient.GetProxyStatus();

            Assert.Equal("No proxy", status);
        }

        [Fact]
        public void RotateProxy_ShouldNotThrow()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
            var smartClient = CreateClient(handler);

            // Should not throw even without proxy
            smartClient.RotateProxy();
        }

        [Fact]
        public async Task GetAsync_WithTrafficStats_ShouldMeasure()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "measured content");
            var trafficStats = new TrafficStatistics("test_stats.txt", TimeSpan.FromHours(1));
            var httpClient = new HttpClient(handler);

            var smartClient = new SmartHttpClient(
                httpClient,
                "traffic-test",
                trafficStats: trafficStats,
                enableTrafficMeasuring: true);

            await smartClient.GetAsync("https://example.com");

            var stats = trafficStats.GetStats("traffic-test");
            Assert.NotNull(stats);
            Assert.Equal(1, stats!.RequestCount);
            Assert.True(stats.TotalBytes > 0);

            trafficStats.Dispose();
            CleanupTestFile("test_stats.txt");
        }

        [Fact]
        public async Task FetchAsync_WithoutRetry_ShouldThrow()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
            var smartClient = CreateClient(handler, enableRetry: false);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                smartClient.FetchAsync("https://example.com"));
        }

        [Fact]
        public async Task FetchAsync_WithRetry_ShouldReturnSuccess()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "success content");
            var smartClient = CreateClient(handler, enableRetry: true);

            var result = await smartClient.FetchAsync("https://example.com");

            Assert.True(result.IsSuccess);
            Assert.Equal("success content", result.Content);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        [Fact]
        public async Task FetchAsync_WithNotFound_ShouldReturnNotSuccess()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.NotFound);
            var smartClient = CreateClient(handler, enableRetry: true);

            var result = await smartClient.FetchAsync("https://example.com");

            Assert.False(result.IsSuccess);
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
            Assert.Null(result.Content);
        }

        [Fact]
        public async Task FetchAsync_With429_ShouldReturnSuccessWithContent()
        {
            var handler = new MockHttpMessageHandler((HttpStatusCode)429, "rate limited content");
            var smartClient = CreateClient(handler, enableRetry: true, maxRetries: 3);

            var result = await smartClient.FetchAsync("https://example.com");

            Assert.True(result.IsSuccess);
            Assert.Equal((HttpStatusCode)429, result.StatusCode);
            Assert.Equal("rate limited content", result.Content);
        }

        [Fact]
        public async Task FetchAsync_ShouldTrackElapsedTime()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "content");
            var smartClient = CreateClient(handler, enableRetry: true);

            var result = await smartClient.FetchAsync("https://example.com");

            Assert.True(result.IsSuccess);
            Assert.True(result.ElapsedTime.TotalMilliseconds >= 0);
            Assert.Equal("https://example.com", result.Url);
        }

        [Fact]
        public async Task GetAsync_ShouldRespectCancellationToken()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
            var smartClient = CreateClient(handler);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                smartClient.GetAsync("https://example.com", cts.Token));
        }

        [Fact]
        public async Task FetchAsync_WithRetry_ShouldExhaustOnTransientError()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
            var smartClient = CreateClient(handler, enableRetry: true, maxRetries: 2);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                smartClient.FetchAsync("https://example.com"));

            Assert.Equal(2, handler.CallCount);
        }

        [Fact]
        public async Task FetchAsync_WithRetryAfter_ShouldRespectDelay()
        {
            var callCount = 0;
            var handler = new MockHttpMessageHandler(_ =>
            {
                var count = Interlocked.Increment(ref callCount);
                var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                if (count == 1)
                {
                    response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                        TimeSpan.FromMilliseconds(5));
                }
                else
                {
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent("success");
                }
                return response;
            });
            var smartClient = CreateClient(handler, enableRetry: true, maxRetries: 3, baseDelay: TimeSpan.FromMilliseconds(1));

            var result = await smartClient.FetchAsync("https://example.com");

            Assert.True(result.IsSuccess);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void GetProxyStatus_WithEnabledProxy_ShouldReturnStatus()
        {
            var rotator = new ProxyRotator(new List<string>
            {
                "http://proxy1:8080",
                "http://proxy2:8080"
            });
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
            var httpClient = new HttpClient(handler);
            var smartClient = new SmartHttpClient(
                httpClient,
                "proxy-test",
                proxyRotator: rotator);

            var status = smartClient.GetProxyStatus();

            Assert.Contains("Proxy", status);
        }

        [Fact]
        public void RotateProxy_WithEnabledProxy_ShouldSwitchProxy()
        {
            var rotator = new ProxyRotator(new List<string>
            {
                "http://proxy1:8080",
                "http://proxy2:8080"
            });
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
            var httpClient = new HttpClient(handler);
            var smartClient = new SmartHttpClient(
                httpClient,
                "proxy-test",
                proxyRotator: rotator);

            var before = smartClient.GetProxyStatus();
            smartClient.RotateProxy();
            var after = smartClient.GetProxyStatus();

            Assert.NotEqual(before, after);
        }

        private static void CleanupTestFile(string path)
        {
            try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
            catch { /* ignore */ }
        }
    }
}