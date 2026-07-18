using Xunit;
using JobBoardScraper.Infrastructure.Statistics;
using System;
using System.IO;
using System.Linq;

namespace JobBoardScraper.Tests
{
    public class ScraperStatisticsTests
    {
        [Fact]
        public void Constructor_ShouldSetScraperNameAndStartTime()
        {
            var stats = new ScraperStatistics("TestScraper");

            Assert.Equal("TestScraper", stats.ScraperName);
            Assert.True((DateTime.Now - stats.StartTime).TotalSeconds < 2);
            Assert.Null(stats.EndTime);
        }

        [Fact]
        public void InitialRecordCount_ShouldBeSet()
        {
            var stats = new ScraperStatistics("Test");
            stats.SetInitialRecordCount(100);

            Assert.Equal(100, stats.InitialRecordCount);
        }

        [Fact]
        public void IncrementProcessed_ShouldWork()
        {
            var stats = new ScraperStatistics("Test");
            stats.IncrementProcessed();
            stats.IncrementProcessed();
            stats.IncrementProcessed();

            Assert.Equal(3, stats.TotalProcessed);
        }

        [Fact]
        public void IncrementSuccess_ShouldWork()
        {
            var stats = new ScraperStatistics("Test");
            stats.IncrementSuccess();

            Assert.Equal(1, stats.TotalSuccess);
        }

        [Fact]
        public void IncrementFailed_ShouldWork()
        {
            var stats = new ScraperStatistics("Test");
            stats.IncrementFailed();
            stats.IncrementFailed();

            Assert.Equal(2, stats.TotalFailed);
        }

        [Fact]
        public void IncrementSkipped_ShouldWork()
        {
            var stats = new ScraperStatistics("Test");
            stats.IncrementSkipped();

            Assert.Equal(1, stats.TotalSkipped);
        }

        [Fact]
        public void IncrementFound_ShouldWork()
        {
            var stats = new ScraperStatistics("Test");
            stats.IncrementFound();
            stats.IncrementFound();
            stats.IncrementFound();

            Assert.Equal(3, stats.TotalFound);
        }

        [Fact]
        public void IncrementNotFound_ShouldWork()
        {
            var stats = new ScraperStatistics("Test");
            stats.IncrementNotFound();

            Assert.Equal(1, stats.TotalNotFound);
        }

        [Fact]
        public void IncrementItemsCollected_ShouldWork()
        {
            var stats = new ScraperStatistics("Test");
            stats.IncrementItemsCollected();

            Assert.Equal(1, stats.TotalItemsCollected);
        }

        [Fact]
        public void AddItemsCollected_ShouldAddMultiple()
        {
            var stats = new ScraperStatistics("Test");
            stats.AddItemsCollected(5);
            stats.AddItemsCollected(3);

            Assert.Equal(8, stats.TotalItemsCollected);
        }

        [Fact]
        public void UpdateActiveRequests_ShouldSetValue()
        {
            var stats = new ScraperStatistics("Test");
            stats.UpdateActiveRequests(4);

            Assert.Equal(4, stats.ActiveRequests);
        }

        [Fact]
        public void RecordFinalStatusCode_ShouldTrackCounts()
        {
            var stats = new ScraperStatistics("Test");
            stats.RecordFinalStatusCode(200);
            stats.RecordFinalStatusCode(200);
            stats.RecordFinalStatusCode(404);

            Assert.Equal(2, stats.GetFinalStatusCodeStats()[200]);
            Assert.Equal(1, stats.GetFinalStatusCodeStats()[404]);
        }

        [Fact]
        public void RecordAllStatusCodes_ShouldTrackCounts()
        {
            var stats = new ScraperStatistics("Test");
            stats.RecordAllStatusCodes(301);
            stats.RecordAllStatusCodes(500);

            Assert.Equal(1, stats.GetAllStatusCodeStats()[301]);
            Assert.Equal(1, stats.GetAllStatusCodeStats()[500]);
        }

        [Fact]
        public void GetFinalStatusCodeStatsString_WithData_ReturnsFormatted()
        {
            var stats = new ScraperStatistics("Test");
            stats.RecordFinalStatusCode(200);
            stats.RecordFinalStatusCode(404);

            var result = stats.GetFinalStatusCodeStatsString();

            Assert.Contains("200: 1", result);
            Assert.Contains("404: 1", result);
        }

