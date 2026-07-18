using Xunit;
using JobBoardScraper.Domain.Models;
using System.Collections.Generic;

namespace JobBoardScraper.Tests
{
    public class DomainModelTests
    {
        #region InsertMode

        [Fact]
        public void InsertMode_ShouldHaveExpectedValues()
        {
            Assert.Equal(0, (int)InsertMode.SkipIfExists);
            Assert.Equal(1, (int)InsertMode.UpdateIfExists);
        }

        #endregion

        #region ResumeRecord

        [Fact]
        public void ResumeRecord_ShouldCreateWithDefaults()
        {
            var r = new ResumeRecord();
            Assert.Equal(InsertMode.SkipIfExists, r.Mode);
            Assert.Null(r.Link);
            Assert.Null(r.Title);
            Assert.Null(r.Slogan);
            Assert.Null(r.Skills);
            Assert.Null(r.IsDeleted);
        }

        [Fact]
        public void ResumeRecord_ShouldStoreValues()
        {
            var r = new ResumeRecord(
                Mode: InsertMode.UpdateIfExists,
                Link: "https://habr.com/users/test/",
                Title: "Test User",
                Slogan: "Test Slogan",
                Code: "abc123",
                Expert: true,
                WorkExperience: "3 years",
                UserCode: "user123",
                UserName: "Test User Name",
                IsExpert: true,
                LevelTitle: "Senior",
                InfoTech: "C#",
                Salary: 150000,
                LastVisit: "2024-01-01",
                Age: "25",
                Registration: "2020-01-01",
                Citizenship: "Russia",
                RemoteWork: true,
                IsPublic: true,
                JobSearchStatus: "active",
                IsEmpty: false,
                IsDeleted: false,
                About: "Test about");

            Assert.Equal(InsertMode.UpdateIfExists, r.Mode);
            Assert.Equal("https://habr.com/users/test/", r.Link);
            Assert.Equal("Test User", r.Title);
            Assert.Equal("Test Slogan", r.Slogan);
            Assert.Equal("abc123", r.Code);
            Assert.True(r.Expert);
            Assert.Equal("3 years", r.WorkExperience);
            Assert.Equal("user123", r.UserCode);
            Assert.Equal("Test User Name", r.UserName);
            Assert.True(r.IsExpert);
            Assert.Equal("Senior", r.LevelTitle);
            Assert.Equal("C#", r.InfoTech);
            Assert.Equal(150000, r.Salary);
            Assert.Equal("2024-01-01", r.LastVisit);
            Assert.Equal("25", r.Age);
            Assert.Equal("2020-01-01", r.Registration);
            Assert.Equal("Russia", r.Citizenship);
            Assert.True(r.RemoteWork);
            Assert.True(r.IsPublic);
            Assert.Equal("active", r.JobSearchStatus);
            Assert.False(r.IsEmpty);
            Assert.False(r.IsDeleted);
            Assert.Equal("Test about", r.About);
        }

        [Fact]
        public void ResumeRecord_ShouldSupportEquality()
        {
            var r1 = new ResumeRecord(Link: "https://habr.com/users/test/", Title: "Test");
            var r2 = new ResumeRecord(Link: "https://habr.com/users/test/", Title: "Test");
            var r3 = new ResumeRecord(Link: "https://habr.com/users/other/", Title: "Other");

            Assert.Equal(r1, r2);
            Assert.NotEqual(r1, r3);
        }

        [Fact]
        public void ResumeRecord_WithSkills_ShouldStoreList()
        {
            var skills = new List<SkillsRecord>
            {
                new SkillsRecord(SkillId: 1, SkillTitle: "C#"),
                new SkillsRecord(SkillId: 2, SkillTitle: "SQL")
            };

            var r = new ResumeRecord(
                Link: "https://habr.com/users/test/",
                Title: "Test",
                Skills: skills);

            Assert.Equal(2, r.Skills!.Count);
            Assert.Equal(1, r.Skills[0].SkillId);
            Assert.Equal("C#", r.Skills[0].SkillTitle);
        }

