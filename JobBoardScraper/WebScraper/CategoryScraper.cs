using JobBoardScraper.Helper.ConsoleHelper;

namespace JobBoardScraper.WebScraper;

/// <summary>
/// Периодически (раз в неделю) обходит страницу career.habr.com/companies
/// и извлекает все значения category_root_id из select элемента для сохранения в базу данных.
/// </summary>
public sealed class CategoryScraper : IDisposable
{
    private readonly SmartHttpClient _httpClient;
    private readonly Action<string, string> _enqueueCategory;
    private readonly TimeSpan _interval;
    private readonly ConsoleLogger _logger;

    public CategoryScraper(
        SmartHttpClient httpClient,
        Action<string, string> enqueueCategory,
        TimeSpan? interval = null,
        OutputMode outputMode = OutputMode.ConsoleOnly)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _enqueueCategory = enqueueCategory ?? throw new ArgumentNullException(nameof(enqueueCategory));
        _interval = interval ?? TimeSpan.FromDays(7);
        
        _logger = new ConsoleLogger("CategoryScraper");
        _logger.SetOutputMode(outputMode);
        _logger.WriteLine($"Инициализация CategoryScraper с режимом вывода: {outputMode}");
    }

    public void Dispose()
    {
        _logger?.Dispose();
    }

    public Task StartAsync(CancellationToken ct)
    {
        return Task.Run(() => LoopAsync(ct), ct);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        await RunOnceSafe(ct);

        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await RunOnceSafe(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Остановка — ок
        }
    }

    private async Task RunOnceSafe(CancellationToken ct)
    {
        try
        {
            await ScrapeCategoryRootIdsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Остановка — ок
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    private async Task ScrapeCategoryRootIdsAsync(CancellationToken ct)
    {
        _logger.WriteLine("Начало сбора category_root_id...");
        
        try
        {
            var url = AppConfig.CompaniesListUrl;
            _logger.WriteLine($"Загрузка страницы: {url}");

            var response = await _httpClient.GetAsync(url, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.WriteLine($"Страница вернула код {response.StatusCode}. Завершение.");
                return;
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            var doc = await HtmlParser.ParseDocumentAsync(html, ct);

            // Ищем select с id="category_root_id"
            var selectElement = doc.QuerySelector("select#category_root_id");
            
            if (selectElement == null)
            {
                _logger.WriteLine("Не найден элемент select#category_root_id");
                return;
            }

            // Собираем все option с value
            var options = selectElement.QuerySelectorAll("option[value]");
            var categoriesFound = 0;

            foreach (var option in options)
            {
                var value = option.GetAttribute("value");
                var text = option.TextContent?.Trim() ?? "";
                
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                _enqueueCategory(value, text);
                _logger.WriteLine($"В очередь: category_root_id={value} ({text})");
                categoriesFound++;
            }

            _logger.WriteLine($"Сбор завершён. Найдено категорий: {categoriesFound}");
        }
        catch (Exception ex)
        {
            _logger.WriteLine($"Ошибка при сборе category_root_id: {ex.Message}");
        }
    }
}
