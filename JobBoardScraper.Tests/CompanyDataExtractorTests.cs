using Xunit;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using JobBoardScraper.Parsing;
using JobBoardScraper.Domain.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;

namespace JobBoardScraper.Tests
{
    public class CompanyDataExtractorTests
    {
        private async Task<IHtmlDocument> GetDocument(string html)
        {
            var config = new AngleSharp.Configuration();
            var context = BrowsingContext.New(config);
            var doc = await context.OpenAsync(req => req.Content(html));
            return (IHtmlDocument)doc;
        }

        [Theory]
        [InlineData("4.5", "4.5")]
        [InlineData("3.8", "3.8")]
        public async Task ExtractCompanyRating_ShouldParseCorrectly(string ratingText, string expectedStr)
        {
            // Arrange
            var html = $@"<span class='rating'>{ratingText}</span>";
            var doc = await GetDocument(html);

            // Act
            var result = CompanyDataExtractor.ExtractCompanyRating(doc);

            // Assert
            var expected = decimal.Parse(expectedStr, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task ExtractFollowersCount_ShouldParseCorrectly()
        {
            // Arrange
            var html = @"<div data-tooltip='Подписчики и те, кто хочет тут работать'>
                            <span class='count'>123 / 45</span>
                         </div>";
            var doc = await GetDocument(html);

            // Act
            var (followers, wantWork) = CompanyDataExtractor.ExtractFollowersCount(doc);

            // Assert
            Assert.Equal(123, followers);
            Assert.Equal(45, wantWork);
        }

        [Fact]
        public async Task ExtractCompanyData_ShouldParseCompleteRecord()
        {
            // Arrange
            var html = @"
                <div class='section-box'>
                    <div class='rating-card__company-title'>
                        <a href='/companies/testcorp'>Test Corp</a>
                    </div>
                    <h2 class='rating-card__company-title-text'>Test Corporation</h2>
                    <span class='rating-card__company-rating'>4.7</span>
                    <div class='rating-card__company-description'>Best company ever</div>
                    <div class='rating-card__company-meta'>
                        <a href='/cities/1?city_id=100'>Moscow</a>
                    </div>
                    <div class='rating-card__company-awards'>
                        <img src='1.png' alt='Award 1'>
                        <img src='2.png' alt='Award 2'>
                    </div>
                    <span class='rating-card__scores-value'>4.8</span>
                    <div class='rating-card__review-message'>Great place to work!</div>
                </div>";
            
            var doc = await GetDocument(html);
            var section = doc.QuerySelector(".section-box");

            // Act
            var result = CompanyDataExtractor.ExtractCompanyData(section);

            // Assert
            Assert.NotNull(result);
            var record = (CompanyRecord)result!;
            Assert.Equal("testcorp", record.CompanyCode);
            Assert.Equal("Test Corporation", record.CompanyTitle);
            Assert.Equal(4.7m, record.Rating);
            Assert.Equal("Best company ever", record.About);
            Assert.Equal("Moscow", record.City);
            Assert.Equal(2, record.Awards?.Count);
            Assert.Equal(4.8m, record.Scores);
            Assert.Single(record.ReviewRecords);
        }
    }
}