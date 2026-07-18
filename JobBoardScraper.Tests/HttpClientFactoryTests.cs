using Xunit;
using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Proxy;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace JobBoardScraper.Tests
{
    public class HttpClientFactoryTests
    {
        [Fact]
        public void CreateDefaultClient_ShouldReturnHttpClient()
        {
            using var client = HttpClientFactory.CreateDefaultClient();
            Assert.NotNull(client);
            Assert.IsType<HttpClient>(client);
        }

        [Fact]
        public void CreateDefaultClient_ShouldHaveDefaultTimeout()
        {
            using var client = HttpClientFactory.CreateDefaultClient();
            Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
        }

        [Fact]
        public void CreateDefaultClient_WithCustomTimeout_ShouldApply()
        {
            using var client = HttpClientFactory.CreateDefaultClient(60);
            Assert.Equal(TimeSpan.FromSeconds(60), client.Timeout);
        }

        [Fact]
        public void CreateHttpClient_WithNullRotator_ShouldReturnClient()
        {
            using var client = HttpClientFactory.CreateHttpClient(null);
            Assert.NotNull(client);
        }

        [Fact]
        public void CreateHttpClient_WithEmptyRotator_ShouldReturnClient()
        {
            var rotator = new ProxyRotator(new List<string>());
            using var client = HttpClientFactory.CreateHttpClient(rotator);
            Assert.NotNull(client);
        }

        [Fact]
        public void CreateHttpClient_WithCustomTimeout_ShouldApply()
        {
            var timeout = TimeSpan.FromSeconds(15);
            using var client = HttpClientFactory.CreateHttpClient(null, timeout);
            Assert.Equal(timeout, client.Timeout);
        }

        [Fact]
        public void CreateHttpClient_WithProxyRotator_ShouldNotThrow()
        {
            var rotator = new ProxyRotator(new List<string> { "http://127.0.0.1:8080" });
            using var client = HttpClientFactory.CreateHttpClient(rotator);
            Assert.NotNull(client);
        }

        [Fact]
        public void CreateHttpClient_WithDisabledProxy_ShouldNotThrow()
        {
            using var client = HttpClientFactory.CreateHttpClient(null);
            Assert.NotNull(client);
        }
    }
}