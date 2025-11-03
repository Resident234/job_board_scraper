using System.Configuration;
using JobBoardScraper.Helper.ConsoleHelper;

namespace JobBoardScraper;

/// <summary>
/// Конфигурация приложения, читается из App.config
/// </summary>
public static class AppConfig
{
    // Настройки для BruteForceUsernameScraper
    public static bool BruteForceEnabled => bool.TryParse(ConfigurationManager.AppSettings["BruteForce:Enabled"], out var value) && value;
    
    public static char[] Chars => (ConfigurationManager.AppSettings["BruteForce:Chars"] ?? "abcdefghijklmnopqrstuvwxyz0123456789-_").ToCharArray();
    
    public static string BaseUrl => ConfigurationManager.AppSettings["BruteForce:BaseUrl"] ?? "http://career.habr.com/";
    
    public static int MinLength => int.TryParse(ConfigurationManager.AppSettings["BruteForce:MinLength"], out var value) ? value : 5;
    
    public static int MaxLength => int.TryParse(ConfigurationManager.AppSettings["BruteForce:MaxLength"], out var value) ? value : 5;
    
    public static int MaxConcurrentRequests => int.TryParse(ConfigurationManager.AppSettings["BruteForce:MaxConcurrentRequests"], out var value) ? value : 5;
    
    public static int MaxRetries => int.TryParse(ConfigurationManager.AppSettings["BruteForce:MaxRetries"], out var value) ? value : 200;
    
    public static bool BruteForceEnableRetry => bool.TryParse(ConfigurationManager.AppSettings["BruteForce:EnableRetry"], out var value) && value;
    
    public static bool BruteForceEnableTrafficMeasuring => bool.TryParse(ConfigurationManager.AppSettings["BruteForce:EnableTrafficMeasuring"], out var value) ? value : true;
    
    // Настройки для CompanyListScraper
    public static bool CompaniesEnabled => bool.TryParse(ConfigurationManager.AppSettings["Companies:Enabled"], out var value) && value;
    
    public static string CompaniesListUrl => ConfigurationManager.AppSettings["Companies:ListUrl"] ?? "https://career.habr.com/companies";
    
    public static string CompaniesBaseUrl => ConfigurationManager.AppSettings["Companies:BaseUrl"] ?? "https://career.habr.com/companies/";
    
    public static string CompaniesLinkSelector => ConfigurationManager.AppSettings["Companies:LinkSelector"] ?? "a[href^='/companies/']";
    
    public static string CompaniesHrefRegex => ConfigurationManager.AppSettings["Companies:HrefRegex"] ?? "/companies/([a-zA-Z0-9_-]+)";
    
    public static string CompaniesNextPageSelector => ConfigurationManager.AppSettings["Companies:NextPageSelector"] ?? "a.page[href*='page={0}']";
    
    public static OutputMode CompaniesOutputMode
    {
        get
        {
            var value = ConfigurationManager.AppSettings["Companies:OutputMode"];
            return Enum.TryParse<OutputMode>(value, out var mode) ? mode : OutputMode.ConsoleOnly;
        }
    }
    
    public static bool CompaniesEnableTrafficMeasuring => bool.TryParse(ConfigurationManager.AppSettings["Companies:EnableTrafficMeasuring"], out var value) ? value : true;
    
    // Настройки для CompanyFollowersScraper
    public static bool CompanyFollowersEnabled => bool.TryParse(ConfigurationManager.AppSettings["CompanyFollowers:Enabled"], out var value) ? value : true;
    
    public static TimeSpan CompanyFollowersTimeout
    {
        get
        {
            var seconds = int.TryParse(ConfigurationManager.AppSettings["CompanyFollowers:TimeoutSeconds"], out var value) ? value : 300;
            return TimeSpan.FromSeconds(seconds);
        }
    }
    
    public static string CompanyFollowersUrlTemplate => ConfigurationManager.AppSettings["CompanyFollowers:UrlTemplate"] ?? "https://career.habr.com/companies/{0}/followers";
    
    public static string CompanyFollowersUserItemSelector => ConfigurationManager.AppSettings["CompanyFollowers:UserItemSelector"] ?? ".user_friends_item";
    
    public static string CompanyFollowersUsernameSelector => ConfigurationManager.AppSettings["CompanyFollowers:UsernameSelector"] ?? ".username";
    
    public static string CompanyFollowersSloganSelector => ConfigurationManager.AppSettings["CompanyFollowers:SloganSelector"] ?? ".specialization";
    
    public static string CompanyFollowersNextPageSelector => ConfigurationManager.AppSettings["CompanyFollowers:NextPageSelector"] ?? "a.page[href*='page={0}']";
    
    public static OutputMode CompanyFollowersOutputMode
    {
        get
        {
            var value = ConfigurationManager.AppSettings["CompanyFollowers:OutputMode"];
            return Enum.TryParse<OutputMode>(value, out var mode) ? mode : OutputMode.ConsoleOnly;
        }
    }
    
    public static bool CompanyFollowersEnableTrafficMeasuring => bool.TryParse(ConfigurationManager.AppSettings["CompanyFollowers:EnableTrafficMeasuring"], out var value) ? value : true;
    
    // Общие настройки для скраперов
    public static bool ResumeListEnabled => bool.TryParse(ConfigurationManager.AppSettings["ResumeList:Enabled"], out var value) && value;
    
    public static bool ResumeListEnableTrafficMeasuring => bool.TryParse(ConfigurationManager.AppSettings["ResumeList:EnableTrafficMeasuring"], out var value) ? value : true;
    
    public static bool CategoryEnabled => bool.TryParse(ConfigurationManager.AppSettings["Category:Enabled"], out var value) && value;
    
    public static bool CategoryEnableTrafficMeasuring => bool.TryParse(ConfigurationManager.AppSettings["Category:EnableTrafficMeasuring"], out var value) ? value : true;
    
    // Настройки логирования
    public static string LoggingOutputDirectory => ConfigurationManager.AppSettings["Logging:OutputDirectory"] ?? "./logs";
    
    // Настройки статистики трафика
    public static string TrafficStatsOutputFile => ConfigurationManager.AppSettings["Traffic:OutputFile"] ?? "./logs/traffic_stats.txt";
    
    public static TimeSpan TrafficStatsSaveInterval
    {
        get
        {
            var minutes = int.TryParse(ConfigurationManager.AppSettings["Traffic:SaveIntervalMinutes"], out var value) ? value : 5;
            return TimeSpan.FromMinutes(minutes);
        }
    }
    
    // Настройки базы данных
    public static string ConnectionString => ConfigurationManager.AppSettings["Database:ConnectionString"] ?? "Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;";
}
