using System;
using System.Data;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace JobBoardScraper;

public enum DbRecordType
{
    Resume,
    Company,
    CategoryRootId,
    CompanyId,
    CompanyDetails,
    CompanySkills,
    UserProfile,
    UserAbout,
    UserSkills,
    UserExperience
}

/// <summary>
/// Структура для хранения детальных данных компании
/// </summary>
public readonly record struct CompanyDetailsData(
    long CompanyId,
    string? Title,
    string? About,
    string? Description,
    string? Site,
    decimal? Rating,
    int? CurrentEmployees,
    int? PastEmployees,
    int? Followers,
    int? WantWork,
    string? EmployeesCount,
    bool? Habr
);

/// <summary>
/// Структура для хранения данных профиля пользователя
/// </summary>
public readonly record struct UserProfileData(
    string? UserCode,
    string? UserName,
    bool? IsExpert,
    string? LevelTitle,
    string? InfoTech,
    int? Salary,
    string? WorkExperience,
    string? LastVisit,
    bool? IsPublic
);

/// <summary>
/// Структура для хранения данных об опыте работы
/// </summary>
public readonly record struct UserExperienceData(
    string UserLink,
    string? CompanyCode,
    string? CompanyUrl,
    string? CompanyTitle,
    string? CompanyAbout,
    string? CompanySize,
    string? Position,
    string? Duration,
    string? Description,
    List<(int? SkillId, string SkillName)>? Skills,
    bool IsFirstRecord = false
);

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

public readonly record struct DbRecord(
    DbRecordType Type, 
    string PrimaryValue, 
    string SecondaryValue, 
    string? TertiaryValue = null, 
    InsertMode Mode = InsertMode.SkipIfExists,
    string? Code = null,
    bool? Expert = null,
    string? WorkExperience = null,
    CompanyDetailsData? CompanyDetails = null,
    List<string>? Skills = null,
    UserProfileData? UserProfile = null,
    UserExperienceData? UserExperience = null);

public sealed class DatabaseClient
{
    private readonly string _connectionString;
    private Task? _dbWriterTask;
    private CancellationTokenSource? _writerCts;
    private ConcurrentQueue<DbRecord>? _saveQueue;

