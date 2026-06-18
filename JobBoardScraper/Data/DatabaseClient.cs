using System;
using System.Data;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using JobBoardScraper.Domain.Models;
using JobBoardScraper.Scrapers;
using JobBoardScraper.Infrastructure.Logging;

namespace JobBoardScraper.Data;

public enum DbRecordType
{
    Resume,
    Company,
    CategoryRootId,
    Skills,
    UserExperience,
    University,
    AdditionalEducation
}

public enum InsertMode
{
    /// <summary>
    /// Пропустить вставку, если запись уже существует
    /// </summary>
    SkipIfExists,

    /// <summary>
    /// Обновить запись, если она уже существует (UPSERT)
    /// </summary>
    UpdateIfExists
}

/// <summary>
/// Data structure for Resume record type.
/// </summary>
public readonly record struct ResumeRecord(
    InsertMode Mode = InsertMode.SkipIfExists,
    string Link = "",
    string? Title = null,
    string? Slogan = null,
    string? Code = null,
    bool? Expert = null,
    string? WorkExperience = null,
    string? UserCode = null,
    string? UserName = null,
    bool? IsExpert = null,
    string? LevelTitle = null,
    string? InfoTech = null,
    int? Salary = null,
    string? LastVisit = null,
    string? Age = null,
    string? Registration = null,
    string? Citizenship = null,
    bool? RemoteWork = null,
    bool? IsPublic = null,
    string? JobSearchStatus = null,
    bool? IsEmpty = null,
    List<SkillsRecord>? Skills = null,
    List<CommunityParticipationRecord>? CommunityParticipation = null,
    List<UserUniversityRecord>? UserUniversities = null,
    bool? IsDeleted = null,
    string? About = null);

/// <summary>
/// Data structure for Company record type.
/// </summary>
public readonly record struct CompanyRecord(
    string CompanyCode,
    string CompanyUrl,
    string? CompanyTitle = null,
    long? CompanyId = null,
    string? About = null,
    string? Description = null,
    string? Site = null,
    decimal? Rating = null,
    int? CurrentEmployees = null,
    int? PastEmployees = null,
    int? Followers = null,
    int? WantWork = null,
    string? EmployeesCount = null,
    bool? Habr = null,
    string? City = null,
    List<string>? Awards = null,
    decimal? Scores = null,
    List<string>? ReviewTexts = null,
    List<SkillsRecord>? Skills = null);

/// <summary>
/// Data structure for CategoryRootId record type.
/// </summary>
public readonly record struct CategoryRootIdRecord(
    string CategoryId,
    string CategoryName);

/// <summary>
/// Data structure for Skills record type.
/// </summary>
public readonly record struct SkillsRecord(
   int? SkillId = null,
   string? SkillTitle = null);

/// <summary>
/// Data structure for CommunityParticipation record type.
/// </summary>
public readonly record struct CommunityParticipationRecord(
   string Name = "",
   string? MemberSince = null,
   string? Contribution = null,
   string? Topics = null);

/// <summary>
/// Data structure for UserExperience record type.
/// </summary>
public readonly record struct UserExperienceRecord(
    string UserLink,
    string? CompanyCode = null,
    string? CompanyUrl = null,
    string? CompanyTitle = null,
    string? CompanyAbout = null,
    string? CompanySize = null,
    string? Position = null,
    string? Duration = null,
    string? Description = null,
    List<SkillsRecord>? Skills = null,
    bool IsFirstRecord = false);

/// <summary>
/// University record type.
/// </summary>
public readonly record struct UniversityRecord(
    int HabrId,
    string Name,
    string? City = null,
    int? GraduateCount = null);

/// <summary>
/// User-university relation record type.
/// </summary>
public readonly record struct UserUniversityRecord(
    string UserLink,
    int UniversityHabrId,
    List<CourseData>? Courses = null,
    string? Description = null);

/// <summary>
/// Additional education record type.
/// </summary>
public readonly record struct AdditionalEducationRecord(
    string UserLink,
    string Title,
    string? Course = null,
    string? Duration = null);

/// <summary>
/// Record structure for database queue operations with specific fields for each record type.
/// </summary>
public readonly record struct DbRecord(
    DbRecordType Type,
    InsertMode Mode = InsertMode.SkipIfExists,
    // Structured data fields - one for each record type
    ResumeRecord? Resume = null,
    CompanyRecord? Company = null,
    CategoryRootIdRecord? CategoryRootId = null,
    SkillsRecord? Skills = null,
    UserExperienceRecord? UserExperience = null,
    UniversityRecord? University = null,
    AdditionalEducationRecord? AdditionalEducation = null);

public sealed class DatabaseClient
{
    private readonly string _connectionString;
    private Task? _dbWriterTask;
    private CancellationTokenSource? _writerCts;
    private ConcurrentQueue<DbRecord>? _saveQueue;
    private readonly ConsoleLogger? _logger;
    private readonly DatabaseStatistics _statistics = new();
    private DateTime _lastStatsDump = DateTime.Now;
    private readonly TimeSpan _statsDumpInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Статистика операций с БД
    /// </summary>
    public DatabaseStatistics Statistics => _statistics;

    public DatabaseClient(string connectionString, ConsoleLogger? logger = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
        _statistics.InitializeAllTables();
    }

    /// <summary>
    /// Периодически выводит статистику в лог (раз в 5 минут)
    /// </summary>
    private void TryDumpStatistics()
    {
        if (DateTime.Now - _lastStatsDump >= _statsDumpInterval)
        {
            Log(_statistics.GetSummary());
            _lastStatsDump = DateTime.Now;
        }
    }

    private void Log(string message)
    {
        if (_logger != null)
        {
            _logger.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }


    #region Connection Management Methods

    // Создание соединения
    //"Server=localhost:5432;User Id=postgres; Password=admin;Database=jobs;"
    public NpgsqlConnection ConnectionInit()
    {
        NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        return conn;
    }

    // Гарантирует, что соединение открыто
    public void EnsureConnectionOpen(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (conn.State != ConnectionState.Open)
            conn.Open();
    }

    // Корректное закрытие соединения
    public void ConnectionClose(NpgsqlConnection conn)
    {
        if (conn is null) return;
        if (conn.State != ConnectionState.Closed)
            conn.Close();
    }

    #endregion

    #region Queue Management Methods

    /// <summary>
    /// Запустить фоновую задачу по записи данных в базу данных с использованием внутренней очереди
    /// </summary>
    /// <param name="conn">Открытое соединение с базой данных</param>
    /// <param name="token">Токен отмены операции</param>
    /// <param name="delayMs">Задержка между циклами проверки очереди в миллисекундах</param>
    public void StartWriterTask(NpgsqlConnection conn, CancellationToken token, int delayMs = 500)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        // Создаем новую очередь
        _saveQueue = new ConcurrentQueue<DbRecord>();

        // Создаем внутренний токен отмены
        _writerCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var linkedToken = _writerCts.Token;

        _dbWriterTask = Task.Run(async () =>
        {
            Log("[DB Writer] Фоновая задача записи в БД запущена");
            var lastQueueSizeLog = DateTime.MinValue;
            try
            {
                while (!linkedToken.IsCancellationRequested)
                {
                    var queueSize = _saveQueue.Count;

                    // Логируем размер очереди каждые 30 секунд
                    if ((DateTime.Now - lastQueueSizeLog).TotalSeconds >= 30)
                    {
                        Log($"[DB Writer] Размер очереди: {queueSize}");
                        lastQueueSizeLog = DateTime.Now;
                    }

                    while (_saveQueue.TryDequeue(out var record))
                    {
                        try
                        {
                            switch (record.Type)
                            {
                                case DbRecordType.Resume:
                                    if (record.Resume.HasValue)
                                    {
                                        var resume = record.Resume.Value;
                                        
                                        // Получаем или создаём level_id если есть данные профиля
                                        int? levelId = LevelsInsert(conn, resume.LevelTitle);

                                        // Объединенная вставка/обновление всех полей
                                        ResumesInsert(conn,
                                            link: resume.Link,
                                            title: resume.UserName ?? resume.Title,
                                            slogan: resume.Slogan,
                                            code: !string.IsNullOrWhiteSpace(resume.UserCode)
                                                ? resume.UserCode
                                                : resume.Code,
                                            expert: resume.IsExpert ?? resume.Expert,
                                            workExperience: resume.WorkExperience,
                                            mode: resume.Mode,
                                            levelId: levelId,
                                            infoTech: resume.InfoTech,
                                            salary: resume.Salary,
                                            lastVisit: resume.LastVisit,
                                            age: resume.Age,
                                            registration: resume.Registration,
                                            citizenship: resume.Citizenship,
                                            remoteWork: resume.RemoteWork,
                                            isPublic: resume.IsPublic,
                                            jobSearchStatus: resume.JobSearchStatus,
                                            isEmpty: resume.IsEmpty,
                                            isDeleted: resume.IsDeleted,
                                            about: resume.About,
                                            communityParticipation: resume.CommunityParticipation
                                        );

                                        // Если есть навыки, добавляем их
                                        if (resume.Skills != null && resume.Skills.Count > 0)
                                        {
                                            UserSkillsInsert(conn, userLink: resume.Link, skills: resume.Skills);
                                        }

                                        // Если есть связи пользователь-университет, добавляем их через ResumeRecord
                                        if (resume.UserUniversities != null && resume.UserUniversities.Count > 0)
                                        {
                                            ResumesUniversitiesInsert(conn, resume.UserUniversities);
                                        }
                                    }
                                    break;
                                case DbRecordType.Company:
                                    if (record.Company.HasValue)
                                    {
                                        var company = record.Company.Value;
                                        CompaniesInsert(
                                            conn,
                                            companyCode: company.CompanyCode,
                                            companyUrl: company.CompanyUrl,
                                            companyTitle: company.CompanyTitle,
                                            companyId: company.CompanyId,
                                            companyAbout: company.About,
                                            companyDescription: company.Description,
                                            companySite: company.Site,
                                            companyRating: company.Rating,
                                            currentEmployees: company.CurrentEmployees,
                                            pastEmployees: company.PastEmployees,
                                            followers: company.Followers,
                                            wantWork: company.WantWork,
                                            employeesCount: company.EmployeesCount,
                                            habr: company.Habr,
                                            city: company.City,
                                            awards: company.Awards,
                                            scores: company.Scores
                                        );

                                        // Если есть отзывы, добавляем их
                                        if (company.ReviewTexts != null && company.ReviewTexts.Count > 0)
                                        {
                                            var companyInternalId = CompaniesGetInternalId(conn, company.CompanyCode);
                                            if (companyInternalId.HasValue)
                                            {
                                                CompanyReviewsInsert(conn, companyInternalId.Value, company.ReviewTexts);
                                            }
                                            else
                                            {
                                                Log($"[DB] Отзывы для компании {company.CompanyCode}: SKIP (компания не найдена в БД)");
                                            }
                                        }

                                        // Если есть навыки, добавляем их
                                        if (company.Skills != null && company.Skills.Count > 0)
                                        {
                                            CompanySkillsInsert(conn, companyCode: company.CompanyCode, skills: company.Skills);
                                        }
                                    }
                                    break;
                                case DbRecordType.CategoryRootId:
                                    if (record.CategoryRootId.HasValue)
                                    {
                                        var category = record.CategoryRootId.Value;
                                        CategoryRootIdsInsert(conn, categoryId: category.CategoryId, categoryName: category.CategoryName);
                                    }
                                    break;
                                case DbRecordType.Skills:
                                    if (record.Skills.HasValue)
                                    {
                                        var skills = record.Skills.Value;
                                        if (skills.SkillId.HasValue)
                                        {
                                            SkillsInsert(conn, skills.SkillId.Value, skills.SkillTitle ?? "");
                                        }
                                    }
                                    break;
                                case DbRecordType.UserExperience:
                                    if (record.UserExperience.HasValue)
                                    {
                                        UserExperienceInsert(conn, record.UserExperience.Value);
                                    }
                                    break;
                                case DbRecordType.University:
                                    if (record.University.HasValue)
                                    {
                                        UniversitiesInsert(conn, record.University.Value);
                                    }
                                    break;
                                case DbRecordType.AdditionalEducation:
                                    if (record.AdditionalEducation.HasValue)
                                    {
                                        ResumesEducationsInsert(conn, record.AdditionalEducation.Value);
                                    }
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"[DB Writer] Ошибка при обработке записи типа {record.Type}: {ex.Message}");
                            Log($"[DB Writer] Stack trace: {ex.StackTrace}");
                            // Продолжаем обработку следующих записей
                        }
                    }

                    await Task.Delay(delayMs, linkedToken);
                }
            }
            catch (OperationCanceledException)
            {
                Log("[DB Writer] Фоновая задача записи в БД остановлена по запросу");
            }
            catch (Exception ex)
            {
                Log($"[DB Writer] Критическая ошибка в фоновой задаче: {ex.Message}");
                Log($"[DB Writer] Stack trace: {ex.StackTrace}");
            }
            finally
            {
                Log("[DB Writer] Фоновая задача записи в БД завершена");
            }
        }, linkedToken);
    }