        [Fact]
        public void ResumeRecord_WithAllCollectionFields_ShouldStoreCorrectly()
        {
            var skills = new List<SkillsRecord> { new SkillsRecord(SkillId: 1) };
            var community = new List<CommunityParticipationData>
            {
                new CommunityParticipationData { Name = "Community1" }
            };
            var experience = new List<UserExperienceRecord>
            {
                new UserExperienceRecord(
                    UserLink: "https://habr.com/users/test/",
                    Company: new CompanyRecord(CompanyCode: "corp", CompanyUrl: "https://example.com/corp"))
            };
            var universities = new List<UserUniversityRecord>
            {
                new UserUniversityRecord(
                    UserLink: "https://habr.com/users/test/",
                    University: new UniversityRecord(HabrId: 1, Name: "MIT"))
            };
            var educations = new List<AdditionalEducationRecord>
            {
                new AdditionalEducationRecord(UserLink: "https://habr.com/users/test/", Title: "Course")
            };

            var r = new ResumeRecord(
                Link: "https://habr.com/users/test/",
                Title: "Test",
                Skills: skills,
                CommunityParticipation: community,
                UserExperience: experience,
                UserUniversities: universities,
                AdditionalEducations: educations);

            Assert.Single(r.Skills!);
            Assert.Single(r.CommunityParticipation!);
            Assert.Single(r.UserExperience!);
            Assert.Single(r.UserUniversities!);
            Assert.Single(r.AdditionalEducations!);
        }

        #endregion

        #region CompanyRecord

        [Fact]
        public void CompanyRecord_ShouldCreateWithDefaults()
        {
            var c = new CompanyRecord(CompanyCode: "test", CompanyUrl: "https://example.com");
            Assert.Equal("test", c.CompanyCode);
            Assert.Equal("https://example.com", c.CompanyUrl);
            Assert.Null(c.CompanyTitle);
            Assert.Null(c.CompanyId);
            Assert.Null(c.Rating);
            Assert.Null(c.Skills);
        }

        [Fact]
        public void CompanyRecord_ShouldStoreValues()
        {
            var c = new CompanyRecord(
                CompanyCode: "testcorp",
                CompanyUrl: "https://example.com/testcorp",
                CompanyTitle: "Test Corporation",
                CompanyId: 12345,
                About: "About company",
                Description: "Full description",
                Site: "https://testcorp.com",
                Rating: 4.5m,
                CurrentEmployees: 100,
                PastEmployees: 50,
                Followers: 200,
                WantWork: 10,
                EmployeesCount: "100-200",
                Habr: true,
                City: "Moscow",
                Awards: new List<string> { "Best Place to Work" },
                Scores: 95.5m);

            Assert.Equal("testcorp", c.CompanyCode);
            Assert.Equal("https://example.com/testcorp", c.CompanyUrl);
            Assert.Equal("Test Corporation", c.CompanyTitle);
            Assert.Equal(12345, c.CompanyId);
            Assert.Equal("About company", c.About);
            Assert.Equal("Full description", c.Description);
            Assert.Equal("https://testcorp.com", c.Site);
            Assert.Equal(4.5m, c.Rating);
            Assert.Equal(100, c.CurrentEmployees);
            Assert.Equal(50, c.PastEmployees);
            Assert.Equal(200, c.Followers);
            Assert.Equal(10, c.WantWork);
            Assert.Equal("100-200", c.EmployeesCount);
            Assert.True(c.Habr);
            Assert.Equal("Moscow", c.City);
            Assert.Single(c.Awards!);
            Assert.Equal("Best Place to Work", c.Awards[0]);
            Assert.Equal(95.5m, c.Scores);
        }

        [Fact]
        public void CompanyRecord_ShouldSupportEquality()
        {
            var c1 = new CompanyRecord(CompanyCode: "corp", CompanyUrl: "https://example.com");
            var c2 = new CompanyRecord(CompanyCode: "corp", CompanyUrl: "https://example.com");
            var c3 = new CompanyRecord(CompanyCode: "other", CompanyUrl: "https://other.com");

            Assert.Equal(c1, c2);
            Assert.NotEqual(c1, c3);
        }

        [Fact]
        public void CompanyRecord_WithReviews_ShouldStoreCorrectly()
        {
            var reviews = new List<CompanyReviewRecord>
            {
                new CompanyReviewRecord("corp", "hash1", "Great company!"),
                new CompanyReviewRecord("corp", "hash2", "Good place to work")
            };

            var c = new CompanyRecord(
                CompanyCode: "corp",
                CompanyUrl: "https://example.com",
                ReviewRecords: reviews);

            Assert.Equal(2, c.ReviewRecords!.Count);
            Assert.Equal("Great company!", c.ReviewRecords[0].ReviewText);
        }

