using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using JobBoardScraper.Core;
using JobBoardScraper.Infrastructure.Utils;
using JobBoardScraper.Infrastructure.Http;
using System.Text;

namespace JobBoardScraper.Parsing;

public static class HtmlParser
{
    private static readonly AngleSharp.Html.Parser.HtmlParser _parser = new();

    /// <summary>
    /// Извлекает заголовок страницы из HTML-кода
    /// </summary>
    /// <param name="html">HTML-код страницы</param>
    /// <returns>Текст заголовка или пустая строка</returns>
    public static string ExtractTitle(string html)
    {
        var doc = _parser.ParseDocument(html);
        return doc.QuerySelector("title")?.TextContent?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Асинхронно парсит HTML-код в документ AngleSharp
    /// </summary>
    /// <param name="html">HTML-код для парсинга</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Документ HTML</returns>
    public static Task<IHtmlDocument> ParseDocumentAsync(string html, CancellationToken ct = default)
    {
        return _parser.ParseDocumentAsync(html, ct);
    }

    public static async Task<(string Html, Encoding Encoding)> ReadHtmlAsync(
        HttpResponseMessage response,
        CancellationToken ct = default)
    {
        var htmlBytes = await response.Content.ReadAsByteArrayAsync(ct);
        var encoding = response.GetEncoding();
        var html = response.DecodeBodyAsString(htmlBytes);

        return (html, encoding);
    }

    /// <summary>
    /// Проверяет, содержит ли HTML-код сообщение о суточном лимите на просмотр профилей.
    /// Используется скраперами для определения момента, когда IP-адрес заблокирован на сутки
    /// и нужно переключиться на другой прокси/прервать обработку.
    /// </summary>
    /// <param name="html">HTML-код страницы</param>
    /// <returns>true, если в HTML найден маркер суточного лимита; иначе false.</returns>
    public static bool ContainsDailyLimitMessage(string html)
    {
        if (string.IsNullOrEmpty(html))
            return false;

        var dailyLimitMessage = AppConfig.ProxyWhitelistDailyLimitMessage;
        return dailyLimitMessage != null && html.Contains(dailyLimitMessage, StringComparison.OrdinalIgnoreCase);
    }

}