    /// <summary>
    /// Остановить задачу записи в базу данных
    /// </summary>
    /// <returns>Task, завершающийся после полной остановки записывающей задачи</returns>
    public async Task StopWriterTask()
    {
        if (_writerCts != null)
        {
            // Отмечаем, что нужно завершить работу
            if (!_writerCts.IsCancellationRequested)
                _writerCts.Cancel();

            // Дожидаемся завершения задачи, если она была запущена
            if (_dbWriterTask != null)
            {
                try
                {
                    await _dbWriterTask;
                }
                catch (OperationCanceledException)
                {
                    // Нормальное завершение при отмене
                }
                catch (Exception ex)
                {
                    Log($"Ошибка при остановке задачи записи в БД: {ex.Message}");
                }
                finally
                {
                    _dbWriterTask = null;
                }
            }

            // Очищаем ресурсы
            _writerCts.Dispose();
            _writerCts = null;
        }
    }

    /// <summary>
    /// Проверить состояние фоновой задачи записи в БД
    /// </summary>
    public bool IsWriterTaskRunning()
    {
        if (_dbWriterTask == null) return false;

        var isRunning = !_dbWriterTask.IsCompleted && !_dbWriterTask.IsCanceled && !_dbWriterTask.IsFaulted;

        if (_dbWriterTask.IsFaulted)
        {
            Log($"[DB Writer] Задача завершилась с ошибкой: {_dbWriterTask.Exception?.Message}");
        }

        return isRunning;
    }

    /// <summary>
    /// Получить размер очереди записи в БД
    /// </summary>
    public int GetQueueSize()
    {
        return _saveQueue?.Count ?? 0;
    }

