using JobBoardScraper.WebScraper;

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
/// TODO при переборе прогресс выводить, там где известно заранее число элементов
/// TODO догружается динамически, надо смотреть через selenium https://career.habr.com/resumes?company_ids[]=1000044730
/// TODO через прокси или selenium https://career.habr.com/slo_omy, здесь ограничение на количество просматриваемых профилей в том числе и под инкогнито
/// TODO в таблицу habr_levels иногда косячно записываются названия
/// TODO info_tech криво парсится иногда
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

        // Создаем логгер для DatabaseClient
        var dbLogger = new Helper.ConsoleHelper.ConsoleLogger("DatabaseClient");
        
        var db = new DatabaseClient(AppConfig.ConnectionString, dbLogger);
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
        if (AppConfig.ResumeListEnabled)
        {
            Console.WriteLine("[Program] ResumeListPageScraper: ВКЛЮЧЕН");
            Console.WriteLine($"[Program] Режим вывода ResumeListPageScraper: {AppConfig.ResumeListOutputMode}");
            Console.WriteLine($"[Program] Перебор навыков: {(AppConfig.ResumeListSkillsEnumerationEnabled ? "ВКЛЮЧЕН" : "ОТКЛЮЧЕН")}");
            if (AppConfig.ResumeListSkillsEnumerationEnabled)
            {
                Console.WriteLine($"[Program] Диапазон навыков: {AppConfig.ResumeListSkillsStartId} - {AppConfig.ResumeListSkillsEndId}");
            }
            
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
                controller: controller,
                interval: TimeSpan.FromMinutes(10),
                outputMode: AppConfig.ResumeListOutputMode);

            _ = resumeListScraper.StartAsync(cts.Token);
        }
        else
        {
            Console.WriteLine("[Program] ResumeListPageScraper: ОТКЛЮЧЕН");
        }

        // Процесс 3: Периодический обход списка компаний
        if (AppConfig.CompaniesEnabled)
        {
            Console.WriteLine("[Program] CompanyListScraper: ВКЛЮЧЕН");
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
                enqueueCompany: (companyCode, companyUrl, companyId) =>
                {
                    db.EnqueueCompany(companyCode, companyUrl, companyId);
                },
                getCategoryIds: () => db.GetAllCategoryIds(conn),
                interval: TimeSpan.FromDays(7),
                outputMode: AppConfig.CompaniesOutputMode);

            _ = companyListScraper.StartAsync(cts.Token);
        }
        else
        {
            Console.WriteLine("[Program] CompanyListScraper: ОТКЛЮЧЕН");
        }

        // Процесс 4: Периодический сбор category_root_id
        if (AppConfig.CategoryEnabled)
        {
            Console.WriteLine("[Program] CategoryScraper: ВКЛЮЧЕН");
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
            Console.WriteLine("[Program] CategoryScraper: ОТКЛЮЧЕН");
        }

        // Процесс 5: Периодический обход подписчиков компаний
        if (AppConfig.CompanyFollowersEnabled)
        {
            Console.WriteLine("[Program] CompanyFollowersScraper: ВКЛЮЧЕН");
            Console.WriteLine($"[Program] Режим вывода CompanyFollowersScraper: {AppConfig.CompanyFollowersOutputMode}");
            Console.WriteLine($"[Program] Timeout CompanyFollowersScraper: {AppConfig.CompanyFollowersTimeout.TotalSeconds} секунд");
            
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
                getCompanyCodes: () => db.GetAllCompanyCodes(conn),
                controller: controller,
                interval: TimeSpan.FromDays(5),
                outputMode: AppConfig.CompanyFollowersOutputMode);

            _ = companyFollowersScraper.StartAsync(cts.Token);
        }
        else
        {
            Console.WriteLine("[Program] CompanyFollowersScraper: ОТКЛЮЧЕН");
        }

        // Процесс 6: Периодический обход экспертов
        if (AppConfig.ExpertsEnabled)
        {
            Console.WriteLine("[Program] ExpertsScraper: ВКЛЮЧЕН");
            Console.WriteLine($"[Program] Режим вывода ExpertsScraper: {AppConfig.ExpertsOutputMode}");
            Console.WriteLine($"[Program] Timeout ExpertsScraper: {AppConfig.ExpertsTimeout.TotalSeconds} секунд");
            
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
            Console.WriteLine("[Program] ExpertsScraper: ОТКЛЮЧЕН");
        }

        // Процесс 7: Периодический обход детальных страниц компаний
        if (AppConfig.CompanyDetailEnabled)
        {
            Console.WriteLine("[Program] CompanyDetailScraper: ВКЛЮЧЕН");
            Console.WriteLine($"[Program] Режим вывода CompanyDetailScraper: {AppConfig.CompanyDetailOutputMode}");
            Console.WriteLine($"[Program] Timeout CompanyDetailScraper: {AppConfig.CompanyDetailTimeout.TotalSeconds} секунд");
            
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
                getCompanies: () => db.GetAllCompaniesWithUrls(conn),
                controller: controller,
                interval: TimeSpan.FromDays(30),
                outputMode: AppConfig.CompanyDetailOutputMode);

            _ = companyDetailScraper.StartAsync(cts.Token);
        }
        else
        {
            Console.WriteLine("[Program] CompanyDetailScraper: ОТКЛЮЧЕН");
        }

        // Процесс 8: Периодический обход профилей пользователей
        if (AppConfig.UserProfileEnabled)
        {
            Console.WriteLine("[Program] UserProfileScraper: ВКЛЮЧЕН");
            Console.WriteLine($"[Program] Режим вывода UserProfileScraper: {AppConfig.UserProfileOutputMode}");
            Console.WriteLine($"[Program] Timeout UserProfileScraper: {AppConfig.UserProfileTimeout.TotalSeconds} секунд");
            
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
                getUserCodes: () => db.GetAllUserLinks(conn),
                controller: controller,
                interval: TimeSpan.FromDays(30),
                outputMode: AppConfig.UserProfileOutputMode);

            _ = userProfileScraper.StartAsync(cts.Token);
        }
        else
        {
            Console.WriteLine("[Program] UserProfileScraper: ОТКЛЮЧЕН");
        }

        // Процесс 9: Периодический обход списков друзей пользователей
        if (AppConfig.UserFriendsEnabled)
        {
            Console.WriteLine("[Program] UserFriendsScraper: ВКЛЮЧЕН");
            Console.WriteLine($"[Program] Режим вывода UserFriendsScraper: {AppConfig.UserFriendsOutputMode}");
            Console.WriteLine($"[Program] Timeout UserFriendsScraper: {AppConfig.UserFriendsTimeout.TotalSeconds} секунд");
            
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
                getUserCodes: () => db.GetAllUserLinks(conn, onlyPublic: AppConfig.UserFriendsOnlyPublic),
                controller: controller,
                interval: TimeSpan.FromDays(30),
                outputMode: AppConfig.UserFriendsOutputMode);

            _ = userFriendsScraper.StartAsync(cts.Token);
        }
        else
        {
            Console.WriteLine("[Program] UserFriendsScraper: ОТКЛЮЧЕН");
        }

        // Процесс 10: Периодический обход резюме пользователей для извлечения "О себе" и навыков
        if (AppConfig.UserResumeDetailEnabled)
        {
            Console.WriteLine("[Program] UserResumeDetailScraper: ВКЛЮЧЕН");
            Console.WriteLine($"[Program] Режим вывода UserResumeDetailScraper: {AppConfig.UserResumeDetailOutputMode}");
            Console.WriteLine($"[Program] Timeout UserResumeDetailScraper: {AppConfig.UserResumeDetailTimeout.TotalSeconds} секунд");
            
            // Создаём отдельный HttpClient с нужным timeout
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
                getUserCodes: () => db.GetAllUserLinks(conn),
                controller: controller,
                interval: TimeSpan.FromDays(30),
                outputMode: AppConfig.UserResumeDetailOutputMode);

            _ = userResumeDetailScraper.StartAsync(cts.Token);
        }
        else
        {
            Console.WriteLine("[Program] UserResumeDetailScraper: ОТКЛЮЧЕН");
        }

        // Процесс 1: Перебор всех возможных имен пользователей
        Task? bruteForceScraperTask = null;
        if (AppConfig.BruteForceEnabled)
        {
            Console.WriteLine("[Program] BruteForceUsernameScraper: ВКЛЮЧЕН");
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
                var bruteForceLogger = new Helper.ConsoleHelper.ConsoleLogger("BruteForceScraper");
                var bruteForceScraper = new BruteForceUsernameScraper(bruteForceHttpClient, db, controller, bruteForceLogger);
                await bruteForceScraper.RunAsync(cts.Token);
            }, cts.Token);
        }
        else
        {
            Console.WriteLine("[Program] BruteForceUsernameScraper: ОТКЛЮЧЕН");
        }

        if (bruteForceScraperTask != null)
        {
            try
            {
                await bruteForceScraperTask;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Процесс перебора остановлен пользователем.");
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
                Console.WriteLine("Приложение остановлено пользователем.");
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

        await db.StopWriterTask();
        db.DatabaseConnectionClose(conn);

        Console.WriteLine("Приложение завершено.");
    }
}
