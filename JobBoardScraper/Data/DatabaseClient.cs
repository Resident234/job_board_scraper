using System;
using System.Reflection;
using System.Data;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using JobBoardScraper.Domain.Models;
using JobBoardScraper.Infrastructure.Statistics;
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
    University
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
/// Entity
/// 
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
    List<AdditionalEducationRecord>? AdditionalEducations = null,
    bool? IsDeleted = null,
    string? About = null);

/// <summary>
/// Entity
/// 
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
    List<CompanyReviewRecord>? ReviewRecords = null,
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
/// Additional education record type.
/// </summary>
public readonly record struct AdditionalEducationRecord(
    string UserLink,
    string Title,
    string? Course = null,
    string? Duration = null);


/// <summary>
/// Data structure for Company Review record type.
/// </summary>
public readonly record struct CompanyReviewRecord(
    string CompanyCode,
    string ReviewHash,
    string ReviewText);

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
    UniversityRecord University,
    List<CourseData>? Courses = null,
    string? Description = null);


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
    UniversityRecord? University = null);

public sealed class DatabaseClient
{
    private readonly string _connectionString;
    private Task? _dbWriterTask;
    private CancellationTokenSource? _writerCts;
    private ConcurrentQueue<DbRecord>? _saveQueue;
    private readonly ConsoleLogger? _logger;

    private const string DbInsertIcon = "✅";
    private const string DbUpdateIcon = "↻";
    private const string DbErrorIcon = "❌";
    private const string DbSkipIcon = "⏭";
    private const string DbInfoIcon = "ℹ";
    private const string DbDeleteIcon = "🗑";

    
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

    private void LogError(string entity, string entityName, string errorText)
    {
        if (string.IsNullOrEmpty(entityName))
            Log($"[DB] {entity}: {DbErrorIcon} ERROR - {errorText}");
        else
            Log($"[DB] {entity} {entityName}: {DbErrorIcon} ERROR - {errorText}");
    }

    private void LogInsert(string entity, string entityName, string id)
    {
        if (string.IsNullOrEmpty(entityName))
            Log($"[DB] {entity}: {DbInsertIcon} INSERT (id={id})");
        else
            Log($"[DB] {entity} {entityName}: {DbInsertIcon} INSERT (id={id})");
    }

    private void LogUpdate(string entity, string entityName, string id)
    {
        if (string.IsNullOrEmpty(entityName))
            Log($"[DB] {entity}: {DbUpdateIcon} UPDATE (id={id})");
        else
            Log($"[DB] {entity} {entityName}: {DbUpdateIcon} UPDATE (id={id})");
    }

    private void LogEnqueue(string recordType, object? record)
    {
        Log($"[DB Queue] {recordType}: {FormatRecord(record)}");
    }

    /// <summary>
    /// Логирует факт удаления записей в едином формате с иконкой 🗑.
    /// Все формирование сообщения (entityLabel, сущность, описание удалённого и количество)
    /// выполняется внутри обёртки. Снаружи передаётся заголовок сущности,
    /// текстовое описание того, что удалено, и количество удалённых записей.
    /// </summary>
    /// <param name="entityLabel">Текст после префикса "[DB] " и до двоеточия, например "UserSkills habr_user" или "Дополнительное образование".</param>
    /// <param name="deletedDescription">Краткое описание того, что удалено (например "старых связей", "старых записей", "записей").</param>
    /// <param name="count">Количество удалённых записей.</param>
    /// <param name="fields">Пары (имя поля, значение). Поля со значением null пропускаются.</param>
    private void LogDelete(string entityLabel, string deletedDescription, int count, params (string Name, object? Value)[] fields)
    {
        var parts = new List<string> { $"[DB] {entityLabel}: {DbDeleteIcon} удалено {deletedDescription}={count}" };

        foreach (var (name, value) in fields)
        {
            if (value is null)
                continue;

            parts.Add(FormatLogField(name, value));
        }

        Log(string.Join(" | ", parts));
    }

    /// <summary>
    /// Логирует количественный результат загрузки/обработки в едином формате.
    /// Например: "Загружено N company_id из БД" или "Пропущено N связей ...".
    /// </summary>
    /// <param name="action">Действие в прошедшем времени (например, "Загружено", "Пропущено", "Добавлено").</param>
    /// <param name="count">Количество элементов.</param>
    /// <param name="entityLabel">Описание того, что считается (например, "company_id", "компаний").</param>
    /// <param name="suffix">Опциональный суффикс сообщения (например, " из БД", " пользователей").</param>
    private void LogCount(string action, int count, string entityLabel, string suffix = "")
    {
        Log($"[DB] {action} {count} {entityLabel}{suffix}");
    }

    /// <summary>
    /// Логирует уже сформированное сообщение из нескольких частей.
    /// Используется в местах, где список частей собирается вручную и затем
    /// передаётся в лог целиком.
    /// </summary>
    private void LogParts(string message)
    {
        Log(message);
    }

    /// <summary>
    /// Логирует событие фоновой задачи DB Writer (запуск, остановка, ошибки и т.п.).
    /// Снаружи передаётся категория события и текст сообщения.
    /// </summary>
    /// <param name="eventName">Имя события, например "запущена", "остановлена", "ошибка".</param>
    /// <param name="message">Текст сообщения (например, текст исключения или описание действия).</param>
    /// <param name="fields">Опциональные пары (имя поля, значение) для деталей — null-значения пропускаются.</param>
    private void LogWriter(string eventName, string message, params (string Name, object? Value)[] fields)
    {
        var parts = new List<string> { $"[DB Writer] {eventName}: {message}" };

        foreach (var (name, value) in fields)
        {
            if (value is null)
                continue;

            parts.Add(FormatLogField(name, value));
        }

        Log(string.Join(" | ", parts));
    }

    /// <summary>
    /// Логирует SKIP-операцию (пропуск записи) с подробным списком непустых полей.
    /// Все проверки на null и форматирование значений выполняются внутри обёртки.
    /// Снаружи передаётся только заголовок сущности, причина пропуска и пары (имя поля, значение).
    /// </summary>
    /// <param name="entityLabel">Текст после префикса "[DB] " и до двоеточия, например "Resume habr_user".</param>
    /// <param name="reason">Краткое описание причины пропуска (например "404 страница", "уже существует").</param>
    /// <param name="fields">Пары (имя поля, значение). Поля со значением null пропускаются.</param>
    private void LogSkip(string entityLabel, string reason, params (string Name, object? Value)[] fields)
    {
        var parts = new List<string> { $"[DB] {entityLabel}: {DbSkipIcon} SKIP ({reason})" };

        foreach (var (name, value) in fields)
        {
            if (value is null)
                continue;

            parts.Add(FormatLogField(name, value));
        }

        Log(string.Join(" | ", parts));
    }

