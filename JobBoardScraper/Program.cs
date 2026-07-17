using JobBoardScraper.Infrastructure.Http;
using JobBoardScraper.Infrastructure.Proxy;
using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Statistics;
using JobBoardScraper.Scrapers;
using JobBoardScraper.Core;
using JobBoardScraper.Data;

namespace JobBoardScraper;

/// <summary>
/// Точка входа приложения.
/// Запускает десять параллельных процессов:
/// 1. BruteForceUsernameScraper - перебор всех возможных имен пользователей
/// 2. ResumeListPageScraper - периодический обход страницы со списком резюме (каждые 10 минут)
/// 3. CompanyListScraper - периодический обход списка компаний (раз в неделю)
/// 4. CategoryScraper - периодический сбор category_root_id (раз в неделю)
/// 5. CompanyFollowersScraper - периодический обход подписчиков компаний (каждые 5 дней)
/// 6. ExpertsScraper - периодический обход экспертов (каждые 4 дня)
/// 7. CompanyDetailScraper - периодический обход детальных страниц компаний для извлечения company_id (раз в месяц)
/// 8. UserProfileScraper - периодический обход профилей пользователей для извлечения детальной информации (раз в месяц)
/// 9. UserFriendsScraper - периодический обход списков друзей пользователей для сбора ссылок (раз в месяц)
/// 10. UserResumeDetailScraper - периодический обход резюме пользователей для извлечения "О себе" и навыков (раз в месяц)
/// TODO поработать через api https://career.habr.com/info/api#q1.7
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
        var programLogger = new ConsoleLogger("Program");
        programLogger.SetOutputMode(OutputMode.ConsoleOnly);

        // Инициализация статистики трафика
        using var trafficStats = new TrafficStatistics(
            AppConfig.TrafficStatsOutputFile,
            AppConfig.TrafficStatsSaveInterval);

        // Создаем логгер для DatabaseClient
        var dbLogger = new ConsoleLogger("DatabaseClient");
        dbLogger.SetOutputMode(AppConfig.DatabaseClientOutputMode);

        var db = new DatabaseClient(AppConfig.ConnectionString, dbLogger);
        using var conn = db.ConnectionInit();
        db.EnsureConnectionOpen(conn);

        using var controller = new AdaptiveConcurrencyController(
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
        if (AppConfig.ResumeListEnabled)
        {
            programLogger.WriteLine("ResumeListPageScraper: ВКЛЮЧЕН");

            var resumeListHttpClient = new SmartHttpClient(
                httpClient,
                "ResumeListPageScraper",
                trafficStats,
                enableRetry: false,
                enableTrafficMeasuring: AppConfig.ResumeListEnableTrafficMeasuring);
            var resumeListScraper = new ResumeListPageScraper(
                resumeListHttpClient,
                db,
                enqueueToSaveQueue: item =>
                {
                    db.EnqueueResume(item.link, item.title);
                },
                getCompanyIds: () => db.CompaniesGetAllIds(conn),
                getUniversityIds: () => db.UniversitiesGetAllIds(conn),
                controller: controller,
                interval: TimeSpan.FromMinutes(10),
                outputMode: AppConfig.ResumeListOutputMode);

            _ = resumeListScraper.StartAsync(cts.Token);
        }
        else
        {
            programLogger.WriteLine("ResumeListPageScraper: ОТКЛЮЧЕН");
        }

        // Процесс 3: Периодический обход списка компаний
        if (AppConfig.CompaniesEnabled)
        {
            programLogger.WriteLine("CompanyListScraper: ВКЛЮЧЕН");

            var companyListHttpClient = new SmartHttpClient(
                httpClient,
                "CompanyListScraper",
                trafficStats,
                enableRetry: false,
                enableTrafficMeasuring: AppConfig.CompaniesEnableTrafficMeasuring);
            var companyListScraper = new CompanyListScraper(
                companyListHttpClient,
                enqueueCompany: (companyCode, companyUrl, companyId) =>
                {
                    db.EnqueueCompany(companyCode, companyUrl, companyId);
                },
                getCategoryIds: () => db.CategoryGetAllIds(conn),
                interval: TimeSpan.FromDays(7),
                outputMode: AppConfig.CompaniesOutputMode);

            _ = companyListScraper.StartAsync(cts.Token);
        }
        else
        {
            programLogger.WriteLine("CompanyListScraper: ОТКЛЮЧЕН");
        }

        // Процесс 4: Периодический сбор category_root_id
        if (AppConfig.CategoryEnabled)
        {
            programLogger.WriteLine("CategoryScraper: ВКЛЮЧЕН");
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
        }
        else
        {
            programLogger.WriteLine("CategoryScraper: ОТКЛЮЧЕН");
        }

        // Процесс 5: Периодический обход подписчиков компаний
        if (AppConfig.CompanyFollowersEnabled)
        {
            programLogger.WriteLine("CompanyFollowersScraper: ВКЛЮЧЕН");

            // Создаём отдельный HttpClient с нужным timeout
            var companyFollowersBaseHttpClient = HttpClientFactory.CreateDefaultClient(
                timeoutSeconds: (int)AppConfig.CompanyFollowersTimeout.TotalSeconds);

            var companyFollowersHttpClient = new SmartHttpClient(
                companyFollowersBaseHttpClient,
                "CompanyFollowersScraper",
                trafficStats,
                enableRetry: false,
                enableTrafficMeasuring: AppConfig.CompanyFollowersEnableTrafficMeasuring,
                timeout: AppConfig.CompanyFollowersTimeout);
            var companyFollowersScraper = new CompanyFollowersScraper(
                companyFollowersHttpClient,
                enqueueUser: (link, username, slogan, mode) =>
                {
                    db.EnqueueResume(link, username, slogan, mode);
                },
                getCompanyCodes: () => db.CompaniesGetAllCodes(conn),
                controller: controller,
                interval: TimeSpan.FromDays(5),
                outputMode: AppConfig.CompanyFollowersOutputMode);

            _ = companyFollowersScraper.StartAsync(cts.Token);
        }
        else
        {
            programLogger.WriteLine("CompanyFollowersScraper: ОТКЛЮЧЕН");
        }

        // Процесс 6: Периодический обход экспертов
        if (AppConfig.ExpertsEnabled)
        {
            programLogger.WriteLine("ExpertsScraper: ВКЛЮЧЕН");

            // Создаём отдельный HttpClient с нужным timeout
            var expertsBaseHttpClient = HttpClientFactory.CreateDefaultClient(
                timeoutSeconds: (int)AppConfig.ExpertsTimeout.TotalSeconds);

            var expertsHttpClient = new SmartHttpClient(
                expertsBaseHttpClient,
                "ExpertsScraper",
                trafficStats,
                enableRetry: AppConfig.ExpertsEnableRetry,
                enableTrafficMeasuring: AppConfig.ExpertsEnableTrafficMeasuring,
                timeout: AppConfig.ExpertsTimeout);
            var expertsScraper = new ExpertsScraper(
                expertsHttpClient,
                db,
                interval: TimeSpan.FromDays(4),
                outputMode: AppConfig.ExpertsOutputMode);

            _ = expertsScraper.StartAsync(cts.Token);
        }
        else
        {
            programLogger.WriteLine("ExpertsScraper: ОТКЛЮЧЕН");
        }

        // Процесс 7: Периодический обход детальных страниц компаний
        if (AppConfig.CompanyDetailEnabled)
        {
            programLogger.WriteLine("CompanyDetailScraper: ВКЛЮЧЕН");

            // Создаём отдельный HttpClient с нужным timeout
            var companyDetailBaseHttpClient = HttpClientFactory.CreateDefaultClient(
                timeoutSeconds: (int)AppConfig.CompanyDetailTimeout.TotalSeconds);

            var companyDetailHttpClient = new SmartHttpClient(
                companyDetailBaseHttpClient,
                "CompanyDetailScraper",
                trafficStats,
                enableRetry: AppConfig.CompanyDetailEnableRetry,
                enableTrafficMeasuring: AppConfig.CompanyDetailEnableTrafficMeasuring,
                timeout: AppConfig.CompanyDetailTimeout);
            var companyDetailScraper = new CompanyDetailScraper(
                companyDetailHttpClient,
                db,
                getCompanies: () => db.CompaniesGetAll(conn),
                controller: controller,
                interval: TimeSpan.FromDays(30),
                outputMode: AppConfig.CompanyDetailOutputMode);

            _ = companyDetailScraper.StartAsync(cts.Token);
        }
        else
        {
            programLogger.WriteLine("CompanyDetailScraper: ОТКЛЮЧЕН");
        }

        // Процесс 8: Периодический обход профилей пользователей
        if (AppConfig.UserProfileEnabled)
        {
            programLogger.WriteLine("UserProfileScraper: ВКЛЮЧЕН");

            // Создаём отдельный HttpClient с нужным timeout
            var userProfileBaseHttpClient = HttpClientFactory.CreateDefaultClient(
                timeoutSeconds: (int)AppConfig.UserProfileTimeout.TotalSeconds);

            var userProfileHttpClient = new SmartHttpClient(
                userProfileBaseHttpClient,
                "UserProfileScraper",
                trafficStats,
                enableRetry: AppConfig.UserProfileEnableRetry,
                enableTrafficMeasuring: AppConfig.UserProfileEnableTrafficMeasuring,
                timeout: AppConfig.UserProfileTimeout);
            var userProfileScraper = new UserProfileScraper(
                userProfileHttpClient,
                db,
                getUserCodes: () => db.ResumesGetAllUserLinks(conn),
                controller: controller,
                interval: TimeSpan.FromDays(30),
                outputMode: AppConfig.UserProfileOutputMode);

            _ = userProfileScraper.StartAsync(cts.Token);
        }
        else
        {
            programLogger.WriteLine("UserProfileScraper: ОТКЛЮЧЕН");
        }

        // Процесс 9: Периодический обход списков друзей пользователей
        if (AppConfig.UserFriendsEnabled)
        {
            programLogger.WriteLine("UserFriendsScraper: ВКЛЮЧЕН");

            // Создаём отдельный HttpClient с нужным timeout
            var userFriendsBaseHttpClient = HttpClientFactory.CreateDefaultClient(
                timeoutSeconds: (int)AppConfig.UserFriendsTimeout.TotalSeconds);

            var userFriendsHttpClient = new SmartHttpClient(
                userFriendsBaseHttpClient,
                "UserFriendsScraper",
                trafficStats,
                enableRetry: AppConfig.UserFriendsEnableRetry,
                enableTrafficMeasuring: AppConfig.UserFriendsEnableTrafficMeasuring,
                timeout: AppConfig.UserFriendsTimeout);
            var userFriendsScraper = new UserFriendsScraper(
                userFriendsHttpClient,
                db,
                getUserCodes: () => db.ResumesGetAllUserLinks(conn, onlyPublic: AppConfig.UserFriendsOnlyPublic),
                controller: controller,
                interval: TimeSpan.FromDays(30),
                outputMode: AppConfig.UserFriendsOutputMode);

            _ = userFriendsScraper.StartAsync(cts.Token);
        }
        else
        {
            programLogger.WriteLine("UserFriendsScraper: ОТКЛЮЧЕН");
        }

        // Инициализация прокси-скрейперов
        ProxyScraperLauncher? proxyScraperLauncher = null;

        if (AppConfig.UserResumeDetailEnabled && AppConfig.EnableFreeProxyRotation)
        {
            programLogger.WriteLine("Прокси-скрейперы: ВКЛЮЧЕНЫ");

            proxyScraperLauncher = ProxyScraperLauncher.LaunchAll(
                poolMaxSize: AppConfig.ProxyPoolMaxSize,
                refreshIntervalMinutes: AppConfig.ProxyRefreshIntervalMinutes,
                adaptiveTriggerThreshold: 200,
                freeProxyListUrl: AppConfig.FreeProxyListUrl,
                proxyScrapeApiUrl: AppConfig.ProxyScrapeApiUrl,
                geoNodeApiUrl: AppConfig.GeoNodeApiUrl,
                freeProxyListEnabled: AppConfig.FreeProxyListEnabled,
                proxyScrapeEnabled: AppConfig.ProxyScrapeEnabled,
                geoNodeEnabled: AppConfig.GeoNodeEnabled,
                outputMode: AppConfig.UserResumeDetailOutputMode);
        }
        else if (AppConfig.UserResumeDetailEnabled)
        {
            programLogger.WriteLine("Прокси-скрейперы: ОТКЛЮЧЕНЫ (работа без прокси)");
        }

        // Процесс 10: Периодический обход резюме пользователей для извлечения "О себе" и навыков
        if (AppConfig.UserResumeDetailEnabled)
        {
            programLogger.WriteLine("UserResumeDetailScraper: ВКЛЮЧЕН");

            // Инициализация ProxyCoordinator (координатор whitelist + general pool)
            ProxyCoordinator? proxyCoordinator = null;
            if (proxyScraperLauncher != null)
            {
                var whitelistStorage = new JsonWhitelistStorage(AppConfig.ProxyWhitelistFilePath);
                var whitelistManager = new ProxyWhitelistManager(whitelistStorage);
                await whitelistManager.LoadStateAsync();

                var generalPoolManager = new GeneralPoolManager(proxyScraperLauncher.Pool);
                proxyCoordinator = new ProxyCoordinator(whitelistManager, generalPoolManager);

                // Register scraper statistics with coordinator
                proxyScraperLauncher.RegisterStatistics(proxyCoordinator);

                // Start periodic statistics reporting (5 minutes interval)
                proxyCoordinator.StartPeriodicStatsReporting(cts.Token, TimeSpan.FromMinutes(5));
            }
            else
            {
                programLogger.WriteLine("UserResumeDetailScraper: Прокси ОТКЛЮЧЕНЫ (работа без прокси)");
            }

            // Создаём отдельный HttpClient БЕЗ прокси (прокси будут применяться динамически)
            var userResumeDetailBaseHttpClient = HttpClientFactory.CreateDefaultClient(
                timeoutSeconds: (int)AppConfig.UserResumeDetailTimeout.TotalSeconds);

            var userResumeDetailHttpClient = new SmartHttpClient(
                userResumeDetailBaseHttpClient,
                "UserResumeDetailScraper",
                trafficStats,
                enableRetry: AppConfig.UserResumeDetailEnableRetry,
                enableTrafficMeasuring: AppConfig.UserResumeDetailEnableTrafficMeasuring,
                timeout: AppConfig.UserResumeDetailTimeout);
            var userResumeDetailScraper = new UserResumeDetailScraper(
                userResumeDetailHttpClient,
                db,
                getUserCodes: () => db.ResumesGetUserLinksWithoutData(conn),
                controller: controller,
                proxyCoordinator: proxyCoordinator,
                interval: TimeSpan.FromMinutes(20),
                outputMode: AppConfig.UserResumeDetailOutputMode);

            _ = userResumeDetailScraper.StartAsync(cts.Token);
        }
        else
        {
            programLogger.WriteLine("UserResumeDetailScraper: ОТКЛЮЧЕН");
        }

        // Процесс 11: Периодический обход страниц рейтингов компаний
        if (AppConfig.CompanyRatingEnabled)
        {
            programLogger.WriteLine("CompanyRatingScraper: ВКЛЮЧЕН");

            // Создаём отдельный HttpClient с нужным timeout
            var companyRatingBaseHttpClient = HttpClientFactory.CreateDefaultClient(
                timeoutSeconds: (int)AppConfig.CompanyRatingTimeout.TotalSeconds);

            var companyRatingHttpClient = new SmartHttpClient(
                companyRatingBaseHttpClient,
                "CompanyRatingScraper",
                trafficStats,
                enableRetry: AppConfig.CompanyRatingEnableRetry,
                enableTrafficMeasuring: AppConfig.CompanyRatingEnableTrafficMeasuring,
                timeout: AppConfig.CompanyRatingTimeout);
            var companyRatingScraper = new CompanyRatingScraper(
                companyRatingHttpClient,
                db,
                controller: controller,
                interval: TimeSpan.FromDays(30),
                outputMode: AppConfig.CompanyRatingOutputMode);

            _ = companyRatingScraper.StartAsync(cts.Token);
        }
        else
        {
            programLogger.WriteLine("CompanyRatingScraper: ОТКЛЮЧЕН");
        }

        // Процесс 1: Перебор всех возможных имен пользователей
        Task? bruteForceScraperTask = null;
        if (AppConfig.BruteForceEnabled)
        {
            programLogger.WriteLine("BruteForceUsernameScraper: ВКЛЮЧЕН");
            var bruteForceHttpClient = new SmartHttpClient(
                httpClient,
                "BruteForceUsernameScraper",
                trafficStats,
                enableRetry: AppConfig.BruteForceEnableRetry,
                enableTrafficMeasuring: AppConfig.BruteForceEnableTrafficMeasuring,
                maxRetries: AppConfig.MaxRetries,
                baseDelay: TimeSpan.FromMilliseconds(400),
                maxDelay: TimeSpan.FromSeconds(30));
            bruteForceScraperTask = Task.Run(async () =>
            {
                var bruteForceScraper = new BruteForceUsernameScraper(
                    bruteForceHttpClient,
                    db,
                    getLastResumeLink: linkLength => db.ResumesGetLastLink(conn, linkLength),
                    controller: controller);
                await bruteForceScraper.RunAsync(cts.Token);
            }, cts.Token);
        }
        else
        {
            programLogger.WriteLine("BruteForceUsernameScraper: ОТКЛЮЧЕН");
        }

        if (bruteForceScraperTask != null)
        {
            try
            {
                await bruteForceScraperTask;
            }
            catch (OperationCanceledException)
            {
                programLogger.WriteLine("Процесс перебора остановлен пользователем.");
            }
        }
        else
        {
            // Если BruteForce отключен, ждём отмены
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                programLogger.WriteLine("Приложение остановлено пользователем.");
            }
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

        // Остановка ProxyScraperLauncher
        proxyScraperLauncher?.Dispose();

        await db.StopWriterTask();
        db.ConnectionClose(conn);

        programLogger.WriteLine("Приложение завершено.");
    }
}
