using System.Configuration;
using JobBoardScraper.Helper.ConsoleHelper;

namespace JobBoardScraper;

/// <summary>
/// Конфигурация приложения, читается из App.config
/// </summary>
public static class AppConfig
{
    // Настройки для BruteForceUsernameScraper
    public static char[] Chars => (ConfigurationManager.AppSettings["BruteForce:Chars"] ?? "abcdefghijklmnopqrstuvwxyz0123456789-_").ToCharArray();
    
    public static string BaseUrl => ConfigurationManager.AppSettings["BruteForce:BaseUrl"] ?? "http://career.habr.com/";
    
    public static int MinLength => int.TryParse(ConfigurationManager.AppSettings["BruteForce:MinLength"], out var value) ? value : 5;
    
    public static int MaxLength => int.TryParse(ConfigurationManager.AppSettings["BruteForce:MaxLength"], out var value) ? value : 5;
    
    public static int MaxConcurrentRequests => int.TryParse(ConfigurationManager.AppSettings["BruteForce:MaxConcurrentRequests"], out var value) ? value : 5;
    
    public static int MaxRetries => int.TryParse(ConfigurationManager.AppSettings["BruteForce:MaxRetries"], out var value) ? value : 200;
    
    // Настройки для CompanyListScraper
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
    
    // Настройки для CompanyFollowersScraper
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
    
    // Настройки логирования
    public static string LoggingOutputDirectory => ConfigurationManager.AppSettings["Logging:OutputDirectory"] ?? "./logs";
    
    // Настройки базы данных
    public static string ConnectionString => ConfigurationManager.AppSettings["Database:ConnectionString"] ?? "Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;";
}
