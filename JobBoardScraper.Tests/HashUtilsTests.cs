using Xunit;
using JobBoardScraper.Infrastructure.Utils;

namespace JobBoardScraper.Tests
{
    public class HashUtilsTests
    {
        [Fact]
        public void ComputeHash_WithKnownInput_ReturnsCorrectSha256()
        {
            // Arrange
            string input = "test";
            string expected = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

            // Act
            string result = HashUtils.ComputeHash(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void ComputeHash_WithInvalidInput_ReturnsEmptyString(string input)
        {
            // Act
            string result = HashUtils.ComputeHash(input);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ComputeHash_IsConsistent()
        {
            // Arrange
            string input = "consistent_test_string";

            // Act
            string hash1 = HashUtils.ComputeHash(input);
            string hash2 = HashUtils.ComputeHash(input);

            // Assert
            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void ComputeHash_ReturnsLowercaseHexString()
        {
            // Arrange
            string input = "LowerCaseTest";

            // Act
            string result = HashUtils.ComputeHash(input);

            // Assert
            Assert.Equal(result.ToLowerInvariant(), result);
            Assert.Matches("^[a-f0-9]{64}$", result);
        }
    }
}