        #endregion

        #region SkillsRecord

        [Fact]
        public void SkillsRecord_ShouldCreateWithDefaults()
        {
            var s = new SkillsRecord();
            Assert.Null(s.SkillId);
            Assert.Null(s.SkillTitle);
        }

        [Fact]
        public void SkillsRecord_ShouldStoreValues()
        {
            var s = new SkillsRecord(SkillId: 42, SkillTitle: "C#");
            Assert.Equal(42, s.SkillId);
            Assert.Equal("C#", s.SkillTitle);
        }

        [Fact]
        public void SkillsRecord_ShouldSupportEquality()
        {
            var s1 = new SkillsRecord(SkillId: 1, SkillTitle: "C#");
            var s2 = new SkillsRecord(SkillId: 1, SkillTitle: "C#");
            var s3 = new SkillsRecord(SkillId: 2, SkillTitle: "SQL");

            Assert.Equal(s1, s2);
            Assert.NotEqual(s1, s3);
        }

        #endregion

        #region CategoryRootIdRecord

        [Fact]
        public void CategoryRootIdRecord_ShouldStoreValues()
        {
            var c = new CategoryRootIdRecord(CategoryId: "1", CategoryName: "Programming");
            Assert.Equal("1", c.CategoryId);
            Assert.Equal("Programming", c.CategoryName);
        }

        [Fact]
        public void CategoryRootIdRecord_ShouldSupportEquality()
        {
            var c1 = new CategoryRootIdRecord("1", "Programming");
            var c2 = new CategoryRootIdRecord("1", "Programming");
            var c3 = new CategoryRootIdRecord("2", "Design");

            Assert.Equal(c1, c2);
            Assert.NotEqual(c1, c3);
        }

        #endregion

        #region UniversityRecord

        [Fact]
        public void UniversityRecord_ShouldCreateWithDefaults()
        {
            var u = new UniversityRecord(HabrId: 1, Name: "MIT");
            Assert.Equal(1, u.HabrId);
            Assert.Equal("MIT", u.Name);
            Assert.Null(u.City);
            Assert.Null(u.GraduateCount);
        }

        [Fact]
        public void UniversityRecord_ShouldStoreValues()
        {
            var u = new UniversityRecord(HabrId: 42, Name: "Stanford", City: "Stanford", GraduateCount: 5000);
            Assert.Equal(42, u.HabrId);
            Assert.Equal("Stanford", u.Name);
            Assert.Equal("Stanford", u.City);
            Assert.Equal(5000, u.GraduateCount);
        }

        [Fact]
        public void UniversityRecord_ShouldSupportEquality()
        {
            var u1 = new UniversityRecord(1, "MIT");
            var u2 = new UniversityRecord(1, "MIT");
            var u3 = new UniversityRecord(2, "Stanford");

            Assert.Equal(u1, u2);
            Assert.NotEqual(u1, u3);
        }

        #endregion

        #region UserExperienceRecord

        [Fact]
        public void UserExperienceRecord_ShouldCreateWithDefaults()
        {
            var u = new UserExperienceRecord(
                UserLink: "https://habr.com/users/test/",
                Company: new CompanyRecord(CompanyCode: "corp", CompanyUrl: "https://example.com"));

            Assert.Equal("https://habr.com/users/test/", u.UserLink);
            Assert.False(u.IsFirstRecord);
            Assert.Null(u.Position);
            Assert.Null(u.Skills);
        }

        [Fact]
        public void UserExperienceRecord_ShouldStoreValues()
        {
            var company = new CompanyRecord(CompanyCode: "corp", CompanyUrl: "https://example.com");
            var skills = new List<SkillsRecord> { new SkillsRecord(SkillId: 1, SkillTitle: "C#") };

            var u = new UserExperienceRecord(
                UserLink: "https://habr.com/users/test/",
                Company: company,
                Position: "Developer",
                Duration: "2 years",
                Description: "Work description",
                Skills: skills,
                IsFirstRecord: true);

            Assert.Equal("Developer", u.Position);
            Assert.Equal("2 years", u.Duration);
            Assert.Equal("Work description", u.Description);
            Assert.Single(u.Skills!);
            Assert.True(u.IsFirstRecord);
        }