    /// <summary>
    /// Логирует INSERT/UPDATE операцию с подробным списком непустых полей.
    /// Все проверки на null, форматирование значений и выбор иконки INSERT/UPDATE
    /// выполняются внутри обёртки. Снаружи передаётся только заголовок сущности,
    /// флаг isInsert и пары (имя поля, значение).
    /// </summary>
    /// <param name="entityLabel">Текст после префикса "[DB] " и до двоеточия, например "Компания habr_company".</param>
    /// <param name="isInsert">true для INSERT (иконка ✅), false для UPDATE (иконка ↻).</param>
    /// <param name="fields">Пары (имя поля, значение). Поля со значением null пропускаются.</param>
    private void LogParts(string entityLabel, bool isInsert, params (string Name, object? Value)[] fields)
    {
        var parts = new List<string> { $"[DB] {entityLabel}:" };

            foreach (var (name, value) in fields)
            {
            if (value is null)
                continue;

            parts.Add(FormatLogField(name, value));
            }

        parts.Add(isInsert ? $"{DbInsertIcon} INSERT" : $"{DbUpdateIcon} UPDATE");

        Log(string.Join(" | ", parts));
    }

    /// <summary>
    /// Форматирует одно поле для логирования. Ожидается, что value уже не null.
    /// Поддерживает: ICollection (логируется количество элементов),
    /// decimal/double/float (формат F2), string (с обрезкой до 50 символов),
    /// остальные типы — через ToString().
    /// </summary>
    private static string FormatLogField(string name, object value)
    {
        if (value is System.Collections.ICollection collection)
            return $"{name}={collection.Count}";

        if (value is decimal d)
            return $"{name}={d.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";

        if (value is double db)
            return $"{name}={db.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";

        if (value is float f)
            return $"{name}={f.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";

        if (value is string s)
        {
            var preview = s.Length > 50 ? s.Substring(0, 50) + "..." : s;
            return $"{name}={preview}";
        }

        return $"{name}={value}";
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
            LogWriter("запущена", "Фоновая задача записи в БД запущена");
            var lastQueueSizeLog = DateTime.MinValue;
            try
            {
                while (!linkedToken.IsCancellationRequested)
                {
                    var queueSize = _saveQueue.Count;

                    // Логируем размер очереди каждые 30 секунд
                    if ((DateTime.Now - lastQueueSizeLog).TotalSeconds >= 30)
                    {
                        LogWriter("очередь", $"Размер очереди: {queueSize}");
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

                                        // Если есть записи дополнительного образования, добавляем их через ResumeRecord
                                        if (resume.AdditionalEducations != null && resume.AdditionalEducations.Count > 0)
                                        {
                                            ResumesEducationsInsert(conn, resume.AdditionalEducations);
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
                                        if (company.ReviewRecords != null && company.ReviewRecords.Count > 0)
                                        {
                                            var companyInternalId = CompaniesGetInternalId(conn, company.CompanyCode);
                                            if (companyInternalId.HasValue)
                                            {
                                                CompanyReviewsInsert(conn, companyInternalId.Value, company.ReviewRecords);
                                            }
                                            else
                                            {
                                                LogSkip($"Отзывы для компании {company.CompanyCode}", "компания не найдена в БД");
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
                                        var experience = record.UserExperience.Value;
                                        var experienceId = UserExperienceInsert(
                                            conn,
                                            experience.UserLink,
                                            experience.CompanyCode,
                                            experience.CompanyUrl,
                                            experience.CompanyTitle,
                                            experience.CompanyAbout,
                                            experience.CompanySize,
                                            experience.Position,
                                            experience.Duration,
                                            experience.Description,
                                            experience.IsFirstRecord);

                                        if (experienceId.HasValue && experience.Skills != null && experience.Skills.Count > 0)
                                        {
                                            UserExperienceSkillsInsert(conn, experienceId.Value, experience.Skills);
                                        }
                                    }
                                    break;
                                case DbRecordType.University:
                                    if (record.University.HasValue)
                                    {
                                        var university = record.University.Value;
                                        UniversitiesInsert(
                                            conn,
                                            university.HabrId,
                                            university.Name,
                                            university.City,
                                            university.GraduateCount);
                                    }
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogWriter("ошибка обработки", ex.Message, ("RecordType", record.Type.ToString()), ("StackTrace", ex.StackTrace));
                            // Продолжаем обработку следующих записей
                        }
                    }

                    await Task.Delay(delayMs, linkedToken);
                }
            }
            catch (OperationCanceledException)
            {
                LogWriter("остановлена", "Фоновая задача записи в БД остановлена по запросу");
            }
            catch (Exception ex)
            {
                LogWriter("критическая ошибка", ex.Message, ("StackTrace", ex.StackTrace));
            }
            finally
            {
                LogWriter("завершена", "Фоновая задача записи в БД завершена");
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
                    LogWriter("ошибка остановки", ex.Message, ("StackTrace", ex.StackTrace));
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
            LogWriter("ошибка задачи", _dbWriterTask.Exception?.Message ?? "неизвестная ошибка", ("StackTrace", _dbWriterTask.Exception?.StackTrace));
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
        List<AdditionalEducationRecord>? additionalEducations = null,
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
            AdditionalEducations: additionalEducations,
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
    /// Добавить резюме в очередь на запись в базу данных, используя готовый ResumeRecord.
    /// Удобно для парсеров, которые формируют ResumeRecord целиком (например, ResumeListPageScraper).
    /// </summary>
    public bool EnqueueResume(ResumeRecord resumeRecord)
    {
        if (_saveQueue == null) return false;
        if (string.IsNullOrWhiteSpace(resumeRecord.Link)) return false;

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
        List<CompanyReviewRecord>? reviewRecords = null,
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
            ReviewRecords: reviewRecords,
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

        try
        {
            EnsureConnectionOpen(conn);
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO habr_levels (title, created_at, updated_at)
                VALUES (@title, NOW(), NOW())
                ON CONFLICT (title) DO UPDATE SET title = EXCLUDED.title, updated_at = NOW()
                RETURNING id, xmax", conn);
            cmd.Parameters.AddWithValue("@title", levelTitle);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                LogError("Level", levelTitle, "запрос не вернул результат");
                _statistics.RecordError("habr_levels", levelTitle);
                return null;
            }

            var levelId = reader.GetInt32(0);
            var xmax = Convert.ToUInt32(reader.GetValue(1));
            var isInsert = xmax == 0;

            if (isInsert)
            {
                _statistics.RecordInsert("habr_levels", $"{levelId}:{levelTitle}");
                LogInsert("Level", levelTitle, levelId.ToString());
            }
            else
            {
                _statistics.RecordUpdate("habr_levels", $"{levelId}:{levelTitle}");
                LogUpdate("Level", levelTitle, levelId.ToString());
            }

            TryDumpStatistics();
            return levelId;
        }
        catch (NpgsqlException dbEx)
        {
            LogError("LevelsInsert", levelTitle, dbEx.Message);
            _statistics.RecordError("habr_levels", levelTitle);
            TryDumpStatistics();
        }
        catch (Exception ex)
        {
            LogError("LevelsInsert", levelTitle, ex.Message);
            _statistics.RecordError("habr_levels", levelTitle);
            TryDumpStatistics();
        }

        return null;
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
                    LogSkip($"Resume {link}", "уже существует");
                    _statistics.RecordSkipped("habr_resumes", link);
                    return;
                }

                _statistics.RecordInsert("habr_resumes", link);

                // Подробное логирование: вся сборка частей, проверка на null и форматирование — внутри LogParts.
                LogParts(
                    $"Resume {link}",
                    isInsert: true,
                    ("Title", title),
                    ("Slogan", slogan),
                    ("Code", code),
                    ("Expert", expert == true ? (object)"?" : null),
                    ("Experience", workExperience),
                    ("LevelID", levelId),
                    ("InfoTech", infoTech),
                    ("Salary", salary),
                    ("LastVisit", lastVisit),
                    ("Age", age),
                    ("Registration", registration),
                    ("Citizenship", citizenship),
                    ("RemoteWork", remoteWork),
                    ("CommunityParticipation", communityParticipationJson),
                    ("Public", isPublic),
                    ("JobStatus", jobSearchStatus)
                );
                TryDumpStatistics();
            }
            else // UpdateIfExists
            {
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

                // Подробное логирование: вся сборка частей, проверка на null и форматирование — внутри LogParts.
                LogParts(
                    $"Resume {link}",
                    isInsert,
                    ("Title", title),
                    ("Slogan", slogan),
                    ("Code", code),
                    ("Expert", expert == true ? (object)"?" : null),
                    ("Experience", workExperience),
                    ("LevelID", levelId),
                    ("InfoTech", infoTech),
                    ("Salary", salary),
                    ("LastVisit", lastVisit),
                    ("Age", age),
                    ("Registration", registration),
                    ("Citizenship", citizenship),
                    ("RemoteWork", remoteWork),
                    ("CommunityParticipation", communityParticipationJson),
                    ("Public", isPublic)
                );
                TryDumpStatistics();
            }
        }
        catch (PostgresException pgEx) when
            (pgEx.SqlState == "23505") // На случай гонки: уникальное ограничение нарушено
        {
            LogSkip($"Resume {link}", "уникальное ограничение");
            _statistics.RecordSkipped("habr_resumes", link);
            TryDumpStatistics();
        }
        catch (NpgsqlException dbEx)
        {
            LogError("Resume", link, dbEx.Message);
            _statistics.RecordError("habr_resumes", link);
            TryDumpStatistics();
        }
        catch (Exception ex)
        {
            LogError("Resume", link, ex.Message);
            _statistics.RecordError("habr_resumes", link);
            TryDumpStatistics();
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
            // Передаём только isInsert, имена полей и сами значения.
            // Все проверки на null, форматирование и выбор иконки выполняются внутри LogParts.
            object? employeesValue = (currentEmployees.HasValue || pastEmployees.HasValue)
                ? $"{currentEmployees?.ToString() ?? "?"}/{pastEmployees?.ToString() ?? "?"}"
                : null;
            object? followersValue = (followers.HasValue || wantWork.HasValue)
                ? $"{followers?.ToString() ?? "?"}/{wantWork?.ToString() ?? "?"}"
                : null;

            LogParts(
                $"Компания {companyCode}",
                isInsert,
                ("URL", companyUrl),
                ("Title", companyTitle),
                ("CompanyID", companyId),
                ("About", companyAbout),
                ("Description", companyDescription),
                ("Site", companySite),
                ("Rating", companyRating),
                ("Employees", employeesValue),
                ("Followers", followersValue),
                ("Size", employeesCount),
                ("Habr", habr),
                ("City", city),
                ("Awards", awards),
                ("Scores", scores)
            );

            TryDumpStatistics();

            return internalId;
        }
        catch (NpgsqlException dbEx)
        {
            LogError("Компания", companyCode, dbEx.Message);
            _statistics.RecordError("habr_companies", companyCode);
            TryDumpStatistics();
            return null;
        }
        catch (Exception ex)
        {
            LogError("Компания", companyCode, ex.Message);
            _statistics.RecordError("habr_companies", companyCode);
            TryDumpStatistics();
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

            var categoryEntity = $"{categoryId} -> {categoryName}";

            if (isInsert)
            {
                _statistics.RecordInsert("habr_category_root_ids", categoryId);
                LogInsert("Category", categoryEntity, categoryId);
            }
            else
            {
                _statistics.RecordUpdate("habr_category_root_ids", categoryId);
                LogUpdate("Category", categoryEntity, categoryId);
            }

            TryDumpStatistics();
        }
        catch (NpgsqlException dbEx)
        {
            LogError("Category", categoryId, dbEx.Message);
            _statistics.RecordError("habr_category_root_ids", categoryId);
            TryDumpStatistics();
        }
        catch (Exception ex)
        {
            LogError("Category", categoryId, ex.Message);
            _statistics.RecordError("habr_category_root_ids", categoryId);
            TryDumpStatistics();
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

            LogCount("Загружено", companyIds.Count, "company_id", " из БД");
        }
        catch (Exception ex)
        {
            LogError("CompaniesGetAllIds", "", ex.Message);
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

            LogCount("Загружено", universityIds.Count, "university_id", " из БД");
        }
        catch (Exception ex)
        {
            LogError("UniversitiesGetAllIds", "", ex.Message);
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

            LogCount("Загружено", categoryIds.Count, "категорий", " из БД");
        }
        catch (Exception ex)
        {
            LogError("CategoryGetAllIds", "", ex.Message);
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

            LogCount("Загружено", companyCodes.Count, "компаний", " из БД");
        }
        catch (Exception ex)
        {
            LogError("CompaniesGetAllCodes", "", ex.Message);
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

            LogCount("Загружено", companies.Count, "компаний с URL", " из БД");
        }
        catch (Exception ex)
        {
            LogError("CompaniesGetAll", "", ex.Message);
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
                    RETURNING id, xmax
                ),
                linked AS (
                    INSERT INTO habr_company_skills (company_id, skill_id, created_at, updated_at)
                    SELECT c.id, s.id, NOW(), NOW()
                    FROM company c
                    CROSS JOIN upserted_skills s
                    ON CONFLICT (company_id, skill_id) DO UPDATE SET updated_at = NOW()
                    RETURNING company_id, skill_id, xmax
                )
                SELECT
                    (SELECT id FROM company) AS company_id,
                    (SELECT COUNT(*)::int FROM linked) AS linked_count,
                    (SELECT COUNT(*)::int FROM linked WHERE xmax = 0) AS linked_inserted_count,
                    (SELECT COUNT(*)::int FROM linked WHERE xmax <> 0) AS linked_updated_count,
                    (SELECT COUNT(*)::int FROM upserted_skills) AS upserted_skills_count,
                    (SELECT COUNT(*)::int FROM upserted_skills WHERE xmax = 0) AS inserted_skills_count,
                    (SELECT COUNT(*)::int FROM upserted_skills WHERE xmax <> 0) AS updated_skills_count,
                    (SELECT COUNT(*)::int FROM deleted) AS deleted_count", conn);

