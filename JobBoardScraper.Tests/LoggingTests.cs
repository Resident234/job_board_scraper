using Xunit;
using JobBoardScraper.Infrastructure.Logging;
using System;
using System.IO;
using System.Collections.Generic;

namespace JobBoardScraper.Tests
{
    public class LoggingTests
    {
        [Theory]
        [InlineData("MyCompanyScraper", "My Company Scraper")]
        [InlineData("URLManager", "URL Manager")]
        [InlineData("HTTPClientFactory", "HTTP Client Factory")]
        [InlineData("SimpleClass", "Simple Class")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void FormatClassName_ShouldFormatCorrectly(string input, string expected)
        {
            Assert.Equal(expected, ConsoleLogger.FormatClassName(input));
        }

        [Fact]
        public void FormatThrottleRetry_ShouldReturnCorrectString()
        {
            int failed = 1;
            int next = 2;
            int max = 3;
            string context = "page 5";
            int delay = 2000;
            string reason = "timeout";

            var result = ScraperLogger.FormatThrottleRetry(failed, next, max, context, delay, reason);

            string expected = "↻ Ошибка на попытке 1/3: timeout. Повторная попытка 2/3 через 2.0с: page 5";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void FormatThrottleRetry_WithoutReason_ShouldReturnCorrectString()
        {
            var result = ScraperLogger.FormatThrottleRetry(1, 2, 3, "test", 500, null);
            
            Assert.Contains("↻ Ошибка на попытке 1/3", result);
            Assert.DoesNotContain(": null", result);
            Assert.Contains("через 500мс", result);
        }

        [Fact]
        public void ConsoleLogger_WriteLine_ShouldOutputToConsole()
        {
            var originalOut = Console.Out;
            using var sw = new StringWriter();
            Console.SetOut(sw);
            try
            {
                using (var logger = new ConsoleLogger("TestProcess"))
                {
                    logger.WriteLine("Hello World");
                    var result = sw.ToString().Trim();
                    Assert.Equal("[Test Process] Hello World", result);
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void ScraperLogger_LogStart_ShouldFormatCorrectly()
        {
            var originalOut = Console.Out;
            using var sw = new StringWriter();
            Console.SetOut(sw);
            try
            {
                using (var logger = new ConsoleLogger("TestProcess"))
                {
                    ScraperLogger.LogStart(logger, "Starting scrap");
                    var result = sw.ToString().Trim();
                    Assert.Contains("▶ Starting scrap", result);
                    Assert.Contains("[Test Process]", result);
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void ScraperLogger_LogError_ShouldFormatCorrectly()
        {
            var originalOut = Console.Out;
            using var sw = new StringWriter();
            Console.SetOut(sw);
            try
            {
                using (var logger = new ConsoleLogger("TestProcess"))
                {
                    ScraperLogger.LogError(logger, "Critical failure");
                    var result = sw.ToString().Trim();
                    Assert.Contains("✖ Critical failure", result);
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void ScraperLogger_LogEnqueue_WithFields_ShouldFormatCorrectly()
        {
            var originalOut = Console.Out;
            using var sw = new StringWriter();
            Console.SetOut(sw);
            try
            {
                using (var logger = new ConsoleLogger("TestProcess"))
                {
                    ScraperLogger.LogEnqueue(logger, "Resume", "user123", ("Name", "Ivan"), ("Level", "Senior"));
                    var result = sw.ToString().Trim();
                    Assert.Contains("⇪ В очередь: Resume[user123]", result);
                    Assert.Contains("{ Name = 'Ivan', Level = 'Senior' }", result);
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}