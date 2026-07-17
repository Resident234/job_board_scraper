using JobBoardScraper.Core;
using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Infrastructure.Throttling;

namespace JobBoardScraper.Infrastructure.Proxy;


/// <summary>
/// Результат выполнения HTTP-запроса через <see cref="ProxyRetryExecutor"/>.
/// </summary>
/// <param name="ProxyUrl">URL прокси, через который был сделан успешный (или последний) запрос. null, если прокси не использовался или запрос не выполнился.</param>
/// <param name="Response">HTTP-ответ (если был получен) или null, если все попытки исчерпаны.</param>
public readonly record struct ProxyRequestResult(string? ProxyUrl, HttpResponseMessage? Response);

/// <summary>
/// Исполнитель HTTP-запросов с автоматическим retry и переключением прокси.
/// Инкапсулирует:
/// <list type="bullet">
///   <item>Внешний цикл переключения прокси (при недоступности текущего).</item>
///   <item>Внутренний цикл retry с тем же прокси (при 5xx / 408 / 429).</item>
///   <item>Расчёт backoff через <see cref="ExponentialBackoff"/>.</item>
///   <item>Анализ status code и реакцию на 403 (немедленная смена прокси).</item>
///   <item>Сообщения координатору об успехе/неудаче прокси.</item>
/// </list>
/// Все эти правила ранее дублировались в каждом скрапере; теперь они собраны в одном месте.
/// </summary>
public sealed class ProxyRetryExecutor
{
    private readonly ProxyHttpClientFactory _clientFactory;
    private readonly ConsoleLogger _logger;
    private readonly int _maxRetriesPerProxy;
    private readonly int _maxProxySwitches;