        [Fact]
        public void UserExperienceRecord_ShouldSupportEquality()
        {
            var company = new CompanyRecord(CompanyCode: "corp", CompanyUrl: "https://example.com");
            var u1 = new UserExperienceRecord("link", company, Position: "Dev");
            var u2 = new UserExperienceRecord("link", company, Position: "Dev");
            var u3 = new UserExperienceRecord("link", company, Position: "Senior");

            Assert.Equal(u1, u2);
            Assert.NotEqual(u1, u3);
        }

        #endregion

        #region UserUniversityRecord

        [Fact]
        public void UserUniversityRecord_ShouldCreateWithDefaults()
        {
            var u = new UserUniversityRecord(
                UserLink: "https://habr.com/users/test/",
                University: new UniversityRecord(HabrId: 1, Name: "MIT"));

            Assert.Equal("https://habr.com/users/test/", u.UserLink);
            Assert.Null(u.Courses);
            Assert.Null(u.Description);
        }

        [Fact]
        public void UserUniversityRecord_ShouldStoreValues()
        {
            var courses = new List<CourseData>
            {
                new CourseData { Name = "CS101", StartDate = "2020-09-01", EndDate = "2021-05-01" }
            };

            var u = new UserUniversityRecord(
                UserLink: "https://habr.com/users/test/",
                University: new UniversityRecord(HabrId: 1, Name: "MIT"),
                Courses: courses,
                Description: "Bachelor degree");

            Assert.Single(u.Courses!);
            Assert.Equal("CS101", u.Courses[0].Name);
            Assert.Equal("Bachelor degree", u.Description);
        }

        #endregion

        #region AdditionalEducationRecord

        [Fact]
        public void AdditionalEducationRecord_ShouldCreateWithDefaults()
        {
            var a = new AdditionalEducationRecord(
                UserLink: "https://habr.com/users/test/",
                Title: "Course");

            Assert.Equal("https://habr.com/users/test/", a.UserLink);
            Assert.Equal("Course", a.Title);
            Assert.Null(a.Course);
            Assert.Null(a.Duration);
        }

        [Fact]
        public void AdditionalEducationRecord_ShouldStoreValues()
        {
            var a = new AdditionalEducationRecord(
                UserLink: "https://habr.com/users/test/",
                Title: "Machine Learning",
                Course: "ML-101",
                Duration: "6 months");

            Assert.Equal("Machine Learning", a.Title);
            Assert.Equal("ML-101", a.Course);
            Assert.Equal("6 months", a.Duration);
        }

        #endregion

        #region CompanyReviewRecord

        [Fact]
        public void CompanyReviewRecord_ShouldStoreValues()
        {
            var r = new CompanyReviewRecord("corp", "hash123", "Great company!");
            Assert.Equal("corp", r.CompanyCode);
            Assert.Equal("hash123", r.ReviewHash);
            Assert.Equal("Great company!", r.ReviewText);
        }

        [Fact]
        public void CompanyReviewRecord_ShouldSupportEquality()
        {
            var r1 = new CompanyReviewRecord("corp", "hash1", "Text");
            var r2 = new CompanyReviewRecord("corp", "hash1", "Text");
            var r3 = new CompanyReviewRecord("corp", "hash2", "Other text");

            Assert.Equal(r1, r2);
            Assert.NotEqual(r1, r3);
        }

        #endregion

        #region CommunityParticipationData

        [Fact]
        public void CommunityParticipationData_ShouldCreateWithDefaults()
        {
            var c = new CommunityParticipationData();
            Assert.Equal(string.Empty, c.Name);
            Assert.Null(c.MemberSince);
            Assert.Null(c.Contribution);
            Assert.Null(c.Topics);
        }

        [Fact]
        public void CommunityParticipationData_ShouldStoreValues()
        {
            var c = new CommunityParticipationData
            {
                Name = "Community1",
                MemberSince = "2020-01",
                Contribution = "Articles",
                Topics = "Programming"
            };

            Assert.Equal("Community1", c.Name);
            Assert.Equal("2020-01", c.MemberSince);
            Assert.Equal("Articles", c.Contribution);
            Assert.Equal("Programming", c.Topics);
        }

