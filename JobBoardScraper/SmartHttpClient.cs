using System.Net;
using JobBoardScraper.Models;

namespace JobBoardScraper;

/// <summary>
/// Умная обёртка над HttpClient с поддержкой:
/// - Автоматических повторов при ошибках (retry)
/// - Измерения трафика
/// - Ротации прокси-серверов
/// - Настройки через конфигурацию
/// </summary>
public sealed class SmartHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly TrafficStatistics? _trafficStats;
    private readonly string _scraperName;
    private readonly bool _enableRetry;
    private readonly bool _enableTrafficMeasuring;
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly TimeSpan _timeout;
    private readonly ProxyRotator? _proxyRotator;

    public SmartHttpClient(
        HttpClient httpClient,
        string scraperName,
        TrafficStatistics? trafficStats = null,
        bool enableRetry = false,
        bool enableTrafficMeasuring = true,
        int maxRetries = 5,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null,
        TimeSpan? timeout = null,
        ProxyRotator? proxyRotator = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _scraperName = scraperName ?? throw new ArgumentNullException(nameof(scraperName));
        _trafficStats = trafficStats;
        _enableRetry = enableRetry;
        _enableTrafficMeasuring = enableTrafficMeasuring && trafficStats != null;
        _maxRetries = maxRetries;
        _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(500);
        _maxDelay = maxDelay ?? TimeSpan.FromSeconds(30);
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
        _proxyRotator = proxyRotator;
    }

    /// <summary>
    /// Получить информацию о текущем прокси (если включен)
    /// </summary>
    public string GetProxyStatus()
    {
        return _proxyRotator?.GetStatus() ?? "No proxy";
    }

    /// <summary>
    /// Ротировать прокси вручную (переключиться на следующий)
    /// </summary>
    public void RotateProxy()
    {
        if (_proxyRotator?.IsEnabled == true)
        {
            _proxyRotator.GetNextProxy();
        }
    }

    /// <summary>
    /// Выполнить GET-запрос с автоматическими повторами и измерением трафика
    /// </summary>
    public async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        if (_enableRetry)
        {
            return await GetWithRetryAsync(requestUri, cancellationToken);
        }
        else
        {
            return await GetSimpleAsync(requestUri, cancellationToken);
        }
    }

    /// <summary>
    /// Выполнить GET-запрос с повторами (для BruteForce)
    /// </summary>
    public async Task<HttpRequestResult> FetchAsync(
        string url,
        Action<string>? infoLog = null,
        Action<HttpResponseMessage>? responseStats = null,
        CancellationToken cancellationToken = default)
    {
        if (!_enableRetry)
        {
            throw new InvalidOperationException("Retry is not enabled for this scraper. Enable it in configuration.");
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await GetSimpleAsync(url, cancellationToken);
                responseStats?.Invoke(response);

                // 404 — не обрабатываем: без повторов и без исключений
                if (response.StatusCode is HttpStatusCode.NotFound)
                {
                    sw.Stop();
                    return new HttpRequestResult
                    {
                        StatusCode = HttpStatusCode.NotFound,
                        Content = null,
                        IsSuccess = false,
                        ElapsedTime = sw.Elapsed,
                        Url = url
                    };
                }

                // 429 - если такой ответ возвращается, то на самом деле страница существует
                if (response.StatusCode is (HttpStatusCode)429)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    sw.Stop();
                    return new HttpRequestResult
                    {
                        StatusCode = response.StatusCode,
                        Content = content,
                        IsSuccess = true,
                        ElapsedTime = sw.Elapsed,
                        Url = url
                    };
                }

                // Не-транзиентный ответ (включая 2xx и большинство 4xx)
                if (!IsTransient(response.StatusCode))
                {
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    sw.Stop();
                    return new HttpRequestResult
                    {
                        StatusCode = response.StatusCode,
                        Content = content,
                        IsSuccess = true,
                        ElapsedTime = sw.Elapsed,
                        Url = url
                    };
                }

                // Транзиентная ошибка: готовим повтор
                if (attempt == _maxRetries)
                {
                    var code = (int)response.StatusCode;
                    var reason = response.ReasonPhrase ?? response.StatusCode.ToString();
                    throw new HttpRequestException($"[Retry] Запрос завершился с кодом {code} ({reason}) после {attempt} попыток.");
                }

                var backoff = ComputeBackoff(attempt);
                var retryAfter = GetRetryAfterDelay(response);
                var delay = retryAfter.HasValue ? Max(backoff, retryAfter.Value) : backoff;

                infoLog?.Invoke($"[Retry] Код={(int)response.StatusCode} ({response.StatusCode}); попытка {attempt}/{_maxRetries}; пауза {FormatTimeSpan(delay)}" +
                                (retryAfter.HasValue ? " (учтен Retry-After)" : ""));

                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                if (attempt == _maxRetries)
                    throw;

                var delay = ComputeBackoff(attempt);
                infoLog?.Invoke($"[Retry] Исключение {ex.GetType().Name}: {ex.Message}; попытка {attempt}/{_maxRetries}; пауза {FormatTimeSpan(delay)}");
                await Task.Delay(delay, cancellationToken);
            }
            finally
            {
                response?.Dispose();
            }
        }

        throw new InvalidOperationException("[Retry] Неожиданное завершение стратегии повторов.");
    }

    private async Task<HttpResponseMessage> GetSimpleAsync(string requestUri, CancellationToken cancellationToken)
    {
        // Создаём CancellationTokenSource с timeout
        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        var response = await _httpClient.GetAsync(requestUri, linkedCts.Token);

        // Измеряем трафик, если включено
        if (_enableTrafficMeasuring && _trafficStats != null && response.Content != null)
        {
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue)
            {
                _trafficStats.RecordRequest(_scraperName, contentLength.Value);
            }
            else
            {
                // Если Content-Length не указан, читаем содержимое для измерения
                var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                _trafficStats.RecordRequest(_scraperName, content.Length);

                // Создаём новый HttpResponseMessage с тем же содержимым
                var newResponse = new HttpResponseMessage(response.StatusCode)
                {
                    Content = new ByteArrayContent(content),
                    ReasonPhrase = response.ReasonPhrase,
                    RequestMessage = response.RequestMessage,
                    Version = response.Version
                };

                // Копируем заголовки
                foreach (var header in response.Headers)
                {
                    newResponse.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                foreach (var header in response.Content.Headers)
                {
                    newResponse.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                response.Dispose();
                return newResponse;
            }
        }

        return response;
    }

    private async Task<HttpResponseMessage> GetWithRetryAsync(string requestUri, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await GetSimpleAsync(requestUri, cancellationToken);

                // Не-транзиентный ответ
                if (!IsTransient(response.StatusCode))
                {
                    return response;
                }

                // Транзиентная ошибка
                if (attempt == _maxRetries)
                {
                    return response;
                }

                var backoff = ComputeBackoff(attempt);
                var retryAfter = GetRetryAfterDelay(response);
                var delay = retryAfter.HasValue ? Max(backoff, retryAfter.Value) : backoff;

                Console.WriteLine($"[{_scraperName}] Retry: код {(int)response.StatusCode}, попытка {attempt}/{_maxRetries}, пауза {FormatTimeSpan(delay)}");

                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                if (attempt == _maxRetries)
                    throw;

                var delay = ComputeBackoff(attempt);
                Console.WriteLine($"[{_scraperName}] Retry: {ex.GetType().Name}, попытка {attempt}/{_maxRetries}, пауза {FormatTimeSpan(delay)}");
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException("[Retry] Неожиданное завершение стратегии повторов.");
    }

    private bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or (HttpStatusCode)429
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private TimeSpan ComputeBackoff(int attempt)
    {
        var expMs = _baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var jitter = expMs * 0.2 * Random.Shared.NextDouble();
        var total = Math.Min(expMs + jitter, _maxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(total);
    }

    private TimeSpan? GetRetryAfterDelay(HttpResponseMessage response)
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

    private TimeSpan Max(TimeSpan a, TimeSpan b) => a >= b ? a : b;

    private string FormatTimeSpan(TimeSpan ts) =>
        ts.TotalSeconds >= 1
            ? $"{ts.TotalSeconds:0.###}s"
            : $"{ts.TotalMilliseconds:0}ms";
}
