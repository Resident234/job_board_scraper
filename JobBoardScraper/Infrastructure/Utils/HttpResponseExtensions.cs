using System.Text;

namespace JobBoardScraper.Infrastructure.Utils;

/// <summary>
/// Расширения для HttpResponseMessage, упрощающие декодирование тела ответа
/// с учётом кодировки из Content-Type.
/// </summary>
public static class HttpResponseExtensions
{
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
        var encoding = response.Content.Headers.ContentType?.CharSet != null
            ? Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet)
            : Encoding.UTF8;
        return encoding.GetString(bytes);
    }
}
