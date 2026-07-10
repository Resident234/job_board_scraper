using System.Text;

namespace JobBoardScraper.Infrastructure.Http;

/// <summary>
/// Расширения для HttpResponseMessage, упрощающие декодирование тела ответа
/// с учётом кодировки из Content-Type.
/// </summary>
public static class HttpResponse
{
    /// <summary>
    /// Возвращает кодировку, указанную в заголовке Content-Type (charset),
    /// или <see cref="Encoding.UTF8"/>, если charset не указан.
    /// </summary>
    /// <param name="response">HTTP-ответ (источник кодировки из Content-Type).</param>
    /// <returns>Кодировка для декодирования тела ответа.</returns>
    public static Encoding GetEncoding(this HttpResponseMessage response)
    {
        var charset = response.Content.Headers.ContentType?.CharSet;
        return charset != null ? Encoding.GetEncoding(charset) : Encoding.UTF8;
    }

    /// <summary>
    /// Декодирует байты тела HTTP-ответа в строку. Кодировка определяется по
    /// заголовку Content-Type (charset). Если charset не указан, используется UTF-8.
    /// </summary>
    /// <param name="response">HTTP-ответ (источник кодировки из Content-Type).</param>
    /// <param name="bytes">Байты тела ответа, полученные, например, из
    /// <see cref="HttpContent.ReadAsByteArrayAsync(CancellationToken)"/>.</param>
    /// <returns>Декодированное тело ответа в виде строки.</returns>
    public static string DecodeBodyAsString(
        this HttpResponseMessage response,
        byte[] bytes)
    {
        return response.GetEncoding().GetString(bytes);
    }
}
