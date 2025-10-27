using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace JobBoardScraper
{
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
    }
}
