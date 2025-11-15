using System.Configuration;
using JobBoardScraper.Helper.ConsoleHelper;

namespace JobBoardScraper;

/// <summary>
/// Конфигурация приложения, читается из App.config
/// </summary>
public static class AppConfig
{
    // Настройки для BruteForceUsernameScraper
    public static bool BruteForceEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["BruteForce:Enabled"], out var value) && value;

    public static char[] Chars =>
        (ConfigurationManager.AppSettings["BruteForce:Chars"] ?? "abcdefghijklmnopqrstuvwxyz0123456789-_")
        .ToCharArray();

    public static string BaseUrl => ConfigurationManager.AppSettings["BruteForce:BaseUrl"] ?? "http://career.habr.com/";

    public static int MinLength => int.TryParse(ConfigurationManager.AppSettings["BruteForce:MinLength"], out var value)
        ? value
        : 5;

    public static int MaxLength => int.TryParse(ConfigurationManager.AppSettings["BruteForce:MaxLength"], out var value)
        ? value
        : 5;

    public static int MaxConcurrentRequests =>
        int.TryParse(ConfigurationManager.AppSettings["BruteForce:MaxConcurrentRequests"], out var value) ? value : 5;

    public static int MaxRetries =>
        int.TryParse(ConfigurationManager.AppSettings["BruteForce:MaxRetries"], out var value) ? value : 200;

    public static bool BruteForceEnableRetry =>
        bool.TryParse(ConfigurationManager.AppSettings["BruteForce:EnableRetry"], out var value) && value;

    public static bool BruteForceEnableTrafficMeasuring =>
        bool.TryParse(ConfigurationManager.AppSettings["BruteForce:EnableTrafficMeasuring"], out var value)
            ? value
            : true;

    // Настройки для CompanyListScraper
    public static bool CompaniesEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["Companies:Enabled"], out var value) && value;

    public static string CompaniesListUrl =>
        ConfigurationManager.AppSettings["Companies:ListUrl"] ?? "https://career.habr.com/companies";

    public static string CompaniesBaseUrl =>
        ConfigurationManager.AppSettings["Companies:BaseUrl"] ?? "https://career.habr.com/companies/";

    public static string CompaniesItemSelector =>
        ConfigurationManager.AppSettings["Companies:ItemSelector"] ?? ".companies-item";

    public static string CompaniesIdAttribute =>
        ConfigurationManager.AppSettings["Companies:IdAttribute"] ?? "data-company-id";

    public static string CompaniesLinkSelector =>
        ConfigurationManager.AppSettings["Companies:LinkSelector"] ?? "a[href^='/companies/']";

    public static string CompaniesHrefRegex =>
        ConfigurationManager.AppSettings["Companies:HrefRegex"] ?? "/companies/([a-zA-Z0-9_-]+)";

    public static string CompaniesNextPageSelector => ConfigurationManager.AppSettings["Companies:NextPageSelector"] ??
                                                      "a.page[href*='page={0}']";

    public static OutputMode CompaniesOutputMode
    {
        get
        {
            var value = ConfigurationManager.AppSettings["Companies:OutputMode"];
            return Enum.TryParse<OutputMode>(value, out var mode) ? mode : OutputMode.ConsoleOnly;
        }
    }

    public static bool CompaniesEnableTrafficMeasuring =>
        bool.TryParse(ConfigurationManager.AppSettings["Companies:EnableTrafficMeasuring"], out var value)
            ? value
            : true;

    // Настройки для CompanyFollowersScraper
    public static bool CompanyFollowersEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["CompanyFollowers:Enabled"], out var value) ? value : true;

    public static TimeSpan CompanyFollowersTimeout
    {
        get
        {
            var seconds =
                int.TryParse(ConfigurationManager.AppSettings["CompanyFollowers:TimeoutSeconds"], out var value)
                    ? value
                    : 300;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public static string CompanyFollowersUrlTemplate =>
        ConfigurationManager.AppSettings["CompanyFollowers:UrlTemplate"] ??
        "https://career.habr.com/companies/{0}/followers";

    public static string CompanyFollowersUserItemSelector =>
        ConfigurationManager.AppSettings["CompanyFollowers:UserItemSelector"] ?? ".user_friends_item";

    public static string CompanyFollowersUsernameSelector =>
        ConfigurationManager.AppSettings["CompanyFollowers:UsernameSelector"] ?? ".username";

    public static string CompanyFollowersLinkSelector =>
        ConfigurationManager.AppSettings["CompanyFollowers:LinkSelector"] ?? "a";

    public static string CompanyFollowersSloganSelector =>
        ConfigurationManager.AppSettings["CompanyFollowers:SloganSelector"] ?? ".specialization";

    public static string CompanyFollowersNextPageSelector =>
        ConfigurationManager.AppSettings["CompanyFollowers:NextPageSelector"] ?? "a.page[href*='page={0}']";

    public static OutputMode CompanyFollowersOutputMode
    {
        get
        {
            var value = ConfigurationManager.AppSettings["CompanyFollowers:OutputMode"];
            return Enum.TryParse<OutputMode>(value, out var mode) ? mode : OutputMode.ConsoleOnly;
        }
    }

    public static bool CompanyFollowersEnableTrafficMeasuring =>
        bool.TryParse(ConfigurationManager.AppSettings["CompanyFollowers:EnableTrafficMeasuring"], out var value)
            ? value
            : true;

    // Настройки для ExpertsScraper
    public static bool ExpertsEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["Experts:Enabled"], out var value) ? value : true;

    public static string ExpertsListUrl => ConfigurationManager.AppSettings["Experts:ListUrl"] ??
                                           "https://career.habr.com/experts?order=lastActive";

    public static TimeSpan ExpertsTimeout
    {
        get
        {
            var seconds = int.TryParse(ConfigurationManager.AppSettings["Experts:TimeoutSeconds"], out var value)
                ? value
                : 60;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public static bool ExpertsEnableRetry =>
        bool.TryParse(ConfigurationManager.AppSettings["Experts:EnableRetry"], out var value) ? value : true;

    public static bool ExpertsEnableTrafficMeasuring =>
        bool.TryParse(ConfigurationManager.AppSettings["Experts:EnableTrafficMeasuring"], out var value) ? value : true;

    public static OutputMode ExpertsOutputMode
    {
        get
        {
            var value = ConfigurationManager.AppSettings["Experts:OutputMode"];
            return Enum.TryParse<OutputMode>(value, out var mode) ? mode : OutputMode.ConsoleOnly;
        }
    }

    public static string ExpertsExpertCardSelector =>
        ConfigurationManager.AppSettings["Experts:ExpertCardSelector"] ?? ".expert-card";

    public static string ExpertsTitleLinkSelector => ConfigurationManager.AppSettings["Experts:TitleLinkSelector"] ??
                                                     "a.expert-card__title-link";

    public static string ExpertsSpanSelector => ConfigurationManager.AppSettings["Experts:SpanSelector"] ?? "span";

    public static string ExpertsCompanyLinkSelector =>
        ConfigurationManager.AppSettings["Experts:CompanyLinkSelector"] ?? "a.link-comp";

    public static string ExpertsUserCodeRegex =>
        ConfigurationManager.AppSettings["Experts:UserCodeRegex"] ?? "^/([^/]+)$";

    public static string ExpertsCompanyCodeRegex =>
        ConfigurationManager.AppSettings["Experts:CompanyCodeRegex"] ?? "/companies/([^/]+)";

    // Общие настройки для скраперов
    public static bool ResumeListEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["ResumeList:Enabled"], out var value) && value;

    public static bool ResumeListEnableTrafficMeasuring =>
        bool.TryParse(ConfigurationManager.AppSettings["ResumeList:EnableTrafficMeasuring"], out var value)
            ? value
            : true;

    public static string ResumeListResumeLinkSelector =>
        ConfigurationManager.AppSettings["ResumeList:ResumeLinkSelector"] ?? "a.resume-card__title-link";

    public static bool ResumeListSkillsEnumerationEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["ResumeList:SkillsEnumerationEnabled"], out var value) && value;

    public static int ResumeListSkillsStartId =>
        int.TryParse(ConfigurationManager.AppSettings["ResumeList:SkillsStartId"], out var value) ? value : 1;

    public static int ResumeListSkillsEndId =>
        int.TryParse(ConfigurationManager.AppSettings["ResumeList:SkillsEndId"], out var value) ? value : 10000;

    public static OutputMode ResumeListOutputMode
    {
        get
        {
            var value = ConfigurationManager.AppSettings["ResumeList:OutputMode"];
            return Enum.TryParse<OutputMode>(value, out var mode) ? mode : OutputMode.ConsoleOnly;
        }
    }

    public static string ResumeListPageUrl =>
        ConfigurationManager.AppSettings["ResumeList:PageUrl"] ?? "/resumes?order=last_visited";

    public static string ResumeListSkillUrlTemplate =>
        ConfigurationManager.AppSettings["ResumeList:SkillUrlTemplate"] ?? "/resumes?skills[]={0}";

    public static string ResumeListProfileSectionSelector =>
        ConfigurationManager.AppSettings["ResumeList:ProfileSectionSelector"] ?? ".base-section";

    public static string ResumeListProfileLinkSelector =>
        ConfigurationManager.AppSettings["ResumeList:ProfileLinkSelector"] ?? "a.text-inherit.visited\\:text-font-gray";

    public static string ResumeListExpertIconSelector =>
        ConfigurationManager.AppSettings["ResumeList:ExpertIconSelector"] ?? "svg use[xlink\\:href*='expert-icon']";

    public static string ResumeListSeparatorSelector =>
        ConfigurationManager.AppSettings["ResumeList:SeparatorSelector"] ?? "span.inline-separator";

    public static string ResumeListSkillsSectionSelector =>
        ConfigurationManager.AppSettings["ResumeList:SkillsSectionSelector"] ?? "section.grid";

    public static string ResumeListSkillButtonSelector =>
        ConfigurationManager.AppSettings["ResumeList:SkillButtonSelector"] ?? "button span";

    public static string ResumeListSalaryRegex =>
        ConfigurationManager.AppSettings["ResumeList:SalaryRegex"] ?? @"От\s+([\d\s]+)\s*₽";

    public static string ResumeListProfileLinkRegex =>
        ConfigurationManager.AppSettings["ResumeList:ProfileLinkRegex"] ?? @"^(?:https://career\.habr\.com/)?([^/]+)$";

    public static string ResumeListProfileUrlTemplate =>
        ConfigurationManager.AppSettings["ResumeList:ProfileUrlTemplate"] ?? "https://career.habr.com/{0}";

    public static bool ResumeListWorkStatesEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["ResumeList:WorkStatesEnabled"], out var value) && value;

    public static string ResumeListWorkStatesUrlTemplate =>
        ConfigurationManager.AppSettings["ResumeList:WorkStatesUrlTemplate"] ?? "/resumes?work_states[]={0}";

    public static string[] ResumeListWorkStates =>
        (ConfigurationManager.AppSettings["ResumeList:WorkStates"] ?? "search,ready,not_search")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool ResumeListExperiencesEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["ResumeList:ExperiencesEnabled"], out var value) && value;

    public static string ResumeListExperiencesUrlTemplate =>
        ConfigurationManager.AppSettings["ResumeList:ExperiencesUrlTemplate"] ?? "/resumes?experiences[]={0}";

    public static string[] ResumeListExperiences =>
        (ConfigurationManager.AppSettings["ResumeList:Experiences"] ?? "without,year,three_year,six_year,more_six")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool ResumeListQidsEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["ResumeList:QidsEnabled"], out var value) && value;

    public static string ResumeListQidsUrlTemplate =>
        ConfigurationManager.AppSettings["ResumeList:QidsUrlTemplate"] ?? "/resumes?qids[]={0}";

    public static int ResumeListQidsStartId =>
        int.TryParse(ConfigurationManager.AppSettings["ResumeList:QidsStartId"], out var value) ? value : 1;

    public static int ResumeListQidsEndId =>
        int.TryParse(ConfigurationManager.AppSettings["ResumeList:QidsEndId"], out var value) ? value : 10;

    public static bool ResumeListOrderEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["ResumeList:OrderEnabled"], out var value) && value;

    public static string[] ResumeListOrders =>
        (ConfigurationManager.AppSettings["ResumeList:Orders"] ?? "last_visited,salary_desc,salary_asc")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool ResumeListCompanyIdsEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["ResumeList:CompanyIdsEnabled"], out var value) && value;

    public static string ResumeListCompanyIdsUrlTemplate =>
        ConfigurationManager.AppSettings["ResumeList:CompanyIdsUrlTemplate"] ?? "/resumes?company_ids[]={0}&current_company=1";

    public static string[] ResumeListCompanyIdsOrders =>
        (ConfigurationManager.AppSettings["ResumeList:CompanyIdsOrders"] ?? "salary_desc,last_visited,relevance,salary_asc")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool CategoryEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["Category:Enabled"], out var value) && value;

    public static bool CategoryEnableTrafficMeasuring =>
        bool.TryParse(ConfigurationManager.AppSettings["Category:EnableTrafficMeasuring"], out var value)
            ? value
            : true;

    public static string CategorySelectElementSelector =>
        ConfigurationManager.AppSettings["Category:SelectElementSelector"] ?? "select#category_root_id";

    public static string CategoryOptionSelector =>
        ConfigurationManager.AppSettings["Category:OptionSelector"] ?? "option[value]";

    // Настройки для CompanyDetailScraper
    public static bool CompanyDetailEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["CompanyDetail:Enabled"], out var value) ? value : false;

    public static TimeSpan CompanyDetailTimeout
    {
        get
        {
            var seconds = int.TryParse(ConfigurationManager.AppSettings["CompanyDetail:TimeoutSeconds"], out var value)
                ? value
                : 60;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public static bool CompanyDetailEnableRetry =>
        bool.TryParse(ConfigurationManager.AppSettings["CompanyDetail:EnableRetry"], out var value) ? value : true;

    public static bool CompanyDetailEnableTrafficMeasuring =>
        bool.TryParse(ConfigurationManager.AppSettings["CompanyDetail:EnableTrafficMeasuring"], out var value)
            ? value
            : true;

    public static OutputMode CompanyDetailOutputMode
    {
        get
        {
            var value = ConfigurationManager.AppSettings["CompanyDetail:OutputMode"];
            return Enum.TryParse<OutputMode>(value, out var mode) ? mode : OutputMode.ConsoleOnly;
        }
    }

    public static string CompanyDetailFavButtonSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:FavButtonSelector"] ?? "[id^='company_fav_button_']";

    public static string CompanyDetailCompanyNameSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:CompanyNameSelector"] ?? ".company_name";

    public static string CompanyDetailCompanyNameLinkSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:CompanyNameLinkSelector"] ?? "a";

    public static string CompanyDetailCompanyAboutSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:CompanyAboutSelector"] ?? ".company_about";

    public static string CompanyDetailCompanySiteSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:CompanySiteSelector"] ?? ".company_site";

    public static string CompanyDetailCompanySiteLinkSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:CompanySiteLinkSelector"] ?? "a";

    public static string CompanyDetailCompanyRatingSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:CompanyRatingSelector"] ?? "span.rating";

    public static string CompanyDetailEmployeesSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:EmployeesSelector"] ??
        "[data-tooltip='Текущие и все сотрудники']";

    public static string CompanyDetailEmployeesCountSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:EmployeesCountSelector"] ?? ".count";

    public static string CompanyDetailEmployeesRegex =>
        ConfigurationManager.AppSettings["CompanyDetail:EmployeesRegex"] ?? "(\\d+)\\s*/\\s*(\\d+)";

    public static string CompanyDetailFollowersSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:FollowersSelector"] ??
        "[data-tooltip='Подписчики и те, кто хочет тут работать']";

    public static string CompanyDetailFollowersCountSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:FollowersCountSelector"] ?? ".count";

    public static string CompanyDetailFollowersRegex =>
        ConfigurationManager.AppSettings["CompanyDetail:FollowersRegex"] ?? "(\\d+)\\s*/\\s*(\\d+)";

    public static string CompanyDetailEmployeesCountElementSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:EmployeesCountElementSelector"] ?? ".employees";

    public static string CompanyDetailEmployeesCountRegex =>
        ConfigurationManager.AppSettings["CompanyDetail:EmployeesCountRegex"] ?? "(\\d+)";

    public static string CompanyDetailCompanyIdRegex =>
        ConfigurationManager.AppSettings["CompanyDetail:CompanyIdRegex"] ?? "company_fav_button_(\\d+)";

    public static string CompanyDetailAlternativeLinkSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:AlternativeLinkSelector"] ?? "a.row[href*='company_ids']";

    public static string CompanyDetailAlternativeLinkRegex =>
        ConfigurationManager.AppSettings["CompanyDetail:AlternativeLinkRegex"] ?? "company_ids%5B%5D=(\\d+)";

    public static string CompanyDetailPublicMemberSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:PublicMemberSelector"] ?? "a.company-public-member";

    public static string CompanyDetailPublicMemberNameSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:PublicMemberNameSelector"] ?? ".company-public-member__name";

    public static string CompanyDetailPublicMemberHrefRegex =>
        ConfigurationManager.AppSettings["CompanyDetail:PublicMemberHrefRegex"] ?? "^/([^/]+)$";

    public static string CompanyDetailPublicMemberBaseUrl =>
        ConfigurationManager.AppSettings["CompanyDetail:PublicMemberBaseUrl"] ?? "https://career.habr.com/";

    public static string CompanyDetailDescriptionSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:DescriptionSelector"] ?? ".description";

    public static string CompanyDetailSkillsContainerSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:SkillsContainerSelector"] ?? ".skills";

    public static string CompanyDetailSkillSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:SkillSelector"] ?? "a.skill";

    public static string CompanyDetailHabrBlogText =>
        ConfigurationManager.AppSettings["CompanyDetail:HabrBlogText"] ?? "Ведет блог на «Хабре»";

    public static string CompanyDetailUsersListSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:UsersListSelector"] ?? ".company_users_list";

    public static string CompanyDetailUserLinkSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:UserLinkSelector"] ?? "a.user";

    public static string CompanyDetailUserHrefRegex =>
        ConfigurationManager.AppSettings["CompanyDetail:UserHrefRegex"] ?? "^/([^/]+)$";

    public static string CompanyDetailUserBaseUrl =>
        ConfigurationManager.AppSettings["CompanyDetail:UserBaseUrl"] ?? "https://career.habr.com/";

    public static string CompanyDetailInlineCompaniesListSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:InlineCompaniesListSelector"] ?? ".inline_companies_list";

    public static string CompanyDetailCompanyItemSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:CompanyItemSelector"] ?? ".company_item";

    public static string CompanyDetailCompanyTitleLinkSelector =>
        ConfigurationManager.AppSettings["CompanyDetail:CompanyTitleLinkSelector"] ?? "a.title";

    public static string CompanyDetailCompanyHrefRegex =>
        ConfigurationManager.AppSettings["CompanyDetail:CompanyHrefRegex"] ?? "/companies/([^/]+)";

    public static string CompanyDetailCompanyBaseUrl =>
        ConfigurationManager.AppSettings["CompanyDetail:CompanyBaseUrl"] ?? "https://career.habr.com/companies/";

    // Настройки для UserProfileScraper
    public static bool UserProfileEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["UserProfile:Enabled"], out var value) ? value : false;

    public static TimeSpan UserProfileTimeout
    {
        get
        {
            var seconds = int.TryParse(ConfigurationManager.AppSettings["UserProfile:TimeoutSeconds"], out var value)
                ? value
                : 60;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public static bool UserProfileEnableRetry =>
        bool.TryParse(ConfigurationManager.AppSettings["UserProfile:EnableRetry"], out var value) ? value : true;

    public static bool UserProfileEnableTrafficMeasuring =>
        bool.TryParse(ConfigurationManager.AppSettings["UserProfile:EnableTrafficMeasuring"], out var value)
            ? value
            : true;

    public static OutputMode UserProfileOutputMode
    {
        get
        {
            var value = ConfigurationManager.AppSettings["UserProfile:OutputMode"];
            return Enum.TryParse<OutputMode>(value, out var mode) ? mode : OutputMode.ConsoleOnly;
        }
    }

    public static string UserProfileFriendsUrlTemplate =>
        ConfigurationManager.AppSettings["UserProfile:FriendsUrlTemplate"] ?? "https://career.habr.com/{0}/friends";

    public static string UserProfilePageTitleSelector =>
        ConfigurationManager.AppSettings["UserProfile:PageTitleSelector"] ?? "h1.page-title__title";

    public static string UserProfileExpertSelector => ConfigurationManager.AppSettings["UserProfile:ExpertSelector"] ??
                                                      ".user-page-sidebar__is-expert";

    public static string UserProfileMetaSelector =>
        ConfigurationManager.AppSettings["UserProfile:MetaSelector"] ?? ".user-page-sidebar__meta";

    public static string UserProfileInlineListSelector =>
        ConfigurationManager.AppSettings["UserProfile:InlineListSelector"] ?? ".inline-list";

    public static string UserProfileCareerSelector => ConfigurationManager.AppSettings["UserProfile:CareerSelector"] ??
                                                      ".user-page-sidebar__career";

    public static string UserProfileSalaryRegex =>
        ConfigurationManager.AppSettings["UserProfile:SalaryRegex"] ?? "От\\s+([\\d\\s]+)\\s*₽";

    public static string UserProfileBasicSectionSelector =>
        ConfigurationManager.AppSettings["UserProfile:BasicSectionSelector"] ?? ".basic-section";

    public static string UserProfileWorkExperienceRegex =>
        ConfigurationManager.AppSettings["UserProfile:WorkExperienceRegex"] ?? "Опыт работы:</span>\\s*(.+?)\\s*</div>";

    public static string UserProfileLastVisitRegex => ConfigurationManager.AppSettings["UserProfile:LastVisitRegex"] ??
                                                      "Последний визит:</span>\\s*(.+?)\\s*</div>";

    // Настройки логирования
    public static string LoggingOutputDirectory =>
        ConfigurationManager.AppSettings["Logging:OutputDirectory"] ?? "./logs";

    // Настройки статистики трафика
    public static string TrafficStatsOutputFile =>
        ConfigurationManager.AppSettings["Traffic:OutputFile"] ?? "./logs/traffic_stats.txt";

    public static TimeSpan TrafficStatsSaveInterval
    {
        get
        {
            var minutes = int.TryParse(ConfigurationManager.AppSettings["Traffic:SaveIntervalMinutes"], out var value)
                ? value
                : 5;
            return TimeSpan.FromMinutes(minutes);
        }
    }

    // Настройки базы данных
    public static string ConnectionString => ConfigurationManager.AppSettings["Database:ConnectionString"] ??
                                             "Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;";

    // Настройки сохранения HTML для отладки
    public static bool ExpertsSaveHtml =>
        bool.TryParse(ConfigurationManager.AppSettings["Experts:SaveHtml"], out var value) && value;

    public static bool CompanyDetailSaveHtml =>
        bool.TryParse(ConfigurationManager.AppSettings["CompanyDetail:SaveHtml"], out var value) && value;

    public static bool UserProfileSaveHtml =>
        bool.TryParse(ConfigurationManager.AppSettings["UserProfile:SaveHtml"], out var value) && value;


    // Настройки для UserFriendsScraper
    public static bool UserFriendsEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["UserFriends:Enabled"], out var value) && value;

    public static TimeSpan UserFriendsTimeout
    {
        get
        {
            var seconds = int.TryParse(ConfigurationManager.AppSettings["UserFriends:TimeoutSeconds"], out var value)
                ? value
                : 60;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public static bool UserFriendsEnableRetry =>
        bool.TryParse(ConfigurationManager.AppSettings["UserFriends:EnableRetry"], out var value) ? value : true;

    public static bool UserFriendsEnableTrafficMeasuring =>
        bool.TryParse(ConfigurationManager.AppSettings["UserFriends:EnableTrafficMeasuring"], out var value)
            ? value
            : true;

    public static OutputMode UserFriendsOutputMode
    {
        get
        {
            var value = ConfigurationManager.AppSettings["UserFriends:OutputMode"];
            return Enum.TryParse<OutputMode>(value, out var mode) ? mode : OutputMode.ConsoleOnly;
        }
    }


    // Фильтр для извлечения пользователей
    public static bool UserFriendsOnlyPublic =>
        bool.TryParse(ConfigurationManager.AppSettings["UserFriends:OnlyPublic"], out var value) && value;

    
    public static string UserFriendsBaseUrl =>
        ConfigurationManager.AppSettings["UserFriends:BaseUrl"] ?? "https://career.habr.com";


    public static string UserFriendsFriendLinkSelector =>
        ConfigurationManager.AppSettings["UserFriends:FriendLinkSelector"] ?? "a.link-comp.user-list-item__link";

    // Настройки для UserResumeDetailScraper
    public static bool UserResumeDetailEnabled =>
        bool.TryParse(ConfigurationManager.AppSettings["UserResumeDetail:Enabled"], out var value) && value;

    public static TimeSpan UserResumeDetailTimeout
    {
        get
        {
            var seconds = int.TryParse(ConfigurationManager.AppSettings["UserResumeDetail:TimeoutSeconds"], out var value)
                ? value
                : 60;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public static bool UserResumeDetailEnableRetry =>
        bool.TryParse(ConfigurationManager.AppSettings["UserResumeDetail:EnableRetry"], out var value) ? value : true;

    public static bool UserResumeDetailEnableTrafficMeasuring =>
        bool.TryParse(ConfigurationManager.AppSettings["UserResumeDetail:EnableTrafficMeasuring"], out var value)
            ? value
            : true;

    public static OutputMode UserResumeDetailOutputMode
    {
        get
        {
            var value = ConfigurationManager.AppSettings["UserResumeDetail:OutputMode"];
            return Enum.TryParse<OutputMode>(value, out var mode) ? mode : OutputMode.ConsoleOnly;
        }
    }

    public static string UserResumeDetailContentSelector =>
        ConfigurationManager.AppSettings["UserResumeDetail:ContentSelector"] ??
        ".content-section.content-section--appearance-resume";

    public static string UserResumeDetailSkillSelector =>
        ConfigurationManager.AppSettings["UserResumeDetail:SkillSelector"] ?? ".skills-list-show-item";

    public static string UserResumeDetailExperienceContainerSelector =>
        ConfigurationManager.AppSettings["UserResumeDetail:ExperienceContainerSelector"] ??
        ".job-experience-item__positions";

    public static string UserResumeDetailExperienceItemSelector =>
        ConfigurationManager.AppSettings["UserResumeDetail:ExperienceItemSelector"] ?? ".job-experience-item";

    public static string UserResumeDetailCompanyLinkSelector =>
        ConfigurationManager.AppSettings["UserResumeDetail:CompanyLinkSelector"] ??
        "a.link-comp.link-comp--appearance-dark";

    public static string UserResumeDetailCompanyAboutSelector =>
        ConfigurationManager.AppSettings["UserResumeDetail:CompanyAboutSelector"] ??
        ".job-experience-item__subtitle";

    public static string UserResumeDetailPositionSelector =>
        ConfigurationManager.AppSettings["UserResumeDetail:PositionSelector"] ?? ".job-position__title";

    public static string UserResumeDetailDurationSelector =>
        ConfigurationManager.AppSettings["UserResumeDetail:DurationSelector"] ?? ".job-position__duration";

    public static string UserResumeDetailDescriptionSelector =>
        ConfigurationManager.AppSettings["UserResumeDetail:DescriptionSelector"] ?? ".job-position__message";

    public static string UserResumeDetailTagsSelector =>
        ConfigurationManager.AppSettings["UserResumeDetail:TagsSelector"] ?? ".job-position__tags";

    public static string UserResumeDetailCompanyCodeRegex =>
        ConfigurationManager.AppSettings["UserResumeDetail:CompanyCodeRegex"] ?? "/companies/([^/?]+)";

    public static string UserResumeDetailSkillIdRegex =>
        ConfigurationManager.AppSettings["UserResumeDetail:SkillIdRegex"] ?? @"skills%5B%5D=(\d+)";

    public static string UserResumeDetailCompanyUrlTemplate =>
        ConfigurationManager.AppSettings["UserResumeDetail:CompanyUrlTemplate"] ??
        "https://career.habr.com/companies/{0}";

    public static string UserResumeDetailCompanySizeUrlPattern =>
        ConfigurationManager.AppSettings["UserResumeDetail:CompanySizeUrlPattern"] ?? "/companies?sz=";
}
