using System;
using System.Data;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using JobBoardScraper.Models;
using JobBoardScraper.WebScraper;

namespace JobBoardScraper;

public enum DbRecordType
{
    Resume,
    Company,
    CategoryRootId,
    CompanyId,
    CompanyDetails,
    CompanySkills,
    CompanyRating,
    UserProfile,
    UserAbout,
    UserSkills,
    UserExperience,
    UserAdditionalData,
    UserCommunityParticipation
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

//TODO Вывод в лог: записано/обновлено нужно конкретизировать и детализировать. записано ли было или все раки обновлено
//TODO Статистка по записанных обновденным записям и полям по каждой таблице в конце выполнения скрипта + раз в 5 минут сброс дампа стаистики в файл

public readonly record struct DbRecord(
    DbRecordType Type, 
    string PrimaryValue, 
    string SecondaryValue, 
    string? TertiaryValue = null, 
    InsertMode Mode = InsertMode.SkipIfExists,
    string? Code = null,
    bool? Expert = null,
    string? WorkExperience = null,
    long? CompanyId = null,
    CompanyDetailsData? CompanyDetails = null,
    CompanyRatingData? CompanyRating = null,
    List<string>? Skills = null,
    UserProfileData? UserProfile = null,
    UserExperienceData? UserExperience = null,
    Dictionary<string, string?>? AdditionalData = null,
    List<CommunityParticipationData>? CommunityParticipation = null);

public sealed class DatabaseClient
{
    private readonly string _connectionString;
    private Task? _dbWriterTask;
    private CancellationTokenSource? _writerCts;
    private ConcurrentQueue<DbRecord>? _saveQueue;
    private readonly Helper.ConsoleHelper.ConsoleLogger? _logger;

    public DatabaseClient(string connectionString, Helper.ConsoleHelper.ConsoleLogger? logger = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
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
    
    /// <summary>
    /// Обрезает строку до указанной длины, если она превышает лимит
    /// </summary>
    private static string TruncateString(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= maxLength) return value;
        
        return value.Substring(0, maxLength - 3) + "...";
    }

    // Создание соединения
    //"Server=localhost:5432;User Id=postgres; Password=admin;Database=jobs;"
    public NpgsqlConnection DatabaseConnectionInit()
    {
        NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        return conn;
    }

    // Гарантирует, что соединение открыто
    public void DatabaseEnsureConnectionOpen(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (conn.State != ConnectionState.Open)
            conn.Open();
    }

    // Корректное закрытие соединения
    public void DatabaseConnectionClose(NpgsqlConnection conn)
    {
        if (conn is null) return;
        if (conn.State != ConnectionState.Closed)
            conn.Close();
    }

    // Проверка существования записи по полю link
    public bool DatabaseRecordExistsByLink(NpgsqlConnection conn, string link)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(link)) throw new ArgumentException("Link must not be empty.", nameof(link));

