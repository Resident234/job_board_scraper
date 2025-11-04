using System.Text;

namespace JobBoardScraper.Helper.Utils;

/// <summary>
/// Вспомогательный класс для сохранения HTML-страниц в файлы для отладки
/// </summary>
public static class HtmlDebug
{
    /// <summary>
    /// Сохраняет HTML-контент в файл для отладки
    /// </summary>
    /// <param name="html">HTML-контент для сохранения</param>
    /// <param name="scraperName">Название скрапера (используется как префикс имени файла)</param>
    /// <param name="fileName">Имя файла (без префикса)</param>
    /// <param name="outputDirectory">Директория для сохранения (по умолчанию из AppConfig)</param>
    /// <param name="encoding">Кодировка для сохранения (по умолчанию UTF-8)</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Путь к сохранённому файлу или null в случае ошибки</returns>
    public static async Task<string?> SaveHtmlAsync(
        string html,
        string scraperName,
        string fileName = "last_page.html",
        string? outputDirectory = null,
        Encoding? encoding = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        if (string.IsNullOrWhiteSpace(scraperName))
            throw new ArgumentException("Scraper name must not be empty.", nameof(scraperName));

        try
        {
            var directory = outputDirectory ?? AppConfig.LoggingOutputDirectory;
            
            // Создаём директорию, если её нет
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Формируем имя файла с префиксом скрапера
            var prefixedFileName = $"{scraperName}_{fileName}";
            var filePath = Path.Combine(directory, prefixedFileName);

            // Используем UTF-8 по умолчанию
            var fileEncoding = encoding ?? Encoding.UTF8;

            // Сохраняем файл
            await File.WriteAllTextAsync(filePath, html, fileEncoding, ct);

            return filePath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Сохраняет HTML-контент в файл для отладки (синхронная версия)
    /// </summary>
    /// <param name="html">HTML-контент для сохранения</param>
    /// <param name="scraperName">Название скрапера (используется как префикс имени файла)</param>
    /// <param name="fileName">Имя файла (без префикса)</param>
    /// <param name="outputDirectory">Директория для сохранения (по умолчанию из AppConfig)</param>
    /// <param name="encoding">Кодировка для сохранения (по умолчанию UTF-8)</param>
    /// <returns>Путь к сохранённому файлу или null в случае ошибки</returns>
    public static string? SaveHtml(
        string html,
        string scraperName,
        string fileName = "last_page.html",
        string? outputDirectory = null,
        Encoding? encoding = null)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        if (string.IsNullOrWhiteSpace(scraperName))
            throw new ArgumentException("Scraper name must not be empty.", nameof(scraperName));

        try
        {
            var directory = outputDirectory ?? AppConfig.LoggingOutputDirectory;
            
            // Создаём директорию, если её нет
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Формируем имя файла с префиксом скрапера
            var prefixedFileName = $"{scraperName}_{fileName}";
            var filePath = Path.Combine(directory, prefixedFileName);

            // Используем UTF-8 по умолчанию
            var fileEncoding = encoding ?? Encoding.UTF8;

            // Сохраняем файл
            File.WriteAllText(filePath, html, fileEncoding);

            return filePath;
        }
        catch
        {
            return null;
        }
    }
}
