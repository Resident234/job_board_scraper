using AngleSharp.Dom;
using JobBoardScraper.Core;
using JobBoardScraper.Data;
using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Utils;
using System.Text.RegularExpressions;

namespace JobBoardScraper.Parsing;

/// <summary>
/// Экстрактор данных компаний с страниц рейтинга
/// </summary>
public static class CompanyDataExtractor
{
    private static readonly Regex _companyHrefRegex = new Regex(AppConfig.CompaniesHrefRegex, RegexOptions.Compiled);
    private static readonly Regex _companyIdRegex = new Regex(AppConfig.CompanyDetailCompanyIdRegex, RegexOptions.Compiled);
    private static readonly Regex _alternativeLinkRegex = new Regex(AppConfig.CompanyDetailAlternativeLinkRegex, RegexOptions.Compiled);
    private static readonly Regex _employeesRegex = new Regex(AppConfig.CompanyDetailEmployeesRegex, RegexOptions.Compiled);
    private static readonly Regex _followersRegex = new Regex(AppConfig.CompanyDetailFollowersRegex, RegexOptions.Compiled);

    private static void LogError(ConsoleLogger? logger, string message, Exception? ex = null)
    {
        if (logger != null)
        {
            if (ex != null)
            {
                logger.WriteLine($"{message}: {ex.Message}");
            }
            else
            {
                logger.WriteLine(message);
            }
        }
        else
        {
            Console.WriteLine(ex != null ? $"{message}: {ex.Message}" : message);
        }
    }

