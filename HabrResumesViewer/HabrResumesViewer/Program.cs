using System.Net;
using System.Text;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Подключение к БД из переменной окружения или appsettings
var connString = builder.Configuration.GetConnectionString("Jobs")
                  ?? Environment.GetEnvironmentVariable("JOBS_CS")
                  ?? "Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;";

var dataSource = new NpgsqlDataSourceBuilder(connString).Build();
builder.Services.AddSingleton(dataSource);

var app = builder.Build();

// Главная страница: показывает одну запись и устанавливает viewed=1
app.MapGet("/", async (HttpContext http, NpgsqlDataSource ds, long? id, CancellationToken ct) =>
{
    await using var conn = await ds.OpenConnectionAsync(ct);

    // 1) Найти текущую запись:
    //    - если id не задан, берём ближайшую (минимальный id) с viewed != 1
    //    - если id задан, загружаем по id (независимо от viewed), чтобы отобразить то, что запросили
    var getOneSqlWhenNoId = @"
        SELECT id, title, link
        FROM habr_resumes
        WHERE viewed IS NULL
        ORDER BY id ASC
        LIMIT 1;
    ";

    var getByIdSql = @"
        select id, title, link
        from habr_resumes
        where id = @id
        limit 1;
    ";
    
    long? currentId = null;
    string? currentTitle = null;
    string? currentUrl = null;

    await using (var cmd = conn.CreateCommand())
    {
        if (id is null)
        {
            cmd.CommandText = getOneSqlWhenNoId;
        }
        else
        {
            cmd.CommandText = getByIdSql;
            cmd.Parameters.AddWithValue("id", id.Value);
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            currentId = reader.GetInt64(0);
            currentTitle = reader.IsDBNull(1) ? "" : reader.GetString(1);
            currentUrl = reader.IsDBNull(2) ? "" : reader.GetString(2);
        }
    }

    // Если ничего не нашли
    if (currentId is null)
    {
        var htmlEmpty = """
            <!doctype html>
            <html lang="ru">
            <head>
              <meta charset="utf-8"/>
              <meta name="viewport" content="width=device-width, initial-scale=1"/>
              <title>Нет записей</title>
              <style>body{font-family:system-ui,Segoe UI,Arial,sans-serif;margin:2rem}</style>
            </head>
            <body>
              <h1>Нет записей для отображения</h1>
              <p>В таблице нет непросмотренных записей (viewed != 1).</p>
            </body>
            </html>
            """;
        http.Response.ContentType = "text/html; charset=utf-8";
        await http.Response.WriteAsync(htmlEmpty, ct);
        return;
    }

    // 2) Пометить запись как просмотренную: viewed = 1
    await using (var updateCmd = conn.CreateCommand())
    {
        updateCmd.CommandText = "update habr_resumes set viewed = B'1'::bit(1) where id = @id and viewed IS NULL";
        updateCmd.Parameters.AddWithValue("id", currentId.Value);
        await updateCmd.ExecuteNonQueryAsync(ct);
    }

    // 3) Найти prev/next среди НЕпросмотренных (viewed != 1), относительно текущего id
    long? prevId = null;
    long? nextId = null;

    await using (var prevCmd = conn.CreateCommand())
    {
        prevCmd.CommandText = @"
            select id
            from habr_resumes
            where id < @id and viewed IS NULL
            order by id desc
            limit 1;";
        prevCmd.Parameters.AddWithValue("id", currentId.Value);
        var val = await prevCmd.ExecuteScalarAsync(ct);
        if (val != null && val != DBNull.Value) prevId = (long)val;
    }

    await using (var nextCmd = conn.CreateCommand())
    {
        nextCmd.CommandText = @"
            select id
            from habr_resumes
            where id > @id and viewed IS NULL
            order by id asc
            limit 1;";
        nextCmd.Parameters.AddWithValue("id", currentId.Value);
        var val = await nextCmd.ExecuteScalarAsync(ct);
        if (val != null && val != DBNull.Value) nextId = (long)val;
    }

    // 4) Рендер HTML
    var title = WebUtility.HtmlEncode(currentTitle ?? "");
    var link = WebUtility.HtmlEncode(currentUrl ?? "");

    var sb = new StringBuilder();
    sb.Append("""
        <!doctype html>
        <html lang="ru">
        <head>
          <meta charset="utf-8"/>
          <meta name="viewport" content="width=device-width, initial-scale=1"/>
          <title>Резюме</title>
          <style>
            :root { color-scheme: light dark; }
            body { font-family: system-ui, Segoe UI, Arial, sans-serif; margin: 2rem; }
            .nav { margin-top: 2rem; display: flex; gap: 1rem; }
            a.button { padding: .6rem 1rem; border: 1px solid #8884; border-radius: .5rem; text-decoration: none; }
            a.button[aria-disabled="true"] { pointer-events: none; opacity: .5; }
            .meta { color: #666; margin-top: .25rem; }
          </style>
        </head>
        <body>
    """);

    sb.Append($"  <h1>{title}</h1>\n");
    if (!string.IsNullOrWhiteSpace(link))
    {
        sb.Append($"  <p><a class=\"resume_link\" href=\"{link}\" target=\"_blank\" rel=\"noopener noreferrer\">{link}</a></p>\n");
    }
    else
    {
        sb.Append("  <p class=\"meta\">Ссылка отсутствует</p>\n");
    }

    sb.Append("  <div class=\"meta\">");
    sb.Append($"ID текущей записи: {currentId}");
    sb.Append("</div>\n");

    sb.Append("  <div class=\"nav\">\n");

    if (prevId is not null)
        sb.Append($"    <a class=\"button\" href=\"/?id={prevId}\">← Предыдущая</a>\n");
    else
        sb.Append("    <a class=\"button\" aria-disabled=\"true\" href=\"#\">← Предыдущая</a>\n");

    if (nextId is not null)
        sb.Append($"    <a class=\"button\" href=\"/?id={nextId}\">Следующая →</a>\n");
    else
        sb.Append("    <a class=\"button\" aria-disabled=\"true\" href=\"#\">Следующая →</a>\n");

    sb.Append("  </div>\n");
    sb.Append("<script>\nwindow.addEventListener('load', function() {\n const link = document.querySelector('a.resume_link:not([aria-disabled=\"true\"])');\n if (link) {\n setTimeout(() => {\n link.click();\n }, 300); \n }\n });\n </script>\n\n");
    sb.Append("""
        </body>
        </html>
    """);

    http.Response.ContentType = "text/html; charset=utf-8";
    await http.Response.WriteAsync(sb.ToString(), ct);
});

app.Run();

// Модель для справки 
public record Resume(long Id, string Title, string Url);

// TODO исключать из перебора заблокированных и скрытых настроками приватности 
// 