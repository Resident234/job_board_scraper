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
    CompanyDetails
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

public readonly record struct DbRecord(
    DbRecordType Type, 
    string PrimaryValue, 
    string SecondaryValue, 
    string? TertiaryValue = null, 
    InsertMode Mode = InsertMode.SkipIfExists,
    string? Code = null,
    bool? Expert = null,
    string? WorkExperience = null);

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
                    "INSERT INTO habr_resumes (link, title, slogan, code, expert, work_experience) VALUES (@link, @title, @slogan, @code, @expert, @work_experience)", conn);
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
    public void DatabaseInsertCompany(NpgsqlConnection conn, string companyCode, string companyUrl, string? companyTitle = null)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(companyCode)) throw new ArgumentException("Company code must not be empty.", nameof(companyCode));

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
        if (string.IsNullOrWhiteSpace(categoryId)) throw new ArgumentException("Category ID must not be empty.", nameof(categoryId));

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
                            DatabaseInsertCompany(conn, companyCode: record.PrimaryValue, companyUrl: record.SecondaryValue, companyTitle: record.TertiaryValue);
                            break;
                        case DbRecordType.CategoryRootId:
                            DatabaseInsertCategoryRootId(conn, categoryId: record.PrimaryValue, categoryName: record.SecondaryValue);
                            break;
                        case DbRecordType.CompanyId:
                            if (long.TryParse(record.SecondaryValue, out var companyId))
                            {
                                DatabaseUpdateCompanyId(conn, companyCode: record.PrimaryValue, companyId: companyId);
                            }
                            break;
                        case DbRecordType.CompanyDetails:
                            // Парсим данные из формата "companyId|companyTitle|companyAbout|companySite|companyRating|currentEmployees|pastEmployees"
                            var parts = record.SecondaryValue.Split('|');
                            if (parts.Length >= 7 && long.TryParse(parts[0], out var companyIdDetails))
                            {
                                var companyTitle = string.IsNullOrEmpty(parts[1]) ? null : parts[1];
                                var companyAbout = string.IsNullOrEmpty(parts[2]) ? null : parts[2];
                                var companySite = string.IsNullOrEmpty(parts[3]) ? null : parts[3];
                                
                                decimal? companyRating = null;
                                if (!string.IsNullOrEmpty(parts[4]) && decimal.TryParse(parts[4], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rating))
                                {
                                    companyRating = rating;
                                }
                                
                                int? currentEmployees = null;
                                if (!string.IsNullOrEmpty(parts[5]) && int.TryParse(parts[5], out var currentEmp))
                                {
                                    currentEmployees = currentEmp;
                                }
                                
                                int? pastEmployees = null;
                                if (!string.IsNullOrEmpty(parts[6]) && int.TryParse(parts[6], out var pastEmp))
                                {
                                    pastEmployees = pastEmp;
                                }
                                
                                DatabaseUpdateCompanyDetails(conn, companyCode: record.PrimaryValue, companyId: companyIdDetails, companyTitle: companyTitle, companyAbout: companyAbout, companySite: companySite, companyRating: companyRating, currentEmployees: currentEmployees, pastEmployees: pastEmployees);
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
    /// Добавить company_id, title, about, site, rating и employees в очередь на обновление в базе данных
    /// </summary>
    public bool EnqueueCompanyDetails(string companyCode, long companyId, string? companyTitle, string? companyAbout = null, string? companySite = null, decimal? companyRating = null, int? currentEmployees = null, int? pastEmployees = null)
    {
        if (_saveQueue == null) return false;

        // Формируем строку с данными: "companyId|companyTitle|companyAbout|companySite|companyRating|currentEmployees|pastEmployees"
        var ratingStr = companyRating.HasValue ? companyRating.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
        var currentEmpStr = currentEmployees.HasValue ? currentEmployees.Value.ToString() : "";
        var pastEmpStr = pastEmployees.HasValue ? pastEmployees.Value.ToString() : "";
        var dataString = $"{companyId}|{companyTitle ?? ""}|{companyAbout ?? ""}|{companySite ?? ""}|{ratingStr}|{currentEmpStr}|{pastEmpStr}";
        var record = new DbRecord(DbRecordType.CompanyDetails, companyCode, dataString);
        _saveQueue.Enqueue(record);
        
        var aboutPreview = companyAbout?.Substring(0, Math.Min(50, companyAbout?.Length ?? 0)) ?? "";
        Console.WriteLine($"[DB Queue] CompanyDetails: {companyCode} -> ID={companyId}, Title={companyTitle}, About={aboutPreview}..., Site={companySite}, Rating={companyRating}, Employees={currentEmployees}/{pastEmployees}");
        
        return true;
    }

    /// <summary>
    /// Обновить company_id для компании
    /// </summary>
    public void DatabaseUpdateCompanyId(NpgsqlConnection conn, string companyCode, long companyId)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(companyCode)) throw new ArgumentException("Company code must not be empty.", nameof(companyCode));

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
    /// Обновить company_id, title, about, site, rating и employees для компании
    /// </summary>
    public void DatabaseUpdateCompanyDetails(NpgsqlConnection conn, string companyCode, long companyId, string? companyTitle, string? companyAbout, string? companySite, decimal? companyRating, int? currentEmployees, int? pastEmployees)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(companyCode)) throw new ArgumentException("Company code must not be empty.", nameof(companyCode));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@"
                UPDATE habr_companies 
                SET company_id = @company_id, 
                    title = COALESCE(@title, title),
                    about = COALESCE(@about, about),
                    site = COALESCE(@site, site),
                    rating = COALESCE(@rating, rating),
                    current_employees = COALESCE(@current_employees, current_employees),
                    past_employees = COALESCE(@past_employees, past_employees),
                    updated_at = NOW()
                WHERE code = @code", conn);
            
            cmd.Parameters.AddWithValue("@code", companyCode);
            cmd.Parameters.AddWithValue("@company_id", companyId);
            cmd.Parameters.AddWithValue("@title", companyTitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@about", companyAbout ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@site", companySite ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@rating", companyRating.HasValue ? (object)companyRating.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@current_employees", currentEmployees.HasValue ? (object)currentEmployees.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@past_employees", pastEmployees.HasValue ? (object)pastEmployees.Value : DBNull.Value);
            
            int rowsAffected = cmd.ExecuteNonQuery();
            
            if (rowsAffected > 0)
            {
                var aboutPreview = companyAbout?.Substring(0, Math.Min(50, companyAbout.Length)) ?? "";
                Console.WriteLine($"[DB] Обновлены данные для {companyCode}: ID={companyId}, Title={companyTitle}, About={aboutPreview}..., Site={companySite}, Rating={companyRating}, Employees={currentEmployees}/{pastEmployees}");
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
}
