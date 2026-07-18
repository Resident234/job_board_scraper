using Xunit;
using JobBoardScraper.Data;
using JobBoardScraper.Domain.Models;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobBoardScraper.Tests
{
    public class DatabaseClientTests
    {
        #region Constructor

        [Fact]
        public void Constructor_ShouldThrowOnNullConnectionString()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new DatabaseClient(null!));
            Assert.Equal("connectionString", ex.ParamName);
        }

        [Fact]
        public void Constructor_ShouldInitializeStatistics()
        {
            var client = new DatabaseClient("test_connection");
            Assert.NotNull(client.Statistics);
        }

        #endregion

        #region Queue Management — EnqueueResume

        [Fact]
        public void EnqueueResume_ShouldReturnFalse_WhenQueueNotInitialized()
        {
            var client = new DatabaseClient("test_connection");
            var result = client.EnqueueResume(
                link: "https://example.com/resume/1",
                title: "Test Title");

            Assert.False(result);
        }

        [Fact]
        public void EnqueueResume_ShouldReturnFalse_WhenLinkIsEmpty()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            var result = client.EnqueueResume(link: "", title: "Test Title");

            Assert.False(result);
        }

        [Fact]
        public void EnqueueResume_ShouldReturnFalse_WhenLinkIsWhitespace()
        {
            var client = new DatabaseClient("test_connection");
            InitSaveQueue(client);

            var result = client.EnqueueResume(link: "   ", title: "Test Title");

            Assert.False(result);
        }

        [Fact]
        public void EnqueueResume_ShouldEnqueue_WhenValidData()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            var result = client.EnqueueResume(
                link: "https://habr.com/users/test1/",
                title: "Test User",
                slogan: "Test Slogan",
                code: "abc123",
                expert: true,
                workExperience: "3 years",
                levelTitle: "Senior",
                infoTech: "C#",
                salary: 150000,
                lastVisit: "2024-01-01",
                age: "25",
                registration: "2020-01-01",
                citizenship: "Russia",
                remoteWork: true,
                isPublic: true,
                jobSearchStatus: "active",
                isEmpty: false,
                about: "Test about",
                isDeleted: false);

            Assert.True(result);
            Assert.Equal(1, queue.Count);
        }

        [Fact]
        public void EnqueueResume_WithRecord_ShouldEnqueue()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            var record = new ResumeRecord(
                Link: "https://habr.com/users/test1/",
                Title: "Test User",
                Slogan: "Slogan");

            var result = client.EnqueueResume(record);

            Assert.True(result);
            Assert.Equal(1, queue.Count);
        }

        [Fact]
        public void EnqueueResume_WithRecord_ShouldReturnFalse_WhenLinkIsEmpty()
        {
            var client = new DatabaseClient("test_connection");
            InitSaveQueue(client);

            var record = new ResumeRecord(Link: "", Title: "Test");
            var result = client.EnqueueResume(record);

            Assert.False(result);
        }

        #endregion

        #region Queue Management — EnqueueCompany

        [Fact]
        public void EnqueueCompany_ShouldReturnFalse_WhenQueueNotInitialized()
        {
            var client = new DatabaseClient("test_connection");
            var result = client.EnqueueCompany(
                companyCode: "testcorp",
                companyUrl: "https://example.com/company/testcorp");

            Assert.False(result);
        }

        [Fact]
        public void EnqueueCompany_ShouldEnqueue_WhenValidData()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            var result = client.EnqueueCompany(
                companyCode: "testcorp",
                companyUrl: "https://example.com/company/testcorp",
                companyTitle: "Test Company",
                companyRating: 4.5m);

            Assert.True(result);
            Assert.Equal(1, queue.Count);
        }

        [Fact]
        public void EnqueueCompany_WithRecord_ShouldEnqueue()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            var record = new CompanyRecord(
                CompanyCode: "testcorp",
                CompanyUrl: "https://example.com/company/testcorp",
                CompanyTitle: "Test Company");

            var result = client.EnqueueCompany(record);

            Assert.True(result);
            Assert.Equal(1, queue.Count);
        }

        [Fact]
        public void EnqueueCompany_WithDefaultRecord_ShouldReturnFalse_WhenQueueNotInitialized()
        {
            var client = new DatabaseClient("test_connection");
            // Queue is not initialized but record is valid — should return false
            var record = new CompanyRecord(
                CompanyCode: "test",
                CompanyUrl: "https://example.com");
            
            var result = client.EnqueueCompany(record);

            Assert.False(result);
        }

        #endregion

        #region Queue Management — EnqueueCategoryRootId

        [Fact]
        public void EnqueueCategoryRootId_ShouldReturnFalse_WhenQueueNotInitialized()
        {
            var client = new DatabaseClient("test_connection");
            var result = client.EnqueueCategoryRootId("1", "Category 1");

            Assert.False(result);
        }

        [Fact]
        public void EnqueueCategoryRootId_ShouldEnqueue_WhenValidData()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            var result = client.EnqueueCategoryRootId("1", "Category 1");

            Assert.True(result);
            Assert.Equal(1, queue.Count);
        }

        #endregion

        #region Queue Management — EnqueueSkill

        [Fact]
        public void EnqueueSkill_ShouldReturnFalse_WhenQueueNotInitialized()
        {
            var client = new DatabaseClient("test_connection");
            var result = client.EnqueueSkill(1, "C#");

            Assert.False(result);
        }

        [Fact]
        public void EnqueueSkill_ShouldEnqueue_WhenValidData()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            var result = client.EnqueueSkill(42, "C#");

            Assert.True(result);
            Assert.Equal(1, queue.Count);
        }

        #endregion

        #region Queue Management — EnqueueUniversity

        [Fact]
        public void EnqueueUniversity_ShouldNotThrow_WhenQueueNotInitialized()
        {
            var client = new DatabaseClient("test_connection");

            // void method — should not throw
            client.EnqueueUniversity(1, "MIT");
            Assert.True(true);
        }

        [Fact]
        public void EnqueueUniversity_ShouldEnqueue_WhenValidData()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            client.EnqueueUniversity(1, "MIT", "Cambridge", 10000);

            Assert.Equal(1, queue.Count);
        }

        [Fact]
        public void EnqueueUniversity_ShouldEnqueue_WithMinimalData()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            client.EnqueueUniversity(1, "MIT");

            Assert.Equal(1, queue.Count);
        }

        #endregion

        #region Queue Management — EnqueueUserExperience

        [Fact]
        public void EnqueueUserExperience_ShouldReturnFalse_WhenQueueNotInitialized()
        {
            var client = new DatabaseClient("test_connection");
            var result = client.EnqueueUserExperience(
                userLink: "https://example.com/user/1",
                company: new CompanyRecord(
                    CompanyCode: "corp",
                    CompanyUrl: "https://example.com/corp"));

            Assert.False(result);
        }

        [Fact]
        public void EnqueueUserExperience_ShouldEnqueue_WhenValidData()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            var result = client.EnqueueUserExperience(
                userLink: "https://example.com/user/1",
                company: new CompanyRecord(
                    CompanyCode: "corp",
                    CompanyUrl: "https://example.com/corp"),
                position: "Developer",
                duration: "2 years",
                description: "Worked on cool stuff",
                skills: new List<SkillsRecord> { new SkillsRecord(SkillId: 1, SkillTitle: "C#") },
                isFirstRecord: true);

            Assert.True(result);
            Assert.Equal(1, queue.Count);
        }

        [Fact]
        public void EnqueueUserExperience_ShouldEnqueue_WithMinimalData()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            var result = client.EnqueueUserExperience(
                userLink: "https://example.com/user/1",
                company: new CompanyRecord(
                    CompanyCode: "corp",
                    CompanyUrl: "https://example.com/corp"));

            Assert.True(result);
            Assert.Equal(1, queue.Count);
        }

        #endregion

        #region Queue Size

        [Fact]
        public void GetQueueSize_ShouldReturnZero_WhenQueueNotInitialized()
        {
            var client = new DatabaseClient("test_connection");
            Assert.Equal(0, client.GetQueueSize());
        }

        [Fact]
        public void GetQueueSize_ShouldReflectEnqueuedItems()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            queue.Enqueue(CreateTestDbRecord());
            queue.Enqueue(CreateTestDbRecord());
            queue.Enqueue(CreateTestDbRecord());

            Assert.Equal(3, client.GetQueueSize());
        }

        [Fact]
        public void GetQueueSize_ShouldReturnZero_AfterDequeueAll()
        {
            var client = new DatabaseClient("test_connection");
            var queue = InitSaveQueue(client);

            queue.Enqueue(CreateTestDbRecord());
            queue.Enqueue(CreateTestDbRecord());

            queue.TryDequeue(out _);
            queue.TryDequeue(out _);

            Assert.Equal(0, client.GetQueueSize());
        }

        #endregion

        #region Writer Task State

        [Fact]
        public void IsWriterTaskRunning_ShouldReturnFalse_WhenNotStarted()
        {
            var client = new DatabaseClient("test_connection");
            Assert.False(client.IsWriterTaskRunning());
        }

        [Fact]
        public void IsWriterTaskRunning_ShouldReturnFalse_WhenTaskNotCreated()
        {
            var client = new DatabaseClient("test_connection");
            // _dbWriterTask is null by default
            Assert.False(client.IsWriterTaskRunning());
        }

        #endregion

        #region StartWriterTask — validation

        [Fact]
        public void StartWriterTask_ShouldThrowOnNullConnection()
        {
            var client = new DatabaseClient("test_connection");

            var ex = Assert.Throws<ArgumentNullException>(() =>
                client.StartWriterTask(null!, CancellationToken.None));

            Assert.Equal("conn", ex.ParamName);
        }

        #endregion

        #region StopWriterTask — edge cases

        [Fact]
        public async Task StopWriterTask_ShouldNotThrow_WhenTaskNotStarted()
        {
            var client = new DatabaseClient("test_connection");
            // Should complete gracefully without exception
            await client.StopWriterTask();
        }

        #endregion

        #region Helpers

        private static ConcurrentQueue<DbRecord> InitSaveQueue(DatabaseClient client)
        {
            var queue = new ConcurrentQueue<DbRecord>();
            var field = typeof(DatabaseClient).GetField("_saveQueue",
                BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(client, queue);
            return queue;
        }

        private static DbRecord CreateTestDbRecord()
        {
            return new DbRecord(
                Type: DbRecordType.Resume,
                Resume: new ResumeRecord(
                    Link: "https://habr.com/users/test/",
                    Title: "Test"));
        }

        #endregion
    }
}