    private static void LogInfo(ConsoleLogger? logger, string message)
    {
        if (logger != null)
        {
            logger.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Извлекает название компании из HTML-документа
    /// </summary>
    public static string? ExtractCompanyTitle(IHtmlDocument doc)
    {
        try
        {
            var companyNameElement = doc.QuerySelector(AppConfig.CompanyDetailCompanyNameSelector);
            if (companyNameElement != null)
            {
                // Ищем ссылку внутри элемента
                var linkElement = companyNameElement.QuerySelector(AppConfig.CompanyDetailCompanyNameLinkSelector);
                if (linkElement != null)
                {
                    return linkElement.TextContent?.Trim();
                }
                else
                {
                    // Если ссылки нет, берём текст из самого элемента
                    return companyNameElement.TextContent?.Trim();
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении названия компании", ex);
            return null;
        }
    }

    /// <summary>
    /// Извлекает рейтинг компании из HTML-документа
    /// </summary>
    public static decimal? ExtractCompanyRating(IHtmlDocument doc)
    {
        try
        {
            var companyRatingElement = doc.QuerySelector(AppConfig.CompanyDetailCompanyRatingSelector);
            if (companyRatingElement != null)
            {
                var ratingText = companyRatingElement.TextContent?.Trim();
                if (!string.IsNullOrWhiteSpace(ratingText) &&
                    decimal.TryParse(ratingText, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var rating))
                {
                    return rating;
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении рейтинга компании", ex);
            return null;
        }
    }

    /// <summary>
    /// Извлекает ссылку на сайт компании из HTML-документа
    /// </summary>
    public static string? ExtractCompanySite(IHtmlDocument doc)
    {
        try
        {
            var companySiteElement = doc.QuerySelector(AppConfig.CompanyDetailCompanySiteSelector);
            if (companySiteElement != null)
            {
                var siteLinkElement = companySiteElement.QuerySelector(AppConfig.CompanyDetailCompanySiteLinkSelector);
                if (siteLinkElement != null)
                {
                    return siteLinkElement.GetAttribute("href");
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении ссылки на сайт компании", ex);
            return null;
        }
    }

    /// <summary>
    /// Извлекает детальное описание компании из HTML-документа
    /// </summary>
    public static string? ExtractCompanyDescription(IHtmlDocument doc)
    {
        try
        {
            var companyDescriptionElement = doc.QuerySelector(AppConfig.CompanyDetailDescriptionSelector);
            if (companyDescriptionElement != null)
            {
                return companyDescriptionElement.TextContent?.Trim();
            }
            return null;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении описания компании", ex);
            return null;
        }
    }

    /// <summary>
    /// Извлекает общее количество компаний из HTML-документа
    /// </summary>
    public static int ExtractTotalCompaniesCount(IHtmlDocument doc)
    {
        try
        {
            var totalElement = doc.QuerySelector(AppConfig.CompaniesTotalSelector);
            if (totalElement == null)
                return 0;

            var text = totalElement.TextContent;
            var match = Regex.Match(text, AppConfig.CompaniesTotalRegex);
            if (!match.Success)
                return 0;

            var numberStr = StringUtils.RemoveAllWhitespace(match.Groups[1].Value);
            if (int.TryParse(numberStr, out var total))
                return total;

            return 0;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении общего количества компаний", ex);
            return 0;
        }
    }

    /// <summary>
    /// Парсит список компаний с HTML-документа
    /// </summary>
    public static List<CompanyRecord> ParseCompaniesFromPage(IDocument doc, CancellationToken ct, ConsoleLogger? logger = null)
    {
        var companies = new List<CompanyRecord>();
        var sections = doc.QuerySelectorAll(AppConfig.CompanyRatingSectionSelector);

        foreach (var section in sections)
        {
            try
            {
                if (ExtractCompanyData(section) is { } company)
                {
                    companies.Add(company);
                }
            }
            catch (Exception ex)
            {
                LogError(logger, "Ошибка при парсинге компании", ex);
            }
        }

        return companies;
    }

    /// <summary>
    /// Извлекает данные компании из HTML-элемента
    /// </summary>
    public static CompanyRecord? ExtractCompanyData(IElement section)
    {
        // 1. Извлекаем код компании из ссылки
        var titleLink = section.QuerySelector(AppConfig.CompanyRatingTitleLinkSelector);
        if (titleLink == null) return null;

        var href = titleLink.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(href)) return null;

        // Извлекаем код из href (например, "/companies/tensor" или "https://career.habr.com/companies/tensor" -> "tensor")
        var path = UrlManager.GetAbsolutePath(href);
        var code = path.TrimStart('/').Replace(AppConfig.CompanyRatingCompanyPathPrefix, "").Trim('/');
        if (string.IsNullOrWhiteSpace(code)) return null;

        var url = string.Format(AppConfig.CompanyRatingCompanyUrlTemplate, code);

        // 2. Извлекаем название компании
        var titleElement = section.QuerySelector(AppConfig.CompanyRatingTitleTextSelector);
        var title = titleElement?.TextContent?.Trim();

        // 3. Извлекаем рейтинг
        decimal? rating = null;
        var ratingElement = section.QuerySelector(AppConfig.CompanyRatingRatingSelector);
        if (ratingElement != null)
        {
            var fullText = ratingElement.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(fullText))
            {
                var match = Regex.Match(fullText, @"(\d+[.,]\d+|\d+)");
                if (match.Success && decimal.TryParse(match.Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ratingValue))
                {
                    rating = ratingValue;
                }
            }
        }

        // 4. Извлекаем описание
        var descriptionElement = section.QuerySelector(AppConfig.CompanyRatingDescriptionSelector);
        var about = descriptionElement?.TextContent?.Trim();

        // 5. Извлекаем город
        string? city = null;
        var metaElement = section.QuerySelector(AppConfig.CompanyRatingMetaSelector);
        if (metaElement != null)
        {
            var cityLink = metaElement.QuerySelector(AppConfig.CompanyRatingCityLinkSelector);
            if (cityLink != null)
            {
                city = cityLink.TextContent?.Trim();
            }
        }

        // 6. Извлекаем награды
        var awards = new List<string>();
        var awardsContainer = section.QuerySelector(AppConfig.CompanyRatingAwardsSelector);
        if (awardsContainer != null)
        {
            var awardImages = awardsContainer.QuerySelectorAll(AppConfig.CompanyRatingAwardImageSelector);
            foreach (var img in awardImages)
            {
                var alt = img.GetAttribute("alt");
                if (!string.IsNullOrWhiteSpace(alt))
                {
                    awards.Add(alt.Trim());
                }
            }
        }

        // 7. Извлекаем среднюю оценку
        decimal? scores = null;
        var scoresElement = section.QuerySelector(AppConfig.CompanyRatingScoresSelector);
        if (scoresElement != null)
        {
            var scoresText = scoresElement.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(scoresText) && decimal.TryParse(scoresText.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var scoresValue))
            {
                scores = scoresValue;
            }
        }

        // 8. Извлекаем текст отзыва и вычисляем хеш
        List<CompanyReviewRecord>? reviewRecords = null;
        var reviewElement = section.QuerySelector(AppConfig.CompanyRatingReviewSelector);
        if (reviewElement != null)
        {
            var reviewText = reviewElement.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(reviewText))
            {
                reviewRecords = new List<CompanyReviewRecord>
                {
                    new CompanyReviewRecord(
                        CompanyCode: code,
                        ReviewHash: HashUtils.ComputeHash(reviewText),
                        ReviewText: reviewText
                    )
                };
            }
        }

        return new CompanyRecord(
            CompanyCode: code,
            CompanyUrl: url,
            CompanyTitle: title,
            Rating: rating,
            About: about,
            City: city,
            Awards: awards.Count > 0 ? awards : null,
            Scores: scores,
            ReviewRecords: reviewRecords
        );
    }

    /// <summary>
    /// Проверяет наличие следующей страницы на основе HTML-документа.
    /// </summary>
    /// <param name="doc">HTML-документ.</param>
    /// <param name="currentPage">Текущая страница.</param>
    /// <returns>True, если есть следующая страница.</returns>
    public static bool HasNextPage(IDocument doc, int currentPage)
    {
        try
        {
            var nextPageSelector = string.Format(AppConfig.CompaniesNextPageSelector, currentPage + 1);
            var nextPageLink = doc.QuerySelector(nextPageSelector);
            return nextPageLink != null;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при проверке наличия следующей страницы", ex);
            return false;
        }
    }

    /// <summary>
    /// Извлекает компании из HTML-документа списка компаний.
    /// </summary>
    /// <param name="doc">HTML-документ.</param>
    /// <param name="page">Текущая страница.</param>
    /// <param name="logger">Логгер для записи ошибок.</param>
    /// <returns>Список найденных компаний.</returns>
    public static List<CompanyRecord> ExtractCompanies(
        IDocument doc,
        int page,
        ConsoleLogger? logger = null)
    {
        var companyItems = doc.QuerySelectorAll(AppConfig.CompaniesItemSelector);
        var companies = new List<CompanyRecord>();

        if (companyItems.Length == 0)
        {
            return companies;
        }

        var seenOnPage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in companyItems)
        {
            try
            {
                // Извлекаем company_id из атрибута
                var companyIdStr = item.GetAttribute(AppConfig.CompaniesIdAttribute);
                long? companyId = null;
                if (!string.IsNullOrWhiteSpace(companyIdStr) && long.TryParse(companyIdStr, out var id))
                {
                    companyId = id;
                }

                // Ищем ссылку внутри элемента
                var link = item.QuerySelector(AppConfig.CompaniesLinkSelector);
                if (link == null)
                    continue;

                var href = link.GetAttribute("href");
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                var match = _companyHrefRegex.Match(href);
                if (!match.Success)
                    continue;

                var companyCode = match.Groups[1].Value;

                if (string.IsNullOrWhiteSpace(companyCode))
                    continue;

                // Дедупликация на странице
                if (!seenOnPage.Add(companyCode))
                    continue;

                var companyUrl = UrlManager.Combine(AppConfig.CompaniesBaseUrl, companyCode);
                companies.Add(new CompanyRecord(companyCode, companyUrl, companyId));
            }
            catch (Exception ex)
            {
                LogError(logger, "Ошибка при парсинге компании", ex);
            }
        }

        return companies;
    }

    /// <summary>
    /// Проверяет наличие следующей страницы подписчиков компании.
    /// </summary>
    public static bool HasNextFollowersPage(IHtmlDocument doc, int currentPage, string companyCode)
    {
        try
        {
            var nextPageSelector = string.Format(AppConfig.CompanyFollowersNextPageSelector, currentPage + 1);
            var nextPageLink = doc.QuerySelector(nextPageSelector);

            return nextPageLink != null;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при проверке наличия следующей страницы подписчиков", ex);
            return false;
        }
    }

    /// <summary>
    /// Извлекает список пользователей из HTML-документа страницы подписчиков компании.
    /// </summary>
    /// <param name="doc">HTML-документ.</param>
    /// <param name="logger">Логгер для записи ошибок парсинга.</param>
    /// <returns>Список пользователей в виде ResumeRecord.</returns>
    public static List<ResumeRecord> ExtractFollowersUsers(IHtmlDocument doc, ConsoleLogger? logger = null)
    {
        var userItems = doc.QuerySelectorAll(AppConfig.CompanyFollowersUserItemSelector);
        var users = new List<ResumeRecord>();

        foreach (var userItem in userItems)
        {
            try
            {
                // Извлекаем имя пользователя
                var usernameElement = userItem.QuerySelector(AppConfig.CompanyFollowersUsernameSelector);
                if (usernameElement == null)
                    continue;

                var username = usernameElement.TextContent?.Trim();
                if (string.IsNullOrWhiteSpace(username))
                    continue;

                // Извлекаем ссылку
                var linkElement = userItem.QuerySelector(AppConfig.CompanyFollowersLinkSelector);
                if (linkElement == null)
                    continue;

                var href = linkElement.GetAttribute("href");
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                // Формируем полный URL
                var fullUrl = UrlManager.ToAbsolute(href);

                // Извлекаем слоган (может отсутствовать)
                var sloganElement = userItem.QuerySelector(AppConfig.CompanyFollowersSloganSelector);
                var slogan = sloganElement?.TextContent?.Trim();

                users.Add(new ResumeRecord(
                    Mode: InsertMode.UpdateIfExists,
                    Link: fullUrl,
                    UserName: username,
                    Slogan: slogan
                ));
            }
            catch (Exception ex)
            {
                LogError(logger, "Ошибка при парсинге пользователя", ex);
            }
        }

        return users;
    }

    /// <summary>
    /// Проверяет наличие блога компании на Хабре
    /// </summary>
    public static bool? HasHabrBlog(IHtmlDocument doc)
    {
        var allDivs = doc.QuerySelectorAll("div.title");
        foreach (var div in allDivs)
        {
            var divText = div.TextContent?.Trim();
            if (divText == AppConfig.CompanyDetailHabrBlogText)
            {
                return true;
            }
        }
        return null;
    }

    /// <summary>
    /// Извлекает размер компании из HTML-документа
    /// </summary>
    public static string? ExtractCompanySize(IHtmlDocument doc)
    {
        try
        {
            var employeesCountElement = doc.QuerySelector(AppConfig.CompanyDetailEmployeesCountElementSelector);
            if (employeesCountElement != null)
            {
                return employeesCountElement.TextContent?.Trim();
            }
            return null;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении размера компании", ex);
            return null;
        }
    }

    /// <summary>
    /// Извлекает company_id из HTML-документа
    /// </summary>
    public static (long? companyId, bool success) ExtractCompanyId(
        IHtmlDocument doc,
        string companyCode,
        ConsoleLogger logger)
    {
        try
        {
            long? companyId = null;
            bool companyIdFound = false;

            // Пробуем извлечь company_id из company_fav_button
            var favButton = doc.QuerySelector(AppConfig.CompanyDetailFavButtonSelector);
            if (favButton != null)
            {
                var elementId = favButton.GetAttribute("id");
                if (!string.IsNullOrWhiteSpace(elementId))
                {
                    var companyIdMatch = _companyIdRegex.Match(elementId);
                    if (companyIdMatch.Success)
                    {
                        var companyIdStr = companyIdMatch.Groups[1].Value;
                        if (long.TryParse(companyIdStr, out var parsedId))
                        {
                            companyId = parsedId;
                            companyIdFound = true;
                            LogInfo(logger, $"Компания {companyCode}: ID извлечен из company_fav_button: {companyId}");
                        }
                    }
                }
            }

            // Если не нашли через company_fav_button, пробуем альтернативный способ
            if (!companyIdFound)
            {
                var alternativeLink = doc.QuerySelector(AppConfig.CompanyDetailAlternativeLinkSelector);
                if (alternativeLink != null)
                {
                    var href = alternativeLink.GetAttribute("href");
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        var altMatch = _alternativeLinkRegex.Match(href);
                        if (altMatch.Success)
                        {
                            var companyIdStr = altMatch.Groups[1].Value;
                            if (long.TryParse(companyIdStr, out var parsedId))
                            {
                                companyId = parsedId;
                                companyIdFound = true;
                                LogInfo(logger, $"Компания {companyCode}: ID извлечен из альтернативной ссылки: {companyId}");
                            }
                        }
                    }
                }
            }

            return (companyId, companyIdFound);
        }
        catch (Exception ex)
        {
            LogError(logger, $"Ошибка при извлечении company_id для компании {companyCode}", ex);
            return (null, false);
        }
    }

    /// <summary>
    /// Извлекает количество подписчиков и желающих работать из HTML-документа
    /// </summary>
    public static (int? followers, int? wantWork) ExtractFollowersCount(IHtmlDocument doc)
    {
        try
        {
            int? followers = null;
            int? wantWork = null;

            var followersElement = doc.QuerySelector(AppConfig.CompanyDetailFollowersSelector);
            if (followersElement != null)
            {
                var countElement = followersElement.QuerySelector(AppConfig.CompanyDetailFollowersCountSelector);
                if (countElement != null)
                {
                    var countText = countElement.TextContent?.Trim();
                    if (!string.IsNullOrWhiteSpace(countText))
                    {
                        var followersMatch = _followersRegex.Match(countText);
                        if (followersMatch.Success && followersMatch.Groups.Count >= 3)
                        {
                            if (int.TryParse(followersMatch.Groups[1].Value, out var follower))
                            {
                                followers = follower;
                            }

                            if (int.TryParse(followersMatch.Groups[2].Value, out var want))
                            {
                                wantWork = want;
                            }
                        }
                    }
                }
            }

            return (followers, wantWork);
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении количества подписчиков", ex);
            return (null, null);
        }
    }

    /// <summary>
    /// Извлекает количество сотрудников из HTML-документа
    /// </summary>
    public static (int? current, int? past) ExtractEmployeesCount(IHtmlDocument doc)
    {
        try
        {
            int? currentEmployees = null;
            int? pastEmployees = null;

            var employeesElement = doc.QuerySelector(AppConfig.CompanyDetailEmployeesSelector);
            if (employeesElement != null)
            {
                var countElement = employeesElement.QuerySelector(AppConfig.CompanyDetailEmployeesCountSelector);
                if (countElement != null)
                {
                    var countText = countElement.TextContent?.Trim();
                    if (!string.IsNullOrWhiteSpace(countText))
                    {
                        var employeesMatch = _employeesRegex.Match(countText);
                        if (employeesMatch.Success && employeesMatch.Groups.Count >= 3)
                        {
                            if (int.TryParse(employeesMatch.Groups[1].Value, out var current))
                            {
                                currentEmployees = current;
                            }

                            if (int.TryParse(employeesMatch.Groups[2].Value, out var past))
                            {
                                pastEmployees = past;
                            }
                        }
                    }
                }
            }

            return (currentEmployees, pastEmployees);
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении количества сотрудников", ex);
            return (null, null);
        }
    }

    /// <summary>
    /// Извлекает навыки компании из HTML-документа
    /// </summary>
    public static List<SkillsRecord> ExtractCompanySkills(IHtmlDocument doc)
    {
        try
        {
            var skills = new List<SkillsRecord>();
            var skillsContainer = doc.QuerySelector(AppConfig.CompanyDetailSkillsContainerSelector);
            if (skillsContainer != null)
            {
                var skillElements = skillsContainer.QuerySelectorAll(AppConfig.CompanyDetailSkillSelector);
                foreach (var skillElement in skillElements)
                {
                    var skillTitle = skillElement.TextContent?.Trim();
                    if (!string.IsNullOrWhiteSpace(skillTitle))
                    {
                        skills.Add(new SkillsRecord(SkillId: null, SkillTitle: skillTitle));
                    }
                }
            }
            return skills;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении навыков компании", ex);
            return new List<SkillsRecord>();
        }
    }

    /// <summary>
    /// Извлекает связанные компании из HTML-документа
    /// </summary>
    public static List<CompanyRecord> ExtractRelatedCompanies(IHtmlDocument doc)
    {
        try
        {
            var relatedCompanies = new List<CompanyRecord>();
            var inlineCompaniesList = doc.QuerySelector(AppConfig.CompanyDetailInlineCompaniesListSelector);

            if (inlineCompaniesList != null)
            {
                var companyItems = inlineCompaniesList.QuerySelectorAll(AppConfig.CompanyDetailCompanyItemSelector);
                var companyHrefRegex = new Regex(AppConfig.CompanyDetailCompanyHrefRegex, RegexOptions.Compiled);

                foreach (var companyItem in companyItems)
                {
                    try
                    {
                        // Извлекаем название компании и ссылку
                        var titleLink = companyItem.QuerySelector(AppConfig.CompanyDetailCompanyTitleLinkSelector);
                        if (titleLink == null)
                            continue;

                        var companyName = titleLink.TextContent?.Trim();
                        var href = titleLink.GetAttribute("href");

                        if (string.IsNullOrWhiteSpace(href))
                            continue;

                        var hrefMatch = companyHrefRegex.Match(href);
                        if (!hrefMatch.Success)
                            continue;

                        var companyCode = hrefMatch.Groups[1].Value;

                        // Формируем полную ссылку
                        var companyUrl = UrlManager.Combine(AppConfig.CompanyDetailCompanyBaseUrl, companyCode);

                        relatedCompanies.Add(new CompanyRecord(companyCode, companyUrl, companyTitle: companyName));
                    }
                    catch (Exception ex)
                    {
                        LogError(null, "Ошибка при обработке связанной компании", ex);
                    }
                }
            }
            return relatedCompanies;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении связанных компаний", ex);
            return new List<CompanyRecord>();
        }
    }

    /// <summary>
    /// Извлекает сотрудников компании из HTML-документа
    /// </summary>
    public static List<ResumeRecord> ExtractCompanyEmployees(IHtmlDocument doc)
    {
        try
        {
            var employees = new List<ResumeRecord>();
            var usersList = doc.QuerySelector(AppConfig.CompanyDetailUsersListSelector);
            if (usersList != null)
            {
                var userLinks = usersList.QuerySelectorAll(AppConfig.CompanyDetailUserLinkSelector);
                var userHrefRegex = new Regex(AppConfig.CompanyDetailUserHrefRegex, RegexOptions.Compiled);

                foreach (var userLink in userLinks)
                {
                    try
                    {
                        // Извлекаем код из href
                        var href = userLink.GetAttribute("href");
                        if (string.IsNullOrWhiteSpace(href))
                            continue;

                        var hrefMatch = userHrefRegex.Match(href);
                        if (!hrefMatch.Success)
                            continue;

                        var userCode = hrefMatch.Groups[1].Value;

                        // Формируем полную ссылку
                        var userFullLink = UrlManager.Combine(AppConfig.CompanyDetailUserBaseUrl, userCode);

                        employees.Add(new ResumeRecord(
                            Mode: InsertMode.SkipIfExists,
                            Link: userFullLink,
                            UserName: userCode,
                            Slogan: null
                        ));
                    }
                    catch (Exception ex)
                    {
                        LogError(null, "Ошибка при обработке сотрудника", ex);
                    }
                }
            }
            return employees;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении сотрудников компании", ex);
            return new List<ResumeRecord>();
        }
    }

    /// <summary>
    /// Извлекает контактных лиц компании из HTML-документа
    /// </summary>
    public static List<ResumeRecord> ExtractPublicMembers(IHtmlDocument doc)
    {
        try
        {
            var members = new List<ResumeRecord>();
            var publicMembers = doc.QuerySelectorAll(AppConfig.CompanyDetailPublicMemberSelector);
            var memberHrefRegex = new Regex(AppConfig.CompanyDetailPublicMemberHrefRegex, RegexOptions.Compiled);

            foreach (var member in publicMembers)
            {
                try
                {
                    // Извлекаем имя
                    var nameElement = member.QuerySelector(AppConfig.CompanyDetailPublicMemberNameSelector);
                    var memberName = nameElement?.TextContent?.Trim();

                    // Извлекаем код из href
                    var href = member.GetAttribute("href");
                    if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(memberName))
                        continue;

                    var hrefMatch = memberHrefRegex.Match(href);
                    if (!hrefMatch.Success)
                        continue;

                    var memberCode = hrefMatch.Groups[1].Value;

                    // Формируем полную ссылку
                    var memberLink = UrlManager.Combine(AppConfig.CompanyDetailPublicMemberBaseUrl, memberCode);

                    members.Add(new ResumeRecord(
                        Mode: InsertMode.UpdateIfExists,
                        Link: memberLink,
                        UserName: memberName,
                        Slogan: null
                    ));
                }
                catch (Exception ex)
                {
                    LogError(null, "Ошибка при обработке контактного лица", ex);
                }
            }
            return members;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении контактных лиц компании", ex);
            return new List<ResumeRecord>();
        }
    }

    /// <summary>
    /// Извлекает краткое описание компании из HTML-документа
    /// </summary>
    public static string? ExtractCompanyAbout(IHtmlDocument doc)
    {
        try
        {
            var companyAboutElement = doc.QuerySelector(AppConfig.CompanyDetailCompanyAboutSelector);
            if (companyAboutElement != null)
            {
                return companyAboutElement.TextContent?.Trim();
            }
            return null;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении краткого описания компании", ex);
            return null;
        }
    }

    /// <summary>
    /// Извлекает категории из HTML-документа
    /// </summary>
    public static List<(string value, string text)> ExtractCategories(IHtmlDocument doc)
    {
        try
        {
            var categories = new List<(string value, string text)>();

            // Ищем select с id="category_root_id"
            var selectElement = doc.QuerySelector(AppConfig.CategorySelectElementSelector);

            if (selectElement == null)
            {
                LogError(null, "Не найден элемент select#category_root_id");
                return categories;
            }

            // Собираем все option с value
            var options = selectElement.QuerySelectorAll(AppConfig.CategoryOptionSelector);

            foreach (var option in options)
            {
                var value = option.GetAttribute("value");
                var text = option.TextContent?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                categories.Add((value, text));
            }

            return categories;
        }
        catch (Exception ex)
        {
            LogError(null, "Ошибка при извлечении категорий", ex);
            return new List<(string value, string text)>();
        }
    }
}
