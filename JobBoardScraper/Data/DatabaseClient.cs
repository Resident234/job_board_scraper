using System;
using System.Reflection;
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
    List<CommunityParticipationData>? CommunityParticipation = null,
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
    string? Duration = null,
    bool DeleteExisting = false);

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

    
    public DatabaseClient(string connectionString, ConsoleLogger? logger = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
        _statistics.InitializeAllTables();
    }
    
    #region Statistics

    private readonly DatabaseStatistics _statistics = new();
    private DateTime _lastStatsDump = DateTime.Now;
    private readonly TimeSpan _statsDumpInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Статистика операций с БД
    /// </summary>
    public DatabaseStatistics Statistics => _statistics;

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

    #endregion
    
    #region Logging
    
    private const int MaxRecordLogDepth = 3;

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
    
    private void LogEnqueue(string recordType, object? record)
    {
        Log($"[DB Queue] {recordType}: {FormatRecord(record)}");
    }

    private static string FormatRecord(object? record)
    {
        return FormatValue(record, depth: 0);
    }

    private static string FormatValue(object? value, int depth)
    {
        if (value == null)
            return "<null>";

        if (depth > MaxRecordLogDepth)
            return value.ToString() ?? "<value>";

        var type = value.GetType();

        if (type.IsEnum)
            return value.ToString() ?? "<enum>";

        if (type == typeof(string))
            return string.IsNullOrWhiteSpace((string)value) ? "<empty>" : EscapeLogValue((string)value);

        if (IsScalarType(type))
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "<value>";

        if (value is System.Collections.IEnumerable enumerable && type != typeof(string))
            return FormatEnumerable(enumerable, depth);

        return FormatObjectProperties(value, depth);
    }

    private static string FormatObjectProperties(object value, int depth)
    {
        var properties = value.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.MetadataToken)
            .Select(property =>
            {
                try
                {
                    return $"{property.Name}={FormatValue(property.GetValue(value), depth + 1)}";
                }
                catch (Exception ex)
                {
                    return $"{property.Name}=<error reading property: {EscapeLogValue(ex.Message)}>";
                }
            });

        return "{" + string.Join(", ", properties) + "}";
    }

    private static string FormatEnumerable(System.Collections.IEnumerable enumerable, int depth)
    {
        var items = new List<string>();

        foreach (var item in enumerable)
        {
            items.Add(FormatValue(item, depth + 1));
        }

        return items.Count == 0 ? "[]" : "[" + string.Join("; ", items) + "]";
    }

    private static bool IsScalarType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return type.IsPrimitive
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid);
    }

    private static string EscapeLogValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    #endregion

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
        List<CommunityParticipationData>? communityParticipation = null,
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
        LogEnqueue("Resume", resumeRecord);

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
        LogEnqueue("Company", companyRecord);

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
        LogEnqueue("CategoryRootId", categoryRecord);

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
        LogEnqueue("UserExperience", experienceRecord);

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
        LogEnqueue("Skills", skillRecord);

        return true;
    }

    /// <summary>
    /// Добавить университет в основную очередь на сохранение
    /// </summary>
    public void EnqueueUniversity(int habrId, string name, string? city = null, int? graduateCount = null)
    {
        if (_saveQueue == null) return;

        var universityRecord = new UniversityRecord(
            HabrId: habrId,
            Name: name,
            City: city,
            GraduateCount: graduateCount);

        var record = new DbRecord(
            Type: DbRecordType.University,
            University: universityRecord
        );
        _saveQueue.Enqueue(record);
        LogEnqueue("University", universityRecord);
    }

    /// <summary>
    /// Добавить дополнительное образование в основную очередь на сохранение
    /// </summary>
    public void EnqueueAdditionalEducation(string userLink, string title, string? course = null, string? duration = null, bool deleteExisting = false)
    {
        if (_saveQueue == null) return;

        var additionalEducationRecord = new AdditionalEducationRecord(
            UserLink: userLink,
            Title: title,
            Course: course,
            Duration: duration,
            DeleteExisting: deleteExisting);

        var record = new DbRecord(
            Type: DbRecordType.AdditionalEducation,
            AdditionalEducation: additionalEducationRecord
        );
        _saveQueue.Enqueue(record);
        LogEnqueue("AdditionalEducation", additionalEducationRecord);
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
        List<CommunityParticipationData>? communityParticipation = null)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(link)) throw new ArgumentException("Link must not be empty.", nameof(link));

        try
        {
           EnsureConnectionOpen(conn);

           string? communityParticipationJson = communityParticipation is { Count: > 0 }
               ? System.Text.Json.JsonSerializer.Serialize(communityParticipation)
               : null;

           if (mode == InsertMode.SkipIfExists)
            {
                if (title != null && title.Contains("Ошибка 404"))
                {
                    Log($"[DB] Resume {link}: ? SKIP (404 страница)");
                    _statistics.RecordSkipped("habr_resumes", link);
                    return;
                }

                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO habr_resumes (link, title, slogan, code, expert, work_experience, level_id, info_tech, salary, last_visit, age, registration, citizenship, remote_work, public, job_search_status, is_empty, is_deleted, about, community_participation, created_at, updated_at)
                      VALUES (@link, @title, @slogan, @code, @expert, @work_experience, @level_id, @info_tech, @salary, @last_visit, @age, @registration, @citizenship, @remote_work, @public, @job_search_status, @is_empty, @is_deleted, @about, @community_participation, NOW(), NOW())
                      ON CONFLICT (link) DO NOTHING
                      RETURNING xmax",
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

                var inserted = cmd.ExecuteScalar();
                if (inserted == null)
                {
                    Log($"[DB] Resume {link}: ? SKIP (уже существует)");
                    _statistics.RecordSkipped("habr_resumes", link);
                    return;
                }

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
    /// Вставить или обновить навыки компании.
    /// </summary>
    /// <remarks>
    /// Метод выполняет все действия одним SQL-запросом через CTE:
    /// - находит компанию в habr_companies по code;
    /// - удаляет устаревшие связи habr_company_skills для этой компании;
    /// - нормализует, триммит и дедуплицирует список навыков;
    /// - делает upsert навыков в habr_skills через ON CONFLICT (title);
    /// - связывает навыки с компанией в habr_company_skills через ON CONFLICT (company_id, skill_id).
    /// </remarks>
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

            var skillTitles = skills
                .Select(skill => skill.SkillTitle)
                .Where(skillTitle => !string.IsNullOrWhiteSpace(skillTitle))
                .Select(skillTitle => skillTitle!.Trim())
                .Distinct()
                .ToArray();

            using var cmd = new NpgsqlCommand(@"
                WITH company AS (
                    SELECT id
                    FROM habr_companies
                    WHERE code = @company_code
                    LIMIT 1
                ),
                deleted AS (
                    DELETE FROM habr_company_skills hcs
                    USING company c
                    WHERE hcs.company_id = c.id
                      AND NOT EXISTS (
                          SELECT 1
                          FROM upserted_skills s
                          WHERE s.id = hcs.skill_id
                      )
                    RETURNING 1
                ),
                dedup_skills AS (
                    SELECT DISTINCT trim(title) AS title
                    FROM string_to_array(@titles, E'\n') AS t(title)
                    WHERE trim(title) <> ''
                ),
                upserted_skills AS (
                    INSERT INTO habr_skills (title, created_at, updated_at)
                    SELECT title, NOW(), NOW()
                    FROM dedup_skills
                    ON CONFLICT (title)
                    DO UPDATE SET title = EXCLUDED.title, updated_at = NOW()
                    RETURNING id
                ),
                linked AS (
                    INSERT INTO habr_company_skills (company_id, skill_id, created_at, updated_at)
                    SELECT c.id, s.id, NOW(), NOW()
                    FROM company c
                    CROSS JOIN upserted_skills s
                    ON CONFLICT (company_id, skill_id) DO UPDATE SET updated_at = NOW()
                    RETURNING 1
                )
                SELECT
                    (SELECT id FROM company) AS company_id,
                    (SELECT COUNT(*)::int FROM linked) AS linked_count,
                    (SELECT COUNT(*)::int FROM upserted_skills) AS upserted_skills_count,
                    (SELECT COUNT(*)::int FROM deleted) AS deleted_count", conn);

            cmd.Parameters.AddWithValue("@company_code", companyCode);
            cmd.Parameters.AddWithValue("@titles", string.Join("\n", skillTitles));

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                Log($"[DB] CompanySkills {companyCode}: ? ERROR - запрос не вернул результат");
                _statistics.RecordError("habr_company_skills", companyCode);
                return;
            }

            var companyId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            var linkedCount = reader.GetInt32(1);
            var upsertedSkillsCount = reader.GetInt32(2);
            var deletedCount = reader.GetInt32(3);

            if (!companyId.HasValue)
            {
                Log($"[DB] Компания {companyCode} не найдена в БД. Пропуск навыков.");
                return;
            }

            if (linkedCount > 0)
            {
                _statistics.RecordInsert("habr_company_skills", companyCode);
                _statistics.RecordInsert("habr_skills", $"{upsertedSkillsCount} навыков для компании {companyCode}");
            }

            Log($"[DB] CompanySkills {companyCode}: ? {linkedCount} навыков добавлено/обновлено, {deletedCount} устаревших связей удалено");
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
    public void ResumesUpdateUserCommunityParticipation(NpgsqlConnection conn, string userLink, List<CommunityParticipationData> communityParticipation)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));
        if (communityParticipation == null || communityParticipation.Count == 0)
            return;

        try
        {
            EnsureConnectionOpen(conn);

            var jsonString = System.Text.Json.JsonSerializer.Serialize(communityParticipation);

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
    /// Вставить или обновить навыки пользователя.
    /// </summary>
    /// <remarks>
    /// Метод выполняет все действия одним SQL-запросом через CTE:
    /// - находит пользователя в habr_resumes по link;
    /// - удаляет устаревшие связи habr_user_skills для этого пользователя;
    /// - нормализует, триммит и дедуплицирует список навыков;
    /// - делает upsert навыков в habr_skills через ON CONFLICT (title);
    /// - связывает навыки с пользователем в habr_user_skills через ON CONFLICT (user_id, skill_id).
    /// </remarks>
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

            var skillTitles = skills
                .Select(skill => skill.SkillTitle)
                .Where(skillTitle => !string.IsNullOrWhiteSpace(skillTitle))
                .Select(skillTitle => skillTitle!.Trim())
                .Distinct()
                .ToArray();

            using var cmd = new NpgsqlCommand(@"
                WITH target_user AS (
                    SELECT id
                    FROM habr_resumes
                    WHERE link = @user_link
                    LIMIT 1
                ),
                dedup_skills AS (
                    SELECT DISTINCT trim(title) AS title
                    FROM string_to_array(@titles, E'\n') AS t(title)
                    WHERE trim(title) <> ''
                ),
                upserted_skills AS (
                    INSERT INTO habr_skills (title, created_at, updated_at)
                    SELECT title, NOW(), NOW()
                    FROM dedup_skills
                    ON CONFLICT (title)
                    DO UPDATE SET title = EXCLUDED.title, updated_at = NOW()
                    RETURNING id
                ),
                deleted AS (
                    DELETE FROM habr_user_skills hus
                    USING target_user u
                    WHERE hus.user_id = u.id
                      AND NOT EXISTS (
                          SELECT 1
                          FROM upserted_skills s
                          WHERE s.id = hus.skill_id
                      )
                    RETURNING 1
                ),
                linked AS (
                    INSERT INTO habr_user_skills (user_id, skill_id, created_at, updated_at)
                    SELECT u.id, s.id, NOW(), NOW()
                    FROM target_user u
                    CROSS JOIN upserted_skills s
                    ON CONFLICT (user_id, skill_id) DO UPDATE SET updated_at = NOW()
                    RETURNING 1
                )
                SELECT
                    (SELECT id FROM target_user) AS user_id,
                    (SELECT COUNT(*)::int FROM linked) AS linked_count,
                    (SELECT COUNT(*)::int FROM upserted_skills) AS upserted_skills_count,
                    (SELECT COUNT(*)::int FROM deleted) AS deleted_count", conn);

            cmd.Parameters.AddWithValue("@user_link", userLink);
            cmd.Parameters.AddWithValue("@titles", string.Join("\n", skillTitles));

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                Log($"[DB] UserSkills {userLink}: ? ERROR - запрос не вернул результат");
                return;
            }

            var userId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            var linkedCount = reader.GetInt32(1);
            var upsertedSkillsCount = reader.GetInt32(2);
            var deletedCount = reader.GetInt32(3);

            if (!userId.HasValue)
            {
                Log($"[DB] Пользователь {userLink} не найден в БД. Пропуск навыков.");
                return;
            }

            if (linkedCount > 0)
            {
                _statistics.RecordInsert("habr_user_skills", $"{userId}-{userLink}");
                _statistics.RecordInsert("habr_skills", $"{upsertedSkillsCount} навыков для {userLink}");
            }

            Log($"[DB] UserSkills {userLink}: ? {linkedCount} навыков добавлено/обновлено, {deletedCount} устаревших связей удалено");
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
    /// Вставить опыт работы пользователя.
    /// </summary>
    /// <remarks>
    /// Метод выполняет все действия одним SQL-запросом через CTE:
    /// - находит пользователя в habr_resumes по link;
    /// - находит, создает или обновляет компанию в habr_companies по code;
    /// - при IsFirstRecord удаляет старые записи habr_user_experience для пользователя;
    /// - вставляет новую запись опыта работы в habr_user_experience;
    /// - нормализует, триммит и дедуплицирует навыки опыта;
    /// - делает upsert навыков в habr_skills через ON CONFLICT (title);
    /// - связывает навыки с опытом в habr_user_experience_skills через ON CONFLICT (experience_id, skill_id).
    /// </remarks>
    public void UserExperienceInsert(NpgsqlConnection conn, UserExperienceRecord exp)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(exp.UserLink))
            throw new ArgumentException("User link must not be empty.", nameof(exp));

        try
        {
            EnsureConnectionOpen(conn);

            var hasCompany = !string.IsNullOrWhiteSpace(exp.CompanyCode);
            var experienceSkillTitles = exp.Skills?
                .Select(skill => skill.SkillTitle)
                .Where(skillTitle => !string.IsNullOrWhiteSpace(skillTitle))
                .Select(skillTitle => skillTitle!.Trim())
                .Distinct()
                .ToArray()
                ?? Array.Empty<string>();

            using var cmd = new NpgsqlCommand(@"
                WITH target_user AS (
                    SELECT id
                    FROM habr_resumes
                    WHERE link = @user_link
                    LIMIT 1
                ),
                existing_company AS (
                    SELECT id
                    FROM habr_companies
                    WHERE code = @company_code
                    LIMIT 1
                ),
                inserted_company AS (
                    INSERT INTO habr_companies (code, url, title, about, employees_count, created_at, updated_at)
                    SELECT @company_code, @company_url, @company_title, @company_about, @company_employees_count, NOW(), NOW()
                    WHERE @has_company
                      AND NOT EXISTS (SELECT 1 FROM existing_company)
                    RETURNING id
                ),
                company_id AS (
                    SELECT id FROM existing_company
                    UNION ALL
                    SELECT id FROM inserted_company
                    LIMIT 1
                ),
                updated_company AS (
                    UPDATE habr_companies
                    SET url = COALESCE(@company_url, url),
                        title = COALESCE(@company_title, title),
                        about = COALESCE(@company_about, about),
                        employees_count = COALESCE(@company_employees_count, employees_count),
                        updated_at = NOW()
                    WHERE @has_company
                      AND id = (SELECT id FROM company_id)
                    RETURNING id
                ),
                dedup_experience_skills AS (
                    SELECT DISTINCT trim(title) AS title
                    FROM string_to_array(@experience_skill_titles, E'\n') AS t(title)
                    WHERE trim(title) <> ''
                ),
                upserted_experience_skills AS (
                    INSERT INTO habr_skills (title, created_at, updated_at)
                    SELECT title, NOW(), NOW()
                    FROM dedup_experience_skills
                    ON CONFLICT (title)
                    DO UPDATE SET title = EXCLUDED.title, updated_at = NOW()
                    RETURNING id
                ),
                inserted_experience AS (
                    INSERT INTO habr_user_experience (user_id, company_id, position, duration, description, created_at, updated_at)
                    SELECT
                        tu.id,
                        (SELECT id FROM company_id),
                        @position,
                        @duration,
                        @description,
                        NOW(),
                        NOW()
                    FROM target_user tu
                    WHERE EXISTS (SELECT 1 FROM target_user)
                    RETURNING id
                ),
                deleted_experiences AS (
                    DELETE FROM habr_user_experience
                    WHERE @delete_old_experiences
                      AND user_id = (SELECT id FROM target_user)
                      AND id <> ALL (SELECT id FROM inserted_experience)
                    RETURNING 1
                ),
                linked_experience_skills AS (
                    INSERT INTO habr_user_experience_skills (experience_id, skill_id, created_at, updated_at)
                    SELECT ie.id, s.id, NOW(), NOW()
                    FROM inserted_experience ie
                    CROSS JOIN upserted_experience_skills s
                    ON CONFLICT (experience_id, skill_id) DO UPDATE SET updated_at = NOW()
                    RETURNING 1
                )
                SELECT
                    (SELECT id FROM target_user) AS user_id,
                    (SELECT id FROM company_id) AS company_id,
                    (SELECT id FROM inserted_experience) AS experience_id,
                    (SELECT COUNT(*)::int FROM deleted_experiences) AS deleted_experiences_count,
                    (SELECT COUNT(*)::int FROM upserted_experience_skills) AS upserted_experience_skills_count,
                    (SELECT COUNT(*)::int FROM linked_experience_skills) AS linked_experience_skills_count,
                    (SELECT COUNT(*)::int FROM updated_company) AS updated_company_count", conn);

            cmd.Parameters.AddWithValue("@user_link", exp.UserLink);
            cmd.Parameters.AddWithValue("@has_company", hasCompany);
            cmd.Parameters.AddWithValue("@company_code", hasCompany ? exp.CompanyCode! : DBNull.Value);
            cmd.Parameters.AddWithValue("@company_url", exp.CompanyUrl ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@company_title", exp.CompanyTitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@company_about", exp.CompanyAbout ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@company_employees_count", exp.CompanySize ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@delete_old_experiences", exp.IsFirstRecord);
            cmd.Parameters.AddWithValue("@position", exp.Position ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@duration", exp.Duration ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@description", exp.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@experience_skill_titles", string.Join("\n", experienceSkillTitles));

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                Log($"[DB] UserExperience {exp.UserLink}: ? ERROR - запрос не вернул результат");
                return;
            }

            var userId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            var companyId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
            var experienceId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
            var deletedExperiencesCount = reader.GetInt32(3);
            var upsertedExperienceSkillsCount = reader.GetInt32(4);
            var linkedExperienceSkillsCount = reader.GetInt32(5);
            var updatedCompanyCount = reader.GetInt32(6);

            if (!userId.HasValue)
            {
                Log($"[DB] Пользователь {exp.UserLink} не найден в БД. Пропуск опыта работы.");
                return;
            }

            if (!experienceId.HasValue)
            {
                Log($"[DB] UserExperience {exp.UserLink}: ? ERROR - опыт работы не вставлен");
                return;
            }

            if (deletedExperiencesCount > 0)
            {
                Log($"[DB] Удалено {deletedExperiencesCount} старых записей опыта работы для пользователя {exp.UserLink}");
            }

            if (linkedExperienceSkillsCount > 0)
            {
                _statistics.RecordInsert("habr_user_experience_skills", $"{experienceId}");
            }

            if (upsertedExperienceSkillsCount > 0)
            {
                _statistics.RecordInsert("habr_skills", $"{upsertedExperienceSkillsCount} навыков для опыта {exp.UserLink}");
            }

            Log($"[DB] UserExperience {exp.UserLink}: ? experience_id={experienceId}, company_id={companyId}, skills={linkedExperienceSkillsCount}, deleted_old_experiences={deletedExperiencesCount}, updated_company={updatedCompanyCount}");
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
    /// Вставить навык с skill_id одним SQL-запросом.
    /// </summary>
    /// <remarks>
    /// Логика запроса:
    /// - ищет навык по skill_id;
    /// - если найден — ничего не делает;
    /// - если не найден по skill_id, ищет по title;
    /// - если найден по title — обновляет skill_id;
    /// - если не найден ни по skill_id, ни по title — вставляет новый навык.
    /// </remarks>
    public void SkillsInsert(NpgsqlConnection conn, int skillId, string? title)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        try
        {
            EnsureConnectionOpen(conn);

            var normalizedTitle = string.IsNullOrWhiteSpace(title) ? skillId.ToString() : title;

            using var cmd = new NpgsqlCommand(@"
                WITH existing_by_skill_id AS (
                    SELECT id
                    FROM habr_skills
                    WHERE skill_id = @skill_id
                    LIMIT 1
                ),
                existing_by_title AS (
                    SELECT id
                    FROM habr_skills
                    WHERE title = @title
                    LIMIT 1
                ),
                updated_by_title AS (
                    UPDATE habr_skills
                    SET skill_id = @skill_id,
                        updated_at = NOW()
                    WHERE NOT EXISTS (SELECT 1 FROM existing_by_skill_id)
                      AND id = (SELECT id FROM existing_by_title)
                    RETURNING id
                ),
                inserted AS (
                    INSERT INTO habr_skills (skill_id, title, created_at, updated_at)
                    SELECT @skill_id, @title, NOW(), NOW()
                    WHERE NOT EXISTS (SELECT 1 FROM existing_by_skill_id)
                      AND NOT EXISTS (SELECT 1 FROM existing_by_title)
                    RETURNING id
                )
                SELECT
                    (SELECT id FROM existing_by_skill_id) AS existing_by_skill_id,
                    (SELECT id FROM updated_by_title) AS updated_by_title,
                    (SELECT id FROM inserted) AS inserted_id", conn);

            cmd.Parameters.AddWithValue("@skill_id", skillId);
            cmd.Parameters.AddWithValue("@title", normalizedTitle);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                Log($"[DB] SkillsInsert {skillId}: ? ERROR - запрос не вернул результат");
                return;
            }

            var existingBySkillId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            var updatedByTitle = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
            var insertedId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);

            if (existingBySkillId.HasValue)
            {
                Log($"[DB] Навык уже существует: skill_id={skillId}, вставка пропущена");
                return;
            }

            if (updatedByTitle.HasValue)
            {
                Log($"[DB] Навык найден по title='{normalizedTitle}' (id={updatedByTitle}), skill_id обновлён на {skillId}");
                _statistics.RecordUpdate("habr_skills", $"skill_id={skillId}");
                return;
            }

            if (insertedId.HasValue)
            {
                Log($"[DB] Навык добавлен: skill_id={skillId}, title={normalizedTitle}");
                _statistics.RecordInsert("habr_skills", $"skill_id={skillId}");
                return;
            }

            Log($"[DB] SkillsInsert {skillId}: ? навык не обработан");
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
    /// Сохранить отзывы о компании одним SQL-запросом с проверкой дубликатов по хешу.
    /// </summary>
    public void CompanyReviewsInsert(NpgsqlConnection conn, int companyId, List<string> reviewTexts)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (reviewTexts == null || reviewTexts.Count == 0) return;

        try
        {
            EnsureConnectionOpen(conn);

            var reviews = reviewTexts
                .Where(reviewText => !string.IsNullOrWhiteSpace(reviewText))
                .Select(reviewText => new
                {
                    review_hash = CompanyRatingScraper.ComputeReviewHash(reviewText),
                    review_text = reviewText
                })
                .ToArray();

            if (reviews.Length == 0) return;

            var reviewsJson = System.Text.Json.JsonSerializer.Serialize(reviews);

            using var cmd = new NpgsqlCommand(@"
                WITH review_rows AS (
                    SELECT review_hash, review_text
                    FROM jsonb_to_recordset(@reviews::jsonb)
                         AS r(review_hash text, review_text text)
                    WHERE review_hash IS NOT NULL
                      AND review_text IS NOT NULL
                      AND btrim(review_text) <> ''
                ),
                dedup_review_rows AS (
                    SELECT DISTINCT ON (review_hash)
                        review_hash,
                        review_text
                    FROM review_rows
                    ORDER BY review_hash, review_text
                ),
                existing_reviews AS (
                    SELECT DISTINCT hcr.review_hash
                    FROM habr_company_reviews hcr
                    JOIN dedup_review_rows drr ON drr.review_hash = hcr.review_hash
                ),
                inserted_reviews AS (
                    INSERT INTO habr_company_reviews (company_id, review_hash, review_text, created_at, updated_at)
                    SELECT @company_id, drr.review_hash, drr.review_text, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                    FROM dedup_review_rows drr
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM existing_reviews er
                        WHERE er.review_hash = drr.review_hash
                    )
                    RETURNING id, review_hash
                )
                SELECT
                    (SELECT COUNT(*)::int FROM inserted_reviews) AS inserted_count,
                    (SELECT COUNT(*)::int FROM existing_reviews) AS existing_count,
                    (SELECT COUNT(*)::int FROM dedup_review_rows) AS total_count", conn);

            cmd.Parameters.AddWithValue("@company_id", companyId);
            cmd.Parameters.AddWithValue("@reviews", reviewsJson);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                Log($"[DB] CompanyReviews {companyId}: ? ERROR - запрос не вернул результат");
                return;
            }

            var insertedCount = reader.GetInt32(0);
            var existingCount = reader.GetInt32(1);
            var totalCount = reader.GetInt32(2);

            if (insertedCount > 0)
            {
                Log($"[DB] Добавлено {insertedCount} новых отзывов для компании ID={companyId}");
            }

            if (existingCount > 0)
            {
                Log($"[DB] Пропущено {existingCount} существующих отзывов для компании ID={companyId}");
            }

            if (totalCount == 0)
            {
                Log($"[DB] CompanyReviews {companyId}: ? отзывов для обработки не найдено");
            }
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при сохранении отзывов для компании ID={companyId}: {ex.Message}");
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
    /// Вставить связи пользователь-университет в БД одним SQL-запросом.
    /// </summary>
    /// <remarks>
    /// Логика запроса:
    /// - входные записи передаются одним JSON-параметром;
    /// - записи дедуплицируются по паре user_link + university_habr_id;
    /// - пользователи находятся в habr_resumes;
    /// - университеты находятся в habr_universities;
    /// - связи upsert-ятся в habr_resumes_universities через ON CONFLICT (user_id, university_id);
    /// - отдельно считаются пропущенные пользователи и университеты.
    /// </remarks>
    private void ResumesUniversitiesInsert(NpgsqlConnection conn, List<UserUniversityRecord>? userUniversities)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (userUniversities == null || userUniversities.Count == 0) return;

        try
        {
            EnsureConnectionOpen(conn);

            var records = userUniversities
                .Where(userUniversity => !string.IsNullOrWhiteSpace(userUniversity.UserLink))
                .GroupBy(userUniversity => (userUniversity.UserLink, userUniversity.UniversityHabrId))
                .Select(group =>
                {
                    var first = group.First();
                    var coursesJson = first.Courses != null && first.Courses.Count > 0
                        ? System.Text.Json.JsonSerializer.Serialize(first.Courses)
                        : null;

                    return new
                    {
                        user_link = first.UserLink,
                        university_habr_id = first.UniversityHabrId,
                        courses = coursesJson,
                        description = first.Description
                    };
                })
                .ToArray();

            if (records.Length == 0) return;

            var recordsJson = System.Text.Json.JsonSerializer.Serialize(records);

            using var cmd = new NpgsqlCommand(@"
                WITH input_rows AS (
                    SELECT user_link, university_habr_id, courses::jsonb AS courses, description
                    FROM jsonb_to_recordset(@user_universities::jsonb)
                         AS r(user_link text, university_habr_id int, courses jsonb, description text)
                    WHERE user_link IS NOT NULL
                      AND university_habr_id IS NOT NULL
                ),
                users AS (
                    SELECT hr.id AS user_id, ir.user_link
                    FROM input_rows ir
                    JOIN habr_resumes hr ON hr.link = ir.user_link
                ),
                universities AS (
                    SELECT hu.id AS university_id, ir.university_habr_id
                    FROM input_rows ir
                    JOIN habr_universities hu ON hu.habr_id = ir.university_habr_id
                ),
                joined_rows AS (
                    SELECT
                        u.user_id,
                        univ.university_id,
                        ir.courses,
                        ir.description
                    FROM input_rows ir
                    JOIN users u ON u.user_link = ir.user_link
                    JOIN universities univ ON univ.university_habr_id = ir.university_habr_id
                ),
                missing_users AS (
                    SELECT DISTINCT ir.user_link
                    FROM input_rows ir
                    LEFT JOIN users u ON u.user_link = ir.user_link
                    WHERE u.user_id IS NULL
                ),
                missing_universities AS (
                    SELECT DISTINCT ir.university_habr_id
                    FROM input_rows ir
                    LEFT JOIN universities univ ON univ.university_habr_id = ir.university_habr_id
                    WHERE univ.university_id IS NULL
                ),
                upserted AS (
                    INSERT INTO habr_resumes_universities (user_id, university_id, courses, description, created_at, updated_at)
                    SELECT user_id, university_id, courses, description, NOW(), NOW()
                    FROM joined_rows
                    ON CONFLICT (user_id, university_id)
                    DO UPDATE SET
                        courses = COALESCE(EXCLUDED.courses, habr_resumes_universities.courses),
                        description = COALESCE(EXCLUDED.description, habr_resumes_universities.description),
                        updated_at = NOW()
                    RETURNING user_id, university_id
                )
                SELECT
                    (SELECT COUNT(*)::int FROM upserted) AS upserted_count,
                    (SELECT COUNT(*)::int FROM missing_users) AS missing_users_count,
                    (SELECT COUNT(*)::int FROM missing_universities) AS missing_universities_count,
                    (SELECT COUNT(*)::int FROM input_rows) AS input_count", conn);

            cmd.Parameters.AddWithValue("@user_universities", recordsJson);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                Log("[DB] ResumesUniversities: ? ERROR - запрос не вернул результат");
                return;
            }

            var upsertedCount = reader.GetInt32(0);
            var missingUsersCount = reader.GetInt32(1);
            var missingUniversitiesCount = reader.GetInt32(2);
            var inputCount = reader.GetInt32(3);

            if (upsertedCount > 0)
            {
                _statistics.RecordUpdate("habr_resumes_universities", $"{upsertedCount}");
            }

            if (missingUsersCount > 0)
            {
                Log($"[DB] Пропущено связей пользователь-университет: не найдено пользователей={missingUsersCount}");
            }

            if (missingUniversitiesCount > 0)
            {
                Log($"[DB] Пропущено связей пользователь-университет: не найдено университетов={missingUniversitiesCount}");
            }

            Log($"[DB] Связи пользователь-университет: input={inputCount}, upsert={upsertedCount}, missing_users={missingUsersCount}, missing_universities={missingUniversitiesCount}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при сохранении связей пользователь-университет: {ex.Message}");
        }
    }

    /// <summary>
    /// Вставить запись дополнительного образования в БД одним SQL-запросом.
    /// При DeleteExisting=true сначала удаляет старые записи пользователя, затем вставляет новую.
    /// </summary>
    private void ResumesEducationsInsert(NpgsqlConnection conn, AdditionalEducationRecord data)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        try
        {
            EnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                WITH target_resume AS (
                    SELECT id
                    FROM habr_resumes
                    WHERE link = @link
                    LIMIT 1
                ),
                deleted AS (
                    DELETE FROM habr_resumes_educations e
                    USING target_resume r
                    WHERE e.resume_id = r.id
                      AND @delete_existing
                    RETURNING e.resume_id
                ),
                inserted AS (
                    INSERT INTO habr_resumes_educations (resume_id, title, course, duration, created_at, updated_at)
                    SELECT id, @title, @course, @duration, NOW(), NOW()
                    FROM target_resume
                    ON CONFLICT DO NOTHING
                    RETURNING resume_id, title
                )
                SELECT
                    (SELECT id FROM target_resume) AS resume_id,
                    (SELECT COUNT(*)::int FROM deleted) AS deleted_count,
                    (SELECT resume_id FROM inserted) AS inserted_resume_id,
                    (SELECT title FROM inserted) AS inserted_title", conn);

            cmd.Parameters.AddWithValue("@link", data.UserLink);
            cmd.Parameters.AddWithValue("@title", data.Title);
            cmd.Parameters.AddWithValue("@course", data.Course ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@duration", data.Duration ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@delete_existing", data.DeleteExisting);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                Log($"[DB] Дополнительное образование {data.UserLink}: ? ERROR - запрос не вернул результат");
                return;
            }

            var resumeId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            var deletedCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            var insertedResumeId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
            var insertedTitle = reader.IsDBNull(3) ? null : reader.GetString(3);

            if (!resumeId.HasValue)
            {
                Log($"[DB] Пользователь не найден для дополнительного образования: {data.UserLink}");
                return;
            }

           if (deletedCount > 0)
           {
               Log($"[DB] Дополнительное образование: resume_id={resumeId}: удалено старых записей={deletedCount}");
           }

            if (insertedResumeId.HasValue)
            {
                _statistics.RecordInsert("habr_resumes_educations", $"{insertedResumeId}-{insertedTitle}");
                Log($"[DB] Дополнительное образование: resume_id={insertedResumeId}, title={insertedTitle}: ? INSERT");
            }
            else
            {
                _statistics.RecordSkipped("habr_resumes_educations", $"{resumeId}-{data.Title}");
                Log($"[DB] Дополнительное образование: resume_id={resumeId}, title={data.Title}: ? SKIPPED");
            }
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при сохранении дополнительного образования для {data.UserLink}: {ex.Message}");
        }
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

            // SQL вынесен в sql/cleanup_404_pages.sql:
            // CREATE OR REPLACE FUNCTION cleanup_404_resumes() RETURNS INTEGER
            using var cmd = new NpgsqlCommand("SELECT cleanup_404_resumes()", conn);
            var result = cmd.ExecuteScalar();
            int deleted = result is null ? 0 : Convert.ToInt32(result);

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

}
