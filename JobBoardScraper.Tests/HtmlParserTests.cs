using Xunit;
using JobBoardScraper.Parsing;
using JobBoardScraper;

namespace JobBoardScraper.Tests
{
    public class HtmlParserTests
    {
        [Theory]
        [InlineData("<html><head><title>Test Page</title></head></html>", "Test Page")]
        [InlineData("<html><head><title>  Trimmed Title  </title></head></html>", "Trimmed Title")]
        [InlineData("<html><body>No title here</body></html>", "")]
        [InlineData("<html><head><title></title></head></html>", "")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void ExtractTitle_ShouldReturnCorrectTitle(string html, string expected)
        {
            // Act
            var result = HtmlParser.ExtractTitle(html);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ContainsDailyLimitMessage_ShouldReturnTrue_WhenMessageIsPresent()
        {
            // Arrange
            // Since AppConfig is static, we use the value actually configured in the system
            var limitMessage = AppConfig.ProxyWhitelistDailyLimitMessage;
            
            if (string.IsNullOrEmpty(limitMessage))
            {
                return; // Skip test if config is not set
            }

            var html = $"<html><body><div>{limitMessage}</div></body></html>";

            // Act
            var result = HtmlParser.ContainsDailyLimitMessage(html);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ContainsDailyLimitMessage_ShouldReturnFalse_WhenMessageIsAbsent()
        {
            // Arrange
            var html = "<html><body><div>Some other content</div></body></html>";

            // Act
            var result = HtmlParser.ContainsDailyLimitMessage(html);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ContainsDailyLimitMessage_ShouldReturnFalse_ForEmptyInput(string html)
        {
            // Act
            var result = HtmlParser.ContainsDailyLimitMessage(html);

            // Assert
            Assert.False(result);
        }
    }
}