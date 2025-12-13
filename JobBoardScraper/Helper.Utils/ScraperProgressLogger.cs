using JobBoardScraper.Helper.ConsoleHelper;
using JobBoardScraper.Models;

namespace JobBoardScraper.Helper.Utils;

/// <summary>
/// Объединяет счётчик прогресса и логирование для скраперов.
/// Предоставляет единый интерфейс для отслеживания и вывода прогресса.
/// </summary>
public sealed class ScraperProgressLogger
{
    private readonly ProgressTracker _progress;
    private readonly ConsoleLogger? _logger;
    private readonly string _scraperName;
    private int _activeRequests;

    /// <summary>
    /// Количество обработанных элементов
    /// </summary>
    public int Processed => _progress.Processed;

    /// <summary>
    /// Общее количество элементов
    /// </summary>
    public int Total => _progress.Total;

    /// <summary>
    /// Процент выполнения (0-100)
    /// </summary>
    public double Percent => _progress.Percent;

    /// <summary>
    /// Название задачи
    /// </summary>
    public string TaskName => _progress.TaskName;

    /// <summary>
    /// Количество активных параллельных запросов
    /// </summary>
    public int ActiveRequests => _activeRequests;

    /// <summary>
    /// Создаёт новый логгер прогресса
    /// </summary>
    /// <param name="total">Общее количество элементов для обработки</param>
    /// <param name="scraperName">Название скрапера</param>
    /// <param name="logger">Логгер для вывода (если null, используется Console.WriteLine)</param>
    /// <param name="taskName">Название задачи (опционально, по умолчанию = scraperName)</param>
    public ScraperProgressLogger(int total, string scraperName, ConsoleLogger? logger = null, string? taskName = null)
    {
        _progress = new ProgressTracker(total, taskName ?? scraperName);
        _scraperName = scraperName;
        _logger = logger;
        _activeRequests = 0;
    }

    /// <summary>
    /// Увеличивает счётчик обработанных элементов на 1
    /// </summary>
    /// <returns>Новое значение счётчика</returns>
    public int Increment()
    {
        return _progress.Increment();
    }

    /// <summary>
    /// Обновляет количество активных запросов
    /// </summary>
    public void UpdateActiveRequests(int count)
    {
        Interlocked.Exchange(ref _activeRequests, count);
    }

    /// <summary>
    /// Сбрасывает счётчик в 0
    /// </summary>
    public void Reset()
    {
        _progress.Reset();
    }

    /// <summary>
    /// Проверяет, завершена ли обработка всех элементов
    /// </summary>
    public bool IsComplete => _progress.IsComplete;

    /// <summary>
    /// Логирует прогресс HTTP запроса
    /// </summary>
    /// <param name="url">URL запроса</param>
    /// <param name="elapsedSeconds">Время выполнения запроса в секундах</param>
    /// <param name="statusCode">HTTP код ответа</param>
    public void LogHttpProgress(string url, double elapsedSeconds, int statusCode)
    {
        var message = $"[{_scraperName}] HTTP {url}: {elapsedSeconds:F3} сек. " +
                     $"Код: {statusCode}. " +
                     $"Прогресс: {_progress}. " +
                     $"Параллельных: {_activeRequests}.";

        WriteMessage(message);
    }

    /// <summary>
    /// Логирует прогресс HTTP запроса с использованием ScraperStatistics
    /// </summary>
    /// <param name="statistics">Статистика скрапера</param>
    /// <param name="url">URL запроса</param>
    /// <param name="elapsedSeconds">Время выполнения запроса в секундах</param>
    /// <param name="statusCode">HTTP код ответа</param>
    public void LogHttpProgress(ScraperStatistics statistics, string url, double elapsedSeconds, int statusCode)
    {
        var message = $"[{statistics.ScraperName}] HTTP {url}: {elapsedSeconds:F3} сек. " +
                     $"Код: {statusCode}. " +
                     $"Прогресс: {_progress}. " +
                     $"Параллельных: {statistics.ActiveRequests}.";

        WriteMessage(message);
    }

    /// <summary>
    /// Логирует прогресс обработки элемента (без HTTP запроса)
    /// </summary>
    /// <param name="itemDescription">Описание обрабатываемого элемента</param>
    /// <param name="itemsFound">Количество найденных элементов (опционально)</param>
    public void LogItemProgress(string itemDescription, int? itemsFound = null)
    {
        var foundPart = itemsFound.HasValue ? $" найдено {itemsFound.Value}." : "";
        var message = $"[{_scraperName}] {itemDescription}:{foundPart} Прогресс: {_progress}.";

        WriteMessage(message);
    }

    /// <summary>
    /// Логирует прогресс обработки страницы
    /// </summary>
    /// <param name="pageNumber">Номер страницы</param>
    /// <param name="itemsFound">Количество найденных элементов</param>
    public void LogPageProgress(int pageNumber, int itemsFound)
    {
        var message = $"[{_scraperName}] Страница {pageNumber}: найдено {itemsFound}. Прогресс: {_progress}.";

        WriteMessage(message);
    }

    /// <summary>
    /// Логирует прогресс обработки фильтра
    /// </summary>
    /// <param name="filterDescription">Описание фильтра</param>
    /// <param name="itemsFound">Количество найденных элементов (опционально)</param>
    public void LogFilterProgress(string filterDescription, int? itemsFound = null)
    {
        var foundPart = itemsFound.HasValue ? $" найдено {itemsFound.Value} профилей." : "";
        var message = $"[{_scraperName}] {filterDescription}:{foundPart} Прогресс: {_progress}.";

        WriteMessage(message);
    }

    /// <summary>
    /// Логирует завершение обработки
    /// </summary>
    /// <param name="totalItemsCollected">Общее количество собранных элементов</param>
    /// <param name="additionalInfo">Дополнительная информация (опционально)</param>
    public void LogCompletion(int totalItemsCollected, string? additionalInfo = null)
    {
        var additionalPart = string.IsNullOrWhiteSpace(additionalInfo) ? "" : $" {additionalInfo}";
        var message = $"[{_scraperName}] Обход завершён. Обработано: {_progress.Processed}, Собрано: {totalItemsCollected}.{additionalPart}";

        WriteMessage(message);
    }

    /// <summary>
    /// Логирует ошибку
    /// </summary>
    /// <param name="errorMessage">Сообщение об ошибке</param>
    public void LogError(string errorMessage)
    {
        var message = $"[{_scraperName}] Ошибка: {errorMessage}. Прогресс: {_progress}.";

        WriteMessage(message);
    }

    /// <summary>
    /// Логирует информационное сообщение с прогрессом
    /// </summary>
    /// <param name="infoMessage">Информационное сообщение</param>
    public void LogInfo(string infoMessage)
    {
        var message = $"[{_scraperName}] {infoMessage} Прогресс: {_progress}.";

        WriteMessage(message);
    }

    /// <summary>
    /// Возвращает строку прогресса
    /// </summary>
    public override string ToString() => _progress.ToString();

    private void WriteMessage(string message)
    {
        if (_logger != null)
        {
            _logger.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }
}