        DatabaseEnsureConnectionOpen(conn);
        using var cmd = new NpgsqlCommand("SELECT 1 FROM habr_resumes WHERE link = @link LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@link", link);
        var result = cmd.ExecuteScalar();
        return result is not null;
    }

    // Вставка ссылки, заголовка страницы, слогана и дополнительных полей
    public void DatabaseInsert(
        NpgsqlConnection conn,
        string link,
        string title,
        string? slogan = null,
        string? code = null,
        bool? expert = null,
        string? workExperience = null,
        InsertMode mode = InsertMode.SkipIfExists,
        int? levelId = null,
        string? infoTech = null,
        int? salary = null,
        string? lastVisit = null,
        bool? isPublic = null,
        string? jobSearchStatus = null)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(link)) throw new ArgumentException("Link must not be empty.", nameof(link));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            if (mode == InsertMode.SkipIfExists)
            {
                // Проверка существования по link
                if (DatabaseRecordExistsByLink(conn, link))
                {
                    Log($"Запись уже есть в БД, вставка пропущена: {link}");
                    return;
                }

                using var cmd = new NpgsqlCommand(
                    "INSERT INTO habr_resumes (link, title, slogan, code, expert, work_experience, level_id, info_tech, salary, last_visit, public, job_search_status, created_at, updated_at) VALUES (@link, @title, @slogan, @code, @expert, @work_experience, @level_id, @info_tech, @salary, @last_visit, @public, @job_search_status, NOW(), NOW())",
                    conn);
                cmd.Parameters.AddWithValue("@link", link);
                cmd.Parameters.AddWithValue("@title", title);
                cmd.Parameters.AddWithValue("@slogan", slogan ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@code", code ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@expert", expert ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@work_experience", workExperience ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@level_id", levelId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@info_tech", infoTech ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@salary", salary ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@last_visit", lastVisit ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@public", isPublic ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@job_search_status", jobSearchStatus ?? (object)DBNull.Value);

                int rowsAffected = cmd.ExecuteNonQuery();
                
                // Подробное логирование
                var logParts = new List<string> { $"[DB] Resume {link}:" };
                
                if (title != null)
                    logParts.Add($"Title={title}");
                
                if (!string.IsNullOrWhiteSpace(slogan))
                    logParts.Add($"Slogan={slogan}");
                
                if (code != null)
                    logParts.Add($"Code={code}");
                
                if (expert == true)
                    logParts.Add("Expert=✓");
                
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
                
                if (isPublic.HasValue)
                    logParts.Add($"Public={isPublic.Value}");
                
                if (jobSearchStatus != null)
                    logParts.Add($"JobStatus={jobSearchStatus}");
                
                logParts.Add($"RowsAffected={rowsAffected}");
                logParts.Add("✓ Записано");
                
                Log(string.Join(" | ", logParts));
            }
            else // UpdateIfExists
            {
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO habr_resumes (link, title, slogan, code, expert, work_experience, level_id, info_tech, salary, last_visit, public, job_search_status, created_at, updated_at) 
                    VALUES (@link, @title, @slogan, @code, @expert, @work_experience, @level_id, @info_tech, @salary, @last_visit, @public, @job_search_status, NOW(), NOW())
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
                        public = COALESCE(EXCLUDED.public, habr_resumes.public),
                        job_search_status = COALESCE(EXCLUDED.job_search_status, habr_resumes.job_search_status),
                        updated_at = NOW()", conn);
                cmd.Parameters.AddWithValue("@link", link);
                cmd.Parameters.AddWithValue("@title", title);
                cmd.Parameters.AddWithValue("@slogan", slogan ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@code", code ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@expert", expert ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@work_experience", workExperience ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@level_id", levelId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@info_tech", infoTech ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@salary", salary ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@last_visit", lastVisit ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@public", isPublic ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@job_search_status", jobSearchStatus ?? (object)DBNull.Value);

                int rowsAffected = cmd.ExecuteNonQuery();
                
                // Подробное логирование
                var logParts = new List<string> { $"[DB] Resume {link}:" };
                
                if (title != null)
                    logParts.Add($"Title={title}");
                
                if (!string.IsNullOrWhiteSpace(slogan))
                    logParts.Add($"Slogan={slogan}");
                
                if (code != null)
                    logParts.Add($"Code={code}");
                
                if (expert == true)
                    logParts.Add("Expert=✓");
                
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
                
                if (isPublic.HasValue)
                    logParts.Add($"Public={isPublic.Value}");
                
                logParts.Add($"RowsAffected={rowsAffected}");
                logParts.Add("✓ Записано/Обновлено");
                
                Log(string.Join(" | ", logParts));
            }
        }
        catch (PostgresException pgEx) when
            (pgEx.SqlState == "23505") // На случай гонки: уникальное ограничение нарушено
        {
            Log($"Запись уже есть в БД (уникальное ограничение), вставка пропущена: {link}");
        }
        catch (NpgsqlException dbEx)
        {
            Log($"Ошибка БД для {link}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"Неожиданная ошибка при записи в БД для {link}: {ex.Message}");
        }

    }

    // Получение последней ссылки.
    // Если linkLength не задан, используется прежний алгоритм:
    //   ORDER BY LENGTH(link) DESC, link DESC
    // Если linkLength задан ( > 0 ), выбирается среди ссылок указанной длины:
    //   WHERE LENGTH(link) = @len ORDER BY link DESC
    public string? DatabaseGetLastLink(NpgsqlConnection conn, int? linkLength = null)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (linkLength is <= 0)
            throw new ArgumentOutOfRangeException(nameof(linkLength));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

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

    // Вставка компании
    public void DatabaseInsertCompany(
        NpgsqlConnection conn, 
        string companyCode, 
        string companyUrl,
        string? companyTitle = null,
        long? companyId = null
    )
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(companyCode))
            throw new ArgumentException("Company code must not be empty.", nameof(companyCode));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                INSERT INTO habr_companies (code, url, title, company_id, created_at, updated_at)
                VALUES (@code, @url, @title, @company_id, NOW(), NOW())
                ON CONFLICT (code) 
                DO UPDATE SET 
                    url = EXCLUDED.url,
                    title = EXCLUDED.title,
                    company_id = COALESCE(EXCLUDED.company_id, habr_companies.company_id),
                    updated_at = NOW()", conn);

            cmd.Parameters.AddWithValue("@code", companyCode);
            cmd.Parameters.AddWithValue("@url", companyUrl);
            cmd.Parameters.AddWithValue("@title", companyTitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@company_id", companyId ?? (object)DBNull.Value);

            int rowsAffected = cmd.ExecuteNonQuery();
            
            // Подробное логирование
            var logParts = new List<string>
            {
                $"[DB] Компания {companyCode}:",
                $"URL={companyUrl}"
            };
            
            if (companyTitle != null)
                logParts.Add($"Title={companyTitle}");
            
            if (companyId.HasValue)
                logParts.Add($"CompanyID={companyId.Value}");
            
            logParts.Add($"RowsAffected={rowsAffected}");
            logParts.Add(rowsAffected > 0 ? "✓ Записано/Обновлено" : "⚠ Не изменено");
            
            Log(string.Join(" | ", logParts));
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Ошибка БД для компании {companyCode}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Неожиданная ошибка при записи компании {companyCode}: {ex.Message}");
        }
    }

    // Вставка category_root_id
    public void DatabaseInsertCategoryRootId(NpgsqlConnection conn, string categoryId, string categoryName)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(categoryId))
            throw new ArgumentException("Category ID must not be empty.", nameof(categoryId));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                INSERT INTO habr_category_root_ids (category_id, category_name, created_at, updated_at)
                VALUES (@id, @name, NOW(), NOW())
                ON CONFLICT (category_id) 
                DO UPDATE SET 
                    category_name = EXCLUDED.category_name,
                    updated_at = NOW()", conn);

            cmd.Parameters.AddWithValue("@id", categoryId);
            cmd.Parameters.AddWithValue("@name", categoryName ?? (object)DBNull.Value);

            int rowsAffected = cmd.ExecuteNonQuery();
            Log($"[DB] Записано в БД (category_root_ids): {categoryId} -> {categoryName}");
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Ошибка БД для категории {categoryId}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Неожиданная ошибка при записи категории {categoryId}: {ex.Message}");
        }
    }

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
                            // Получаем или создаём level_id если есть данные профиля
                            int? levelId = null;
                            if (record.UserProfile.HasValue && !string.IsNullOrWhiteSpace(record.UserProfile.Value.LevelTitle))
                            {
                                using (var cmdLevel = new NpgsqlCommand(@"
                                    INSERT INTO habr_levels (title, created_at, updated_at)
                                    VALUES (@title, NOW(), NOW())
                                    ON CONFLICT (title) DO UPDATE SET title = EXCLUDED.title, updated_at = NOW()
                                    RETURNING id", conn))
                                {
                                    cmdLevel.Parameters.AddWithValue("@title", record.UserProfile.Value.LevelTitle);
                                    var result = cmdLevel.ExecuteScalar();
                                    if (result != null)
                                    {
                                        levelId = Convert.ToInt32(result);
                                    }
                                }
                            }
                            
                            // Объединенная вставка/обновление всех полей
                            DatabaseInsert(conn,
                                link: record.PrimaryValue,
                                title: record.UserProfile.HasValue && !string.IsNullOrWhiteSpace(record.UserProfile.Value.UserName) 
                                    ? record.UserProfile.Value.UserName 
                                    : record.SecondaryValue,
                                slogan: record.TertiaryValue,
                                code: record.UserProfile.HasValue && !string.IsNullOrWhiteSpace(record.UserProfile.Value.UserCode)
                                    ? record.UserProfile.Value.UserCode
                                    : record.Code,
                                expert: record.UserProfile.HasValue && record.UserProfile.Value.IsExpert.HasValue
                                    ? record.UserProfile.Value.IsExpert
                                    : record.Expert,
                                workExperience: record.UserProfile.HasValue && !string.IsNullOrWhiteSpace(record.UserProfile.Value.WorkExperience)
                                    ? record.UserProfile.Value.WorkExperience
                                    : record.WorkExperience,
                                mode: record.Mode,
                                levelId: levelId,
                                infoTech: record.UserProfile?.InfoTech,
                                salary: record.UserProfile?.Salary,
                                lastVisit: record.UserProfile?.LastVisit,
                                isPublic: record.UserProfile?.IsPublic,
                                jobSearchStatus: record.UserProfile?.JobSearchStatus
                            );
                            
                            // Если есть навыки, добавляем их
                            if (record.Skills != null && record.Skills.Count > 0)
                            {
                                DatabaseInsertUserSkills(conn, userLink: record.PrimaryValue, skills: record.Skills);
                            }
                            break;
                        case DbRecordType.Company:
                            DatabaseInsertCompany(
                                conn, 
                                companyCode: record.PrimaryValue,
                                companyUrl: record.SecondaryValue, 
                                companyTitle: record.TertiaryValue,
                                companyId: record.CompanyId  
                            );
                            break;
                        case DbRecordType.CategoryRootId:
                            DatabaseInsertCategoryRootId(conn, categoryId: record.PrimaryValue,
                                categoryName: record.SecondaryValue);
                            break;
                        case DbRecordType.CompanyId:
                            if (long.TryParse(record.SecondaryValue, out var companyId))
                            {
                                DatabaseUpdateCompanyId(conn, companyCode: record.PrimaryValue, companyId: companyId);
                            }

                            break;
                        case DbRecordType.CompanyDetails:
                            // Используем структуру CompanyDetailsData
                            if (record.CompanyDetails.HasValue)
                            {
                                var details = record.CompanyDetails.Value;
                                DatabaseUpdateCompanyDetails(
                                    conn,
                                    companyCode: record.PrimaryValue,
                                    companyUrl: details.Url,
                                    companyId: details.CompanyId,
                                    companyTitle: details.Title,
                                    companyAbout: details.About,
                                    companyDescription: details.Description,
                                    companySite: details.Site,
                                    companyRating: details.Rating,
                                    currentEmployees: details.CurrentEmployees,
                                    pastEmployees: details.PastEmployees,
                                    followers: details.Followers,
                                    wantWork: details.WantWork,
                                    employeesCount: details.EmployeesCount,
                                    habr: details.Habr
                                );
                            }

                            break;
                        case DbRecordType.CompanySkills:
                            // Обрабатываем навыки компании
                            if (record.Skills != null && record.Skills.Count > 0)
                            {
                                DatabaseInsertCompanySkills(conn, companyCode: record.PrimaryValue,
                                    skills: record.Skills);
                            }

                            break;
                        case DbRecordType.UserProfile:
                            // Обрабатываем профиль пользователя
                            if (record.UserProfile.HasValue)
                            {
                                var profile = record.UserProfile.Value;
                                
                                // Получаем или создаём level_id
                                int? profileLevelId = null;
                                if (!string.IsNullOrWhiteSpace(profile.LevelTitle))
                                {
                                    using (var cmdLevel = new NpgsqlCommand(@"
                                        INSERT INTO habr_levels (title, created_at, updated_at)
                                        VALUES (@title, NOW(), NOW())
                                        ON CONFLICT (title) DO UPDATE SET title = EXCLUDED.title, updated_at = NOW()
                                        RETURNING id", conn))
                                    {
                                        cmdLevel.Parameters.AddWithValue("@title", profile.LevelTitle);
                                        var result = cmdLevel.ExecuteScalar();
                                        if (result != null)
                                        {
                                            profileLevelId = Convert.ToInt32(result);
                                        }
                                    }
                                }
                                
                                // Обновляем профиль через DatabaseInsert
                                DatabaseInsert(
                                    conn,
                                    link: record.PrimaryValue,
                                    title: profile.UserName ?? "",
                                    code: profile.UserCode,
                                    expert: profile.IsExpert,
                                    workExperience: profile.WorkExperience,
                                    mode: InsertMode.UpdateIfExists,
                                    levelId: profileLevelId,
                                    infoTech: profile.InfoTech,
                                    salary: profile.Salary,
                                    lastVisit: profile.LastVisit,
                                    isPublic: profile.IsPublic,
                                    jobSearchStatus: profile.JobSearchStatus
                                );
                            }
                            // Если это просто обновление статуса публичности
                            else if (record.Mode == InsertMode.UpdateIfExists && bool.TryParse(record.SecondaryValue, out var isPublic))
                            {
                                DatabaseUpdateUserPublicStatus(conn, userLink: record.PrimaryValue, isPublic: isPublic);
                            }

                            break;
                        case DbRecordType.UserAbout:
                            DatabaseUpdateUserAbout(conn, userLink: record.PrimaryValue, about: record.SecondaryValue);
                            break;
                        case DbRecordType.UserSkills:
                            // Если PrimaryValue - число, это добавление навыка с skill_id
                            if (int.TryParse(record.PrimaryValue, out var skillId))
                            {
                                DatabaseInsertSkillWithId(conn, skillId, record.SecondaryValue);
                            }
                            // Иначе это навыки пользователя
                            else if (record.Skills != null && record.Skills.Count > 0)
                            {
                                DatabaseInsertUserSkills(conn, userLink: record.PrimaryValue, skills: record.Skills);
                            }
                            break;
                        case DbRecordType.UserExperience:
                            if (record.UserExperience != null)
                            {
                                DatabaseInsertUserExperience(conn, record.UserExperience);
                            }
                            break;
                        case DbRecordType.UserAdditionalData:
                            if (record.AdditionalData != null)
                            {
                                DatabaseUpdateUserAdditionalData(conn, userLink: record.PrimaryValue, additionalData: record.AdditionalData);
                            }
                            break;
                        case DbRecordType.UserCommunityParticipation:
                            if (record.CommunityParticipation != null)
                            {
                                DatabaseUpdateUserCommunityParticipation(conn, userLink: record.PrimaryValue, communityParticipation: record.CommunityParticipation);
                            }
                            break;
                        case DbRecordType.CompanyRating:
                            if (record.CompanyRating != null)
                            {
                                DatabaseInsertOrUpdateCompanyRating(conn, record.CompanyRating);
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

                    // Обрабатываем очереди университетов
                    FlushUniversityQueue(conn);
                    FlushUserUniversityQueue(conn);
                    
                    // Обрабатываем очередь дополнительного образования
                    FlushAdditionalEducationQueue(conn);

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
        string? workExperience = null)
    {
        if (_saveQueue == null) return false;

        var record = new DbRecord(DbRecordType.Resume, link, title, slogan, mode, code, expert, workExperience);
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] Resume ({mode}): {title} -> {link}" +
                          (string.IsNullOrWhiteSpace(slogan) ? "" : $" | {slogan}") +
                          (expert == true ? " | ЭКСПЕРТ" : ""));

        return true;
    }

    /// <summary>
    /// Добавить компанию в очередь на запись в базу данных
    /// </summary>
    public bool EnqueueCompany(string companyCode, string companyUrl, long? companyId = null, string? companyTitle = null)
    {
        if (_saveQueue == null) return false;

        var record = new DbRecord(
            Type: DbRecordType.Company, 
            PrimaryValue: companyCode, 
            SecondaryValue: companyUrl, 
            TertiaryValue: companyTitle,
            CompanyId: companyId
        );
        _saveQueue.Enqueue(record);
        
        var logMessage = $"[DB Queue] Company: {companyCode} -> {companyUrl}";
        if (companyId.HasValue)
            logMessage += $" (ID: {companyId})";
        if (companyTitle != null)
            logMessage += $" | {companyTitle}";
        Log(logMessage);

        return true;
    }

    /// <summary>
    /// Добавить category_root_id в очередь на запись в базу данных
    /// </summary>
    public bool EnqueueCategoryRootId(string categoryId, string categoryName)
    {
        if (_saveQueue == null) return false;

        var record = new DbRecord(DbRecordType.CategoryRootId, categoryId, categoryName);
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] CategoryRootId: {categoryId} -> {categoryName}");

        return true;
    }

    /// <summary>
    /// Получить все company_id из таблицы habr_companies где company_id не NULL
    /// </summary>
    public List<long> GetAllCompanyIds(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var companyIds = new List<long>();
        
        try
        {
            DatabaseEnsureConnectionOpen(conn);
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
    public List<int> GetAllUniversityIds(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var universityIds = new List<int>();
        
        try
        {
            DatabaseEnsureConnectionOpen(conn);
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
    public List<string> GetAllCategoryIds(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var categoryIds = new List<string>();

        try
        {
            DatabaseEnsureConnectionOpen(conn);

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
    public List<string> GetAllCompanyCodes(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var companyCodes = new List<string>();

        try
        {
            DatabaseEnsureConnectionOpen(conn);

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
    public List<(string code, string url)> GetAllCompaniesWithUrls(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var companies = new List<(string code, string url)>();

        try
        {
            DatabaseEnsureConnectionOpen(conn);

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
    /// Добавить company_id в очередь на обновление в базе данных
    /// </summary>
    public bool EnqueueCompanyId(string companyCode, long companyId)
    {
        if (_saveQueue == null) return false;

        // Используем специальный тип записи для обновления company_id
        var record = new DbRecord(DbRecordType.CompanyId, companyCode, companyId.ToString());
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] CompanyId: {companyCode} -> {companyId}");

        return true;
    }

    /// <summary>
    /// Добавить company_id, url, title, about, description, site, rating, employees, followers, employees_count и habr в очередь на обновление в базе данных
    /// </summary>
    public bool EnqueueCompanyDetails(string companyCode, string companyUrl, long? companyId, string? companyTitle,
        string? companyAbout = null, string? companyDescription = null, string? companySite = null,
        decimal? companyRating = null, int? currentEmployees = null, int? pastEmployees = null, int? followers = null,
        int? wantWork = null, string? employeesCount = null, bool? habr = null)
    {
        if (_saveQueue == null) return false;

        // Создаём структуру с данными компании
        var companyDetails = new CompanyDetailsData(
            Url: companyUrl,
            CompanyId: companyId,
            Title: companyTitle,
            About: companyAbout,
            Description: companyDescription,
            Site: companySite,
            Rating: companyRating,
            CurrentEmployees: currentEmployees,
            PastEmployees: pastEmployees,
            Followers: followers,
            WantWork: wantWork,
            EmployeesCount: employeesCount,
            Habr: habr
        );

        var record = new DbRecord(
            Type: DbRecordType.CompanyDetails,
            PrimaryValue: companyCode,
            SecondaryValue: "", // Не используется для CompanyDetails
            CompanyDetails: companyDetails
        );
        _saveQueue.Enqueue(record);

        var aboutPreview = companyAbout?.Substring(0, Math.Min(50, companyAbout?.Length ?? 0)) ?? "";
        Log(
            $"[DB Queue] CompanyDetails: {companyCode} -> ID={companyId}, Title={companyTitle}, About={aboutPreview}..., Site={companySite}, Rating={companyRating}, Employees={currentEmployees}/{pastEmployees}, Followers={followers}/{wantWork}, Size={employeesCount}");

        return true;
    }

    /// <summary>
    /// Обновить company_id для компании
    /// </summary>
    public void DatabaseUpdateCompanyId(NpgsqlConnection conn, string companyCode, long companyId)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(companyCode))
            throw new ArgumentException("Company code must not be empty.", nameof(companyCode));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                UPDATE habr_companies 
                SET company_id = @company_id, updated_at = NOW()
                WHERE code = @code", conn);

            cmd.Parameters.AddWithValue("@code", companyCode);
            cmd.Parameters.AddWithValue("@company_id", companyId);

            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                Log($"[DB] Обновлён company_id для {companyCode}: {companyId}");
            }
            else
            {
                Log($"[DB] Компания {companyCode} не найдена в БД.");
            }
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Ошибка БД для компании {companyCode}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Неожиданная ошибка при обновлении company_id для {companyCode}: {ex.Message}");
        }
    }

    /// <summary>
    /// Обновить company_id, url, title, about, description, site, rating, employees, followers, employees_count и habr для компании
    /// </summary>
    public void DatabaseUpdateCompanyDetails(NpgsqlConnection conn, string companyCode, string companyUrl, long? companyId,
        string? companyTitle, string? companyAbout, string? companyDescription, string? companySite,
        decimal? companyRating, int? currentEmployees, int? pastEmployees, int? followers, int? wantWork,
        string? employeesCount, bool? habr)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(companyCode))
            throw new ArgumentException("Company code must not be empty.", nameof(companyCode));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                INSERT INTO habr_companies (code, url, company_id, title, about, description, site, rating, 
                    current_employees, past_employees, followers, want_work, employees_count, habr, created_at, updated_at)
                VALUES (@code, @url, @company_id, @title, @about, @description, @site, @rating, 
                    @current_employees, @past_employees, @followers, @want_work, @employees_count, @habr, NOW(), NOW())
                ON CONFLICT (code) 
                DO UPDATE SET 
                    url = EXCLUDED.url,
                    company_id = EXCLUDED.company_id,
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
                    updated_at = NOW()", conn);

            cmd.Parameters.AddWithValue("@code", companyCode);
            cmd.Parameters.AddWithValue("@url", companyUrl);
            cmd.Parameters.AddWithValue("@company_id", companyId.HasValue ? (object)companyId.Value : DBNull.Value);
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

            int rowsAffected = cmd.ExecuteNonQuery();

            // Подробное логирование
            var logParts = new List<string> { $"[DB] CompanyDetails {companyCode}:" };
            
            if (companyId.HasValue)
                logParts.Add($"CompanyID={companyId.Value}");
            
            logParts.Add($"URL={companyUrl}");
            
            if (companyTitle != null)
                logParts.Add($"Title={companyTitle}");
            
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
            
            logParts.Add($"RowsAffected={rowsAffected}");
            logParts.Add(rowsAffected > 0 ? "✓ Записано/Обновлено" : "⚠ Не найдено");
            
            Log(string.Join(" | ", logParts));
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Ошибка БД для компании {companyCode}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Неожиданная ошибка при обновлении данных для {companyCode}: {ex.Message}");
        }
    }

    /// <summary>
    /// Добавить элемент в очередь на запись в базу данных (устаревший метод для обратной совместимости)
    /// </summary>
    [Obsolete("Use EnqueueResume instead")]
    public bool EnqueueItem(string link, string title) => EnqueueResume(link, title);

    /// <summary>
    /// Добавить навыки компании в очередь на запись в базу данных
    /// </summary>
    public bool EnqueueCompanySkills(string companyCode, List<string> skills)
    {
        if (_saveQueue == null) return false;
        if (skills == null || skills.Count == 0) return false;

        var record = new DbRecord(DbRecordType.CompanySkills, companyCode, "", Skills: skills);
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] CompanySkills: {companyCode} -> {skills.Count} навыков");

        return true;
    }

    /// <summary>
    /// Вставить или обновить навыки компании
    /// </summary>
    public void DatabaseInsertCompanySkills(NpgsqlConnection conn, string companyCode, List<string> skills)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(companyCode))
            throw new ArgumentException("Company code must not be empty.", nameof(companyCode));
        if (skills == null || skills.Count == 0) return;

        try
        {
            DatabaseEnsureConnectionOpen(conn);

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
            foreach (var skillTitle in skills)
            {
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
            }

            Log($"[DB] Добавлено {addedCount} навыков для компании {companyCode}");
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Ошибка БД при добавлении навыков для {companyCode}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Неожиданная ошибка при добавлении навыков для {companyCode}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Получить все коды пользователей из таблицы habr_resumes
    /// </summary>
    public List<string> GetAllUserCodes(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var userCodes = new List<string>();

        try
        {
            DatabaseEnsureConnectionOpen(conn);

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
    /// Добавить информацию о профиле пользователя в очередь на обновление
    /// </summary>
    public bool EnqueueUserProfile(string userLink, string? userCode, string? userName, bool? isExpert,
        string? levelTitle, string? infoTech, int? salary, string? workExperience = null, string? lastVisit = null,
        bool? isPublic = null)
    {
        if (_saveQueue == null) return false;
        if (string.IsNullOrWhiteSpace(userLink)) return false;

        // Создаём структуру с данными профиля
        var profileData = new UserProfileData(
            UserCode: userCode,
            UserName: userName,
            IsExpert: isExpert,
            LevelTitle: levelTitle,
            InfoTech: infoTech,
            Salary: salary,
            WorkExperience: workExperience,
            LastVisit: lastVisit,
            IsPublic: isPublic,
            JobSearchStatus: null
        );

        var record = new DbRecord(
            Type: DbRecordType.UserProfile,
            PrimaryValue: userLink,
            SecondaryValue: "",
            UserProfile: profileData
        );
        _saveQueue.Enqueue(record);
        Log(
            $"[DB Queue] UserProfile: {userLink} (code={userCode}) -> Name={userName}, Expert={isExpert}, Level={levelTitle}, Salary={salary}, WorkExp={workExperience}, LastVisit={lastVisit}, Public={isPublic}");

        return true;
    }

    /// <summary>
    /// Получить ссылки пользователей с опциональным фильтром по публичности
    /// </summary>
    public List<string> GetAllUserLinks(NpgsqlConnection conn, bool onlyPublic = false)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var userLinks = new List<string>();

        try
        {
            DatabaseEnsureConnectionOpen(conn);

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
    public List<string> GetUserLinksWithoutData(NpgsqlConnection conn)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        var userLinks = new List<string>();

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            // Противоположный фильтр к count_filled_profiles.sql:
            // Выбираем профили, которые НЕ приватные И НЕ имеют заполненных данных
            var query = @"
                SELECT r.link 
                FROM habr_resumes r
                WHERE r.link IS NOT NULL
                  -- НЕ приватный профиль
                  AND NOT (r.public = false AND r.about = 'Доступ ограничен настройками приватности')
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
    /// Добавить детальную информацию о резюме пользователя в очередь
    /// </summary>
    public bool EnqueueUserResumeDetail(string userLink, string? about, List<string>? skills)
    {
        return EnqueueUserResumeDetail(userLink, about, skills, null, null, null, null, null, null, null, null, null, null, null, null);
    }
    
    /// <summary>
    /// Добавить детальную информацию о резюме пользователя в очередь (с дополнительными полями)
    /// </summary>
    public bool EnqueueUserResumeDetail(
        string userLink, 
        string? about, 
        List<string>? skills,
        string? age,
        string? experienceText,
        string? registration,
        string? lastVisit,
        string? citizenship,
        bool? remoteWork,
        string? userName = null,
        string? infoTech = null,
        string? levelTitle = null,
        int? salary = null,
        string? jobSearchStatus = null,
        List<CommunityParticipationData>? communityParticipation = null)
    {
        if (_saveQueue == null) return false;
        if (string.IsNullOrWhiteSpace(userLink)) return false;

        // Обновляем основные данные профиля (имя, техническая информация, уровень, зарплата)
        if (!string.IsNullOrWhiteSpace(userName) || 
            !string.IsNullOrWhiteSpace(infoTech) || 
            !string.IsNullOrWhiteSpace(levelTitle) || 
            salary.HasValue ||
            !string.IsNullOrWhiteSpace(lastVisit))
        {
            var profileRecord = new DbRecord(
                Type: DbRecordType.UserProfile,
                PrimaryValue: userLink,
                SecondaryValue: "",
                Mode: InsertMode.UpdateIfExists,
                UserProfile: new UserProfileData(
                    UserCode: null,
                    UserName: userName,
                    IsExpert: null,
                    LevelTitle: levelTitle,
                    InfoTech: infoTech,
                    Salary: salary,
                    WorkExperience: experienceText,
                    LastVisit: lastVisit,
                    IsPublic: null,
                    JobSearchStatus: jobSearchStatus
                )
            );
            _saveQueue.Enqueue(profileRecord);
        }
        
        // Обновляем about (записываем пустую строку если не найден)
        var aboutRecord = new DbRecord(
            Type: DbRecordType.UserAbout,
            PrimaryValue: userLink,
            SecondaryValue: about ?? ""
        );
        _saveQueue.Enqueue(aboutRecord);
        
        // Добавляем навыки
        if (skills != null && skills.Count > 0)
        {
            var skillsRecord = new DbRecord(
                Type: DbRecordType.UserSkills,
                PrimaryValue: userLink,
                SecondaryValue: "",
                Skills: skills
            );
            _saveQueue.Enqueue(skillsRecord);
        }
        
        // Добавляем дополнительные данные профиля
        if (!string.IsNullOrWhiteSpace(age) || 
            !string.IsNullOrWhiteSpace(registration) || 
            !string.IsNullOrWhiteSpace(citizenship) || 
            remoteWork.HasValue ||
            !string.IsNullOrWhiteSpace(jobSearchStatus))
        {
            var additionalDataRecord = new DbRecord(
                Type: DbRecordType.UserAdditionalData,
                PrimaryValue: userLink,
                SecondaryValue: "",
                AdditionalData: new Dictionary<string, string?>
                {
                    { "age", age },
                    { "registration", registration },
                    { "citizenship", citizenship },
                    { "remote_work", remoteWork?.ToString() },
                    { "job_search_status", jobSearchStatus }
                }
            );
            _saveQueue.Enqueue(additionalDataRecord);
        }
        
        // Добавляем участие в профсообществах
        if (communityParticipation != null && communityParticipation.Count > 0)
        {
            var communityRecord = new DbRecord(
                Type: DbRecordType.UserCommunityParticipation,
                PrimaryValue: userLink,
                SecondaryValue: "",
                CommunityParticipation: communityParticipation
            );
            _saveQueue.Enqueue(communityRecord);
        }
        
        Log($"[DB Queue] UserResumeDetail: {userLink} -> UserName={userName}, InfoTech={infoTech}, Level={levelTitle}, Salary={salary}, JobStatus={jobSearchStatus}, About={!string.IsNullOrWhiteSpace(about)}, Skills={skills?.Count ?? 0}, Age={age}, ExperienceText={experienceText}, Registration={registration}, LastVisit={lastVisit}, Citizenship={citizenship}, RemoteWork={remoteWork}, CommunityParticipation={communityParticipation?.Count ?? 0}");
        
        return true;
    }

    /// <summary>
    /// Обновить статус публичности профиля пользователя
    /// </summary>
    public bool EnqueueUpdateUserPublicStatus(string userLink, bool isPublic)
    {
        if (_saveQueue == null) return false;
        if (string.IsNullOrWhiteSpace(userLink)) return false;

        // Используем тип UserProfile для обновления is_public
        var record = new DbRecord(
            Type: DbRecordType.UserProfile,
            PrimaryValue: userLink,
            SecondaryValue: isPublic.ToString(),
            Mode: InsertMode.UpdateIfExists
        );
        _saveQueue.Enqueue(record);
        
        Log($"[DB Queue] UpdateUserPublicStatus: {userLink} -> public={isPublic}");
        
        return true;
    }

    /// <summary>
    /// Обновить информацию "О себе" для пользователя
    /// </summary>
    public void DatabaseUpdateUserAbout(NpgsqlConnection conn, string userLink, string? about)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                UPDATE habr_resumes 
                SET about = @about
                WHERE link = @link", conn);

            cmd.Parameters.AddWithValue("@link", userLink);
            cmd.Parameters.AddWithValue("@about", about ?? (object)DBNull.Value);

            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                var aboutPreview = about?.Substring(0, Math.Min(50, about.Length)) ?? "";
                Log($"[DB] Обновлено 'О себе' для {userLink}: {aboutPreview}...");
            }
            else
            {
                Log($"[DB] Пользователь {userLink} не найден в БД.");
            }
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Ошибка БД для пользователя {userLink}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Неожиданная ошибка при обновлении 'О себе' для {userLink}: {ex.Message}");
        }
    }

    /// <summary>
    /// Обновить дополнительные данные профиля пользователя (возраст, регистрация, гражданство, удаленная работа)
    /// </summary>
    public void DatabaseUpdateUserAdditionalData(NpgsqlConnection conn, string userLink, Dictionary<string, string?> additionalData)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));
        if (additionalData == null || additionalData.Count == 0)
            return;

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            var setClauses = new List<string>();
            var cmd = new NpgsqlCommand { Connection = conn };

            if (additionalData.TryGetValue("age", out var age) && !string.IsNullOrWhiteSpace(age))
            {
                setClauses.Add("age = @age");
                cmd.Parameters.AddWithValue("@age", age);
            }

            if (additionalData.TryGetValue("experience_text", out var experienceText) && !string.IsNullOrWhiteSpace(experienceText))
            {
                setClauses.Add("experience_text = @experience_text");
                cmd.Parameters.AddWithValue("@experience_text", experienceText);
            }

            if (additionalData.TryGetValue("registration", out var registration) && !string.IsNullOrWhiteSpace(registration))
            {
                setClauses.Add("registration = @registration");
                cmd.Parameters.AddWithValue("@registration", registration);
            }

            if (additionalData.TryGetValue("last_visit", out var lastVisit) && !string.IsNullOrWhiteSpace(lastVisit))
            {
                setClauses.Add("last_visit = @last_visit");
                cmd.Parameters.AddWithValue("@last_visit", lastVisit);
            }

            if (additionalData.TryGetValue("citizenship", out var citizenship) && !string.IsNullOrWhiteSpace(citizenship))
            {
                setClauses.Add("citizenship = @citizenship");
                cmd.Parameters.AddWithValue("@citizenship", citizenship);
            }

            if (additionalData.TryGetValue("remote_work", out var remoteWorkStr) && !string.IsNullOrWhiteSpace(remoteWorkStr))
            {
                setClauses.Add("remote_work = @remote_work");
                // Парсим строку в boolean
                if (bool.TryParse(remoteWorkStr, out var remoteWorkBool))
                {
                    cmd.Parameters.AddWithValue("@remote_work", remoteWorkBool);
                }
                else
                {
                    cmd.Parameters.AddWithValue("@remote_work", DBNull.Value);
                }
            }

            if (setClauses.Count == 0)
                return;

            cmd.CommandText = $@"
                UPDATE habr_resumes 
                SET {string.Join(", ", setClauses)}
                WHERE link = @link";

            cmd.Parameters.AddWithValue("@link", userLink);

            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                Log($"[DB] Обновлены дополнительные данные для {userLink}: Age={age}, ExperienceText={experienceText}, Registration={registration}, LastVisit={lastVisit}, Citizenship={citizenship}, RemoteWork={remoteWorkStr}");
            }
            else
            {
                Log($"[DB] Пользователь {userLink} не найден в БД.");
            }
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Ошибка БД для пользователя {userLink}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Неожиданная ошибка при обновлении дополнительных данных для {userLink}: {ex.Message}");
        }
    }

    /// <summary>
    /// Обновить участие в профсообществах для пользователя (Хабр, GitHub и др.)
    /// Сохраняет данные в поле community_participation как JSON массив
    /// </summary>
    public void DatabaseUpdateUserCommunityParticipation(NpgsqlConnection conn, string userLink, List<CommunityParticipationData> communityParticipation)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));
        if (communityParticipation == null || communityParticipation.Count == 0)
            return;

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            // Сериализуем данные в JSON
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
            var jsonString = jsonArray.ToJsonString();

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
                var names = string.Join(", ", communityParticipation.Select(c => c.Name));
                Log($"[DB] Обновлено участие в профсообществах для {userLink}: {names} ({communityParticipation.Count} записей)");
            }
            else
            {
                Log($"[DB] Пользователь {userLink} не найден в БД.");
            }
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Ошибка БД при обновлении участия в профсообществах для {userLink}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Неожиданная ошибка при обновлении участия в профсообществах для {userLink}: {ex.Message}");
        }
    }

    /// <summary>
    /// Обновить статус публичности профиля пользователя
    /// </summary>
    public void DatabaseUpdateUserPublicStatus(NpgsqlConnection conn, string userLink, bool isPublic)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

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
                Log($"[DB] Обновлен статус публичности для {userLink}: public={isPublic}");
            }
            else
            {
                Log($"[DB] Пользователь {userLink} не найден в БД.");
            }
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Ошибка БД для пользователя {userLink}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Log($"[DB] Неожиданная ошибка при обновлении статуса публичности для {userLink}: {ex.Message}");
        }
    }

    /// <summary>
    /// Вставить или обновить навыки пользователя
    /// </summary>
    public void DatabaseInsertUserSkills(NpgsqlConnection conn, string userLink, List<string> skills)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));
        if (skills == null || skills.Count == 0) return;

        try
        {
            DatabaseEnsureConnectionOpen(conn);

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
            foreach (var skillTitle in skills)
            {
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
    /// Добавить опыт работы пользователя в очередь
    /// </summary>
    public bool EnqueueUserExperience(UserExperienceData experienceData)
    {
        if (_saveQueue == null) return false;

        var record = new DbRecord(
            Type: DbRecordType.UserExperience,
            PrimaryValue: "",
            SecondaryValue: "",
            UserExperience: experienceData
        );
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] UserExperience: {experienceData.UserLink} -> Company={experienceData.CompanyCode}, Position={experienceData.Position}, Skills={experienceData.Skills?.Count ?? 0}");

        return true;
    }

    /// <summary>
    /// Вставить опыт работы пользователя
    /// </summary>
    public void DatabaseInsertUserExperience(NpgsqlConnection conn, UserExperienceData exp)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(exp.UserLink))
            throw new ArgumentException("User link must not be empty.", nameof(exp));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

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
                }
            }

            Log($"[DB] Добавлен опыт работы для {exp.UserLink}: Company={exp.CompanyTitle}, Position={exp.Position}, Skills={exp.Skills?.Count ?? 0}");
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
    /// Добавить навык в очередь (с skill_id из URL)
    /// </summary>
    public bool EnqueueSkill(int skillId, string title)
    {
        if (_saveQueue == null) return false;

        // Используем специальный тип для навыков
        var record = new DbRecord(DbRecordType.UserSkills, skillId.ToString(), title);
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] Skill: ID={skillId}, Title={title}");

        return true;
    }

    /// <summary>
    /// Добавить детальный профиль резюме в очередь
    /// </summary>
    public bool EnqueueResumeProfile(ResumeProfileData profileData)
    {
        if (_saveQueue == null) return false;

        // Создаём запись с профилем
        var record = new DbRecord(
            Type: DbRecordType.Resume,
            PrimaryValue: profileData.Link,
            SecondaryValue: profileData.Title,
            Code: profileData.Code,
            Expert: profileData.IsExpert,
            Mode: InsertMode.UpdateIfExists,
            UserProfile: new UserProfileData(
                UserCode: profileData.Code,
                UserName: profileData.Title,
                IsExpert: profileData.IsExpert,
                LevelTitle: profileData.LevelTitle,
                InfoTech: profileData.InfoTech,
                Salary: profileData.Salary,
                WorkExperience: null,
                LastVisit: null,
                IsPublic: null,
                JobSearchStatus: null
            ),
            Skills: profileData.Skills
        );
        _saveQueue.Enqueue(record);
        Log($"[DB Queue] ResumeProfile: {profileData.Code} -> {profileData.Title}, Expert={profileData.IsExpert}, Skills={profileData.Skills?.Count ?? 0}");

        return true;
    }

    /// <summary>
    /// Вставить навык с skill_id (только если его еще нет)
    /// </summary>
    public void DatabaseInsertSkillWithId(NpgsqlConnection conn, int skillId, string? title)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

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

            // Если не существует, вставляем с skillId в оба поля
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO habr_skills (skill_id, title, created_at, updated_at)
                VALUES (@skill_id, @title, NOW(), NOW())", conn);

            cmd.Parameters.AddWithValue("@skill_id", skillId);
            // Если title пустой, используем skillId как строку
            cmd.Parameters.AddWithValue("@title", string.IsNullOrWhiteSpace(title) ? skillId.ToString() : title);

            cmd.ExecuteNonQuery();
            Log($"[DB] Навык добавлен: skill_id={skillId}, title={title ?? skillId.ToString()}");
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
    /// Добавить данные рейтинга компании в очередь на запись в базу данных
    /// </summary>
    public bool EnqueueCompanyRating(CompanyRatingData ratingData)
    {
        if (_saveQueue == null) return false;

        var record = new DbRecord(
            Type: DbRecordType.CompanyRating,
            PrimaryValue: ratingData.Code,
            SecondaryValue: "", 
            CompanyRating: ratingData
        );
        _saveQueue.Enqueue(record);

        Log($"[DB Queue] CompanyRating: {ratingData.Code} -> Title={ratingData.Title}, Rating={ratingData.Rating}, City={ratingData.City}, Scores={ratingData.Scores}");

        return true;
    }

    /// <summary>
    /// Сохранить или обновить данные рейтинга компании в базе данных
    /// </summary>
    public void DatabaseInsertOrUpdateCompanyRating(NpgsqlConnection conn, CompanyRatingData ratingData)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (ratingData is null) throw new ArgumentNullException(nameof(ratingData));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            // Сначала получаем или создаем компанию
            int companyId;
            using (var cmdSelect = new NpgsqlCommand(@"
                SELECT id FROM habr_companies WHERE code = @code", conn))
            {
                cmdSelect.Parameters.AddWithValue("@code", ratingData.Code);
                var result = cmdSelect.ExecuteScalar();

                if (result != null)
                {
                    // Компания существует - обновляем
                    companyId = Convert.ToInt32(result);

                    using var cmdUpdate = new NpgsqlCommand(@"
                        UPDATE habr_companies 
                        SET 
                            url = @url,
                            title = COALESCE(@title, title),
                            rating = COALESCE(@rating, rating),
                            about = COALESCE(@about, about),
                            city = COALESCE(@city, city),
                            awards = COALESCE(@awards, awards),
                            scores = COALESCE(@scores, scores),
                            updated_at = CURRENT_TIMESTAMP
                        WHERE code = @code", conn);

                    cmdUpdate.Parameters.AddWithValue("@code", ratingData.Code);
                    cmdUpdate.Parameters.AddWithValue("@url", ratingData.Url);
                    cmdUpdate.Parameters.AddWithValue("@title", ratingData.Title ?? (object)DBNull.Value);
                    cmdUpdate.Parameters.AddWithValue("@rating", ratingData.Rating ?? (object)DBNull.Value);
                    cmdUpdate.Parameters.AddWithValue("@about", ratingData.About ?? (object)DBNull.Value);
                    cmdUpdate.Parameters.AddWithValue("@city", ratingData.City ?? (object)DBNull.Value);
                    cmdUpdate.Parameters.AddWithValue("@awards", ratingData.Awards?.ToArray() ?? (object)DBNull.Value);
                    cmdUpdate.Parameters.AddWithValue("@scores", ratingData.Scores ?? (object)DBNull.Value);

                    cmdUpdate.ExecuteNonQuery();
                    Log($"[DB] Обновлена компания: {ratingData.Code}");
                }
                else
                {
                    // Компания не существует - создаем
                    using var cmdInsert = new NpgsqlCommand(@"
                        INSERT INTO habr_companies (code, url, title, rating, about, city, awards, scores, created_at, updated_at)
                        VALUES (@code, @url, @title, @rating, @about, @city, @awards, @scores, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                        RETURNING id", conn);

                    cmdInsert.Parameters.AddWithValue("@code", ratingData.Code);
                    cmdInsert.Parameters.AddWithValue("@url", ratingData.Url);
                    cmdInsert.Parameters.AddWithValue("@title", ratingData.Title ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@rating", ratingData.Rating ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@about", ratingData.About ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@city", ratingData.City ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@awards", ratingData.Awards?.ToArray() ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@scores", ratingData.Scores ?? (object)DBNull.Value);

                    var insertResult = cmdInsert.ExecuteScalar();
                    companyId = Convert.ToInt32(insertResult!);
                    Log($"[DB] Добавлена новая компания: {ratingData.Code} (ID={companyId})");
                }
            }

            // Сохраняем отзыв, если он есть
            if (!string.IsNullOrWhiteSpace(ratingData.ReviewText))
            {
                DatabaseInsertReview(conn, companyId, ratingData.ReviewText);
            }
        }
        catch (Exception ex)
        {
            Log($"[DB] Ошибка при сохранении рейтинга компании {ratingData.Code}: {ex.Message}");
        }
    }

    /// <summary>
    /// Сохранить отзыв о компании (с проверкой дубликатов по хешу)
    /// </summary>
    private void DatabaseInsertReview(NpgsqlConnection conn, int companyId, string reviewText)
    {
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
                    return;
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

    #region University Education Methods

    private readonly ConcurrentQueue<Models.UniversityData> _universityQueue = new();
    private readonly ConcurrentQueue<Models.UserUniversityData> _userUniversityQueue = new();

    /// <summary>
    /// Добавить университет в очередь на сохранение
    /// </summary>
    public void EnqueueUniversity(Models.UniversityData data)
    {
        _universityQueue.Enqueue(data);
    }

    /// <summary>
    /// Добавить связь пользователь-университет в очередь на сохранение
    /// </summary>
    public void EnqueueUserUniversity(Models.UserUniversityData data)
    {
        _userUniversityQueue.Enqueue(data);
    }

    /// <summary>
    /// Сохранить все университеты из очереди в БД
    /// </summary>
    public void FlushUniversityQueue(NpgsqlConnection conn)
    {
        while (_universityQueue.TryDequeue(out var data))
        {
            try
            {
                DatabaseInsertUniversity(conn, data);
            }
            catch (Exception ex)
            {
                Log($"[DB] Ошибка при сохранении университета {data.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Сохранить все связи пользователь-университет из очереди в БД
    /// </summary>
    public void FlushUserUniversityQueue(NpgsqlConnection conn)
    {
        while (_userUniversityQueue.TryDequeue(out var data))
        {
            try
            {
                DatabaseInsertUserUniversity(conn, data);
            }
            catch (Exception ex)
            {
                Log($"[DB] Ошибка при сохранении связи пользователь-университет: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Вставить или обновить университет в БД
    /// </summary>
    private void DatabaseInsertUniversity(NpgsqlConnection conn, Models.UniversityData data)
    {
        DatabaseEnsureConnectionOpen(conn);

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
        Log($"[DB] Университет {data.Name} (ID={data.HabrId}): {(rowsAffected > 0 ? "сохранён" : "не изменён")}");
    }

    /// <summary>
    /// Вставить связь пользователь-университет в БД
    /// </summary>
    private void DatabaseInsertUserUniversity(NpgsqlConnection conn, Models.UserUniversityData data)
    {
        DatabaseEnsureConnectionOpen(conn);

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
        Log($"[DB] Связь пользователь-университет: user_id={userId}, university_id={universityId}, courses={data.Courses?.Count ?? 0}");
    }

    #endregion

    #region Additional Education Methods

    private readonly ConcurrentQueue<Models.AdditionalEducationData> _additionalEducationQueue = new();

    /// <summary>
    /// Добавить дополнительное образование в очередь на сохранение
    /// </summary>
    public void EnqueueAdditionalEducation(Models.AdditionalEducationData data)
    {
        _additionalEducationQueue.Enqueue(data);
    }

    /// <summary>
    /// Сохранить все записи дополнительного образования из очереди в БД
    /// </summary>
    public void FlushAdditionalEducationQueue(NpgsqlConnection conn)
    {
        while (_additionalEducationQueue.TryDequeue(out var data))
        {
            try
            {
                DatabaseInsertAdditionalEducation(conn, data);
            }
            catch (Exception ex)
            {
                Log($"[DB] Ошибка при сохранении дополнительного образования: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Вставить запись дополнительного образования в БД
    /// </summary>
    private void DatabaseInsertAdditionalEducation(NpgsqlConnection conn, Models.AdditionalEducationData data)
    {
        DatabaseEnsureConnectionOpen(conn);

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
        Log($"[DB] Дополнительное образование: resume_id={resumeId}, title={data.Title}, course={data.Course ?? "(нет)"}");
    }

    /// <summary>
    /// Удалить все записи дополнительного образования пользователя перед добавлением новых
    /// </summary>
    public void DeleteUserAdditionalEducation(NpgsqlConnection conn, string userLink)
    {
        DatabaseEnsureConnectionOpen(conn);

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

    #endregion

}