            cmd.Parameters.AddWithValue("@company_code", companyCode);
            cmd.Parameters.AddWithValue("@titles", string.Join("\n", skillTitles));

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                LogError("CompanySkills", companyCode, "запрос не вернул результат");
                _statistics.RecordError("habr_company_skills", companyCode);
                TryDumpStatistics();
                return;
            }

            var companyId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            var linkedCount = reader.GetInt32(1);
            var linkedInsertedCount = reader.GetInt32(2);
            var linkedUpdatedCount = reader.GetInt32(3);
            var upsertedSkillsCount = reader.GetInt32(4);
            var insertedSkillsCount = reader.GetInt32(5);
            var updatedSkillsCount = reader.GetInt32(6);
            var deletedCount = reader.GetInt32(7);

            if (!companyId.HasValue)
            {
                LogSkip($"CompanySkills {companyCode}", "компания не найдена в БД");
                _statistics.RecordSkipped("habr_company_skills", companyCode);
                TryDumpStatistics();
                return;
            }

            if (deletedCount > 0)
            {
                _statistics.RecordDelete("habr_company_skills", $"{companyId}-{deletedCount}");
                LogDelete($"CompanySkills {companyCode}", "старых связей", deletedCount, ("CompanyID", companyId));
            }

            if (insertedSkillsCount > 0)
            {
                _statistics.RecordInsert("habr_skills", $"{insertedSkillsCount} навыков для компании {companyCode}");
                LogInsert($"CompanySkills {companyCode} → habr_skills", "навыков", $"{insertedSkillsCount} (title=...)");
            }

            if (updatedSkillsCount > 0)
            {
                _statistics.RecordUpdate("habr_skills", $"{updatedSkillsCount} навыков для компании {companyCode}");
                LogUpdate($"CompanySkills {companyCode} → habr_skills", "навыков", $"{updatedSkillsCount} (title=...)");
            }

            if (linkedInsertedCount > 0)
            {
                _statistics.RecordInsert("habr_company_skills", companyCode);
                LogInsert($"CompanySkills {companyCode}", "связей", $"{linkedInsertedCount} (company_id={companyId})");
            }

            if (linkedUpdatedCount > 0)
            {
                _statistics.RecordUpdate("habr_company_skills", companyCode);
                LogUpdate($"CompanySkills {companyCode}", "связей", $"{linkedUpdatedCount} (company_id={companyId})");
            }

            TryDumpStatistics();
        }
        catch (NpgsqlException dbEx)
        {
            LogError("CompanySkills", companyCode, dbEx.Message);
            _statistics.RecordError("habr_company_skills", companyCode);
            TryDumpStatistics();
        }
        catch (Exception ex)
        {
            LogError("CompanySkills", companyCode, ex.Message);
            _statistics.RecordError("habr_company_skills", companyCode);
            TryDumpStatistics();
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

            LogCount("Загружено", userCodes.Count, "кодов пользователей", " из БД");
        }
        catch (Exception ex)
        {
            LogError("ResumesGetAllUserCodes", "", ex.Message);
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
            LogCount("Загружено", userLinks.Count, "ссылок пользователей", $" из БД{filterText}");
        }
        catch (Exception ex)
        {
            LogError("ResumesGetAllUserLinks", "", ex.Message);
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

            LogCount("Загружено", userLinks.Count, "ссылок пользователей без данных", " из БД");
        }
        catch (Exception ex)
        {
            LogError("ResumesGetUserLinksWithoutData", "", ex.Message);
        }

        return userLinks;
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
                    RETURNING id, xmax
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
                    RETURNING user_id, skill_id, xmax
                )
                SELECT
                    (SELECT id FROM target_user) AS user_id,
                    (SELECT COUNT(*)::int FROM linked) AS linked_count,
                    (SELECT COUNT(*)::int FROM linked WHERE xmax = 0) AS linked_inserted_count,
                    (SELECT COUNT(*)::int FROM linked WHERE xmax <> 0) AS linked_updated_count,
                    (SELECT COUNT(*)::int FROM upserted_skills) AS upserted_skills_count,
                    (SELECT COUNT(*)::int FROM upserted_skills WHERE xmax = 0) AS inserted_skills_count,
                    (SELECT COUNT(*)::int FROM upserted_skills WHERE xmax <> 0) AS updated_skills_count,
                    (SELECT COUNT(*)::int FROM deleted) AS deleted_count", conn);

            cmd.Parameters.AddWithValue("@user_link", userLink);
            cmd.Parameters.AddWithValue("@titles", string.Join("\n", skillTitles));

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                LogError("UserSkills", userLink, "запрос не вернул результат");
                _statistics.RecordError("habr_user_skills", userLink);
                TryDumpStatistics();
                return;
            }

            var userId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            var linkedCount = reader.GetInt32(1);
            var linkedInsertedCount = reader.GetInt32(2);
            var linkedUpdatedCount = reader.GetInt32(3);
            var upsertedSkillsCount = reader.GetInt32(4);
            var insertedSkillsCount = reader.GetInt32(5);
            var updatedSkillsCount = reader.GetInt32(6);
            var deletedCount = reader.GetInt32(7);

            if (!userId.HasValue)
            {
                LogSkip($"UserSkills {userLink}", "пользователь не найден в БД");
                _statistics.RecordSkipped("habr_user_skills", userLink);
                TryDumpStatistics();
                return;
            }

            if (deletedCount > 0)
            {
                _statistics.RecordDelete("habr_user_skills", $"{userId}-{deletedCount}");
                LogDelete($"UserSkills {userLink}", "старых связей", deletedCount, ("UserID", userId));
            }

            if (insertedSkillsCount > 0)
            {
                _statistics.RecordInsert("habr_skills", $"{insertedSkillsCount} навыков для {userLink}");
                LogInsert($"UserSkills {userLink} → habr_skills", "навыков", $"{insertedSkillsCount} (title=...)");
            }

            if (updatedSkillsCount > 0)
            {
                _statistics.RecordUpdate("habr_skills", $"{updatedSkillsCount} навыков для {userLink}");
                LogUpdate($"UserSkills {userLink} → habr_skills", "навыков", $"{updatedSkillsCount} (title=...)");
            }

            if (linkedInsertedCount > 0)
            {
                _statistics.RecordInsert("habr_user_skills", $"{userId}-{userLink}");
                LogInsert($"UserSkills {userLink}", "связей", $"{linkedInsertedCount} (user_id={userId})");
            }

            if (linkedUpdatedCount > 0)
            {
                _statistics.RecordUpdate("habr_user_skills", $"{userId}-{userLink}");
                LogUpdate($"UserSkills {userLink}", "связей", $"{linkedUpdatedCount} (user_id={userId})");
            }

            TryDumpStatistics();
        }
        catch (NpgsqlException dbEx)
        {
            LogError("UserSkills", userLink, dbEx.Message);
            _statistics.RecordError("habr_user_skills", userLink);
            TryDumpStatistics();
        }
        catch (Exception ex)
        {
            LogError("UserSkills", userLink, ex.Message);
            _statistics.RecordError("habr_user_skills", userLink);
            TryDumpStatistics();
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
    public int? UserExperienceInsert(
        NpgsqlConnection conn,
        string userLink,
        string? companyCode = null,
        string? companyUrl = null,
        string? companyTitle = null,
        string? companyAbout = null,
        string? companySize = null,
        string? position = null,
        string? duration = null,
        string? description = null,
        bool isFirstRecord = false)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));

        try
        {
            EnsureConnectionOpen(conn);

            var hasCompany = !string.IsNullOrWhiteSpace(companyCode);

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
                )
                SELECT
                    (SELECT id FROM target_user) AS user_id,
                    (SELECT id FROM company_id) AS company_id,
                    (SELECT id FROM inserted_experience) AS experience_id,
                    (SELECT COUNT(*)::int FROM deleted_experiences) AS deleted_experiences_count,
                    (SELECT COUNT(*)::int FROM updated_company) AS updated_company_count,
                    (SELECT COUNT(*)::int FROM inserted_company) AS inserted_company_count", conn);

            cmd.Parameters.AddWithValue("@user_link", userLink);
            cmd.Parameters.AddWithValue("@has_company", hasCompany);
            cmd.Parameters.AddWithValue("@company_code", hasCompany ? companyCode! : DBNull.Value);
            cmd.Parameters.AddWithValue("@company_url", companyUrl ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@company_title", companyTitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@company_about", companyAbout ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@company_employees_count", companySize ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@delete_old_experiences", isFirstRecord);
            cmd.Parameters.AddWithValue("@position", position ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@duration", duration ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                LogError("UserExperience", userLink, "запрос не вернул результат");
                _statistics.RecordError("habr_user_experience", userLink);
                TryDumpStatistics();
                return null;
            }

            var userId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            var companyId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
            var experienceId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
            var deletedExperiencesCount = reader.GetInt32(3);
            var updatedCompanyCount = reader.GetInt32(4);
            var insertedCompanyCount = reader.GetInt32(5);

            if (!userId.HasValue)
            {
                LogSkip($"UserExperience {userLink}", "пользователь не найден в БД");
                _statistics.RecordSkipped("habr_user_experience", userLink);
                TryDumpStatistics();
                return null;
            }

            if (!experienceId.HasValue)
            {
                LogError("UserExperience", userLink, "опыт работы не вставлен");
                _statistics.RecordError("habr_user_experience", userLink);
                TryDumpStatistics();
                return null;
            }

            if (deletedExperiencesCount > 0)
            {
                _statistics.RecordDelete("habr_user_experience", $"{userId}-{deletedExperiencesCount}");
                LogDelete($"UserExperience {userLink}", "старых записей опыта", deletedExperiencesCount, ("UserID", userId));
            }

            if (insertedCompanyCount > 0)
            {
                _statistics.RecordInsert("habr_companies", companyCode ?? companyId.ToString());
                LogInsert($"UserExperience {userLink} → habr_companies", "компаний", $"{insertedCompanyCount} (code={companyCode ?? "?"})");
            }

            if (updatedCompanyCount > 0)
            {
                _statistics.RecordUpdate("habr_companies", companyCode ?? companyId.ToString());
                LogUpdate($"UserExperience {userLink} → habr_companies", "компаний", $"{updatedCompanyCount} (code={companyCode ?? "?"})");
            }

            LogInsert($"UserExperience {userLink}", "опыт работы", $"experience_id={experienceId}");
            _statistics.RecordInsert("habr_user_experience", $"{userId}-{companyTitle}");
            TryDumpStatistics();

            return experienceId;
        }
        catch (NpgsqlException dbEx)
        {
            LogError("UserExperience", $"{userLink} (БД)", dbEx.Message);
            _statistics.RecordError("habr_user_experience", userLink);
            TryDumpStatistics();
            return null;
        }
        catch (Exception ex)
        {
            LogError("UserExperience", $"{userLink} (неожиданная)", ex.Message);
            _statistics.RecordError("habr_user_experience", userLink);
            TryDumpStatistics();
            return null;
        }
    }

    public void UserExperienceSkillsInsert(NpgsqlConnection conn, int experienceId, List<SkillsRecord> skills)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
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

            if (skillTitles.Length == 0) return;

            using var cmd = new NpgsqlCommand(@"
                WITH dedup_skills AS (
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
                    RETURNING id, xmax
                ),
                linked AS (
                    INSERT INTO habr_user_experience_skills (experience_id, skill_id, created_at, updated_at)
                    SELECT @experience_id, s.id, NOW(), NOW()
                    FROM upserted_skills s
                    ON CONFLICT (experience_id, skill_id) DO UPDATE SET updated_at = NOW()
                    RETURNING experience_id, skill_id, xmax
                )
                SELECT
                    (SELECT COUNT(*)::int FROM linked) AS linked_count,
                    (SELECT COUNT(*)::int FROM linked WHERE xmax = 0) AS linked_inserted_count,
                    (SELECT COUNT(*)::int FROM linked WHERE xmax <> 0) AS linked_updated_count,
                    (SELECT COUNT(*)::int FROM upserted_skills) AS upserted_skills_count,
                    (SELECT COUNT(*)::int FROM upserted_skills WHERE xmax = 0) AS inserted_skills_count,
                    (SELECT COUNT(*)::int FROM upserted_skills WHERE xmax <> 0) AS updated_skills_count", conn);

            cmd.Parameters.AddWithValue("@experience_id", experienceId);
            cmd.Parameters.AddWithValue("@titles", string.Join("\n", skillTitles));

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                LogError("UserExperienceSkills", experienceId.ToString(), "запрос не вернул результат");
                _statistics.RecordError("habr_user_experience_skills", experienceId.ToString());
                TryDumpStatistics();
                return;
            }

            var linkedCount = reader.GetInt32(0);
            var linkedInsertedCount = reader.GetInt32(1);
            var linkedUpdatedCount = reader.GetInt32(2);
            var upsertedSkillsCount = reader.GetInt32(3);
            var insertedSkillsCount = reader.GetInt32(4);
            var updatedSkillsCount = reader.GetInt32(5);

            if (linkedInsertedCount > 0)
            {
                _statistics.RecordInsert("habr_user_experience_skills", $"{experienceId}");
                LogInsert($"UserExperienceSkills {experienceId}", "связей", $"{linkedInsertedCount}");
            }

            if (linkedUpdatedCount > 0)
            {
                _statistics.RecordUpdate("habr_user_experience_skills", $"{experienceId}");
                LogUpdate($"UserExperienceSkills {experienceId}", "связей", $"{linkedUpdatedCount}");
            }

            if (insertedSkillsCount > 0)
            {
                _statistics.RecordInsert("habr_skills", $"{insertedSkillsCount} навыков для опыта ID={experienceId}");
                LogInsert($"UserExperienceSkills {experienceId} → habr_skills", "навыков", $"{insertedSkillsCount}");
            }

            if (updatedSkillsCount > 0)
            {
                _statistics.RecordUpdate("habr_skills", $"{updatedSkillsCount} навыков для опыта ID={experienceId}");
                LogUpdate($"UserExperienceSkills {experienceId} → habr_skills", "навыков", $"{updatedSkillsCount}");
            }

            TryDumpStatistics();
        }
        catch (Exception ex)
        {
            LogError("UserExperienceSkills", experienceId.ToString(), ex.Message);
            _statistics.RecordError("habr_user_experience_skills", experienceId.ToString());
            TryDumpStatistics();
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
                LogError("SkillsInsert", skillId.ToString(), "запрос не вернул результат");
                _statistics.RecordError("habr_skills", $"skill_id={skillId}");
                TryDumpStatistics();
                return;
            }

            var existingBySkillId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            var updatedByTitle = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
            var insertedId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);

            if (existingBySkillId.HasValue)
            {
                LogSkip($"SkillsInsert {skillId}", "навык уже существует", ("SkillID", skillId));
                _statistics.RecordSkipped("habr_skills", $"skill_id={skillId}");
                TryDumpStatistics();
                return;
            }

            if (updatedByTitle.HasValue)
            {
                LogParts($"SkillsInsert {skillId}", isInsert: false, ("Title", normalizedTitle), ("DBID", updatedByTitle), ("SkillID", skillId));
                _statistics.RecordUpdate("habr_skills", $"skill_id={skillId}");
                TryDumpStatistics();
                return;
            }

            if (insertedId.HasValue)
            {
                LogParts($"SkillsInsert {skillId}", isInsert: true, ("Title", normalizedTitle), ("DBID", insertedId), ("SkillID", skillId));
                _statistics.RecordInsert("habr_skills", $"skill_id={skillId}");
                TryDumpStatistics();
                return;
            }

            LogSkip($"SkillsInsert {skillId}", "навык не обработан", ("SkillID", skillId));
            _statistics.RecordSkipped("habr_skills", $"skill_id={skillId}");
            TryDumpStatistics();
        }
        catch (NpgsqlException dbEx)
        {
            LogError("SkillsInsert", skillId.ToString(), dbEx.Message);
            _statistics.RecordError("habr_skills", $"skill_id={skillId}");
            TryDumpStatistics();
        }
        catch (Exception ex)
        {
            LogError("SkillsInsert", skillId.ToString(), ex.Message);
            _statistics.RecordError("habr_skills", $"skill_id={skillId}");
            TryDumpStatistics();
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
    public void CompanyReviewsInsert(NpgsqlConnection conn, int companyId, List<CompanyReviewRecord> reviewsRecords)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (reviewsRecords == null || reviewsRecords.Count == 0) return;

        try
        {
            EnsureConnectionOpen(conn);

            var reviews = reviewsRecords
                .Where(r => !string.IsNullOrWhiteSpace(r.ReviewText))
                .Select(r => new
                {
                    review_hash = r.ReviewHash,
                    review_text = r.ReviewText
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
                LogError("CompanyReviews", companyId.ToString(), "запрос не вернул результат");
                _statistics.RecordError("habr_company_reviews", companyId.ToString());
                TryDumpStatistics();
                return;
            }

            var insertedCount = reader.GetInt32(0);
            var existingCount = reader.GetInt32(1);
            var totalCount = reader.GetInt32(2);

            if (insertedCount > 0)
            {
                _statistics.RecordInsert("habr_company_reviews", $"{companyId}:{insertedCount}");
                LogCount("Добавлено", insertedCount, "новых отзывов", $" для компании ID={companyId}");
            }

            if (existingCount > 0)
            {
                _statistics.RecordSkipped("habr_company_reviews", $"{companyId}:{existingCount}");
                LogSkip($"CompanyReviews {companyId}", "существующие отзывы", ("ExistingCount", existingCount));
            }

            if (totalCount == 0)
            {
                LogParts($"CompanyReviews {companyId}", isInsert: false, ("Info", "отзывов для обработки не найдено"));
                _statistics.RecordSkipped("habr_company_reviews", $"{companyId}:0");
            }

            TryDumpStatistics();
        }
        catch (Exception ex)
        {
            LogError("CompanyReviews", companyId.ToString(), ex.Message);
            _statistics.RecordError("habr_company_reviews", companyId.ToString());
            TryDumpStatistics();
        }
    }

    /// <summary>
    /// Вставить или обновить университет в БД
    /// </summary>
    private void UniversitiesInsert(NpgsqlConnection conn, int habrId, string name, string? city = null, int? graduateCount = null)
    {
        try
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
                    updated_at = NOW()
                RETURNING id, xmax", conn);

            cmd.Parameters.AddWithValue("@habr_id", habrId);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@city", city ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@graduate_count", graduateCount ?? (object)DBNull.Value);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                LogError("Университет", $"{name} (ID={habrId})", "запрос не вернул результат");
                _statistics.RecordError("habr_universities", habrId.ToString());
                return;
            }

            var universityId = reader.GetInt32(0);
            var xmax = Convert.ToUInt32(reader.GetValue(1));
            var isInsert = xmax == 0;

            if (isInsert)
            {
                _statistics.RecordInsert("habr_universities", $"{habrId}:{name}");
                LogParts($"Университет {name}", isInsert: true, ("HabrID", habrId), ("DBID", universityId));
            }
            else
            {
                _statistics.RecordUpdate("habr_universities", $"{habrId}:{name}");
                LogParts($"Университет {name}", isInsert: false, ("HabrID", habrId), ("DBID", universityId));
            }

            TryDumpStatistics();
        }
        catch (NpgsqlException dbEx)
        {
            LogError("UniversitiesInsert", $"{name} (ID={habrId})", dbEx.Message);
            _statistics.RecordError("habr_universities", habrId.ToString());
            TryDumpStatistics();
        }
        catch (Exception ex)
        {
            LogError("UniversitiesInsert", $"{name} (ID={habrId})", ex.Message);
            _statistics.RecordError("habr_universities", habrId.ToString());
            TryDumpStatistics();
        }
    }

    /// <summary>
    /// Вставить связи пользователь-университет в БД одним SQL-запросом.
    /// </summary>
    /// <remarks>
    /// Логика запроса:
    /// - входные записи передаются одним JSON-параметром;
    /// - записи дедуплицируются по паре user_link + university_habr_id;
    /// - пользователи находятся в habr_resumes;
    /// - университеты upsert-ятся в habr_universities (с использованием переданных данных имени, города, количества выпускников);
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
                .GroupBy(userUniversity => (userUniversity.UserLink, userUniversity.University.HabrId))
                .Select(group =>
                {
                    var first = group.First();
                    var coursesJson = first.Courses != null && first.Courses.Count > 0
                        ? System.Text.Json.JsonSerializer.Serialize(first.Courses)
                        : null;

                    return new
                    {
                        user_link = first.UserLink,
                        university_habr_id = first.University.HabrId,
                        university_name = first.University.Name,
                        university_city = first.University.City,
                        university_graduate_count = first.University.GraduateCount,
                        courses = coursesJson,
                        description = first.Description
                    };
                })
                .ToArray();

            if (records.Length == 0) return;

            var recordsJson = System.Text.Json.JsonSerializer.Serialize(records);

            using var cmd = new NpgsqlCommand(@"
                WITH input_rows AS (
                    SELECT 
                        user_link, 
                        university_habr_id, 
                        university_name,
                        university_city,
                        university_graduate_count,
                        courses::jsonb AS courses, 
                        description
                    FROM jsonb_to_recordset(@user_universities::jsonb)
                         AS r(user_link text, university_habr_id int, university_name text, university_city text, university_graduate_count int, courses jsonb, description text)
                    WHERE user_link IS NOT NULL
                      AND university_habr_id IS NOT NULL
                ),
                users AS (
                    SELECT hr.id AS user_id, ir.user_link
                    FROM input_rows ir
                    JOIN habr_resumes hr ON hr.link = ir.user_link
                ),
                upserted_universities AS (
                    INSERT INTO habr_universities (habr_id, name, city, graduate_count, created_at, updated_at)
                    SELECT DISTINCT ON (university_habr_id)
                        university_habr_id,
                        university_name,
                        university_city,
                        university_graduate_count,
                        NOW(),
                        NOW()
                    FROM input_rows
                    WHERE university_name IS NOT NULL AND btrim(university_name) <> ''
                    ON CONFLICT (habr_id)
                    DO UPDATE SET
                        name = COALESCE(EXCLUDED.name, habr_universities.name),
                        city = COALESCE(EXCLUDED.city, habr_universities.city),
                        graduate_count = COALESCE(EXCLUDED.graduate_count, habr_universities.graduate_count),
                        updated_at = NOW()
                    RETURNING id, habr_id
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
                upserted AS (
                    INSERT INTO habr_resumes_universities (user_id, university_id, courses, description, created_at, updated_at)
                    SELECT user_id, university_id, courses, description, NOW(), NOW()
                    FROM joined_rows
                    ON CONFLICT (user_id, university_id)
                    DO UPDATE SET
                        courses = COALESCE(EXCLUDED.courses, habr_resumes_universities.courses),
                        description = COALESCE(EXCLUDED.description, habr_resumes_universities.description),
                        updated_at = NOW()
                    RETURNING user_id, university_id, xmax
                )
                SELECT
                    (SELECT COUNT(*)::int FROM upserted) AS upserted_count,
                    (SELECT COUNT(*)::int FROM upserted WHERE xmax = 0) AS inserted_count,
                    (SELECT COUNT(*)::int FROM upserted WHERE xmax <> 0) AS updated_count,
                    (SELECT COUNT(*)::int FROM missing_users) AS missing_users_count,
                    (SELECT COUNT(*)::int FROM upserted_universities) AS upserted_universities_count,
                    (SELECT COUNT(*)::int FROM input_rows) AS input_count", conn);

            cmd.Parameters.AddWithValue("@user_universities", recordsJson);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                LogError("ResumesUniversities", "", "запрос не вернул результат");
                _statistics.RecordError("habr_resumes_universities", "query");
                TryDumpStatistics();
                return;
            }

            var upsertedCount = reader.GetInt32(0);
            var insertedCount = reader.GetInt32(1);
            var updatedCount = reader.GetInt32(2);
            var missingUsersCount = reader.GetInt32(3);
            var upsertedUniversitiesCount = reader.GetInt32(4);
            var inputCount = reader.GetInt32(5);

            if (insertedCount > 0)
            {
                _statistics.RecordInsert("habr_resumes_universities", $"{insertedCount}");
            }

            if (updatedCount > 0)
            {
                _statistics.RecordUpdate("habr_resumes_universities", $"{updatedCount}");
            }

            if (upsertedUniversitiesCount > 0)
            {
                _statistics.RecordInsert("habr_universities", $"{upsertedUniversitiesCount}");
            }

            if (missingUsersCount > 0)
            {
                _statistics.RecordSkipped("habr_resumes_universities", $"missing_users:{missingUsersCount}");
                LogSkip("ResumesUniversities", "не найдены пользователи", ("MissingUsersCount", missingUsersCount));
            }

            LogParts("ResumesUniversities summary", isInsert: false,
                ("Input", inputCount),
                ("Upserted", upsertedCount),
                ("Inserted", insertedCount),
                ("Updated", updatedCount),
                ("UpsertedUniversities", upsertedUniversitiesCount),
                ("MissingUsers", missingUsersCount));
            TryDumpStatistics();
        }
        catch (Exception ex)
        {
            LogError("ResumesUniversities", "", ex.Message);
            _statistics.RecordError("habr_resumes_universities", "exception");
            TryDumpStatistics();
        }
    }


    /// <summary>
    /// Вставить записи дополнительного образования в БД одним SQL-запросом.
    /// Все записи одного пользователя обрабатываются пачкой:
    /// - если хоть одна запись имеет DeleteExisting=true, сначала удаляются все старые записи пользователя;
    /// - записи дедуплицируются по (resume_id, title);
    /// - вставка делается через ON CONFLICT DO NOTHING;
    /// - в конце возвращается статистика: удалено, вставлено, пропущено, не найдено резюме.
    /// </summary>
    /// <remarks>
    /// Полный аналог по подходу: UserSkillsInsert (пачка навыков одного пользователя) и
    /// ResumesUniversitiesInsert (пачка связей резюме-ВУЗ) — все в одном CTE.
    /// </remarks>
    private void ResumesEducationsInsert(
        NpgsqlConnection conn,
        List<AdditionalEducationRecord>? additionalEducations)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (additionalEducations == null || additionalEducations.Count == 0) return;

        try
        {
            EnsureConnectionOpen(conn);

            // Нормализация и сериализация входных записей в JSON для CTE.
            var records = additionalEducations
                .Where(education =>
                    !string.IsNullOrWhiteSpace(education.UserLink) &&
                    !string.IsNullOrWhiteSpace(education.Title))
                .Select(education => new
                {
                    user_link = education.UserLink,
                    title = education.Title.Trim(),
                    course = education.Course?.Trim(),
                    duration = education.Duration?.Trim()
                })
                .ToArray();

            if (records.Length == 0) return;

            var recordsJson = System.Text.Json.JsonSerializer.Serialize(records);

            using var cmd = new NpgsqlCommand(@"
                WITH input_rows AS (
                    SELECT user_link, title, course, duration
                    FROM jsonb_to_recordset(@educations::jsonb)
                         AS r(user_link text, title text, course text, duration text)
                    WHERE user_link IS NOT NULL
                      AND btrim(title) <> ''
                ),
                target_resumes AS (
                    SELECT hr.id AS resume_id, ir.user_link
                    FROM input_rows ir
                    JOIN habr_resumes hr ON hr.link = ir.user_link
                ),
                -- Всегда удаляем все старые записи дополнительного образования для каждого
                -- пользователя в пачке: при повторном парсинге профиля его набор курсов
                -- полностью перезаписывается, а не дополняется.
                deleted AS (
                    DELETE FROM habr_resumes_educations e
                    USING target_resumes tr
                    WHERE e.resume_id = tr.resume_id
                    RETURNING e.resume_id
                ),
                dedup_rows AS (
                    -- Дедупликация по (resume_id, title), чтобы один и тот же курс
                    -- с одинаковым названием не вставлялся дважды.
                    SELECT DISTINCT ON (tr.resume_id, ir.title)
                        tr.resume_id,
                        ir.title,
                        ir.course,
                        ir.duration
                    FROM input_rows ir
                    JOIN target_resumes tr ON tr.user_link = ir.user_link
                ),
                inserted AS (
                    INSERT INTO habr_resumes_educations (resume_id, title, course, duration, created_at, updated_at)
                    SELECT resume_id, title, course, duration, NOW(), NOW()
                    FROM dedup_rows
                    ON CONFLICT DO NOTHING
                    RETURNING resume_id, title
                )
                SELECT
                    (SELECT COUNT(*)::int FROM (SELECT DISTINCT user_link FROM input_rows) t) AS input_user_count,
                    (SELECT COUNT(*)::int FROM target_resumes) AS found_user_count,
                    (SELECT COUNT(*)::int FROM deleted) AS deleted_count,
                    (SELECT COUNT(*)::int FROM dedup_rows) AS deduped_count,
                    (SELECT COUNT(*)::int FROM inserted) AS inserted_count", conn);

            cmd.Parameters.AddWithValue("@educations", recordsJson);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                LogError("Дополнительное образование", "", "запрос не вернул результат");
                _statistics.RecordError("habr_resumes_educations", "query");
                TryDumpStatistics();
                return;
            }

            var inputUserCount = reader.GetInt32(0);
            var foundUserCount = reader.GetInt32(1);
            var deletedCount = reader.GetInt32(2);
            var dedupedCount = reader.GetInt32(3);
            var insertedCount = reader.GetInt32(4);

            var missingUsersCount = inputUserCount - foundUserCount;

            if (missingUsersCount > 0)
            {
                _statistics.RecordSkipped("habr_resumes_educations", $"missing_users:{missingUsersCount}");
                LogSkip("ResumesEducations", "не найдены пользователи", ("MissingUsersCount", missingUsersCount));
            }

            if (deletedCount > 0)
            {
                _statistics.RecordDelete("habr_resumes_educations", $"{deletedCount}");
                LogDelete("Дополнительное образование", "старых записей", deletedCount);
            }

            if (insertedCount > 0)
            {
                _statistics.RecordInsert("habr_resumes_educations", $"{insertedCount}");
                LogInsert($"Дополнительное образование", "записей", $"{insertedCount}");
            }

            var skippedCount = dedupedCount - insertedCount;
            if (skippedCount > 0)
            {
                _statistics.RecordSkipped("habr_resumes_educations", $"already_exists:{skippedCount}");
                LogSkip($"Дополнительное образование", "записи уже существуют", ("SkippedCount", skippedCount));
            }

            LogParts(
                "ResumesEducations summary",
                isInsert: false,
                ("Input", inputUserCount),
                ("FoundUsers", foundUserCount),
                ("Deleted", deletedCount),
                ("Deduped", dedupedCount),
                ("Inserted", insertedCount),
                ("Skipped", skippedCount),
                ("MissingUsers", missingUsersCount));

            TryDumpStatistics();
        }
        catch (Exception ex)
        {
            LogError("ResumesEducationsInsert", "", ex.Message);
            _statistics.RecordError("habr_resumes_educations", "exception");
            TryDumpStatistics();
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

            if (deleted > 0)
            {
                _statistics.RecordDelete("habr_resumes", $"404:{deleted}");
            }

            LogDelete("Очистка 404", "записей", deleted);
            TryDumpStatistics();
            return deleted;
        }
        catch (Exception ex)
        {
            LogError("ResumesCleanup404Pages", "", ex.Message);
            _statistics.RecordError("habr_resumes", "404_cleanup");
            TryDumpStatistics();
            return 0;
        }
    }

    #endregion

}
