using Xunit;
using AngleSharp;
using AngleSharp.Dom;
using JobBoardScraper.Parsing;
using JobBoardScraper.Domain.Models;
using System.Threading.Tasks;

namespace JobBoardScraper.Tests
{
    public class UserDataExtractorTests
    {
        private async Task<IDocument> GetDocument(string html)
        {
            var config = new AngleSharp.Configuration();
            var context = BrowsingContext.New(config);
            return await context.OpenAsync(req => req.Content(html));
        }

        [Fact]
        public async Task IsDeletedProfile_ShouldReturnTrue_WhenClassIsPresent()
        {
            // Arrange
            var html = "<div class='user-profile__deleted'>Deleted</div>";
            var doc = await GetDocument(html);

            // Act
            var result = UserDataExtractor.IsDeletedProfile(doc);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsDeletedProfile_ShouldReturnTrue_WhenTextIsPresent()
        {
            // Arrange
            var html = "<div>Профиль удален</div>";
            var doc = await GetDocument(html);

            // Act
            var result = UserDataExtractor.IsDeletedProfile(doc);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsPrivateProfile_ShouldReturnTrue_WhenClassIsPresent()
        {
            // Arrange
            var html = "<div class='user-page-sidebar--status-hidden'>Hidden</div>";
            var doc = await GetDocument(html);

            // Act
            var result = UserDataExtractor.IsPrivateProfile(doc);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsPrivateProfile_ShouldReturnTrue_WhenTextIsPresent()
        {
            // Arrange
            var html = "<div>Доступ ограничен настройками приватности</div>";
            var doc = await GetDocument(html);

            // Act
            var result = UserDataExtractor.IsPrivateProfile(doc);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsPublicProfile_ShouldReturnCorrectName()
        {
            // Arrange
            var html = "<html><body><h1 class='page-title__title'>Ivan Ivanov</h1></body></html>";
            var doc = await GetDocument(html);

            // Act
            var (name, isPublic) = UserDataExtractor.IsPublicProfile(doc);

            // Assert
            Assert.True(isPublic);
            Assert.Equal("Ivan Ivanov", name);
        }

        [Fact]
        public async Task IsPublicProfile_ShouldReturnFalse_WhenNameMissing()
        {
            // Arrange
            var html = "<html><body><h1>No Title</h1></body></html>";
            var doc = await GetDocument(html);

            // Act
            var (name, isPublic) = UserDataExtractor.IsPublicProfile(doc);

            // Assert
            Assert.False(isPublic);
            Assert.Null(name);
        }

        [Fact]
        public async Task ExtractSalaryAndJobStatus_ShouldParseCorrectly()
        {
            // Arrange
            var html = "<div class='user-page-sidebar__career'>Ищу работу. От 150 000 ₽</div>";
            var doc = await GetDocument(html);

            // Act
            var (salary, status) = UserDataExtractor.ExtractSalaryAndJobStatus(doc);

            // Assert
            Assert.Equal(150000, salary);
            Assert.Equal("Ищу работу", status);
        }

        [Fact]
        public async Task ExtractSalaryAndJobStatus_ShouldHandleNoSalary()
        {
            // Arrange
            var html = "<div class='user-page-sidebar__career'>Рассматриваю предложения</div>";
            var doc = await GetDocument(html);

            // Act
            var (salary, status) = UserDataExtractor.ExtractSalaryAndJobStatus(doc);

            // Assert
            Assert.Null(salary);
            Assert.Equal("Рассматриваю предложения", status);
        }

        [Fact]
        public async Task IsEmptyProfile_ShouldReturnTrue_WhenReallyEmpty()
        {
            // Arrange
            var html = "<html><body><div class='content-section'>Empty</div></body></html>";
            var doc = await GetDocument(html);

            // Act
            var result = UserDataExtractor.IsEmptyProfile(doc);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsEmptyProfile_ShouldReturnFalse_WhenHasAbout()
        {
            // Arrange
            // CSS selector: .content-section.content-section--appearance-resume
            // HTML class: "content-section content-section--appearance-resume"
            var html = @"
                <div class='content-section content-section--appearance-resume'>
                    <div class='content-section__title'>Обо мне</div>
                    <div class='style-ugc'>I am a professional developer</div>
                </div>";
            var doc = await GetDocument(html);

            // Act
            var result = UserDataExtractor.IsEmptyProfile(doc);

            // Assert
            Assert.False(result);
        }
    }
}