        [Fact]
        public void CommunityParticipationData_ShouldHaveJsonPropertyNames()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                new CommunityParticipationData { Name = "Test", MemberSince = "2020" });

            Assert.Contains("\"name\"", json);
            Assert.Contains("\"member_since\"", json);
            Assert.Contains("\"contribution\"", json);
            Assert.Contains("\"topics\"", json);
            Assert.DoesNotContain("\"Name\"", json);
            Assert.DoesNotContain("\"MemberSince\"", json);
        }

        [Fact]
        public void CommunityParticipationData_ShouldDeserializeFromJson()
        {
            var json = @"{""name"":""TestName"",""member_since"":""2020""}";
            var obj = System.Text.Json.JsonSerializer.Deserialize<CommunityParticipationData>(json);

            Assert.NotNull(obj);
            Assert.Equal("TestName", obj!.Name);
            Assert.Equal("2020", obj.MemberSince);
        }

        #endregion

        #region DbRecord

        [Fact]
        public void DbRecord_ShouldCreateWithResumeType()
        {
            var resume = new ResumeRecord(Link: "https://habr.com/users/test/", Title: "Test");
            var record = new DbRecord(Type: DbRecordType.Resume, Resume: resume);

            Assert.Equal(DbRecordType.Resume, record.Type);
            Assert.NotNull(record.Resume);
            Assert.Equal("https://habr.com/users/test/", record.Resume.Value.Link);
        }

        [Fact]
        public void DbRecord_ShouldCreateWithCompanyType()
        {
            var company = new CompanyRecord(CompanyCode: "corp", CompanyUrl: "https://example.com");
            var record = new DbRecord(Type: DbRecordType.Company, Company: company);

            Assert.Equal(DbRecordType.Company, record.Type);
            Assert.NotNull(record.Company);
            Assert.Equal("corp", record.Company.Value.CompanyCode);
        }

        [Fact]
        public void DbRecord_ShouldCreateWithCategoryRootIdType()
        {
            var category = new CategoryRootIdRecord("1", "Programming");
            var record = new DbRecord(Type: DbRecordType.CategoryRootId, CategoryRootId: category);

            Assert.Equal(DbRecordType.CategoryRootId, record.Type);
            Assert.NotNull(record.CategoryRootId);
            Assert.Equal("1", record.CategoryRootId.Value.CategoryId);
        }

        [Fact]
        public void DbRecord_ShouldCreateWithSkillsType()
        {
            var skills = new SkillsRecord(SkillId: 42, SkillTitle: "C#");
            var record = new DbRecord(Type: DbRecordType.Skills, Skills: skills);

            Assert.Equal(DbRecordType.Skills, record.Type);
            Assert.NotNull(record.Skills);
            Assert.Equal(42, record.Skills.Value.SkillId);
        }

        [Fact]
        public void DbRecord_ShouldCreateWithUserExperienceType()
        {
            var experience = new UserExperienceRecord(
                UserLink: "https://habr.com/users/test/",
                Company: new CompanyRecord(CompanyCode: "corp", CompanyUrl: "https://example.com"));
            var record = new DbRecord(Type: DbRecordType.UserExperience, UserExperience: experience);

            Assert.Equal(DbRecordType.UserExperience, record.Type);
            Assert.NotNull(record.UserExperience);
        }

        [Fact]
        public void DbRecord_ShouldCreateWithUniversityType()
        {
            var university = new UniversityRecord(HabrId: 1, Name: "MIT");
            var record = new DbRecord(Type: DbRecordType.University, University: university);

            Assert.Equal(DbRecordType.University, record.Type);
            Assert.NotNull(record.University);
            Assert.Equal("MIT", record.University.Value.Name);
        }

        [Fact]
        public void DbRecord_ShouldSupportEquality()
        {
            var resume1 = new ResumeRecord(Link: "https://habr.com/users/test/", Title: "Test");
            var resume2 = new ResumeRecord(Link: "https://habr.com/users/test/", Title: "Test");

            var r1 = new DbRecord(Type: DbRecordType.Resume, Resume: resume1);
            var r2 = new DbRecord(Type: DbRecordType.Resume, Resume: resume2);

            Assert.Equal(r1, r2);
        }

        #endregion
    }
}