    public DatabaseClient(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
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
        InsertMode mode = InsertMode.SkipIfExists)
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
                    Console.WriteLine($"Запись уже есть в БД, вставка пропущена: {link}");
                    return;
                }

                using var cmd = new NpgsqlCommand(
                    "INSERT INTO habr_resumes (link, title, slogan, code, expert, work_experience) VALUES (@link, @title, @slogan, @code, @expert, @work_experience)",
                    conn);
                cmd.Parameters.AddWithValue("@link", link);
                cmd.Parameters.AddWithValue("@title", title);
                cmd.Parameters.AddWithValue("@slogan", slogan ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@code", code ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@expert", expert ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@work_experience", workExperience ?? (object)DBNull.Value);

                int rowsAffected = cmd.ExecuteNonQuery();
                Console.WriteLine($"Записано в БД: {rowsAffected} строка, {link} | {title}" +
                                  (string.IsNullOrWhiteSpace(slogan) ? "" : $" | {slogan}") +
                                  (expert == true ? " | ЭКСПЕРТ" : ""));
            }
            else // UpdateIfExists
            {
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO habr_resumes (link, title, slogan, code, expert, work_experience) 
                    VALUES (@link, @title, @slogan, @code, @expert, @work_experience)
                    ON CONFLICT (link) 
                    DO UPDATE SET 
                        title = EXCLUDED.title,
                        slogan = EXCLUDED.slogan,
                        code = EXCLUDED.code,
                        expert = EXCLUDED.expert,
                        work_experience = EXCLUDED.work_experience", conn);
                cmd.Parameters.AddWithValue("@link", link);
                cmd.Parameters.AddWithValue("@title", title);
                cmd.Parameters.AddWithValue("@slogan", slogan ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@code", code ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@expert", expert ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@work_experience", workExperience ?? (object)DBNull.Value);

                int rowsAffected = cmd.ExecuteNonQuery();
                Console.WriteLine($"Записано/обновлено в БД: {link} | {title}" +
                                  (string.IsNullOrWhiteSpace(slogan) ? "" : $" | {slogan}") +
                                  (expert == true ? " | ЭКСПЕРТ" : ""));
            }
        }
        catch (PostgresException pgEx) when
            (pgEx.SqlState == "23505") // На случай гонки: уникальное ограничение нарушено
        {
            Console.WriteLine($"Запись уже есть в БД (уникальное ограничение), вставка пропущена: {link}");
        }
        catch (NpgsqlException dbEx)
        {
            Console.WriteLine($"Ошибка БД для {link}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Неожиданная ошибка при записи в БД для {link}: {ex.Message}");
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
    public void DatabaseInsertCompany(NpgsqlConnection conn, string companyCode, string companyUrl,
        string? companyTitle = null)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(companyCode))
            throw new ArgumentException("Company code must not be empty.", nameof(companyCode));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                INSERT INTO habr_companies (code, url, title, created_at, updated_at)
                VALUES (@code, @url, @title, NOW(), NOW())
                ON CONFLICT (code) 
                DO UPDATE SET 
                    url = EXCLUDED.url,
                    title = EXCLUDED.title,
                    updated_at = NOW()", conn);

            cmd.Parameters.AddWithValue("@code", companyCode);
            cmd.Parameters.AddWithValue("@url", companyUrl);
            cmd.Parameters.AddWithValue("@title", companyTitle ?? (object)DBNull.Value);

            int rowsAffected = cmd.ExecuteNonQuery();
            Console.WriteLine($"[DB] Записано в БД (companies): {companyCode} -> {companyUrl}" +
                              (companyTitle != null ? $" | {companyTitle}" : ""));
        }
        catch (NpgsqlException dbEx)
        {
            Console.WriteLine($"[DB] Ошибка БД для компании {companyCode}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Неожиданная ошибка при записи компании {companyCode}: {ex.Message}");
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
            Console.WriteLine($"[DB] Записано в БД (category_root_ids): {categoryId} -> {categoryName}");
        }
        catch (NpgsqlException dbEx)
        {
            Console.WriteLine($"[DB] Ошибка БД для категории {categoryId}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Неожиданная ошибка при записи категории {categoryId}: {ex.Message}");
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
            while (!linkedToken.IsCancellationRequested)
            {
                while (_saveQueue.TryDequeue(out var record))
                {
                    switch (record.Type)
                    {
                        case DbRecordType.Resume:
                            DatabaseInsert(conn,
                                link: record.PrimaryValue,
                                title: record.SecondaryValue,
                                slogan: record.TertiaryValue,
                                code: record.Code,
                                expert: record.Expert,
                                workExperience: record.WorkExperience,
                                mode: record.Mode);
                            break;
                        case DbRecordType.Company:
                            DatabaseInsertCompany(conn, companyCode: record.PrimaryValue,
                                companyUrl: record.SecondaryValue, companyTitle: record.TertiaryValue);
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
                                DatabaseUpdateUserProfile(
                                    conn,
                                    userLink: record.PrimaryValue,
                                    userCode: profile.UserCode,
                                    userName: profile.UserName,
                                    isExpert: profile.IsExpert,
                                    levelTitle: profile.LevelTitle,
                                    infoTech: profile.InfoTech,
                                    salary: profile.Salary,
                                    workExperience: profile.WorkExperience,
                                    lastVisit: profile.LastVisit,
                                    isPublic: profile.IsPublic
                                );
                            }

                            break;
                        case DbRecordType.UserAbout:
                            DatabaseUpdateUserAbout(conn, userLink: record.PrimaryValue, about: record.SecondaryValue);
                            break;
                        case DbRecordType.UserSkills:
                            if (record.Skills != null && record.Skills.Count > 0)
                            {
                                DatabaseInsertUserSkills(conn, userLink: record.PrimaryValue, skills: record.Skills);
                            }
                            break;
                        case DbRecordType.UserExperience:
                            if (record.UserExperience.HasValue)
                            {
                                var exp = record.UserExperience.Value;
                                DatabaseInsertUserExperience(conn, exp);
                            }
                            break;
                    }
                }

                await Task.Delay(delayMs, linkedToken);
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
                    Console.WriteLine($"Ошибка при остановке задачи записи в БД: {ex.Message}");
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
        Console.WriteLine($"[DB Queue] Resume ({mode}): {title} -> {link}" +
                          (string.IsNullOrWhiteSpace(slogan) ? "" : $" | {slogan}") +
                          (expert == true ? " | ЭКСПЕРТ" : ""));

        return true;
    }

    /// <summary>
    /// Добавить компанию в очередь на запись в базу данных
    /// </summary>
    public bool EnqueueCompany(string companyCode, string companyUrl, string? companyTitle = null)
    {
        if (_saveQueue == null) return false;

        var record = new DbRecord(DbRecordType.Company, companyCode, companyUrl, companyTitle);
        _saveQueue.Enqueue(record);
        Console.WriteLine($"[DB Queue] Company: {companyCode} -> {companyUrl}" +
                          (companyTitle != null ? $" | {companyTitle}" : ""));

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
        Console.WriteLine($"[DB Queue] CategoryRootId: {categoryId} -> {categoryName}");

        return true;
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

            Console.WriteLine($"[DB] Загружено {categoryIds.Count} категорий из БД");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Ошибка при получении категорий: {ex.Message}");
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

            Console.WriteLine($"[DB] Загружено {companyCodes.Count} компаний из БД");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Ошибка при получении компаний: {ex.Message}");
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

            Console.WriteLine($"[DB] Загружено {companies.Count} компаний с URL из БД");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Ошибка при получении компаний: {ex.Message}");
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
        Console.WriteLine($"[DB Queue] CompanyId: {companyCode} -> {companyId}");

        return true;
    }

    /// <summary>
    /// Добавить company_id, title, about, description, site, rating, employees, followers, employees_count и habr в очередь на обновление в базе данных
    /// </summary>
    public bool EnqueueCompanyDetails(string companyCode, long companyId, string? companyTitle,
        string? companyAbout = null, string? companyDescription = null, string? companySite = null,
        decimal? companyRating = null, int? currentEmployees = null, int? pastEmployees = null, int? followers = null,
        int? wantWork = null, string? employeesCount = null, bool? habr = null)
    {
        if (_saveQueue == null) return false;

        // Создаём структуру с данными компании
        var companyDetails = new CompanyDetailsData(
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
        Console.WriteLine(
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
                Console.WriteLine($"[DB] Обновлён company_id для {companyCode}: {companyId}");
            }
            else
            {
                Console.WriteLine($"[DB] Компания {companyCode} не найдена в БД.");
            }
        }
        catch (NpgsqlException dbEx)
        {
            Console.WriteLine($"[DB] Ошибка БД для компании {companyCode}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Неожиданная ошибка при обновлении company_id для {companyCode}: {ex.Message}");
        }
    }

    /// <summary>
    /// Обновить company_id, title, about, description, site, rating, employees, followers, employees_count и habr для компании
    /// </summary>
    public void DatabaseUpdateCompanyDetails(NpgsqlConnection conn, string companyCode, long companyId,
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
                UPDATE habr_companies 
                SET company_id = @company_id, 
                    title = COALESCE(@title, title),
                    about = COALESCE(@about, about),
                    description = COALESCE(@description, description),
                    site = COALESCE(@site, site),
                    rating = COALESCE(@rating, rating),
                    current_employees = COALESCE(@current_employees, current_employees),
                    past_employees = COALESCE(@past_employees, past_employees),
                    followers = COALESCE(@followers, followers),
                    want_work = COALESCE(@want_work, want_work),
                    employees_count = COALESCE(@employees_count, employees_count),
                    habr = COALESCE(@habr, habr),
                    updated_at = NOW()
                WHERE code = @code", conn);

            cmd.Parameters.AddWithValue("@code", companyCode);
            cmd.Parameters.AddWithValue("@company_id", companyId);
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

            if (rowsAffected > 0)
            {
                var aboutPreview = companyAbout?.Substring(0, Math.Min(50, companyAbout.Length)) ?? "";
                Console.WriteLine(
                    $"[DB] Обновлены данные для {companyCode}: ID={companyId}, Title={companyTitle}, About={aboutPreview}..., Site={companySite}, Rating={companyRating}, Employees={currentEmployees}/{pastEmployees}, Followers={followers}/{wantWork}, Size={employeesCount}");
            }
            else
            {
                Console.WriteLine($"[DB] Компания {companyCode} не найдена в БД.");
            }
        }
        catch (NpgsqlException dbEx)
        {
            Console.WriteLine($"[DB] Ошибка БД для компании {companyCode}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Неожиданная ошибка при обновлении данных для {companyCode}: {ex.Message}");
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
        Console.WriteLine($"[DB Queue] CompanySkills: {companyCode} -> {skills.Count} навыков");

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
                Console.WriteLine($"[DB] Компания {companyCode} не найдена в БД. Пропуск навыков.");
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
                    INSERT INTO habr_skills (title, created_at)
                    VALUES (@title, NOW())
                    ON CONFLICT (title) 
                    DO UPDATE SET title = EXCLUDED.title
                    RETURNING id", conn))
                {
                    cmdInsertSkill.Parameters.AddWithValue("@title", skillTitle.Trim());
                    var result = cmdInsertSkill.ExecuteScalar();
                    skillId = Convert.ToInt32(result);
                }

                // Связываем навык с компанией
                using (var cmdLinkSkill = new NpgsqlCommand(@"
                    INSERT INTO habr_company_skills (company_id, skill_id, created_at)
                    VALUES (@company_id, @skill_id, NOW())
                    ON CONFLICT (company_id, skill_id) DO NOTHING", conn))
                {
                    cmdLinkSkill.Parameters.AddWithValue("@company_id", companyId.Value);
                    cmdLinkSkill.Parameters.AddWithValue("@skill_id", skillId);
                    cmdLinkSkill.ExecuteNonQuery();
                }

                addedCount++;
            }

            Console.WriteLine($"[DB] Добавлено {addedCount} навыков для компании {companyCode}");
        }
        catch (NpgsqlException dbEx)
        {
            Console.WriteLine($"[DB] Ошибка БД при добавлении навыков для {companyCode}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Неожиданная ошибка при добавлении навыков для {companyCode}: {ex.Message}");
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

            Console.WriteLine($"[DB] Загружено {userCodes.Count} кодов пользователей из БД");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Ошибка при получении кодов пользователей: {ex.Message}");
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
            IsPublic: isPublic
        );

        var record = new DbRecord(
            Type: DbRecordType.UserProfile,
            PrimaryValue: userLink,
            SecondaryValue: "",
            UserProfile: profileData
        );
        _saveQueue.Enqueue(record);
        Console.WriteLine(
            $"[DB Queue] UserProfile: {userLink} (code={userCode}) -> Name={userName}, Expert={isExpert}, Level={levelTitle}, Salary={salary}, WorkExp={workExperience}, LastVisit={lastVisit}, Public={isPublic}");

        return true;
    }

    /// <summary>
    /// Обновить информацию о профиле пользователя
    /// </summary>
    public void DatabaseUpdateUserProfile(NpgsqlConnection conn, string userLink, string? userCode, string? userName,
        bool? isExpert, string? levelTitle, string? infoTech, int? salary, string? workExperience = null,
        string? lastVisit = null, bool? isPublic = null)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(userLink))
            throw new ArgumentException("User link must not be empty.", nameof(userLink));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            // Получаем или создаём level_id
            int? levelId = null;
            if (!string.IsNullOrWhiteSpace(levelTitle))
            {
                using (var cmdLevel = new NpgsqlCommand(@"
                    INSERT INTO habr_levels (title, created_at)
                    VALUES (@title, NOW())
                    ON CONFLICT (title) DO UPDATE SET title = EXCLUDED.title
                    RETURNING id", conn))
                {
                    cmdLevel.Parameters.AddWithValue("@title", levelTitle);
                    var result = cmdLevel.ExecuteScalar();
                    if (result != null)
                    {
                        levelId = Convert.ToInt32(result);
                    }
                }
            }

            // Обновляем профиль пользователя по link
            using var cmd = new NpgsqlCommand(@"
                UPDATE habr_resumes 
                SET code = COALESCE(@code, code),
                    title = COALESCE(@title, title),
                    expert = COALESCE(@expert, expert),
                    level_id = COALESCE(@level_id, level_id),
                    info_tech = COALESCE(@info_tech, info_tech),
                    salary = COALESCE(@salary, salary),
                    work_experience = COALESCE(@work_experience, work_experience),
                    last_visit = COALESCE(@last_visit, last_visit),
                    public = COALESCE(@public, public)
                WHERE link = @link", conn);

            cmd.Parameters.AddWithValue("@link", userLink);
            cmd.Parameters.AddWithValue("@code", userCode ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@title", userName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@expert", isExpert ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@level_id", levelId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@info_tech", infoTech ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@salary", salary ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@work_experience", workExperience ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@last_visit", lastVisit ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@public", isPublic ?? (object)DBNull.Value);

            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                //TODO сюда в вывод все поля добавить
                Console.WriteLine(
                    $"[DB] Обновлён профиль для {userLink}: Name={userName}, Expert={isExpert}, Level={levelTitle}, Salary={salary}, WorkExp={workExperience}, LastVisit={lastVisit}");
            }
            else
            {
                Console.WriteLine($"[DB] Пользователь {userLink} не найден в БД.");
            }
        }
        catch (NpgsqlException dbEx)
        {
            Console.WriteLine($"[DB] Ошибка БД для пользователя {userLink}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Неожиданная ошибка при обновлении профиля {userCode}: {ex.Message}");
        }
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
                ? "SELECT link FROM habr_resumes WHERE link IS NOT NULL AND public = true ORDER BY link"
                : "SELECT link FROM habr_resumes WHERE link IS NOT NULL ORDER BY link";

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
            Console.WriteLine($"[DB] Загружено {userLinks.Count} ссылок пользователей из БД{filterText}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Ошибка при получении ссылок пользователей: {ex.Message}");
        }

        return userLinks;
    }

    
    /// <summary>
    /// Добавить детальную информацию о резюме пользователя в очередь
    /// </summary>
    public bool EnqueueUserResumeDetail(string userLink, string? about, List<string>? skills)
    {
        if (_saveQueue == null) return false;
        if (string.IsNullOrWhiteSpace(userLink)) return false;

        // Для about используем структуру UserProfileData (можно расширить позже)
        // Для skills используем отдельную запись
        
        // Обновляем about
        if (!string.IsNullOrWhiteSpace(about))
        {
            var record = new DbRecord(
                Type: DbRecordType.UserAbout,
                PrimaryValue: userLink,
                SecondaryValue: about
            );
            _saveQueue.Enqueue(record);
        }
        
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
        
        Console.WriteLine($"[DB Queue] UserResumeDetail: {userLink} -> About={!string.IsNullOrWhiteSpace(about)}, Skills={skills?.Count ?? 0}");
        
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
                Console.WriteLine($"[DB] Обновлено 'О себе' для {userLink}: {aboutPreview}...");
            }
            else
            {
                Console.WriteLine($"[DB] Пользователь {userLink} не найден в БД.");
            }
        }
        catch (NpgsqlException dbEx)
        {
            Console.WriteLine($"[DB] Ошибка БД для пользователя {userLink}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Неожиданная ошибка при обновлении 'О себе' для {userLink}: {ex.Message}");
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
                Console.WriteLine($"[DB] Пользователь {userLink} не найден в БД. Пропуск навыков.");
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
                    INSERT INTO habr_skills (title, created_at)
                    VALUES (@title, NOW())
                    ON CONFLICT (title) 
                    DO UPDATE SET title = EXCLUDED.title
                    RETURNING id", conn))
                {
                    cmdInsertSkill.Parameters.AddWithValue("@title", skillTitle.Trim());
                    var result = cmdInsertSkill.ExecuteScalar();
                    skillId = Convert.ToInt32(result);
                }

                // Связываем навык с пользователем
                using (var cmdLinkSkill = new NpgsqlCommand(@"
                    INSERT INTO habr_user_skills (user_id, skill_id, created_at)
                    VALUES (@user_id, @skill_id, NOW())
                    ON CONFLICT (user_id, skill_id) DO NOTHING", conn))
                {
                    cmdLinkSkill.Parameters.AddWithValue("@user_id", userId.Value);
                    cmdLinkSkill.Parameters.AddWithValue("@skill_id", skillId);
                    cmdLinkSkill.ExecuteNonQuery();
                }

                addedCount++;
            }

            Console.WriteLine($"[DB] Добавлено {addedCount} навыков для пользователя {userLink}");
        }
        catch (NpgsqlException dbEx)
        {
            Console.WriteLine($"[DB] Ошибка БД при добавлении навыков для {userLink}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Неожиданная ошибка при добавлении навыков для {userLink}: {ex.Message}");
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
        Console.WriteLine($"[DB Queue] UserExperience: {experienceData.UserLink} -> Company={experienceData.CompanyCode}, Position={experienceData.Position}, Skills={experienceData.Skills?.Count ?? 0}");

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
                Console.WriteLine($"[DB] Пользователь {exp.UserLink} не найден в БД. Пропуск опыта работы.");
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
                        Console.WriteLine($"[DB] Удалено {deletedCount} старых записей опыта работы для пользователя {exp.UserLink}");
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
                                INSERT INTO habr_skills (title, created_at)
                                VALUES (@title, NOW())
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
                        INSERT INTO habr_user_experience_skills (experience_id, skill_id, created_at)
                        VALUES (@experience_id, @skill_id, NOW())
                        ON CONFLICT (experience_id, skill_id) DO NOTHING", conn))
                    {
                        cmdLinkSkill.Parameters.AddWithValue("@experience_id", experienceId);
                        cmdLinkSkill.Parameters.AddWithValue("@skill_id", actualSkillId);
                        cmdLinkSkill.ExecuteNonQuery();
                    }
                }
            }

            Console.WriteLine($"[DB] Добавлен опыт работы для {exp.UserLink}: Company={exp.CompanyTitle}, Position={exp.Position}, Skills={exp.Skills?.Count ?? 0}");
        }
        catch (NpgsqlException dbEx)
        {
            Console.WriteLine($"[DB] Ошибка БД при добавлении опыта работы для {exp.UserLink}: {dbEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Неожиданная ошибка при добавлении опыта работы для {exp.UserLink}: {ex.Message}");
        }
    }
}