    /// <summary>
    /// Добавить резюме в очередь на запись в базу данных
    /// </summary>
    public bool EnqueueResume(
        string link,
        string title,
        string? slogan = null,
        InsertMode mode = InsertMode.SkipIfExists,
        string? code = null,
        bool? expert = null,
        string? workExperience = null,
        string? userCode = null,
        string? userName = null,
        bool? isExpert = null,
        string? levelTitle = null,
        string? infoTech = null,
        int? salary = null,
        string? lastVisit = null,
        bool? isPublic = null,
        string? age = null,
        string? registration = null,
        string? citizenship = null,
        bool? remoteWork = null,
        string? jobSearchStatus = null,
        bool? isEmpty = null,
        List<SkillsRecord>? skills = null,
        List<CommunityParticipationRecord>? communityParticipation = null,
        List<UserUniversityRecord>? userUniversities = null,
        string? about = null,
        bool? isDeleted = null)
    {
        if (_saveQueue == null) return false;
        if (string.IsNullOrWhiteSpace(link)) return false;

        var resumeRecord = new ResumeRecord(
            Link: link,
            Title: title,
            Slogan: slogan,
            Code: code,
            Expert: expert,
            WorkExperience: workExperience,
            UserCode: userCode,
            UserName: userName,
            IsExpert: isExpert,
            LevelTitle: levelTitle,
            InfoTech: infoTech,
            Salary: salary,
            LastVisit: lastVisit,
            IsPublic: isPublic,
            Age: age,
            Registration: registration,
            Citizenship: citizenship,
            RemoteWork: remoteWork,
            JobSearchStatus: jobSearchStatus,
            IsEmpty: isEmpty,
            Skills: skills,
            CommunityParticipation: communityParticipation,
            UserUniversities: userUniversities,
            About: about,
            IsDeleted: isDeleted,
            Mode: mode
        );

        var record = new DbRecord(
            Type: DbRecordType.Resume,
            Resume: resumeRecord
        );
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] Resume ({mode}): {title} -> {link}" +
                          (string.IsNullOrWhiteSpace(slogan) ? "" : $" | {slogan}") +
                          (expert == true || isExpert == true ? " | ЭКСПЕРТ" : "") +
                          (levelTitle != null ? $" | Level={levelTitle}" : "") +
                          (salary.HasValue ? $" | Salary={salary.Value}" : "") +
                          (isPublic.HasValue ? $" | Public={isPublic.Value}" : "") +
                          (!string.IsNullOrWhiteSpace(about) ? $" | About={about}" : "") +
                          (skills != null && skills.Count > 0 ? $" | Skills={skills.Count}" : ""));

        return true;
    }

    /// <summary>
    /// Добавить компанию в очередь на запись в базу данных
    /// </summary>
    public bool EnqueueCompany(
        string companyCode,
        string companyUrl,
        long? companyId = null,
        string? companyTitle = null,
        string? companyAbout = null,
        string? companyDescription = null,
        string? companySite = null,
        decimal? companyRating = null,
        int? currentEmployees = null,
        int? pastEmployees = null,
        int? followers = null,
        int? wantWork = null,
        string? employeesCount = null,
        bool? habr = null,
        string? city = null,
        List<string>? awards = null,
        decimal? scores = null,
        List<string>? reviewTexts = null,
        List<SkillsRecord>? skills = null)
    {
        if (_saveQueue == null) return false;

        var companyRecord = new CompanyRecord(
            CompanyCode: companyCode,
            CompanyUrl: companyUrl,
            CompanyTitle: companyTitle,
            CompanyId: companyId,
            About: companyAbout,
            Description: companyDescription,
            Site: companySite,
            Rating: companyRating,
            CurrentEmployees: currentEmployees,
            PastEmployees: pastEmployees,
            Followers: followers,
            WantWork: wantWork,
            EmployeesCount: employeesCount,
            Habr: habr,
            City: city,
            Awards: awards,
            Scores: scores,
            ReviewTexts: reviewTexts,
            Skills: skills
        );

        var record = new DbRecord(
            Type: DbRecordType.Company,
            Company: companyRecord
        );
        _saveQueue.Enqueue(record);

        var logMessage = $"[DB Queue] Company: {companyCode} -> {companyUrl}";
        if (companyId.HasValue)
            logMessage += $" (ID: {companyId})";
        if (companyTitle != null)
            logMessage += $" | {companyTitle}";
        if (skills != null && skills.Count > 0)
            logMessage += $" | Skills: {skills.Count}";
        Log(logMessage);

        return true;
    }

    /// <summary>
    /// Добавить category_root_id в очередь на запись в базу данных
    /// </summary>
    public bool EnqueueCategoryRootId(string categoryId, string categoryName)
    {
        if (_saveQueue == null) return false;

        var categoryRecord = new CategoryRootIdRecord(
            CategoryId: categoryId,
            CategoryName: categoryName
        );

        var record = new DbRecord(
            Type: DbRecordType.CategoryRootId,
            CategoryRootId: categoryRecord
        );
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] CategoryRootId: {categoryId} -> {categoryName}");

        return true;
    }
    
    /// <summary>
    /// Добавить опыт работы пользователя в очередь
    /// </summary>
    public bool EnqueueUserExperience(
        string userLink,
        string? companyCode = null,
        string? companyUrl = null,
        string? companyTitle = null,
        string? companyAbout = null,
        string? companySize = null,
        string? position = null,
        string? duration = null,
        string? description = null,
        List<SkillsRecord>? skills = null,
        bool isFirstRecord = false)
    {
        if (_saveQueue == null) return false;

        var experienceRecord = new UserExperienceRecord(
            UserLink: userLink,
            CompanyCode: companyCode,
            CompanyUrl: companyUrl,
            CompanyTitle: companyTitle,
            CompanyAbout: companyAbout,
            CompanySize: companySize,
            Position: position,
            Duration: duration,
            Description: description,
            Skills: skills,
            IsFirstRecord: isFirstRecord
        );

        var record = new DbRecord(
            Type: DbRecordType.UserExperience,
            UserExperience: experienceRecord
        );
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] UserExperience: {userLink} -> Company={companyCode}, Position={position}, Skills={skills?.Count ?? 0}");

        return true;
    }

    /// <summary>
    /// Добавить навык в очередь (с skill_id из URL)
    /// </summary>
    public bool EnqueueSkill(int skillId, string title)
    {
        if (_saveQueue == null) return false;

        var skillRecord = new SkillsRecord(
            SkillId: skillId,
            SkillTitle: title
        );

        var record = new DbRecord(
            Type: DbRecordType.Skills,
            Skills: skillRecord
        );
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] Skill: ID={skillId}, Title={title}");

        return true;
    }

    /// <summary>
    /// Добавить университет в основную очередь на сохранение
    /// </summary>
    public void EnqueueUniversity(UniversityData data)
    {
        if (_saveQueue == null) return;

        var record = new DbRecord(
            Type: DbRecordType.University,
            University: new UniversityRecord(
                HabrId: data.HabrId,
                Name: data.Name,
                City: data.City,
                GraduateCount: data.GraduateCount)
        );
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] University: {data.Name}");
    }

    /// <summary>
    /// Добавить дополнительное образование в основную очередь на сохранение
    /// </summary>
    public void EnqueueAdditionalEducation(AdditionalEducationRecord data)
    {
        if (_saveQueue == null) return;

        var record = new DbRecord(
            Type: DbRecordType.AdditionalEducation,
            AdditionalEducation: data
        );
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] AdditionalEducation: {data.UserLink} -> {data.Title}");
    }

    #endregion

    #region Database Table Operations Methods

    /// <summary>
    /// Получает или создаёт идентификатор уровня (habr_levels) по названию.
    /// Возвращает null, если title пустой или не задан.
    /// </summary>
    private int? LevelsInsert(NpgsqlConnection conn, string? levelTitle)
    {
        if (string.IsNullOrWhiteSpace(levelTitle))
            return null;

        EnsureConnectionOpen(conn);
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO habr_levels (title, created_at, updated_at)
            VALUES (@title, NOW(), NOW())
            ON CONFLICT (title) DO UPDATE SET title = EXCLUDED.title, updated_at = NOW()
            RETURNING id", conn);
        cmd.Parameters.AddWithValue("@title", levelTitle);
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : null;
    }

    // Проверка существования записи по полю link
    public bool ResumesRecordExistsByLink(NpgsqlConnection conn, string link)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(link)) throw new ArgumentException("Link must not be empty.", nameof(link));

        EnsureConnectionOpen(conn);
        using var cmd = new NpgsqlCommand("SELECT 1 FROM habr_resumes WHERE link = @link LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@link", link);
        var result = cmd.ExecuteScalar();
        return result is not null;
    }

    // Вставка ссылки, заголовка страницы, слогана и дополнительных полей в таблицу resumes
    public void ResumesInsert(
        NpgsqlConnection conn,
        string link,
        string? title = null,
        string? slogan = null,
        string? code = null,
        bool? expert = null,
        string? workExperience = null,
        InsertMode mode = InsertMode.SkipIfExists,
        int? levelId = null,
        string? infoTech = null,
        int? salary = null,
        string? lastVisit = null,
        string? age = null,
        string? registration = null,
        string? citizenship = null,
        bool? remoteWork = null,
        bool? isPublic = null,
        string? jobSearchStatus = null,
        bool? isEmpty = null,
        bool? isDeleted = null,
        string? about = null,
        List<CommunityParticipationRecord>? communityParticipation = null)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(link)) throw new ArgumentException("Link must not be empty.", nameof(link));

        try
        {
           EnsureConnectionOpen(conn);

           string? communityParticipationJson = communityParticipation is { Count: > 0 }
               ? SerializeCommunityParticipation(communityParticipation)
               : null;

           if (mode == InsertMode.SkipIfExists)
            {
                // Проверка существования по link
                if (ResumesRecordExistsByLink(conn, link))
                {
                    Log($"[DB] Resume {link}: ? SKIP (уже существует)");
                    _statistics.RecordSkipped("habr_resumes", link);
                    return;
                }

                if (title != null && title.Contains("Ошибка 404"))
                {
                    Log($"[DB] Resume {link}: ? SKIP (404 страница)");
                    _statistics.RecordSkipped("habr_resumes", link);
                    return;
                }



                using var cmd = new NpgsqlCommand(
                    "INSERT INTO habr_resumes (link, title, slogan, code, expert, work_experience, level_id, info_tech, salary, last_visit, age, registration, citizenship, remote_work, public, job_search_status, is_empty, is_deleted, about, community_participation, created_at, updated_at) VALUES (@link, @title, @slogan, @code, @expert, @work_experience, @level_id, @info_tech, @salary, @last_visit, @age, @registration, @citizenship, @remote_work, @public, @job_search_status, @is_empty, @is_deleted, @about, @community_participation, NOW(), NOW())",
                    conn);
                cmd.Parameters.AddWithValue("@link", link);
                cmd.Parameters.AddWithValue("@title", title ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@slogan", slogan ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@code", code ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@expert", expert ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@work_experience", workExperience ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@level_id", levelId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@info_tech", infoTech ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@salary", salary ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@last_visit", lastVisit ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@age", age ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@registration", registration ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@citizenship", citizenship ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@remote_work", remoteWork ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@public", isPublic ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@job_search_status", jobSearchStatus ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@is_empty", isEmpty ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@is_deleted", isDeleted ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@about", about ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@community_participation", communityParticipationJson ?? (object)DBNull.Value);

                cmd.ExecuteNonQuery();
                _statistics.RecordInsert("habr_resumes", link);

                // Подробное логирование
                var logParts = new List<string> { $"[DB] Resume {link}:" };

                if (title != null)
                    logParts.Add($"Title={title}");

                if (!string.IsNullOrWhiteSpace(slogan))
                    logParts.Add($"Slogan={slogan}");

                if (code != null)
                    logParts.Add($"Code={code}");

                if (expert == true)
                    logParts.Add("Expert=?");

                if (workExperience != null)
                    logParts.Add($"Experience={workExperience}");

                if (levelId.HasValue)
                    logParts.Add($"LevelID={levelId.Value}");

                if (infoTech != null)
                    logParts.Add($"InfoTech={infoTech}");

                if (salary.HasValue)
                    logParts.Add($"Salary={salary.Value}");

                if (lastVisit != null)
                    logParts.Add($"LastVisit={lastVisit}");

                if (age != null)
                    logParts.Add($"Age={age}");

                if (registration != null)
                    logParts.Add($"Registration={registration}");

                if (citizenship != null)
                    logParts.Add($"Citizenship={citizenship}");

                if (remoteWork.HasValue)
                    logParts.Add($"RemoteWork={remoteWork.Value}");

                if (communityParticipationJson != null)
                    logParts.Add($"CommunityParticipation={communityParticipationJson}");

                if (isPublic.HasValue)
                    logParts.Add($"Public={isPublic.Value}");

                if (jobSearchStatus != null)
                    logParts.Add($"JobStatus={jobSearchStatus}");

                logParts.Add("? INSERT");

                Log(string.Join(" | ", logParts));
                TryDumpStatistics();
            }
            else // UpdateIfExists
            {
                if (title != null && title.Contains("Ошибка 404"))
                {
                    Log($"[DB] Resume {link}: ? SKIP (404 страница)");
                    _statistics.RecordSkipped("habr_resumes", link);
                    return;
                }

                if (title != null && title.Contains("Профиль удален") && isDeleted == true)
                {
                    Log($"[DB] Resume {link}: ? Обработка удалённого профиля");
                }

                // Используем RETURNING xmax для определения INSERT (xmax=0) или UPDATE (xmax>0)
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO habr_resumes (link, title, slogan, code, expert, work_experience, level_id, info_tech, salary, last_visit, age, registration, citizenship, remote_work, public, job_search_status, is_empty, is_deleted, about, community_participation, created_at, updated_at)
                    VALUES (@link, @title, @slogan, @code, @expert, @work_experience, @level_id, @info_tech, @salary, @last_visit, @age, @registration, @citizenship, @remote_work, @public, @job_search_status, @is_empty, @is_deleted, @about, @community_participation, NOW(), NOW())
                    ON CONFLICT (link)
                    DO UPDATE SET
                        title = COALESCE(EXCLUDED.title, habr_resumes.title),
                        slogan = COALESCE(EXCLUDED.slogan, habr_resumes.slogan),
                        code = COALESCE(EXCLUDED.code, habr_resumes.code),
                        expert = COALESCE(EXCLUDED.expert, habr_resumes.expert),
                        work_experience = COALESCE(EXCLUDED.work_experience, habr_resumes.work_experience),
                        level_id = COALESCE(EXCLUDED.level_id, habr_resumes.level_id),
                        info_tech = COALESCE(EXCLUDED.info_tech, habr_resumes.info_tech),
                        salary = COALESCE(EXCLUDED.salary, habr_resumes.salary),
                        last_visit = COALESCE(EXCLUDED.last_visit, habr_resumes.last_visit),
                        age = COALESCE(EXCLUDED.age, habr_resumes.age),
                        registration = COALESCE(EXCLUDED.registration, habr_resumes.registration),
                        citizenship = COALESCE(EXCLUDED.citizenship, habr_resumes.citizenship),
                        remote_work = COALESCE(EXCLUDED.remote_work, habr_resumes.remote_work),
                        public = COALESCE(EXCLUDED.public, habr_resumes.public),
                        job_search_status = COALESCE(EXCLUDED.job_search_status, habr_resumes.job_search_status),
                        is_empty = COALESCE(EXCLUDED.is_empty, habr_resumes.is_empty),
                        is_deleted = COALESCE(EXCLUDED.is_deleted, habr_resumes.is_deleted),
                        about = COALESCE(EXCLUDED.about, habr_resumes.about),
                        community_participation = COALESCE(EXCLUDED.community_participation, habr_resumes.community_participation),
                        updated_at = NOW()
                    RETURNING xmax", conn);
                cmd.Parameters.AddWithValue("@link", link);
                cmd.Parameters.AddWithValue("@title", title ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@slogan", slogan ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@code", code ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@expert", expert ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@work_experience", workExperience ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@level_id", levelId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@info_tech", infoTech ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@salary", salary ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@last_visit", lastVisit ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@age", age ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@registration", registration ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@citizenship", citizenship ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@remote_work", remoteWork ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@public", isPublic ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@job_search_status", jobSearchStatus ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@is_empty", isEmpty ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@is_deleted", isDeleted ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@about", about ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@community_participation", communityParticipationJson ?? (object)DBNull.Value);

                var xmaxResult = cmd.ExecuteScalar();
                var xmax = Convert.ToUInt32(xmaxResult);
                var isInsert = xmax == 0;

                // Записываем статистику
                if (isInsert)
                    _statistics.RecordInsert("habr_resumes", link);
                else
                    _statistics.RecordUpdate("habr_resumes", link);

                // Подробное логирование
                var logParts = new List<string> { $"[DB] Resume {link}:" };

                if (title != null)
                    logParts.Add($"Title={title}");

                if (!string.IsNullOrWhiteSpace(slogan))
                    logParts.Add($"Slogan={slogan}");

                if (code != null)
                    logParts.Add($"Code={code}");

                if (expert == true)
                    logParts.Add("Expert=?");

                if (workExperience != null)
                    logParts.Add($"Experience={workExperience}");

                if (levelId.HasValue)
                    logParts.Add($"LevelID={levelId.Value}");

                if (infoTech != null)
                    logParts.Add($"InfoTech={infoTech}");

                if (salary.HasValue)
                    logParts.Add($"Salary={salary.Value}");

                if (lastVisit != null)
                    logParts.Add($"LastVisit={lastVisit}");

                if (age != null)
                    logParts.Add($"Age={age}");

                if (registration != null)
                    logParts.Add($"Registration={registration}");

                if (citizenship != null)
                    logParts.Add($"Citizenship={citizenship}");

                if (remoteWork.HasValue)
                    logParts.Add($"RemoteWork={remoteWork.Value}");

                if (communityParticipationJson != null)
                    logParts.Add($"CommunityParticipation={communityParticipationJson}");

                if (isPublic.HasValue)
                    logParts.Add($"Public={isPublic.Value}");

                logParts.Add(isInsert ? "? INSERT" : "? UPDATE");

                Log(string.Join(" | ", logParts));
                TryDumpStatistics();
            }
        }
        catch (PostgresException pgEx) when
            (pgEx.SqlState == "23505") // На случай гонки: уникальное ограничение нарушено
        {
            Log($"[DB] Resume {link}: ? SKIP (уникальное ограничение)");
            _statistics.RecordSkipped("habr_resumes", link);
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Resume {link}: ? ERROR - {dbEx.Message}");
            _statistics.RecordError("habr_resumes", link);
        }
        catch (Exception ex)
        {
            Log($"[DB] Resume {link}: ? ERROR - {ex.Message}");
            _statistics.RecordError("habr_resumes", link);
        }

    }

    // Получение последней ссылки из таблицы resumes.
    // Если linkLength не задан, используется прежний алгоритм:
    //   ORDER BY LENGTH(link) DESC, link DESC
    // Если linkLength задан ( > 0 ), выбирается среди ссылок указанной длины:
    //   WHERE LENGTH(link) = @len ORDER BY link DESC
    public string? ResumesGetLastLink(NpgsqlConnection conn, int? linkLength = null)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (linkLength is <= 0)
            throw new ArgumentOutOfRangeException(nameof(linkLength));

        try
        {
            EnsureConnectionOpen(conn);

            using var cmd = linkLength is null
                ? new NpgsqlCommand(
                    "SELECT link " +
                    "FROM habr_resumes " +
                    "ORDER BY id DESC " +
                    "LIMIT 1", conn)
                : new NpgsqlCommand(
                    "SELECT link " +
                    "FROM habr_resumes " +
                    "WHERE LENGTH(link) = @len " +
                    "ORDER BY id DESC " +
                    "LIMIT 1", conn);

            if (linkLength is not null)
                cmd.Parameters.AddWithValue("@len", linkLength.Value);

            var result = cmd.ExecuteScalar();

            return result?.ToString();
        }
        catch
        {
            return null;
        }
    }

    // Вставка компании в таблицу companies
    public int? CompaniesInsert(
        NpgsqlConnection conn,
        string companyCode,
        string? companyUrl = null,
        string? companyTitle = null,
        long? companyId = null,
        string? companyAbout = null,
        string? companyDescription = null,
        string? companySite = null,
        decimal? companyRating = null,
        int? currentEmployees = null,
        int? pastEmployees = null,
        int? followers = null,
        int? wantWork = null,
        string? employeesCount = null,
        bool? habr = null,
        string? city = null,
        List<string>? awards = null,
        decimal? scores = null)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(companyCode))
            throw new ArgumentException("Company code must not be empty.", nameof(companyCode));

        try
        {
            EnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                INSERT INTO habr_companies (code, url, company_id, title, about, description, site, rating,
                    current_employees, past_employees, followers, want_work, employees_count, habr,
                    city, awards, scores, created_at, updated_at)
                VALUES (@code, @url, @company_id, @title, @about, @description, @site, @rating,
                    @current_employees, @past_employees, @followers, @want_work, @employees_count, @habr,
                    @city, @awards, @scores, NOW(), NOW())
                ON CONFLICT (code)
                DO UPDATE SET
                    url = COALESCE(EXCLUDED.url, habr_companies.url),
                    company_id = COALESCE(EXCLUDED.company_id, habr_companies.company_id),
                    title = COALESCE(EXCLUDED.title, habr_companies.title),
                    about = COALESCE(EXCLUDED.about, habr_companies.about),
                    description = COALESCE(EXCLUDED.description, habr_companies.description),
                    site = COALESCE(EXCLUDED.site, habr_companies.site),
                    rating = COALESCE(EXCLUDED.rating, habr_companies.rating),
                    current_employees = COALESCE(EXCLUDED.current_employees, habr_companies.current_employees),
                    past_employees = COALESCE(EXCLUDED.past_employees, habr_companies.past_employees),
                    followers = COALESCE(EXCLUDED.followers, habr_companies.followers),
                    want_work = COALESCE(EXCLUDED.want_work, habr_companies.want_work),
                    employees_count = COALESCE(EXCLUDED.employees_count, habr_companies.employees_count),
                    habr = COALESCE(EXCLUDED.habr, habr_companies.habr),
                    city = COALESCE(EXCLUDED.city, habr_companies.city),
                    awards = COALESCE(EXCLUDED.awards, habr_companies.awards),
                    scores = COALESCE(EXCLUDED.scores, habr_companies.scores),
                    updated_at = NOW()
                RETURNING id, xmax", conn);

            cmd.Parameters.AddWithValue("@code", companyCode);
            cmd.Parameters.AddWithValue("@url", companyUrl ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@company_id", companyId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@title", companyTitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@about", companyAbout ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@description", companyDescription ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@site", companySite ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@rating", companyRating.HasValue ? (object)companyRating.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@current_employees",
                currentEmployees.HasValue ? (object)currentEmployees.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@past_employees",
                pastEmployees.HasValue ? (object)pastEmployees.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@followers", followers.HasValue ? (object)followers.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@want_work", wantWork.HasValue ? (object)wantWork.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@employees_count",
                !string.IsNullOrWhiteSpace(employeesCount) ? employeesCount : DBNull.Value);
            cmd.Parameters.AddWithValue("@habr", habr.HasValue ? (object)habr.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@city", city ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@awards", awards?.ToArray() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@scores", scores ?? (object)DBNull.Value);

            using var reader = cmd.ExecuteReader();
            reader.Read();
            var internalId = reader.GetInt32(0);
            var xmax = Convert.ToUInt32(reader.GetValue(1));
            reader.Close();

            var isInsert = xmax == 0;

            if (isInsert)
                _statistics.RecordInsert("habr_companies", companyCode);
            else
                _statistics.RecordUpdate("habr_companies", companyCode);

            // Подробное логирование
            var logParts = new List<string> { $"[DB] Компания {companyCode}:" };

            if (companyUrl != null)
                logParts.Add($"URL={companyUrl}");

            if (companyTitle != null)
                logParts.Add($"Title={companyTitle}");

            if (companyId.HasValue)
                logParts.Add($"CompanyID={companyId.Value}");

            if (companyAbout != null)
            {
                var aboutPreview = companyAbout.Length > 50 ? companyAbout.Substring(0, 50) + "..." : companyAbout;
                logParts.Add($"About={aboutPreview}");
            }

            if (companyDescription != null)
            {
                var descPreview = companyDescription.Length > 50 ? companyDescription.Substring(0, 50) + "..." : companyDescription;
                logParts.Add($"Description={descPreview}");
            }

            if (companySite != null)
                logParts.Add($"Site={companySite}");

            if (companyRating.HasValue)
                logParts.Add($"Rating={companyRating.Value:F2}");

            if (currentEmployees.HasValue || pastEmployees.HasValue)
                logParts.Add($"Employees={currentEmployees?.ToString() ?? "?"}/{pastEmployees?.ToString() ?? "?"}");

            if (followers.HasValue || wantWork.HasValue)
                logParts.Add($"Followers={followers?.ToString() ?? "?"}/{wantWork?.ToString() ?? "?"}");

            if (employeesCount != null)
                logParts.Add($"Size={employeesCount}");

            if (habr.HasValue)
                logParts.Add($"Habr={habr.Value}");

            if (city != null)
                logParts.Add($"City={city}");

            if (awards != null && awards.Count > 0)
                logParts.Add($"Awards={awards.Count}");

            if (scores.HasValue)
                logParts.Add($"Scores={scores.Value:F2}");

            logParts.Add(isInsert ? "? INSERT" : "? UPDATE");

            Log(string.Join(" | ", logParts));
            TryDumpStatistics();

            return internalId;
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Компания {companyCode}: ? ERROR - {dbEx.Message}");
            _statistics.RecordError("habr_companies", companyCode);
            return null;
        }
        catch (Exception ex)
        {
            Log($"[DB] Компания {companyCode}: ? ERROR - {ex.Message}");
            _statistics.RecordError("habr_companies", companyCode);
            return null;
        }
    }

    // Вставка category_root_id в таблицу category_root_ids
    public void CategoryRootIdsInsert(NpgsqlConnection conn, string categoryId, string categoryName)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(categoryId))
            throw new ArgumentException("Category ID must not be empty.", nameof(categoryId));

        try
        {
            EnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                INSERT INTO habr_category_root_ids (category_id, category_name, created_at, updated_at)
                VALUES (@id, @name, NOW(), NOW())
                ON CONFLICT (category_id)
                DO UPDATE SET
                    category_name = EXCLUDED.category_name,
                    updated_at = NOW()
                RETURNING xmax", conn);

            cmd.Parameters.AddWithValue("@id", categoryId);
            cmd.Parameters.AddWithValue("@name", categoryName ?? (object)DBNull.Value);

            var xmaxResult = cmd.ExecuteScalar();
            var xmax = Convert.ToUInt32(xmaxResult);
            var isInsert = xmax == 0;

            if (isInsert)
                _statistics.RecordInsert("habr_category_root_ids", categoryId);
            else
                _statistics.RecordUpdate("habr_category_root_ids", categoryId);

            Log($"[DB] Category {categoryId} -> {categoryName}: {(isInsert ? "? INSERT" : "? UPDATE")}");
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Category {categoryId}: ? ERROR - {dbEx.Message}");
            _statistics.RecordError("habr_category_root_ids", categoryId);
        }
        catch (Exception ex)
        {
            Log($"[DB] Category {categoryId}: ? ERROR - {ex.Message}");
            _statistics.RecordError("habr_category_root_ids", categoryId);
        }
    }

    /// <summary>
    /// Получить все company_id из таблицы habr_companies где company_id не NULL
    /// </summary>
    public List<long> CompaniesGetAllIds(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var companyIds = new List<long>();

        try
        {
            EnsureConnectionOpen(conn);
            using var cmd = new NpgsqlCommand("SELECT company_id FROM habr_companies WHERE company_id IS NOT NULL ORDER BY company_id", conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var companyId = reader.GetInt64(0);
                companyIds.Add(companyId);
            }

            Log($"[DB] Загружено {companyIds.Count} company_id из БД");
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при загрузке company_id: {ex.Message}");
        }

        return companyIds;
    }

    /// <summary>
    /// Получить все habr_id из таблицы habr_universities
    /// </summary>
    public List<int> UniversitiesGetAllIds(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var universityIds = new List<int>();

        try
        {
            EnsureConnectionOpen(conn);
            using var cmd = new NpgsqlCommand("SELECT habr_id FROM habr_universities ORDER BY habr_id", conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var universityId = reader.GetInt32(0);
                universityIds.Add(universityId);
            }

            Log($"[DB] Загружено {universityIds.Count} university_id из БД");
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при загрузке university_id: {ex.Message}");
        }

        return universityIds;
    }

    /// <summary>
    /// Получить все category_id из таблицы habr_category_root_ids
    /// </summary>
    public List<string> CategoryGetAllIds(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var categoryIds = new List<string>();

        try
        {
            EnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(
                "SELECT category_id FROM habr_category_root_ids ORDER BY category_id", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var categoryId = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(categoryId))
                {
                    categoryIds.Add(categoryId);
                }
            }

            Log($"[DB] Загружено {categoryIds.Count} категорий из БД");
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при получении категорий: {ex.Message}");
        }

        return categoryIds;
    }

    /// <summary>
    /// Получить все company_code из таблицы habr_companies
    /// </summary>
    public List<string> CompaniesGetAllCodes(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var companyCodes = new List<string>();

        try
        {
            EnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(
                "SELECT code FROM habr_companies ORDER BY code", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var companyCode = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(companyCode))
                {
                    companyCodes.Add(companyCode);
                }
            }

            Log($"[DB] Загружено {companyCodes.Count} компаний из БД");
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при получении компаний: {ex.Message}");
        }

        return companyCodes;
    }

    /// <summary>
    /// Получить все компании (code и url) из таблицы habr_companies
    /// </summary>
    public List<(string code, string url)> CompaniesGetAll(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var companies = new List<(string code, string url)>();

        try
        {
            EnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(
                "SELECT code, url FROM habr_companies ORDER BY code", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var code = reader.GetString(0);
                var url = reader.GetString(1);

                if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(url))
                {
                    companies.Add((code, url));
                }
            }

            Log($"[DB] Загружено {companies.Count} компаний с URL из БД");
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при получении компаний: {ex.Message}");
        }

        return companies;
    }

    /// <summary>
    /// Вставить или обновить навыки компании
    /// </summary>
    public void CompanySkillsInsert(NpgsqlConnection conn, string companyCode, List<string> skills)
    {
        CompanySkillsInsert(conn, companyCode, skills.Select(skill => new SkillsRecord(SkillTitle: skill)).ToList());
    }

    public void CompanySkillsInsert(NpgsqlConnection conn, string companyCode, List<SkillsRecord> skills)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(companyCode))
            throw new ArgumentException("Company code must not be empty.", nameof(companyCode));
        if (skills == null || skills.Count == 0) return;

        try
        {
            EnsureConnectionOpen(conn);

            // Получаем ID компании по коду
            int? companyId = null;
            using (var cmdGetCompany =
                   new NpgsqlCommand("SELECT id FROM habr_companies WHERE code = @code LIMIT 1", conn))
            {
                cmdGetCompany.Parameters.AddWithValue("@code", companyCode);
                var result = cmdGetCompany.ExecuteScalar();
                if (result != null)
                {
                    companyId = Convert.ToInt32(result);
                }
            }

            if (!companyId.HasValue)
            {
                Log($"[DB] Компания {companyCode} не найдена в БД. Пропуск навыков.");
                return;
            }

            // Удаляем старые связи навыков для этой компании
            using (var cmdDelete =
                   new NpgsqlCommand("DELETE FROM habr_company_skills WHERE company_id = @company_id", conn))
            {
                cmdDelete.Parameters.AddWithValue("@company_id", companyId.Value);
                cmdDelete.ExecuteNonQuery();
            }

            // Добавляем навыки
            int addedCount = 0;
            int newSkillsCount = 0;
            foreach (var skillRecord in skills)
            {
                var skillTitle = skillRecord.SkillTitle;
                if (string.IsNullOrWhiteSpace(skillTitle)) continue;

                // Вставляем навык в таблицу habr_skills (если его нет)
                int skillId;
                using (var cmdInsertSkill = new NpgsqlCommand(@"
                    INSERT INTO habr_skills (title, created_at, updated_at)
                    VALUES (@title, NOW(), NOW())
                    ON CONFLICT (title)
                    DO UPDATE SET title = EXCLUDED.title, updated_at = NOW()
                    RETURNING id", conn))
                {
                    cmdInsertSkill.Parameters.AddWithValue("@title", skillTitle.Trim());
                    var result = cmdInsertSkill.ExecuteScalar();
                    skillId = Convert.ToInt32(result);
                }

                // Связываем навык с компанией
                using (var cmdLinkSkill = new NpgsqlCommand(@"
                    INSERT INTO habr_company_skills (company_id, skill_id, created_at, updated_at)
                    VALUES (@company_id, @skill_id, NOW(), NOW())
                    ON CONFLICT (company_id, skill_id) DO UPDATE SET updated_at = NOW()", conn))
                {
                    cmdLinkSkill.Parameters.AddWithValue("@company_id", companyId.Value);
                    cmdLinkSkill.Parameters.AddWithValue("@skill_id", skillId);
                    cmdLinkSkill.ExecuteNonQuery();
                }

                addedCount++;
                newSkillsCount++;
            }

            if (addedCount > 0)
            {
                _statistics.RecordInsert("habr_company_skills", companyCode);
                _statistics.RecordInsert("habr_skills", $"{newSkillsCount} навыков для компании {companyCode}");
            }
            Log($"[DB] CompanySkills {companyCode}: ? {addedCount} навыков добавлено");
            TryDumpStatistics();
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] CompanySkills {companyCode}: ? ERROR - {dbEx.Message}");
            _statistics.RecordError("habr_company_skills", companyCode);
        }
        catch (Exception ex)
        {
            Log($"[DB] CompanySkills {companyCode}: ? ERROR - {ex.Message}");
            _statistics.RecordError("habr_company_skills", companyCode);
        }
    }

    /// <summary>
    /// Получить все коды пользователей из таблицы habr_resumes
    /// </summary>
    public List<string> ResumesGetAllUserCodes(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var userCodes = new List<string>();

        try
        {
            EnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(
                "SELECT code FROM habr_resumes WHERE code IS NOT NULL ORDER BY code", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var code = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(code))
                {
                    userCodes.Add(code);
                }
            }

            Log($"[DB] Загружено {userCodes.Count} кодов пользователей из БД");
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при получении кодов пользователей: {ex.Message}");
        }

        return userCodes;
    }

    /// <summary>
    /// Получить ссылки пользователей с опциональным фильтром по публичности
    /// </summary>
    public List<string> ResumesGetAllUserLinks(NpgsqlConnection conn, bool onlyPublic = false)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var userLinks = new List<string>();

        try
        {
            EnsureConnectionOpen(conn);

            var query = onlyPublic
                ? "SELECT link FROM habr_resumes WHERE link IS NOT NULL AND public = true ORDER BY updated_at ASC NULLS FIRST"
                : @"SELECT link FROM habr_resumes
                    WHERE link IS NOT NULL
                    AND NOT (public = false AND about = 'Доступ ограничен настройками приватности')
                    ORDER BY updated_at ASC NULLS FIRST";

            using var cmd = new NpgsqlCommand(query, conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var link = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(link))
                {
                    userLinks.Add(link);
                }
            }

            var filterText = onlyPublic ? " (только публичные)" : "";
            Log($"[DB] Загружено {userLinks.Count} ссылок пользователей из БД{filterText}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при получении ссылок пользователей: {ex.Message}");
        }

        return userLinks;
    }

    /// <summary>
    /// Получить ссылки пользователей без заполненных данных (для UserResumeDetailScraper).
    /// Выбирает профили, которые:
    /// 1. НЕ приватные (public != false ИЛИ public IS NULL)
    /// 2. НЕ имеют заполненных данных:
    ///    - Нет about (NULL или пустая строка)
    ///    - Нет опыта работы в habr_user_experience
    ///    - Нет высшего образования в habr_resumes_universities
    ///    - Нет дополнительного образования в habr_resumes_educations
    ///    - Нет участия в профсообществах (community_participation IS NULL или пустой массив)
    /// </summary>
    public List<string> ResumesGetUserLinksWithoutData(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var userLinks = new List<string>();

        try
        {
            EnsureConnectionOpen(conn);

            // Выбираем профили без заполненных данных (включая приватные - они будут обработаны и помечены)
            var query = @"
                SELECT r.link
                FROM habr_resumes r
                WHERE r.link IS NOT NULL
                  -- НЕТ заполненного about
                  AND (r.about IS NULL OR TRIM(r.about) = '')
                  -- НЕТ опыта работы
                  AND NOT EXISTS (SELECT 1 FROM habr_user_experience ue WHERE ue.user_id = r.id)
                  -- НЕТ высшего образования (колонка user_id в habr_resumes_universities)
                  AND NOT EXISTS (SELECT 1 FROM habr_resumes_universities ru WHERE ru.user_id = r.id)
                  -- НЕТ дополнительного образования (колонка resume_id в habr_resumes_educations)
                  AND NOT EXISTS (SELECT 1 FROM habr_resumes_educations re WHERE re.resume_id = r.id)
                  -- НЕТ участия в профсообществах
                  AND (r.community_participation IS NULL OR jsonb_array_length(r.community_participation) = 0)
                ORDER BY r.updated_at ASC NULLS FIRST";

            using var cmd = new NpgsqlCommand(query, conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var link = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(link))
                {
                    userLinks.Add(link);
                }
            }

            Log($"[DB] Загружено {userLinks.Count} ссылок пользователей без данных из БД");
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при получении ссылок пользователей без данных: {ex.Message}");
        }

        return userLinks;
    }

    /// <summary>
    /// Обновить флаг пустого профиля для пользователя
    /// </summary>
    public void ResumesUpdateUserEmptyProfile(NpgsqlConnection conn, string userLink, bool isEmpty)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));

        try
        {
            EnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                UPDATE habr_resumes
                SET is_empty = @is_empty
                WHERE link = @link", conn);

            cmd.Parameters.AddWithValue("@link", userLink);
            cmd.Parameters.AddWithValue("@is_empty", isEmpty);

            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                _statistics.RecordUpdate("habr_resumes", userLink);
                Log($"[DB] UserEmptyProfile {userLink}: ? UPDATE (isEmpty={isEmpty})");
            }
            else
            {
                _statistics.RecordSkipped("habr_resumes", userLink);
                Log($"[DB] UserEmptyProfile {userLink}: ? NOT FOUND");
            }
            TryDumpStatistics();
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] UserEmptyProfile {userLink}: ? ERROR - {dbEx.Message}");
            _statistics.RecordError("habr_resumes", userLink);
        }
        catch (Exception ex)
        {
            Log($"[DB] UserEmptyProfile {userLink}: ? ERROR - {ex.Message}");
            _statistics.RecordError("habr_resumes", userLink);
        }
    }

    /// <summary>
    /// Пометить профиль как удалённый
    /// </summary>
    public void ResumesMarkProfileAsDeleted(NpgsqlConnection conn, string userLink)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));

        try
        {
            EnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                UPDATE habr_resumes
                SET is_deleted = true,
                    title = 'Профиль удален',
                    about = 'Профиль пользователя удален со всей информацией, которую он о себе оставлял',
                    updated_at = NOW()
                WHERE link = @link", conn);

            cmd.Parameters.AddWithValue("@link", userLink);

            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                _statistics.RecordUpdate("habr_resumes", userLink);
                Log($"[DB] MarkDeleted {userLink}: ? UPDATE (is_deleted=true)");
            }
            else
            {
                _statistics.RecordSkipped("habr_resumes", userLink);
                Log($"[DB] MarkDeleted {userLink}: ? NOT FOUND");
            }
            TryDumpStatistics();
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] MarkDeleted {userLink}: ? ERROR - {dbEx.Message}");
            _statistics.RecordError("habr_resumes", userLink);
        }
        catch (Exception ex)
        {
            Log($"[DB] MarkDeleted {userLink}: ? ERROR - {ex.Message}");
            _statistics.RecordError("habr_resumes", userLink);
        }
    }


    /// <summary>
    /// Обновить участие в профсообществах для пользователя (Хабр, GitHub и др.)
    /// Сохраняет данные в поле community_participation как JSON массив
    /// </summary>
    public void ResumesUpdateUserCommunityParticipation(NpgsqlConnection conn, string userLink, List<CommunityParticipationRecord> communityParticipation)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));
        if (communityParticipation == null || communityParticipation.Count == 0)
            return;

        try
        {
            EnsureConnectionOpen(conn);

            var jsonString = SerializeCommunityParticipation(communityParticipation);

            using var cmd = new NpgsqlCommand(@"
                UPDATE habr_resumes
                SET community_participation = @community_participation::jsonb,
                    updated_at = NOW()
                WHERE link = @link", conn);

            cmd.Parameters.AddWithValue("@link", userLink);
            cmd.Parameters.AddWithValue("@community_participation", jsonString);

            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                _statistics.RecordUpdate("habr_resumes", userLink);
                var names = string.Join(", ", communityParticipation.Select(c => c.Name));
                Log($"[DB] UserCommunity {userLink}: ? UPDATE ({communityParticipation.Count} записей: {names})");
            }
            else
            {
                _statistics.RecordSkipped("habr_resumes", userLink);
                Log($"[DB] UserCommunity {userLink}: ? NOT FOUND");
            }
            TryDumpStatistics();
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] UserCommunity {userLink}: ? ERROR - {dbEx.Message}");
            _statistics.RecordError("habr_resumes", userLink);
        }
        catch (Exception ex)
        {
            Log($"[DB] UserCommunity {userLink}: ? ERROR - {ex.Message}");
            _statistics.RecordError("habr_resumes", userLink);
        }
    }

    /// <summary>
    /// Обновить статус публичности профиля пользователя
    /// </summary>
    public void ResumesUpdateUserPublicStatus(NpgsqlConnection conn, string userLink, bool isPublic)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));

        try
        {
            EnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                UPDATE habr_resumes
                SET public = @public,
                    updated_at = NOW()
                WHERE link = @link", conn);

            cmd.Parameters.AddWithValue("@link", userLink);
            cmd.Parameters.AddWithValue("@public", isPublic);

            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                _statistics.RecordUpdate("habr_resumes", userLink);
                Log($"[DB] UserPublicStatus {userLink}: ? UPDATE (public={isPublic})");
            }
            else
            {
                _statistics.RecordSkipped("habr_resumes", userLink);
                Log($"[DB] UserPublicStatus {userLink}: ? NOT FOUND");
            }
            TryDumpStatistics();
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] UserPublicStatus {userLink}: ? ERROR - {dbEx.Message}");
            _statistics.RecordError("habr_resumes", userLink);
        }
        catch (Exception ex)
        {
            Log($"[DB] UserPublicStatus {userLink}: ? ERROR - {ex.Message}");
            _statistics.RecordError("habr_resumes", userLink);
        }
    }

    /// <summary>
    /// Вставить или обновить навыки пользователя
    /// </summary>
    public void UserSkillsInsert(NpgsqlConnection conn, string userLink, List<string> skills)
    {
        UserSkillsInsert(conn, userLink, skills.Select(skill => new SkillsRecord(SkillTitle: skill)).ToList());
    }

    public void UserSkillsInsert(NpgsqlConnection conn, string userLink, List<SkillsRecord> skills)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));
        if (skills == null || skills.Count == 0) return;

        try
        {
            EnsureConnectionOpen(conn);

            // Получаем ID пользователя по ссылке
            int? userId = null;
            using (var cmdGetUser =
                   new NpgsqlCommand("SELECT id FROM habr_resumes WHERE link = @link LIMIT 1", conn))
            {
                cmdGetUser.Parameters.AddWithValue("@link", userLink);
                var result = cmdGetUser.ExecuteScalar();
                if (result != null)
                {
                    userId = Convert.ToInt32(result);
                }
            }

            if (!userId.HasValue)
            {
                Log($"[DB] Пользователь {userLink} не найден в БД. Пропуск навыков.");
                return;
            }

            // Удаляем старые связи навыков для этого пользователя
            using (var cmdDelete =
                   new NpgsqlCommand("DELETE FROM habr_user_skills WHERE user_id = @user_id", conn))
            {
                cmdDelete.Parameters.AddWithValue("@user_id", userId.Value);
                cmdDelete.ExecuteNonQuery();
            }

            // Добавляем навыки
            int addedCount = 0;
            int newSkillsCount = 0;
            foreach (var skillRecord in skills)
            {
                var skillTitle = skillRecord.SkillTitle;
                if (string.IsNullOrWhiteSpace(skillTitle)) continue;

                // Вставляем навык в таблицу habr_skills (если его нет)
                int skillId;
                using (var cmdInsertSkill = new NpgsqlCommand(@"
                    INSERT INTO habr_skills (title, created_at, updated_at)
                    VALUES (@title, NOW(), NOW())
                    ON CONFLICT (title)
                    DO UPDATE SET title = EXCLUDED.title, updated_at = NOW()
                    RETURNING id", conn))
                {
                    cmdInsertSkill.Parameters.AddWithValue("@title", skillTitle.Trim());
                    var result = cmdInsertSkill.ExecuteScalar();
                    skillId = Convert.ToInt32(result);
                }

                // Связываем навык с пользователем
                using (var cmdLinkSkill = new NpgsqlCommand(@"
                    INSERT INTO habr_user_skills (user_id, skill_id, created_at, updated_at)
                    VALUES (@user_id, @skill_id, NOW(), NOW())
                    ON CONFLICT (user_id, skill_id) DO UPDATE SET updated_at = NOW()", conn))
                {
                    cmdLinkSkill.Parameters.AddWithValue("@user_id", userId.Value);
                    cmdLinkSkill.Parameters.AddWithValue("@skill_id", skillId);
                    cmdLinkSkill.ExecuteNonQuery();
                }

                addedCount++;
                newSkillsCount++;
            }

            if (addedCount > 0)
            {
                _statistics.RecordInsert("habr_user_skills", $"{userId}-{userLink}");
                _statistics.RecordInsert("habr_skills", $"{newSkillsCount} навыков для {userLink}");
            }
            Log($"[DB] Добавлено {addedCount} навыков для пользователя {userLink}");
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Ошибка БД при добавлении навыков для {userLink}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Неожиданная ошибка при добавлении навыков для {userLink}: {ex.Message}");
        }
    }


    /// <summary>
    /// Вставить опыт работы пользователя
    /// </summary>
    public void UserExperienceInsert(NpgsqlConnection conn, UserExperienceRecord exp)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(exp.UserLink))
            throw new ArgumentException("User link must not be empty.", nameof(exp));

        try
        {
            EnsureConnectionOpen(conn);

            // Получаем ID пользователя по ссылке
            int? userId = null;
            using (var cmdGetUser = new NpgsqlCommand("SELECT id FROM habr_resumes WHERE link = @link LIMIT 1", conn))
            {
                cmdGetUser.Parameters.AddWithValue("@link", exp.UserLink);
                var result = cmdGetUser.ExecuteScalar();
                if (result != null)
                {
                    userId = Convert.ToInt32(result);
                }
            }

            if (!userId.HasValue)
            {
                Log($"[DB] Пользователь {exp.UserLink} не найден в БД. Пропуск опыта работы.");
                return;
            }

            // Обрабатываем компанию
            int? companyId = null;
            if (!string.IsNullOrWhiteSpace(exp.CompanyCode))
            {
                // Ищем компанию по коду
                using (var cmdGetCompany = new NpgsqlCommand("SELECT id FROM habr_companies WHERE code = @code LIMIT 1", conn))
                {
                    cmdGetCompany.Parameters.AddWithValue("@code", exp.CompanyCode);
                    var result = cmdGetCompany.ExecuteScalar();
                    if (result != null)
                    {
                        companyId = Convert.ToInt32(result);

                        // Обновляем информацию о компании
                        using (var cmdUpdateCompany = new NpgsqlCommand(@"
                            UPDATE habr_companies
                            SET url = COALESCE(@url, url),
                                title = COALESCE(@title, title),
                                about = COALESCE(@about, about),
                                employees_count = COALESCE(@employees_count, employees_count),
                                updated_at = NOW()
                            WHERE id = @id", conn))
                        {
                            cmdUpdateCompany.Parameters.AddWithValue("@id", companyId.Value);
                            cmdUpdateCompany.Parameters.AddWithValue("@url", exp.CompanyUrl ?? (object)DBNull.Value);
                            cmdUpdateCompany.Parameters.AddWithValue("@title", exp.CompanyTitle ?? (object)DBNull.Value);
                            cmdUpdateCompany.Parameters.AddWithValue("@about", exp.CompanyAbout ?? (object)DBNull.Value);
                            cmdUpdateCompany.Parameters.AddWithValue("@employees_count", exp.CompanySize ?? (object)DBNull.Value);
                            cmdUpdateCompany.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // Добавляем новую компанию
                        using (var cmdInsertCompany = new NpgsqlCommand(@"
                            INSERT INTO habr_companies (code, url, title, about, employees_count, created_at, updated_at)
                            VALUES (@code, @url, @title, @about, @employees_count, NOW(), NOW())
                            RETURNING id", conn))
                        {
                            cmdInsertCompany.Parameters.AddWithValue("@code", exp.CompanyCode);
                            cmdInsertCompany.Parameters.AddWithValue("@url", exp.CompanyUrl ?? (object)DBNull.Value);
                            cmdInsertCompany.Parameters.AddWithValue("@title", exp.CompanyTitle ?? (object)DBNull.Value);
                            cmdInsertCompany.Parameters.AddWithValue("@about", exp.CompanyAbout ?? (object)DBNull.Value);
                            cmdInsertCompany.Parameters.AddWithValue("@employees_count", exp.CompanySize ?? (object)DBNull.Value);
                            var insertResult = cmdInsertCompany.ExecuteScalar();
                            if (insertResult != null)
                            {
                                companyId = Convert.ToInt32(insertResult);
                            }
                        }
                    }
                }
            }

            // Удаляем старые записи опыта работы для этого пользователя (только для первой записи)
            // Каскадное удаление автоматически удалит связанные записи из habr_user_experience_skills
            if (exp.IsFirstRecord)
            {
                using (var cmdDelete = new NpgsqlCommand("DELETE FROM habr_user_experience WHERE user_id = @user_id", conn))
                {
                    cmdDelete.Parameters.AddWithValue("@user_id", userId.Value);
                    int deletedCount = cmdDelete.ExecuteNonQuery();
                    if (deletedCount > 0)
                    {
                        Log($"[DB] Удалено {deletedCount} старых записей опыта работы для пользователя {exp.UserLink}");
                    }
                }
            }

            // Добавляем запись опыта работы
            int experienceId;
            using (var cmdInsertExperience = new NpgsqlCommand(@"
                INSERT INTO habr_user_experience (user_id, company_id, position, duration, description, created_at, updated_at)
                VALUES (@user_id, @company_id, @position, @duration, @description, NOW(), NOW())
                RETURNING id", conn))
            {
                cmdInsertExperience.Parameters.AddWithValue("@user_id", userId.Value);
                cmdInsertExperience.Parameters.AddWithValue("@company_id", companyId ?? (object)DBNull.Value);
                cmdInsertExperience.Parameters.AddWithValue("@position", exp.Position ?? (object)DBNull.Value);
                cmdInsertExperience.Parameters.AddWithValue("@duration", exp.Duration ?? (object)DBNull.Value);
                cmdInsertExperience.Parameters.AddWithValue("@description", exp.Description ?? (object)DBNull.Value);
                var result = cmdInsertExperience.ExecuteScalar();
                experienceId = Convert.ToInt32(result);
            }

            // Добавляем навыки
            int experienceSkillsCount = 0;
            if (exp.Skills != null && exp.Skills.Count > 0)
            {
                foreach (var (skillId, skillName) in exp.Skills)
                {
                    if (string.IsNullOrWhiteSpace(skillName)) continue;

                    // Ищем или создаём навык
                    int actualSkillId;
                    using (var cmdGetSkill = new NpgsqlCommand("SELECT id FROM habr_skills WHERE title = @title LIMIT 1", conn))
                    {
                        cmdGetSkill.Parameters.AddWithValue("@title", skillName.Trim());
                        var result = cmdGetSkill.ExecuteScalar();
                        if (result != null)
                        {
                            actualSkillId = Convert.ToInt32(result);
                        }
                        else
                        {
                            // Создаём новый навык
                            using (var cmdInsertSkill = new NpgsqlCommand(@"
                                INSERT INTO habr_skills (title, created_at, updated_at)
                                VALUES (@title, NOW(), NOW())
                                RETURNING id", conn))
                            {
                                cmdInsertSkill.Parameters.AddWithValue("@title", skillName.Trim());
                                var insertResult = cmdInsertSkill.ExecuteScalar();
                                actualSkillId = Convert.ToInt32(insertResult);
                                _statistics.RecordInsert("habr_skills", skillName.Trim());
                            }
                        }
                    }

                    // Связываем навык с опытом работы
                    using (var cmdLinkSkill = new NpgsqlCommand(@"
                        INSERT INTO habr_user_experience_skills (experience_id, skill_id, created_at, updated_at)
                        VALUES (@experience_id, @skill_id, NOW(), NOW())
                        ON CONFLICT (experience_id, skill_id) DO UPDATE SET updated_at = NOW()", conn))
                    {
                        cmdLinkSkill.Parameters.AddWithValue("@experience_id", experienceId);
                        cmdLinkSkill.Parameters.AddWithValue("@skill_id", actualSkillId);
                        cmdLinkSkill.ExecuteNonQuery();
                    }
                    experienceSkillsCount++;
                }
                _statistics.RecordInsert("habr_user_experience_skills", $"{experienceId}");
            }

            Log($"[DB] Добавлен опыт работы для {exp.UserLink}: Company={exp.CompanyTitle}, Position={exp.Position}, Skills={exp.Skills?.Count ?? 0}");
            _statistics.RecordInsert("habr_user_experience", $"{userId}-{exp.CompanyTitle}");
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Ошибка БД при добавлении опыта работы для {exp.UserLink}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Неожиданная ошибка при добавлении опыта работы для {exp.UserLink}: {ex.Message}");
        }
    }

    /// <summary>
    /// Вставить навык с skill_id (только если его еще нет).
    /// Если не найден по skill_id, но найден по title — обновляет skill_id у существующей записи.
    /// </summary>
    public void SkillsInsert(NpgsqlConnection conn, int skillId, string? title)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        try
        {
            EnsureConnectionOpen(conn);

            var normalizedTitle = string.IsNullOrWhiteSpace(title) ? skillId.ToString() : title;

            // Проверяем существование по skill_id
            using (var checkCmd = new NpgsqlCommand(@"
                SELECT COUNT(*) FROM habr_skills WHERE skill_id = @skill_id", conn))
            {
                checkCmd.Parameters.AddWithValue("@skill_id", skillId);
                var count = (long)(checkCmd.ExecuteScalar() ?? 0L);

                if (count > 0)
                {
                    Log($"[DB] Навык уже существует: skill_id={skillId}, вставка пропущена");
                    return;
                }
            }

            // Не найден по skill_id — ищем по title
            using (var findCmd = new NpgsqlCommand(@"
                SELECT id FROM habr_skills WHERE title = @title LIMIT 1", conn))
            {
                findCmd.Parameters.AddWithValue("@title", normalizedTitle);
                var existingId = findCmd.ExecuteScalar();

                if (existingId != null)
                {
                    // Найден по title — обновляем skill_id
                    using var updateCmd = new NpgsqlCommand(@"
                        UPDATE habr_skills SET skill_id = @skill_id, updated_at = NOW()
                        WHERE id = @id", conn);
                    updateCmd.Parameters.AddWithValue("@skill_id", skillId);
                    updateCmd.Parameters.AddWithValue("@id", Convert.ToInt32(existingId));
                    updateCmd.ExecuteNonQuery();
                    Log($"[DB] Навык найден по title='{normalizedTitle}' (id={existingId}), skill_id обновлён на {skillId}");
                    _statistics.RecordUpdate("habr_skills", $"skill_id={skillId}");
                    return;
                }
            }

            // Не найден ни по skill_id, ни по title — вставляем
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO habr_skills (skill_id, title, created_at, updated_at)
                VALUES (@skill_id, @title, NOW(), NOW())", conn);

            cmd.Parameters.AddWithValue("@skill_id", skillId);
            cmd.Parameters.AddWithValue("@title", normalizedTitle);

            cmd.ExecuteNonQuery();
            Log($"[DB] Навык добавлен: skill_id={skillId}, title={normalizedTitle}");
            _statistics.RecordInsert("habr_skills", $"skill_id={skillId}");
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Ошибка БД при добавлении навыка {skillId}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Неожиданная ошибка при добавлении навыка {skillId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Получить внутренний id компании по её коду
    /// </summary>
    private int? CompaniesGetInternalId(NpgsqlConnection conn, string companyCode)
    {
        EnsureConnectionOpen(conn);
        using var cmd = new NpgsqlCommand(
            "SELECT id FROM habr_companies WHERE code = @code LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@code", companyCode);
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : null;
    }

    /// <summary>
    /// Сохранить отзыв о компании (с проверкой дубликатов по хешу)
    /// </summary>
    public void CompanyReviewsInsert(NpgsqlConnection conn, int companyId, List<string> reviewTexts)
    {
        foreach (var reviewText in reviewTexts)
        {
            if (string.IsNullOrWhiteSpace(reviewText)) continue;

            try
            {
                var reviewHash = CompanyRatingScraper.ComputeReviewHash(reviewText);

                // Проверяем, существует ли уже отзыв с таким хешем
                using (var cmdCheck = new NpgsqlCommand(@"
                    SELECT COUNT(*) FROM habr_company_reviews WHERE review_hash = @review_hash", conn))
                {
                    cmdCheck.Parameters.AddWithValue("@review_hash", reviewHash);
                    var count = Convert.ToInt32(cmdCheck.ExecuteScalar());

                    if (count > 0)
                    {
                        Log($"[DB] Отзыв с хешем {reviewHash.Substring(0, 8)}... уже существует, пропускаем");
                        continue;
                    }
                }

                // Вставляем новый отзыв
                using var cmdInsert = new NpgsqlCommand(@"
                    INSERT INTO habr_company_reviews (company_id, review_hash, review_text, created_at, updated_at)
                    VALUES (@company_id, @review_hash, @review_text, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)", conn);

                cmdInsert.Parameters.AddWithValue("@company_id", companyId);
                cmdInsert.Parameters.AddWithValue("@review_hash", reviewHash);
                cmdInsert.Parameters.AddWithValue("@review_text", reviewText);

                cmdInsert.ExecuteNonQuery();
                Log($"[DB] Добавлен новый отзыв для компании ID={companyId}");
            }
            catch (Exception ex)
            {
                Log($"[DB] Ошибка при сохранении отзыва для компании ID={companyId}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Вставить или обновить университет в БД
    /// </summary>
    private void UniversitiesInsert(NpgsqlConnection conn, UniversityRecord data)
    {
        EnsureConnectionOpen(conn);

        using var cmd = new NpgsqlCommand(@"
            INSERT INTO habr_universities (habr_id, name, city, graduate_count, created_at, updated_at)
            VALUES (@habr_id, @name, @city, @graduate_count, NOW(), NOW())
            ON CONFLICT (habr_id)
            DO UPDATE SET
                name = EXCLUDED.name,
                city = COALESCE(EXCLUDED.city, habr_universities.city),
                graduate_count = COALESCE(EXCLUDED.graduate_count, habr_universities.graduate_count),
                updated_at = NOW()", conn);

        cmd.Parameters.AddWithValue("@habr_id", data.HabrId);
        cmd.Parameters.AddWithValue("@name", data.Name);
        cmd.Parameters.AddWithValue("@city", data.City ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@graduate_count", data.GraduateCount ?? (object)DBNull.Value);

        int rowsAffected = cmd.ExecuteNonQuery();
        // Для UPSERT без RETURNING xmax считаем как UPDATE если rowsAffected > 0
        if (rowsAffected > 0)
            _statistics.RecordUpdate("habr_universities", data.HabrId.ToString());
        Log($"[DB] Университет {data.Name} (ID={data.HabrId}): {(rowsAffected > 0 ? "? UPSERT" : "? NO CHANGE")}");
    }

    /// <summary>
    /// Вставить связи пользователь-университет в БД
    /// </summary>
    private void ResumesUniversitiesInsert(NpgsqlConnection conn, List<UserUniversityRecord>? userUniversities)
    {
        if (userUniversities == null || userUniversities.Count == 0)
        {
            return;
        }

        foreach (var userUniversity in userUniversities)
        {
            ResumesUniversityInsert(conn, userUniversity);
        }
    }

    private void ResumesUniversityInsert(NpgsqlConnection conn, UserUniversityRecord data)
    {
        EnsureConnectionOpen(conn);

        // Получаем user_id по ссылке
        int? userId = null;
        using (var cmdUser = new NpgsqlCommand("SELECT id FROM habr_resumes WHERE link = @link LIMIT 1", conn))
        {
            cmdUser.Parameters.AddWithValue("@link", data.UserLink);
            var result = cmdUser.ExecuteScalar();
            if (result != null)
            {
                userId = Convert.ToInt32(result);
            }
        }

        if (!userId.HasValue)
        {
            Log($"[DB] Пользователь не найден: {data.UserLink}");
            return;
        }

        // Получаем university_id по habr_id
        int? universityId = null;
        using (var cmdUniv = new NpgsqlCommand("SELECT id FROM habr_universities WHERE habr_id = @habr_id LIMIT 1", conn))
        {
            cmdUniv.Parameters.AddWithValue("@habr_id", data.UniversityHabrId);
            var result = cmdUniv.ExecuteScalar();
            if (result != null)
            {
                universityId = Convert.ToInt32(result);
            }
        }

        if (!universityId.HasValue)
        {
            Log($"[DB] Университет не найден: habr_id={data.UniversityHabrId}");
            return;
        }

        // Сериализуем курсы в JSON
        string? coursesJson = null;
        if (data.Courses != null && data.Courses.Count > 0)
        {
            coursesJson = System.Text.Json.JsonSerializer.Serialize(data.Courses);
        }

        using var cmd = new NpgsqlCommand(@"
            INSERT INTO habr_resumes_universities (user_id, university_id, courses, description, created_at, updated_at)
            VALUES (@user_id, @university_id, @courses::jsonb, @description, NOW(), NOW())
            ON CONFLICT (user_id, university_id)
            DO UPDATE SET
                courses = COALESCE(EXCLUDED.courses, habr_resumes_universities.courses),
                description = COALESCE(EXCLUDED.description, habr_resumes_universities.description),
                updated_at = NOW()", conn);

        cmd.Parameters.AddWithValue("@user_id", userId.Value);
        cmd.Parameters.AddWithValue("@university_id", universityId.Value);
        cmd.Parameters.AddWithValue("@courses", coursesJson ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@description", data.Description ?? (object)DBNull.Value);

        int rowsAffected = cmd.ExecuteNonQuery();
        if (rowsAffected > 0)
            _statistics.RecordUpdate("habr_resumes_universities", $"{userId}-{universityId}");
        Log($"[DB] Связь пользователь-университет: user_id={userId}, university_id={universityId}, courses={data.Courses?.Count ?? 0}: ? UPSERT");
    }

    /// <summary>
    /// Вставить запись дополнительного образования в БД
    /// </summary>
    private void ResumesEducationsInsert(NpgsqlConnection conn, AdditionalEducationRecord data)
    {
        EnsureConnectionOpen(conn);

        // Получаем resume_id по ссылке
        int? resumeId = null;
        using (var cmdUser = new NpgsqlCommand("SELECT id FROM habr_resumes WHERE link = @link LIMIT 1", conn))
        {
            cmdUser.Parameters.AddWithValue("@link", data.UserLink);
            var result = cmdUser.ExecuteScalar();
            if (result != null)
            {
                resumeId = Convert.ToInt32(result);
            }
        }

        if (!resumeId.HasValue)
        {
            Log($"[DB] Пользователь не найден для дополнительного образования: {data.UserLink}");
            return;
        }

        using var cmd = new NpgsqlCommand(@"
            INSERT INTO habr_resumes_educations (resume_id, title, course, duration, created_at, updated_at)
            VALUES (@resume_id, @title, @course, @duration, NOW(), NOW())
            ON CONFLICT DO NOTHING", conn);

        cmd.Parameters.AddWithValue("@resume_id", resumeId.Value);
        cmd.Parameters.AddWithValue("@title", data.Title);
        cmd.Parameters.AddWithValue("@course", data.Course ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@duration", data.Duration ?? (object)DBNull.Value);

        int rowsAffected = cmd.ExecuteNonQuery();
        if (rowsAffected > 0)
            _statistics.RecordInsert("habr_resumes_educations", $"{resumeId}-{data.Title}");
        else
            _statistics.RecordSkipped("habr_resumes_educations", $"{resumeId}-{data.Title}");
        Log($"[DB] Дополнительное образование: resume_id={resumeId}, title={data.Title}: {(rowsAffected > 0 ? "? INSERT" : "? SKIPPED")}");
    }

    /// <summary>
    /// Удалить все записи дополнительного образования пользователя перед добавлением новых
    /// </summary>
    public void ResumesEducationsDelete(NpgsqlConnection conn, string userLink)
    {
        EnsureConnectionOpen(conn);

        // Получаем resume_id по ссылке
        int? resumeId = null;
        using (var cmdUser = new NpgsqlCommand("SELECT id FROM habr_resumes WHERE link = @link LIMIT 1", conn))
        {
            cmdUser.Parameters.AddWithValue("@link", userLink);
            var result = cmdUser.ExecuteScalar();
            if (result != null)
            {
                resumeId = Convert.ToInt32(result);
            }
        }

        if (!resumeId.HasValue)
        {
            return;
        }

        using var cmd = new NpgsqlCommand("DELETE FROM habr_resumes_educations WHERE resume_id = @resume_id", conn);
        cmd.Parameters.AddWithValue("@resume_id", resumeId.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Удаляет все записи с 404 ошибками из таблицы habr_resumes
    /// </summary>
    /// <returns>Количество удалённых записей</returns>
    public int ResumesCleanup404Pages(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        try
        {
            EnsureConnectionOpen(conn);
            using var cmd = new NpgsqlCommand(
                "DELETE FROM habr_resumes WHERE title LIKE '%Ошибка 404%' OR about LIKE '%Ошибка 404%'", conn);
            int deleted = cmd.ExecuteNonQuery();
            Log($"[DB] Очистка 404: удалено {deleted} записей");
            return deleted;
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при очистке 404: {ex.Message}");
            return 0;
        }
    }

    #endregion

    #region Helper methods

    private static string SerializeCommunityParticipation(List<CommunityParticipationRecord> communityParticipation)
    {
        var jsonArray = new System.Text.Json.Nodes.JsonArray();
        foreach (var item in communityParticipation)
        {
            var jsonObj = new System.Text.Json.Nodes.JsonObject
            {
                ["name"] = item.Name,
                ["member_since"] = item.MemberSince,
                ["contribution"] = item.Contribution,
                ["topics"] = item.Topics
            };
            jsonArray.Add(jsonObj);
        }

        return jsonArray.ToJsonString();
    }

    #endregion
}