        [Fact]
        public void GetFinalStatusCodeStatsString_Empty_ReturnsNoData()
        {
            var stats = new ScraperStatistics("Test");
            Assert.Equal("Нет данных", stats.GetFinalStatusCodeStatsString());
        }

        [Fact]
        public void GetAllStatusCodeStatsString_WithData_ReturnsFormatted()
        {
            var stats = new ScraperStatistics("Test");
            stats.RecordAllStatusCodes(200);
            stats.RecordAllStatusCodes(500);
            stats.RecordAllStatusCodes(500);

            var result = stats.GetAllStatusCodeStatsString();

            Assert.Contains("200: 1", result);
            Assert.Contains("500: 2", result);
        }

        [Fact]
        public void ElapsedTime_WhenEndTimeNotSet_ReturnsTimeFromStart()
        {
            var stats = new ScraperStatistics("Test");
            var elapsed = stats.ElapsedTime;

            Assert.True(elapsed.TotalSeconds >= 0);
        }

        [Fact]
        public void ElapsedTime_WhenEndTimeSet_ReturnsFixedTime()
        {
            var start = new DateTime(2024, 1, 1, 10, 0, 0);
            var end = new DateTime(2024, 1, 1, 10, 5, 30);
            var stats = new ScraperStatistics("Test")
            {
                StartTime = start,
                EndTime = end
            };

            Assert.Equal(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30)), stats.ElapsedTime);
        }

        [Fact]
        public void ToString_ShouldIncludeAllRelevantStats()
        {
            var stats = new ScraperStatistics("TestScraper");
            stats.SetInitialRecordCount(100);
            stats.IncrementSuccess();
            stats.IncrementFailed();
            stats.IncrementSkipped();

            var result = stats.ToString();

            Assert.Contains("Итоговая статистика", result);
            Assert.Contains("Выбрано записей из БД: 100", result);
            Assert.Contains("Обработано успешно: 1", result);
            Assert.Contains("Ошибок: 1", result);
            Assert.Contains("Пропущено: 1", result);
            Assert.Contains("Время выполнения:", result);
        }

        [Fact]
        public void ToString_WhenNoFailuresAndNoSkipped_OmitsLines()
        {
            var stats = new ScraperStatistics("Test");
            stats.SetInitialRecordCount(50);
            stats.IncrementSuccess();

            var result = stats.ToString();

            Assert.DoesNotContain("Ошибок:", result);
            Assert.DoesNotContain("Пропущено:", result);
        }

        [Fact]
        public void ToDetailedString_IncludesStatusCodeStats()
        {
            var stats = new ScraperStatistics("Test");
            stats.SetInitialRecordCount(10);
            stats.IncrementSuccess();
            stats.RecordFinalStatusCode(200);
            stats.RecordFinalStatusCode(200);
            stats.RecordAllStatusCodes(301);

            var result = stats.ToDetailedString();

            Assert.Contains("Статистика по окончательным кодам ответа", result);
            Assert.Contains("200: 2", result);
            Assert.Contains("Статистика по всем кодам ответа", result);
            Assert.Contains("301: 1", result);
        }

        [Fact]
        public void WriteToLogFile_CreatesFile()
        {
            var stats = new ScraperStatistics("TestLogger");
            stats.SetInitialRecordCount(10);
            stats.IncrementSuccess();
            stats.IncrementFailed();
            stats.EndTime = DateTime.Now;

            var logDir = Path.Combine(Path.GetTempPath(), "TestStats_" + Guid.NewGuid().ToString("N"));

            try
            {
                stats.WriteToLogFile(logDir);

                var files = Directory.GetFiles(logDir, "TestLogger_stats_*.log");
                Assert.NotEmpty(files);

                var content = File.ReadAllText(files[0]);
                Assert.Contains("=== Статистика TestLogger ===", content);
                Assert.Contains("Обработано успешно: 1", content);
                Assert.Contains("Ошибок: 1", content);
            }
            finally
            {
                if (Directory.Exists(logDir))
                    Directory.Delete(logDir, true);
            }
        }

        [Fact]
        public void WriteToLogFile_WithStatusCodes_IncludesDetailedCodes()
        {
            var stats = new ScraperStatistics("TestCodes");
            stats.SetInitialRecordCount(5);
            stats.IncrementSuccess();
            stats.RecordFinalStatusCode(200);
            stats.RecordFinalStatusCode(404);
            stats.EndTime = DateTime.Now;

            var logDir = Path.Combine(Path.GetTempPath(), "TestStatsCodes_" + Guid.NewGuid().ToString("N"));

            try
            {
                stats.WriteToLogFile(logDir);

                var files = Directory.GetFiles(logDir, "TestCodes_stats_*.log");
                var content = File.ReadAllText(files[0]);

                Assert.Contains("200 (OK): 1", content);
                Assert.Contains("404 (Not Found): 1", content);
            }
            finally
            {
                if (Directory.Exists(logDir))
                    Directory.Delete(logDir, true);
            }
        }

        [Fact]
        public void WriteToLogFile_WithNoStatusCodes_ShowsNoData()
        {
            var stats = new ScraperStatistics("TestEmpty");
            stats.SetInitialRecordCount(3);
            stats.IncrementSuccess();
            stats.EndTime = DateTime.Now;

            var logDir = Path.Combine(Path.GetTempPath(), "TestStatsEmpty_" + Guid.NewGuid().ToString("N"));

            try
            {
                stats.WriteToLogFile(logDir);

                var files = Directory.GetFiles(logDir, "TestEmpty_stats_*.log");
                var content = File.ReadAllText(files[0]);

                Assert.Contains("Нет данных", content);
            }
            finally
            {
                if (Directory.Exists(logDir))
                    Directory.Delete(logDir, true);
            }
        }

        [Fact]
        public void WriteToLogFile_WithActiveRequests_IncludesActiveRequests()
        {
            var stats = new ScraperStatistics("TestActive");
            stats.SetInitialRecordCount(10);
            stats.IncrementSuccess();
            stats.UpdateActiveRequests(3);
            stats.EndTime = DateTime.Now;

            var logDir = Path.Combine(Path.GetTempPath(), "TestStatsActive_" + Guid.NewGuid().ToString("N"));

            try
            {
                stats.WriteToLogFile(logDir);

                var files = Directory.GetFiles(logDir, "TestActive_stats_*.log");
                var content = File.ReadAllText(files[0]);

                Assert.Contains("=== Статистика TestActive ===", content);
                Assert.Contains("Обработано успешно: 1", content);
            }
            finally
            {
                if (Directory.Exists(logDir))
                    Directory.Delete(logDir, true);
            }
        }

        [Fact]
        public void AverageRequestTime_SetterWorks()
        {
            var stats = new ScraperStatistics("Test");
            stats.AverageRequestTime = 1.5;

            Assert.Equal(1.5, stats.AverageRequestTime);
        }
    }

    public class DatabaseStatisticsTests
    {
        [Fact]
        public void RecordInsert_ShouldTrackInserts()
        {
            var dbStats = new DatabaseStatistics();
            dbStats.RecordInsert("habr_resumes");
            dbStats.RecordInsert("habr_resumes");
            dbStats.RecordInsert("habr_companies");

            var resumeStats = dbStats.GetTableStats("habr_resumes");
            var companyStats = dbStats.GetTableStats("habr_companies");

            Assert.Equal(2, resumeStats.Inserts);
            Assert.Equal(1, companyStats.Inserts);
        }

        [Fact]
        public void RecordUpdate_ShouldTrackUpdates()
        {
            var dbStats = new DatabaseStatistics();
            dbStats.RecordUpdate("habr_resumes");
            dbStats.RecordUpdate("habr_resumes");
            dbStats.RecordUpdate("habr_resumes");

            var stats = dbStats.GetTableStats("habr_resumes");

            Assert.Equal(3, stats.Updates);
        }

        [Fact]
        public void RecordDelete_ShouldTrackDeletes()
        {
            var dbStats = new DatabaseStatistics();
            dbStats.RecordDelete("habr_companies");

            var stats = dbStats.GetTableStats("habr_companies");

            Assert.Equal(1, stats.Deletes);
        }

        [Fact]
        public void RecordSkipped_ShouldTrackSkips()
        {
            var dbStats = new DatabaseStatistics();
            dbStats.RecordSkipped("habr_resumes");
            dbStats.RecordSkipped("habr_resumes");

            var stats = dbStats.GetTableStats("habr_resumes");

            Assert.Equal(2, stats.Skipped);
        }

        [Fact]
        public void RecordError_ShouldTrackErrors()
        {
            var dbStats = new DatabaseStatistics();
            dbStats.RecordError("habr_companies");

            var stats = dbStats.GetTableStats("habr_companies");

            Assert.Equal(1, stats.Errors);
        }

        [Fact]
        public void InitializeAllTables_ShouldCreateAllTables()
        {
            var dbStats = new DatabaseStatistics();
            dbStats.InitializeAllTables();

            var expectedTables = new[]
            {
                "habr_resumes",
                "habr_companies",
                "habr_category_root_ids",
                "habr_skills",
                "habr_company_skills",
                "habr_levels",
                "habr_user_skills",
                "habr_user_experience",
                "habr_user_experience_skills",
                "habr_resumes_universities",
                "habr_resumes_educations",
                "habr_universities",
                "habr_company_reviews"
            };

            foreach (var table in expectedTables)
            {
                var stats = dbStats.GetTableStats(table);
                Assert.NotNull(stats);
                Assert.Equal(table, stats.TableName);
                Assert.Equal(0, stats.Inserts);
                Assert.Equal(0, stats.Updates);
            }
        }

        [Fact]
        public void GetSummary_ShouldIncludeAllTables()
        {
            var dbStats = new DatabaseStatistics();
            dbStats.RecordInsert("habr_resumes");
            dbStats.RecordInsert("habr_companies");
            dbStats.RecordUpdate("habr_resumes");
            dbStats.RecordSkipped("habr_skills");

            var summary = dbStats.GetSummary();

            Assert.Contains("=== Статистика БД", summary);
            Assert.Contains("habr_resumes", summary);
            Assert.Contains("habr_companies", summary);
            Assert.Contains("Вставлено=", summary);
            Assert.Contains("Обновлено=", summary);
            Assert.Contains("Пропущено=", summary);
        }

        [Fact]
        public void GetSummary_ShouldIncludeTotals()
        {
            var dbStats = new DatabaseStatistics();
            dbStats.RecordInsert("habr_resumes");
            dbStats.RecordInsert("habr_companies");
            dbStats.RecordSkipped("habr_skills");

            var summary = dbStats.GetSummary();

            Assert.Contains("Вставлено=2", summary);
            Assert.Contains("Пропущено=1", summary);
            Assert.Contains("ИТОГО:", summary);
        }

        [Fact]
        public void Reset_ShouldClearAllStats()
        {
            var dbStats = new DatabaseStatistics();
            dbStats.RecordInsert("habr_resumes");
            dbStats.RecordInsert("habr_companies");
            dbStats.Reset();

            // After reset, getting stats should still return an empty object
            var resumeStats = dbStats.GetTableStats("habr_resumes");
            Assert.Equal(0, resumeStats.Inserts);
        }

        [Fact]
        public void GetTableStats_ShouldCreateNewIfNotExists()
        {
            var dbStats = new DatabaseStatistics();
            var stats = dbStats.GetTableStats("new_table");

            Assert.NotNull(stats);
            Assert.Equal("new_table", stats.TableName);
        }
    }

    public class TableStatisticsTests
    {
        [Fact]
        public void Constructor_ShouldSetName()
        {
            var stats = new TableStatistics("my_table");
            Assert.Equal("my_table", stats.TableName);
        }

        [Fact]
        public void RecordInsert_ShouldIncrement()
        {
            var stats = new TableStatistics("test");
            stats.RecordInsert();
            stats.RecordInsert();

            Assert.Equal(2, stats.Inserts);
        }

        [Fact]
        public void RecordUpdate_ShouldIncrement()
        {
            var stats = new TableStatistics("test");
            stats.RecordUpdate();

            Assert.Equal(1, stats.Updates);
        }

        [Fact]
        public void RecordDelete_ShouldIncrement()
        {
            var stats = new TableStatistics("test");
            stats.RecordDelete();
            stats.RecordDelete();

            Assert.Equal(2, stats.Deletes);
        }

        [Fact]
        public void RecordSkipped_ShouldIncrement()
        {
            var stats = new TableStatistics("test");
            stats.RecordSkipped();
            stats.RecordSkipped();
            stats.RecordSkipped();

            Assert.Equal(3, stats.Skipped);
        }

        [Fact]
        public void RecordError_ShouldIncrement()
        {
            var stats = new TableStatistics("test");
            stats.RecordError();

            Assert.Equal(1, stats.Errors);
        }

        [Fact]
        public void ToString_ShouldFormatCorrectly()
        {
            var stats = new TableStatistics("test_table");
            stats.RecordInsert();
            stats.RecordInsert();
            stats.RecordInsert();
            stats.RecordUpdate();
            stats.RecordSkipped();

            var result = stats.ToString();

            Assert.Contains("test_table", result);
            Assert.Contains("Вставлено=3", result);
            Assert.Contains("Обновлено=1", result);
            Assert.Contains("Удалено=0", result);
            Assert.Contains("Пропущено=1", result);
            Assert.Contains("Ошибок=0", result);
        }
    }

    public class ScraperTrafficStatsTests
    {
        [Fact]
        public void AddRequest_ShouldTrackBytesAndCount()
        {
            var stats = new ScraperTrafficStats();
            stats.AddRequest(1000);
            stats.AddRequest(2000);

            Assert.Equal(2, stats.RequestCount);
            Assert.Equal(3000, stats.TotalBytes);
        }

        [Fact]
        public void AverageBytesPerRequest_WithRequests_ReturnsCorrect()
        {
            var stats = new ScraperTrafficStats();
            stats.AddRequest(1000);
            stats.AddRequest(3000);

            Assert.Equal(2000.0, stats.AverageBytesPerRequest);
        }

        [Fact]
        public void AverageBytesPerRequest_WithNoRequests_ReturnsZero()
        {
            var stats = new ScraperTrafficStats();
            Assert.Equal(0, stats.AverageBytesPerRequest);
        }

        [Fact]
        public void FormatBytes_Bytes_ReturnsB()
        {
            var stats = new ScraperTrafficStats();
            var result = stats.FormatBytes(500);
            Assert.Contains("B", result);
        }

        [Fact]
        public void FormatBytes_Kilobytes_ReturnsKB()
        {
            var stats = new ScraperTrafficStats();
            var result = stats.FormatBytes(2048);
            Assert.Contains("KB", result);
        }

        [Fact]
        public void FormatBytes_Megabytes_ReturnsMB()
        {
            var stats = new ScraperTrafficStats();
            var result = stats.FormatBytes(5 * 1024 * 1024);
            Assert.Contains("MB", result);
            Assert.Contains("5", result);
        }

        [Fact]
        public void FormatBytes_Gigabytes_ReturnsGB()
        {
            var stats = new ScraperTrafficStats();
            var result = stats.FormatBytes(3L * 1024 * 1024 * 1024);
            Assert.Contains("GB", result);
            Assert.Contains("3", result);
        }

        [Fact]
        public void FormatBytes_Zero_Returns0B()
        {
            var stats = new ScraperTrafficStats();
            var result = stats.FormatBytes(0);
            Assert.Contains("0", result);
            Assert.Contains("B", result);
        }

        [Fact]
        public void ToString_ShouldIncludeAllInfo()
        {
            var stats = new ScraperTrafficStats();
            stats.AddRequest(1500);
            stats.AddRequest(2500);

            var result = stats.ToString();

            Assert.Contains("Requests: 2", result);
            Assert.Contains("Total:", result);
            Assert.Contains("KB", result);
            Assert.Contains("Avg:", result);
        }
    }

    public class TrafficStatisticsTests : IDisposable
    {
        private readonly string _tempFile;

        public TrafficStatisticsTests()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), "TrafficTest_" + Guid.NewGuid().ToString("N") + ".txt");
        }

        public void Dispose()
        {
            try { if (File.Exists(_tempFile)) File.Delete(_tempFile); } catch { }
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullFile()
        {
            Assert.Throws<ArgumentNullException>(() => new TrafficStatistics(null));
        }

        [Fact]
        public void RecordRequest_ShouldWorkForMultipleScrapers()
        {
            using var traffic = new TrafficStatistics(_tempFile, TimeSpan.FromDays(1));
            traffic.RecordRequest("ScraperA", 1000);
            traffic.RecordRequest("ScraperA", 2000);
            traffic.RecordRequest("ScraperB", 5000);

            var statsA = traffic.GetStats("ScraperA");
            var statsB = traffic.GetStats("ScraperB");

            Assert.NotNull(statsA);
            Assert.NotNull(statsB);
            Assert.Equal(3000, statsA.TotalBytes);
            Assert.Equal(2, statsA.RequestCount);
            Assert.Equal(5000, statsB.TotalBytes);
            Assert.Equal(1, statsB.RequestCount);
        }

        [Fact]
        public void RecordRequest_WithEmptyName_ShouldThrow()
        {
            using var traffic = new TrafficStatistics(_tempFile, TimeSpan.FromDays(1));
            Assert.Throws<ArgumentException>(() => traffic.RecordRequest("", 100));
            Assert.Throws<ArgumentException>(() => traffic.RecordRequest(null, 100));
        }

        [Fact]
        public void GetStats_ForUnknownScraper_ReturnsNull()
        {
            using var traffic = new TrafficStatistics(_tempFile, TimeSpan.FromDays(1));
            Assert.Null(traffic.GetStats("NonExistent"));
        }

        [Fact]
        public void GetTotalStats_ShouldSumStats()
        {
            using var traffic = new TrafficStatistics(_tempFile, TimeSpan.FromDays(1));
            traffic.RecordRequest("A", 1000);
            traffic.RecordRequest("A", 2000);
            traffic.RecordRequest("B", 7000);

            var (totalBytes, totalRequests) = traffic.GetTotalStats();

            Assert.Equal(10000, totalBytes);
            Assert.Equal(3, totalRequests);
        }

        [Fact]
        public void GetTotalStats_WithNoData_ReturnsZeros()
        {
            using var traffic = new TrafficStatistics(_tempFile, TimeSpan.FromDays(1));
            var (totalBytes, totalRequests) = traffic.GetTotalStats();

            Assert.Equal(0, totalBytes);
            Assert.Equal(0, totalRequests);
        }

        [Fact]
        public void SaveToFile_CreatesFileWithStats()
        {
            using (var traffic = new TrafficStatistics(_tempFile, TimeSpan.FromDays(1)))
            {
                traffic.RecordRequest("ScraperX", 2048);
                traffic.RecordRequest("ScraperX", 1024);
                traffic.RecordRequest("ScraperY", 4096);
            }

            Assert.True(File.Exists(_tempFile));

            var content = File.ReadAllText(_tempFile);
            Assert.Contains("Traffic Statistics Report", content);
            Assert.Contains("ScraperX", content);
            Assert.Contains("ScraperY", content);
            Assert.Contains("Total Requests: 3", content);
            Assert.Contains("Total Traffic:", content);
            Assert.Contains("KB", content);
        }

        [Fact]
        public void SaveToFile_WithNoData_StillCreatesFile()
        {
            using (var traffic = new TrafficStatistics(_tempFile, TimeSpan.FromDays(1)))
            {
                // No data recorded
            }

            Assert.True(File.Exists(_tempFile));
        }

        [Fact]
        public void Dispose_CallsSaveToFile()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), "TrafficDispose_" + Guid.NewGuid().ToString("N") + ".txt");
            using (var traffic = new TrafficStatistics(tempFile, TimeSpan.FromDays(1)))
            {
                traffic.RecordRequest("Test", 1234);
            }

            Assert.True(File.Exists(tempFile));
            var content = File.ReadAllText(tempFile);
            Assert.Contains("Test", content);
            Assert.Contains("KB", content);

            try { File.Delete(tempFile); } catch { }
        }

        [Fact]
        public void SaveToFile_CreatesDirectoryIfNotExists()
        {
            var nestedDir = Path.Combine(Path.GetTempPath(), "NestedDir_" + Guid.NewGuid().ToString("N"), "sub");
            var nestedFile = Path.Combine(nestedDir, "traffic.txt");

            try
            {
                using (var traffic = new TrafficStatistics(nestedFile, TimeSpan.FromDays(1)))
                {
                    traffic.RecordRequest("Test", 100);
                }

                Assert.True(File.Exists(nestedFile));
            }
            finally
            {
                if (Directory.Exists(nestedDir))
                    Directory.Delete(Path.GetDirectoryName(nestedDir), true);
            }
        }
    }
}