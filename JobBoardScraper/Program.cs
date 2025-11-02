using JobBoardScraper.WebScraper;

namespace JobBoardScraper;

/// <summary>
/// Точка входа приложения.
/// Запускает пять параллельных процессов:
/// 1. BruteForceUsernameScraper - перебор всех возможных имен пользователей
/// 2. ResumeListPageScraper - периодический обход страницы со списком резюме (каждые 10 минут)
/// 3. CompanyListScraper - периодический обход списка компаний (раз в неделю)
/// 4. CategoryScraper - периодический сбор category_root_id (раз в неделю)
/// 5. CompanyFollowersScraper - периодический обход подписчиков компаний (каждые 5 дней)
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        using var httpClient = HttpClientFactory.CreateDefaultClient(timeoutSeconds: 10);
        
        // Инициализация статистики трафика
        using var trafficStats = new TrafficStatistics(
            AppConfig.TrafficStatsOutputFile,
            AppConfig.TrafficStatsSaveInterval);
        
        Console.WriteLine($"[Program] Статистика трафика будет сохраняться в: {AppConfig.TrafficStatsOutputFile}");
        Console.WriteLine($"[Program] Интервал сохранения статистики: {AppConfig.TrafficStatsSaveInterval.TotalMinutes} минут");

        var db = new DatabaseClient(AppConfig.ConnectionString);
        using var conn = db.DatabaseConnectionInit();
        db.DatabaseEnsureConnectionOpen(conn);

        var controller = new AdaptiveConcurrencyController(
            defaultConcurrency: AppConfig.MaxConcurrentRequests,
            minConcurrency: 1,
            maxConcurrency: 128,
            fastThreshold: TimeSpan.FromMilliseconds(250),
            slowThreshold: TimeSpan.FromSeconds(1),
            evaluationPeriod: TimeSpan.FromSeconds(2),
            emaAlpha: 0.2,
            increaseStep: 1,
            decreaseFactor: 0.75
        );

        var controllerLoop = controller.RunAsync(cts.Token);

        db.StartWriterTask(conn, cts.Token, delayMs: 500);

        // Процесс 2: Периодический обход страницы со списком резюме
        var resumeListHttpClient = new SmartHttpClient(
            httpClient, 
            "ResumeListPageScraper", 
            trafficStats,
            enableRetry: false,
            enableTrafficMeasuring: AppConfig.ResumeListEnableTrafficMeasuring);
        var resumeListScraper = new ResumeListPageScraper(
            resumeListHttpClient,
            enqueueToSaveQueue: item =>
            {
                db.EnqueueResume(item.link, item.title);
            },
            interval: TimeSpan.FromMinutes(10));

        _ = resumeListScraper.StartAsync(cts.Token);

        // Процесс 3: Периодический обход списка компаний
        Console.WriteLine($"[Program] Режим вывода CompanyListScraper: {AppConfig.CompaniesOutputMode}");
        Console.WriteLine($"[Program] Директория логов: {AppConfig.LoggingOutputDirectory}");
        
        var companyListHttpClient = new SmartHttpClient(
            httpClient, 
            "CompanyListScraper", 
            trafficStats,
            enableRetry: false,
            enableTrafficMeasuring: AppConfig.CompaniesEnableTrafficMeasuring);
        var companyListScraper = new CompanyListScraper(
            companyListHttpClient,
            enqueueCompany: (companyCode, companyUrl) =>
            {
                db.EnqueueCompany(companyCode, companyUrl);
            },
            getCategoryIds: () => db.GetAllCategoryIds(conn),
            interval: TimeSpan.FromDays(7),
            outputMode: AppConfig.CompaniesOutputMode);

        _ = companyListScraper.StartAsync(cts.Token);

        // Процесс 4: Периодический сбор category_root_id
        var categoryHttpClient = new SmartHttpClient(
            httpClient, 
            "CategoryScraper", 
            trafficStats,
            enableRetry: false,
            enableTrafficMeasuring: AppConfig.CategoryEnableTrafficMeasuring);
        var categoryScraper = new CategoryScraper(
            categoryHttpClient,
            enqueueCategory: (categoryId, categoryName) =>
            {
                db.EnqueueCategoryRootId(categoryId, categoryName);
            },
            interval: TimeSpan.FromDays(7),
            outputMode: AppConfig.CompaniesOutputMode);

        _ = categoryScraper.StartAsync(cts.Token);

        // Процесс 5: Периодический обход подписчиков компаний
        Console.WriteLine($"[Program] Режим вывода CompanyFollowersScraper: {AppConfig.CompanyFollowersOutputMode}");
        
        var companyFollowersHttpClient = new SmartHttpClient(
            httpClient, 
            "CompanyFollowersScraper", 
            trafficStats,
            enableRetry: false,
            enableTrafficMeasuring: AppConfig.CompanyFollowersEnableTrafficMeasuring);
        var companyFollowersScraper = new CompanyFollowersScraper(
            companyFollowersHttpClient,
            enqueueUser: (link, username, slogan, mode) =>
            {
                db.EnqueueResume(link, username, slogan, mode);
            },
            getCompanyCodes: () => db.GetAllCompanyCodes(conn),
            interval: TimeSpan.FromDays(5),
            outputMode: AppConfig.CompanyFollowersOutputMode);

        _ = companyFollowersScraper.StartAsync(cts.Token);

        // Процесс 1: Перебор всех возможных имен пользователей
        var bruteForceHttpClient = new SmartHttpClient(
            httpClient, 
            "BruteForceUsernameScraper", 
            trafficStats,
            enableRetry: AppConfig.BruteForceEnableRetry,
            enableTrafficMeasuring: AppConfig.BruteForceEnableTrafficMeasuring,
            maxRetries: AppConfig.MaxRetries,
            baseDelay: TimeSpan.FromMilliseconds(400),
            maxDelay: TimeSpan.FromSeconds(30));
        var bruteForceScraperTask = Task.Run(async () =>
        {
            var bruteForceScraper = new BruteForceUsernameScraper(bruteForceHttpClient, db, controller);
            await bruteForceScraper.RunAsync(cts.Token);
        }, cts.Token);

        try
        {
            await bruteForceScraperTask;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Процесс перебора остановлен пользователем.");
        }

        try
        {
            await controllerLoop;
        }
        catch (OperationCanceledException)
        {
            // игнорируем отмену при выходе
        }

        cts.Cancel();

        await db.StopWriterTask();
        db.DatabaseConnectionClose(conn);

        Console.WriteLine("Приложение завершено.");
    }
}
