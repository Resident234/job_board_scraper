using System.Text;

namespace JobBoardScraper.Infrastructure.Utils;

/// <summary>
/// Вспомогательный класс для сохранения HTML-страниц в файлы для отладки
/// </summary>
public static class HtmlDebug
{
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

        try
        {
            var directory = outputDirectory ?? AppConfig.LoggingOutputDirectory;
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var filePath = Path.Combine(directory, $"{scraperName}_{fileName}");
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