    /// <summary>
    /// Создаёт исполнитель.
    /// </summary>
    public ProxyRetryExecutor(
        ProxyHttpClientFactory clientFactory,
        ConsoleLogger? logger = null,
        int? maxRetriesPerProxy = null,
        int? maxProxySwitches = null)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger ?? new ConsoleLogger(nameof(ProxyRetryExecutor));
        _maxRetriesPerProxy = maxRetriesPerProxy ?? AppConfig.ProxyMaxRetries;
        _maxProxySwitches = maxProxySwitches ?? AppConfig.ProxyMaxSwitches;
    }

    /// <summary>
    /// Выполняет HTTP-запрос с retry и переключением прокси.
    /// </summary>
    /// <param name="url">URL запроса (используется только для логирования).</param>
    /// <param name="coordinator">Координатор прокси. Если null — прокси не используются.</param>
    /// <param name="fallbackSend">
    /// Функция запроса через "обычный" клиент (без прокси).
    /// Вызывается, когда прокси недоступен или координатор не задан.
    /// </param>
    /// <param name="proxySend">
    /// Функция запроса, которая получает HttpClient с настроенным прокси.
    /// Вызывается для каждой попытки с активным прокси.
    /// </param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат: ProxyUrl + Response (последняя успешная попытка), либо Response=null если все попытки провалились.</returns>
    public async Task<ProxyRequestResult> ExecuteAsync(
        string url,
        IProxyManager? coordinator,
        Func<Task<HttpResponseMessage>> fallbackSend,
        Func<HttpClient, Task<HttpResponseMessage>> proxySend,
        CancellationToken ct)
    {
        if (fallbackSend == null) throw new ArgumentNullException(nameof(fallbackSend));
        if (proxySend == null) throw new ArgumentNullException(nameof(proxySend));

        // Если прокси не настроены — выполняем простой запрос без ретраев.
        if (coordinator == null)
        {
            _logger.WriteLine($"Обработка: {url}");
            var resp = await fallbackSend().ConfigureAwait(false);
            return new ProxyRequestResult(null, resp);
        }

        HttpResponseMessage? response = null;
        string? proxyUrl = null;
        int proxySwitch = 0;
        bool success = false;

        // Внешний цикл: переключение прокси при исчерпании попыток / 403
        while (proxySwitch <= _maxProxySwitches && !success)
        {
            HttpClient? proxyHttpClient = null;

            proxyUrl = await _clientFactory.WaitForProxyAsync(coordinator, ct).ConfigureAwait(false);
            if (proxyUrl != null)
            {
                _logger.WriteLine($"Обработка: {url} | Прокси #{proxySwitch + 1}: {proxyUrl}");
                proxyHttpClient = _clientFactory.CreateClient(proxyUrl);
            }
            else
            {
                _logger.WriteLine($"Обработка: {url} | Нет доступных прокси");
            }

            // Внутренний цикл: retry с тем же прокси
            int attempt = 0;
            while (attempt < _maxRetriesPerProxy && !success)
            {
                attempt++;

                try
                {
                    response = proxyHttpClient != null
                        ? await proxySend(proxyHttpClient).ConfigureAwait(false)
                        : await fallbackSend().ConfigureAwait(false);

                    var statusCode = (int)response.StatusCode;

                    // 200/404 — успех (404 не повторяем, но он считается "доставленным ответом")
                    if (response.IsSuccessStatusCode || statusCode == 404)
                    {
                        success = true;
                        break;
                    }

                    // 403 — IP заблокирован, сразу меняем прокси
                    if (statusCode == 403)
                    {
                        _logger.WriteLine("Forbidden 403 - IP заблокирован, переключаем прокси");
                        response.Dispose();
                        response = null;
                        break;
                    }

                    // Расчёт backoff и принятие решения о повторе
                    var decision = EvaluateRetry(statusCode, attempt, _maxRetriesPerProxy, response);
                    if (decision.ShouldRetry)
                    {
                        _logger.WriteLine(HttpClientLogger.FormatThrottleRetry(
                            attempt,
                            attempt + 1,
                            _maxRetriesPerProxy,
                            $"прокси #{proxySwitch + 1}: {url}",
                            decision.DelayMs,
                            $"{decision.ErrorType} {statusCode}"));
                        response.Dispose();
                        response = null;
                        await Task.Delay(decision.DelayMs, ct).ConfigureAwait(false);
                        continue;
                    }

                    // Исчерпаны попытки с текущим прокси
                    if (attempt >= _maxRetriesPerProxy)
                    {
                        _logger.WriteLine(
                            $"{decision.ErrorType} {statusCode} - исчерпаны попытки с текущим прокси");
                        response.Dispose();
                        response = null;
                        break; // → переход на смену прокси
                    }

                    // Другие неуспешные коды — повторяем
                    var delayMs = ExponentialBackoff.CalculateProxyErrorDelay(attempt);
                    _logger.WriteLine(HttpClientLogger.FormatThrottleRetry(
                        attempt,
                        attempt + 1,
                        _maxRetriesPerProxy,
                        $"прокси #{proxySwitch + 1}: {url}",
                        delayMs,
                        $"HTTP error {statusCode}"));
                    response.Dispose();
                    response = null;
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < _maxRetriesPerProxy)
                {
                    var delay = ExponentialBackoff.CalculateProxyErrorDelay(attempt);
                    _logger.WriteLine(HttpClientLogger.FormatThrottleRetry(
                        attempt,
                        attempt + 1,
                        _maxRetriesPerProxy,
                        $"прокси #{proxySwitch + 1}: {url}",
                        delay,
                        $"{ex.GetType().Name}: {ex.Message}"));
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.WriteLine($"✖ Ошибка (попытка {attempt}/{_maxRetriesPerProxy}). Исчерпаны попытки с текущим прокси: {ex.Message}");
                    break;
                }
            }

            _clientFactory.DisposeClient(proxyHttpClient);

            if (success)
                break;

            // Неуспех с конкретным прокси — сообщаем координатору
            if (proxyUrl != null)
            {
                coordinator.ReportFailure(proxyUrl);
            }
            proxySwitch++;
            if (proxySwitch <= _maxProxySwitches)
            {
                _logger.WriteLine($"Переключение на следующий прокси (смена #{proxySwitch}/{_maxProxySwitches})...");
            }
        }

        if (!success)
        {
            _logger.WriteLine(
                $"Не удалось получить ответ после {_maxProxySwitches + 1} прокси × {_maxRetriesPerProxy} попыток");
            return new ProxyRequestResult(proxyUrl, null);
        }

        return new ProxyRequestResult(proxyUrl, response);
    }

    /// <summary>
    /// Принимает решение о повторе HTTP-запроса по status code.
    /// </summary>
    private static (bool ShouldRetry, int DelayMs, string ErrorType) EvaluateRetry(
        int statusCode, int attempt, int maxRetries, HttpResponseMessage response)
    {
        // 5xx — server error, retry с exponential backoff
        if (statusCode >= 500 && statusCode < 600)
        {
            return (attempt < maxRetries, ExponentialBackoff.CalculateServerErrorDelay(attempt), "Server error");
        }

        // 429 — rate limit, повтор с увеличенным backoff (или Retry-After header)
        if (statusCode == 429)
        {
            int delayMs;
            if (response.Headers.TryGetValues("Retry-After", out var values)
                && int.TryParse(values.FirstOrDefault(), out var seconds))
            {
                delayMs = seconds * 1000;
            }
            else
            {
                delayMs = ExponentialBackoff.CalculateServerErrorDelay(attempt) * 2;
            }
            return (attempt < maxRetries, delayMs, "Rate limited");
        }

        // 408 — request timeout, retry
        if (statusCode == 408)
        {
            return (attempt < maxRetries, ExponentialBackoff.CalculateProxyErrorDelay(attempt), "Request timeout");
        }

        // 404 — не повторяем
        if (statusCode == 404)
        {
            return (false, 0, "Not found");
        }

        return (false, 0, "HTTP error");
    }

    /// <summary>
    /// Уведомляет координатор об успешном использовании прокси (если координатор задан).
    /// </summary>
    public static void ReportSuccessSafe(IProxyManager? coordinator, string? proxyUrl)
    {
        if (coordinator != null && !string.IsNullOrEmpty(proxyUrl))
        {
            coordinator.ReportSuccess(proxyUrl);
        }
    }

    /// <summary>
    /// Уведомляет координатор о достижении суточного лимита (если координатор задан)
    /// и логирует сообщение через указанный логгер.
    /// </summary>
    public static void ReportDailyLimitSafe(IProxyManager? coordinator, string? proxyUrl, ConsoleLogger? logger = null)
    {
        logger ??= new ConsoleLogger(nameof(ProxyRetryExecutor));

        if (coordinator != null && !string.IsNullOrEmpty(proxyUrl))
        {
            logger.WriteLine($"Обнаружен суточный лимит для прокси: {proxyUrl}");
            coordinator.ReportDailyLimitReached(proxyUrl);
        }
    }

    /// <summary>
    /// Реакция на обнаружение суточного лимита: уведомляет координатор,
    /// запрашивает следующий прокси и пишет соответствующее сообщение в лог.
    /// Возвращает true, если удалось получить новый прокси для повторной обработки.
    /// </summary>
    public static bool HandleDailyLimit(
        IProxyManager? coordinator,
        string? proxyUrl,
        string userLink,
        ConsoleLogger? logger = null)
    {
        logger ??= new ConsoleLogger(nameof(ProxyRetryExecutor));
        ReportDailyLimitSafe(coordinator, proxyUrl, logger);

        var newProxy = coordinator?.GetNextProxy();
        if (newProxy != null)
        {
            logger.WriteLine($"⏭ Переключение на новый прокси: {newProxy}");
            return true;
        }

        logger.WriteLine($"⏭ Нет доступных прокси, пропускаем профиль: {userLink}");
        return false;
    }
}
