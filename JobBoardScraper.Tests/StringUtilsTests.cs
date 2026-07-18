using Xunit;
using JobBoardScraper.Infrastructure.Utils;

namespace JobBoardScraper.Tests
{
    public class StringUtilsTests
    {
        [Theory]
        [InlineData("Hello World", "HelloWorld")]
        [InlineData("  Hello   World  ", "HelloWorld")]
        [InlineData("Hello\u00A0World", "HelloWorld")]
        [InlineData("Hello \u00A0 World", "HelloWorld")]
        [InlineData("NoSpaces", "NoSpaces")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void RemoveAllWhitespace_ShouldRemoveAllSpacesAndNonBreakingSpaces(string input, string expected)
        {
            var result = StringUtils.RemoveAllWhitespace(input);
            Assert.Equal(expected, result);
        }
    }
}