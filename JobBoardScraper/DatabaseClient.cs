using System;
using System.Data;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace JobBoardScraper;

public sealed class DatabaseClient
{
    private readonly string _connectionString;
    private Task? _dbWriterTask;
    private CancellationTokenSource? _writerCts;
    private ConcurrentQueue<(string link, string title)>? _saveQueue;

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

    // Вставка ссылки и заголовка страницы
    public void DatabaseInsert(NpgsqlConnection conn, string link, string title)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(link)) throw new ArgumentException("Link must not be empty.", nameof(link));

        try
        {
            DatabaseEnsureConnectionOpen(conn);

            // Проверка существования по link
            if (DatabaseRecordExistsByLink(conn, link))
            {
                Console.WriteLine($"Запись уже есть в БД, вставка пропущена: {link}");
                return;
            }

            using var cmd = new NpgsqlCommand("INSERT INTO habr_resumes (link, title) VALUES (@link, @title)", conn);
            cmd.Parameters.AddWithValue("@link", link);
            cmd.Parameters.AddWithValue("@title", title);
            int rowsAffected = cmd.ExecuteNonQuery();
            Console.WriteLine($"Записано в БД: {rowsAffected} строка, {link} | {title}");
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

    /// <summary>
    /// Запустить фоновую задачу по записи данных из очереди в базу данных
    /// </summary>
    /// <param name="conn">Открытое соединение с базой данных</param>
    /// <param name="queue">Очередь элементов для записи</param>
    /// <param name="token">Токен отмены операции</param>
    /// <param name="delayMs">Задержка между циклами проверки очереди в миллисекундах</param>
    /// <returns>Запущенную задачу</returns>
    public Task StartWriterTask(NpgsqlConnection conn, ConcurrentQueue<(string link, string title)> queue,
        CancellationToken token, int delayMs = 500)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        // Сохраняем ссылку на очередь
        _saveQueue = queue;

        // Создаем внутренний токен отмены, который можно будет отменить при вызове StopWriterTask
        _writerCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var linkedToken = _writerCts.Token;

        _dbWriterTask = Task.Run(async () =>
        {
            while (!linkedToken.IsCancellationRequested)
            {
                while (queue.TryDequeue(out var item))
                {
                    DatabaseInsert(conn, link: item.link, title: item.title);
                }

                await Task.Delay(delayMs, linkedToken);
            }
        }, linkedToken);

        return _dbWriterTask;
    }

    /// <summary>
    /// Запустить фоновую задачу по записи данных в базу данных с использованием внутренней очереди
    /// </summary>
    /// <param name="conn">Открытое соединение с базой данных</param>
    /// <param name="token">Токен отмены операции</param>
    /// <param name="delayMs">Задержка между циклами проверки очереди в миллисекундах</param>
    /// <returns>Запущенную задачу и очередь для добавления элементов</returns>
    public (Task task, ConcurrentQueue<(string link, string title)> queue) StartWriterTask(NpgsqlConnection conn,
        CancellationToken token, int delayMs = 500)
    {
        // Создаем новую очередь
        var queue = new ConcurrentQueue<(string link, string title)>();
        var task = StartWriterTask(conn, queue, token, delayMs);
        return (task, queue);
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
    /// Добавить элемент в очередь на запись в базу данных
    /// </summary>
    /// <param name="link">Ссылка</param>
    /// <param name="title">Заголовок</param>
    /// <returns>true если элемент добавлен, false если задача записи не запущена</returns>
    public bool EnqueueItem(string link, string title)
    {
        if (_saveQueue == null) return false;

        _saveQueue.Enqueue((link, title));
        Console.WriteLine($"[DB] Поступило в очередь: {title} -> {link}");
        
        return true;
    }
}
