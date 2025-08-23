namespace job_board_scraper;

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public static class HttpRetry
{
    public static async Task<HttpResponseMessage> GetStringWithRetriesAsync(
        HttpClient client,
        string url,
        int maxRetries = 5,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null,
        Action<string> infoLog = null,
        Action<HttpResponseMessage> responseStats = null)
    {
        if (client == null) throw new ArgumentNullException(nameof(client));
        if (url == null) throw new ArgumentNullException(nameof(url));
        if (infoLog == null) throw new ArgumentNullException(nameof(infoLog));
        if (responseStats == null) throw new ArgumentNullException(nameof(responseStats));
        baseDelay ??= TimeSpan.FromMilliseconds(500);
        maxDelay ??= TimeSpan.FromSeconds(30);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

                responseStats?.Invoke(response);

                // 404 — не обрабатываем: без повторов и исключений
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return response;
                
                if (!IsTransient(response.StatusCode))
                {
                    // Бросит исключение для неуспешных не‑транзиентных кодов (например, 4xx, кроме 408/429)
                    response.EnsureSuccessStatusCode();
                    return response;
                }

                // Транзиентная ошибка: готовим повтор (если есть попытки)
                if (attempt == maxRetries)
                {
                    // Последняя попытка — вернем подробность ошибки
                    var code = (int)response.StatusCode;
                    var reason = response.ReasonPhrase ?? response.StatusCode.ToString();
                    throw new HttpRequestException($"Запрос завершился с кодом {code} ({reason}) после {attempt} попыток.");
                }

                var backoff = ComputeBackoff(attempt, baseDelay.Value, maxDelay.Value);
                var retryAfter = GetRetryAfterDelay(response);
                var delay = retryAfter.HasValue ? Max(backoff, retryAfter.Value) : backoff;

                infoLog?.Invoke($"[Retry] Код={(int)response.StatusCode} ({response.StatusCode}); попытка {attempt}/{maxRetries}; пауза {Format(delay)}" +
                                (retryAfter.HasValue ? " (учтен Retry-After)" : ""));

                await Task.Delay(delay).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                if (attempt == maxRetries)
                    throw;

                var delay = ComputeBackoff(attempt, baseDelay.Value, maxDelay.Value);
                infoLog?.Invoke($"[Retry] Исключение {ex.GetType().Name}: {ex.Message}; попытка {attempt}/{maxRetries}; пауза {Format(delay)}");
                await Task.Delay(delay).ConfigureAwait(false);
            }
            finally
            {
                response?.Dispose();
            }
        }

        // Сюда не дойдем
        throw new InvalidOperationException("Неожиданное завершение стратегии повторов.");
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout         // 408
            or (HttpStatusCode)429                       // 429 Too Many Requests
            or HttpStatusCode.InternalServerError        // 500
            or HttpStatusCode.BadGateway                 // 502
            or HttpStatusCode.ServiceUnavailable         // 503
            or HttpStatusCode.GatewayTimeout;            // 504

    private static TimeSpan ComputeBackoff(int attempt, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        // экспонента + 0..20% джиттера
        var expMs = baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var jitter = expMs * 0.2 * Random.Shared.NextDouble();
        var total = Math.Min(expMs + jitter, maxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(total);
    }

    private static TimeSpan? GetRetryAfterDelay(HttpResponseMessage response)
    {
        var ra = response.Headers.RetryAfter;
        if (ra == null) return null;

        if (ra.Delta.HasValue)
            return ra.Delta;

        if (ra.Date.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            var delay = ra.Date.Value - now;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a >= b ? a : b;

    private static string Format(TimeSpan ts) =>
        ts.TotalSeconds >= 1
            ? $"{ts.TotalSeconds:0.###}s"
            : $"{ts.TotalMilliseconds:0}ms";

}