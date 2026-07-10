using System.Text;
using JobBoardScraper.Infrastructure.Logging;

namespace JobBoardScraper.Infrastructure.Utils;

/// <summary>
/// Вспомогательный класс для сохранения HTML-страниц в файлы для отладки.
/// Логирует результат сохранения через переданный <see cref="ConsoleLogger"/>:
/// при успехе — <c>💾 HTML сохранён: {path} (кодировка: {encoding})</c>,
/// при ошибке/пустом html — <c>⚠ Не удалось сохранить HTML...</c>.
/// </summary>
public static class HtmlDebug
{
    /// <summary>
    /// Сохраняет HTML в файл и логирует результат через <paramref name="logger"/>.
    /// </summary>
    /// <param name="html">HTML-код для сохранения.</param>
    /// <param name="scraperName">Имя скрапера (используется в имени файла).</param>
    /// <param name="logger">Логгер, в который пишется информация о сохранении/ошибке.</param>
    /// <param name="outputDirectory">Каталог для сохранения (если не задан — берётся из <see cref="AppConfig"/>).</param>
    /// <param name="encoding">Кодировка файла (по умолчанию UTF-8).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Полный путь к сохранённому файлу или <c>null</c>, если сохранить не удалось.</returns>
    public static async Task<string?> SaveHtmlAsync(
        string html,
        string scraperName,
        ConsoleLogger? logger,
        string? outputDirectory = null,
        Encoding? encoding = null,
        CancellationToken ct = default)
    {
        var savedPath = await SaveHtmlAsync(
            html,
            scraperName,
            outputDirectory,
            encoding,
            ct);

        if (savedPath != null)
        {
            logger?.WriteLine($"💾 HTML сохранён: {savedPath} (кодировка: {encoding?.WebName ?? "utf-8"})");
        }
        else
        {
            logger?.WriteLine("⚠ Не удалось сохранить HTML для отладки.");
        }

        return savedPath;
    }

    /// <summary>
    /// Сохраняет HTML в файл без логирования.
    /// </summary>
    /// <param name="html">HTML-код для сохранения. Если пустой — возвращается <c>null</c>.</param>
    /// <param name="scraperName">Имя скрапера (используется в имени файла).</param>
    /// <param name="outputDirectory">Каталог для сохранения (если не задан — берётся из <see cref="AppConfig"/>).</param>
    /// <param name="encoding">Кодировка файла (по умолчанию UTF-8).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Полный путь к сохранённому файлу или <c>null</c>, если сохранить не удалось.</returns>
    public static async Task<string?> SaveHtmlAsync(
        string html,
        string scraperName,
        string? outputDirectory = null,
        Encoding? encoding = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        try
        {
            var directory = outputDirectory ?? AppConfig.LoggingOutputDirectory;
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var cleanName = scraperName.EndsWith("Scraper")
                ? scraperName.Substring(0, scraperName.Length - "Scraper".Length)
                : scraperName;

            var fileName = $"{cleanName}_last_page.html";
            var filePath = Path.Combine(directory, fileName);
            var enc = encoding ?? Encoding.UTF8;

            await File.WriteAllTextAsync(filePath, html, enc, ct);
            return filePath;
        }
        catch
        {
            return null;
        }
    }
}
