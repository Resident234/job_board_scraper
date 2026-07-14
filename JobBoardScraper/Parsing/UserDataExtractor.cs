using AngleSharp.Dom;
using JobBoardScraper.Data;
using JobBoardScraper.Domain.Models;
using JobBoardScraper.Infrastructure.Logging;
using JobBoardScraper.Infrastructure.Url;

namespace JobBoardScraper.Parsing;

/// <summary>
/// Вспомогательные методы для извлечения данных профиля из DOM
/// </summary>
public static class UserDataExtractor
{
    private static readonly ConsoleLogger DefaultLogger = new("UserDataExtractor");

    #region Константы для кириллических строк

    private const string PrivacyRestrictedText = "Доступ ограничен настройками приватности";
    private const string Error404Text = "Ошибка 404";
    private const string ProfileDeletedText = "Профиль удален";
    private const string PageDeletedText = "Страница удалена";
    private const string InfoHiddenText = "Информация скрыта";
    private const string SpecialistsNotFoundRuText = "Специалисты не найдены";
    private const string SpecialistsNotFoundEnText = "Specialists not found";
    private const string AboutMeText = "Обо мне";
    private const string BulletText = "•";
    private const string WorkExperienceText = "Опыт работы:";
    private const string LastVisitText = "Последний визит:";
    private const string AgeText = "Возраст:";
    private const string RegistrationText = "Регистрация:";
    private const string CitizenshipText = "Гражданство:";
    private const string RemoteWorkText = "Удаленн";
    private const string SearchForWorkText = "Ищу работу";
    private const string NotSearchingForWorkText = "Не ищу работу";
    private const string ConsiderOffersText = "Рассматриваю предложения";
    private const string AdditionalInfoText = "Дополнительно:";
    private const string JobSearchStatusesPrefixText = "Стаж ";
    private const string SalaryText = "От";
    private const string RubleText = "₽";
    private const string SalaryRegexPattern = @"От\s+([\d\s]+)\s*₽";
    private const string ProfileParsingErrorText = "Ошибка при парсинге профиля:";

    #endregion

    #region CSS селекторы и классы

    private const string ProfileDeletedClass = "user-profile__deleted";
    private const string ProfileHiddenClass = "user-page-sidebar--status-hidden";
    private const string ContentSectionTitleSelector = ".content-section__title";
    private const string StyleUgcSelector = ".style-ugc";
    private const string BasicSectionSelector = ".basic-section";
    private const string PageTitleSelector = "h1.page-title__title";
    private const string UserPageSidebarMetaSelector = ".user-page-sidebar__meta";
    private const string InlineListSelector = ".inline-list";
    private const string FirstChildSpanSelector = "span:first-child";
    private const string UserPageSidebarCareerSelector = ".user-page-sidebar__career";
    private const string ProfileLinkSelector = "a[href^='/']";
    private const string InlineSeparatorSelector = "span.inline-separator";
    private const string LinkCompSelector = "a.link-comp";

    #endregion

    /// <summary>
    /// Определяет, является ли профиль пустым (нет ни одного блока данных).
    /// Профиль считается пустым, если выполняются ВСЕ условия:
    /// - секция "О себе" пуста (или содержит только служебные сообщения);
    /// - нет навыков;
    /// - нет записей об опыте работы;
    /// - нет записей о высшем образовании;
    /// - нет записей о дополнительном образовании;
    /// - нет данных об участии в профсообществах.
    /// </summary>
    /// <param name="doc">Документ для проверки</param>
    /// <returns>true, если профиль не содержит значимых данных; иначе false</returns>
    public static bool IsEmptyProfile(IDocument doc)
    {
        if (doc == null)
            return true;

        // 1) Секция "О себе" — ищем непустой текст внутри .style-ugc, исключая служебные сообщения.
        string? about = ExtractAboutSection(doc);
        bool isServiceMessage = !string.IsNullOrWhiteSpace(about) &&
                                (about == PrivacyRestrictedText || about == Error404Text);
        bool hasAbout = !isServiceMessage && !string.IsNullOrWhiteSpace(about);
        if (hasAbout)
        {
            return false;
        }

        // 2) Навыки
        bool hasSkills = doc.QuerySelectorAll(AppConfig.UserResumeDetailSkillSelector).Length > 0;
        if (hasSkills)
        {
            return false;
        }

        // 3) Опыт работы
        bool hasExperience = doc.QuerySelectorAll(AppConfig.UserResumeDetailExperienceItemSelector).Length > 0;
        if (hasExperience)
        {
            return false;
        }

        // 4) Высшее образование
        bool hasEducation = doc.QuerySelectorAll(AppConfig.EducationItemSelector).Length > 0;
        if (hasEducation)
        {
            return false;
        }

        // 5) Дополнительное образование
        bool hasAdditionalEducation = doc.QuerySelectorAll(AppConfig.AdditionalEducationItemSelector).Length > 0;
        if (hasAdditionalEducation)
        {
            return false;
        }

        // 6) Участие в профсообществах
        bool hasCommunityParticipation = doc.QuerySelectorAll(AppConfig.CommunityParticipationItemSelector).Length > 0;
        if (hasCommunityParticipation)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Проверяет, содержит ли документ маркеры удалённого профиля пользователя.
    /// Используется скраперами для определения профилей, которые были удалены
    /// (с последующим сохранением статуса "Профиль удален" в БД).
    /// </summary>
    /// <param name="doc">Документ для проверки</param>
    /// <returns>true, если в документе найден любой из маркеров удалённого профиля; иначе false.</returns>
    public static bool IsDeletedProfile(IDocument doc)
    {
        if (doc == null)
            return false;

        // Проверяем CSS-класс через DOM-запрос
        if (!string.IsNullOrEmpty(doc.QuerySelector($".{ProfileDeletedClass}")?.ClassName))
        {
            return true;
        }

        // Проверяем текстовые маркеры в содержимом документа
        var documentText = doc.DocumentElement?.TextContent ?? string.Empty;
        return documentText.Contains(ProfileDeletedText, StringComparison.OrdinalIgnoreCase) ||
               documentText.Contains(PageDeletedText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет, является ли профиль приватным (доступ ограничен настройками приватности).
    /// Проверяются несколько признаков:
    /// 1. Текст "Доступ ограничен настройками приватности"
    /// 2. Текст "Информация скрыта"
    /// 3. CSS-класс "user-page-sidebar--status-hidden"
    /// </summary>
    /// <param name="doc">Документ для проверки</param>
    /// <returns>true, если найден любой из признаков приватного профиля; иначе false.</returns>
    public static bool IsPrivateProfile(IDocument doc)
    {
        if (doc == null)
            return false;

        // Проверяем CSS-класс через DOM-запрос
        if (doc.QuerySelector($".{ProfileHiddenClass}") != null)
        {
            return true;
        }

        // Проверяем текстовые маркеры в содержимом документа
        var documentText = doc.DocumentElement?.TextContent ?? string.Empty;
        return documentText.Contains(PrivacyRestrictedText, StringComparison.OrdinalIgnoreCase) ||
               documentText.Contains(InfoHiddenText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет, является ли пользователь экспертом (на странице профиля присутствует маркер эксперта).
    /// Метод возвращает true, если в документе найден элемент по указанному селектору;
    /// null, если документ не задан или селектор пустой (значение не определено);
    /// false, если селектор корректный, но элемент не найден (т.е. это обычный пользователь, не эксперт).
    /// </summary>
    /// <param name="doc">Документ для проверки</param>
    /// <param name="expertSelector">CSS-селектор маркера эксперта (например, ".user-page-sidebar__expert").</param>
    /// <returns>
    /// true — элемент найден, пользователь эксперт;
    /// false — селектор валиден, но элемент не найден (обычный пользователь);
    /// null — не удалось выполнить проверку (doc == null или пустой селектор).
    /// </returns>
    public static bool? IsExpertProfile(IDocument doc, string expertSelector)
    {
        if (doc == null || string.IsNullOrWhiteSpace(expertSelector))
        {
            return null;
        }

        return doc.QuerySelector(expertSelector) != null;
    }

    /// <summary>
    /// Определяет, является ли профиль пользователя публичным.
    /// Профиль считается публичным, если в документе присутствует имя пользователя
    /// (ищется по селектору заголовка страницы). Иначе — приватный (редирект/скрыт).
    /// </summary>
    /// <param name="doc">Документ для проверки</param>
    /// <param name="pageTitleSelector">CSS-селектор заголовка страницы (например, "h1.page-title__title").</param>
    /// <returns>
    /// Кортеж (userName, isPublic):
    ///   userName — извлечённое имя пользователя (или null, если не найдено);
    ///   isPublic — true, если имя найдено; false, если пусто/null/whitespace (приватный профиль).
    /// </returns>
    public static (string? userName, bool isPublic) IsPublicProfile(
        IDocument doc,
        string pageTitleSelector = PageTitleSelector)
    {
        if (doc == null)
        {
            return (null, false);
        }

        var userName = ExtractUserName(doc, pageTitleSelector);
        bool isPublic = !string.IsNullOrWhiteSpace(userName);
        return (userName, isPublic);
    }

    /// <summary>
    /// Проверяет, является ли строка допустимым названием уровня.
    /// Список допустимых уровней читается из конфига (Levels:ValidTitles).
    /// </summary>
    /// <param name="levelTitle">Строка для проверки</param>
    /// <returns>true если это допустимый уровень, иначе false</returns>
    public static bool IsValidLevelTitle(string? levelTitle)
    {
        if (string.IsNullOrWhiteSpace(levelTitle))
            return false;

        return AppConfig.ValidLevelTitles.Contains(levelTitle.Trim());
    }

    /// <summary>
    /// Проверяет, содержит ли документ списка резюме сообщение об отсутствии специалистов.
    /// </summary>
    public static bool IsNotFoundProfiles(IDocument doc)
    {
        if (doc == null)
            return false;

        const string notFoundProfilesText1 = SpecialistsNotFoundRuText;
        const string notFoundProfilesText2 = SpecialistsNotFoundEnText;

        var documentText = doc.DocumentElement?.TextContent ?? string.Empty;
        return documentText.Contains(notFoundProfilesText1, StringComparison.OrdinalIgnoreCase) ||
               documentText.Contains(notFoundProfilesText2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Извлекает текст "О себе" из секции с заголовком "Обо мне".
    /// </summary>
    public static string? ExtractAboutSection(IDocument doc)
    {
        var contentSections = doc.QuerySelectorAll(AppConfig.UserResumeDetailContentSelector);
        foreach (var section in contentSections)
        {
            var titleElement = section.QuerySelector(ContentSectionTitleSelector);
            var titleText = titleElement?.TextContent?.Trim();
            if (titleText != null && titleText.Contains(AboutMeText, StringComparison.OrdinalIgnoreCase))
            {
                var ugcContent = section.QuerySelector(StyleUgcSelector);
                if (ugcContent != null)
                {
                    return NormalizeHtmlToText(ugcContent.InnerHtml);
                }
                break;
            }
        }
        return null;
    }


    /// <summary>
    /// Извлекает опыт работы и последний визит из секций .basic-section
    /// </summary>
    /// <param name="doc">Документ для парсинга</param>
    /// <param name="basicSectionSelector">Селектор для .basic-section</param>
    /// <returns>Кортеж (workExperience, lastVisit)</returns>
    public static (string? workExperience, string? lastVisit) ExtractWorkExperienceAndLastVisit(
        IDocument doc,
        string basicSectionSelector = BasicSectionSelector)
    {
        string? workExperience = null;
        string? lastVisit = null;

        var basicSectionElements = doc.QuerySelectorAll(basicSectionSelector);
        foreach (var basicSectionElement in basicSectionElements)
        {
            // Ищем все div элементы в секции
            var divElements = basicSectionElement.QuerySelectorAll("div");
            foreach (var div in divElements)
            {
                var textContent = div.TextContent?.Trim();
                if (string.IsNullOrWhiteSpace(textContent))
                    continue;

                // Проверяем на опыт работы
                if (textContent.Contains(WorkExperienceText))
                {
                    // Извлекаем текст после "Опыт работы:"
                    var parts = textContent.Split(new[] { WorkExperienceText }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        workExperience = parts[1].Trim();
                    }
                }

                // Проверяем на последний визит
                if (textContent.Contains(LastVisitText))
                {
                    // Извлекаем текст после "Последний визит:"
                    var parts = textContent.Split(new[] { LastVisitText }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        lastVisit = parts[1].Trim();
                    }
                }
            }
        }

        return (workExperience, lastVisit);
    }

    /// <summary>
    /// Извлекает текст после указанного префикса из элементов
    /// </summary>
    /// <param name="elements">Коллекция элементов для поиска</param>
    /// <param name="prefix">Префикс для поиска (например, "Опыт работы:")</param>
    /// <returns>Извлеченный текст или null</returns>
    public static string? ExtractTextAfterPrefix(IEnumerable<IElement> elements, string prefix)
    {
        foreach (var element in elements)
        {
            var textContent = element.TextContent?.Trim();
            if (string.IsNullOrWhiteSpace(textContent))
                continue;

            if (textContent.Contains(prefix))
            {
                var parts = textContent.Split(new[] { prefix }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    return parts[1].Trim();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Извлекает дополнительные данные профиля из секций .basic-section
    /// и возвращает частично заполненную <see cref="ResumeRecord"/> (Age, WorkExperience,
    /// Registration, LastVisit, Citizenship, RemoteWork).
    /// </summary>
    /// <param name="doc">Документ для парсинга</param>
    /// <param name="basicSectionSelector">Селектор для .basic-section</param>
    /// <returns>Частично заполненная <see cref="ResumeRecord"/>.</returns>
    public static ResumeRecord ExtractAdditionalProfileData(
        IDocument doc,
        string basicSectionSelector = BasicSectionSelector)
    {
        string? age = null;
        string? experienceText = null;
        string? registration = null;
        string? lastVisit = null;
        string? citizenship = null;
        bool? remoteWork = null;

        var basicSectionElements = doc.QuerySelectorAll(basicSectionSelector);
        foreach (var basicSectionElement in basicSectionElements)
        {
            var divElements = basicSectionElement.QuerySelectorAll("div");
            foreach (var div in divElements)
            {
                var textContent = div.TextContent?.Trim();
                if (string.IsNullOrWhiteSpace(textContent))
                    continue;

                // Извлекаем возраст
                if (textContent.Contains(AgeText))
                {
                    var parts = textContent.Split(new[] { AgeText }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        age = parts[1].Trim();
                    }
                }

                // Извлекаем опыт работы (текстовое описание)
                if (textContent.Contains(WorkExperienceText))
                {
                    var parts = textContent.Split(new[] { WorkExperienceText }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        experienceText = parts[1].Trim();
                    }
                }

                // Извлекаем регистрацию
                if (textContent.Contains(RegistrationText))
                {
                    var parts = textContent.Split(new[] { RegistrationText }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        registration = parts[1].Trim();
                    }
                }

                // Извлекаем последний визит
                if (textContent.Contains(LastVisitText))
                {
                    var parts = textContent.Split(new[] { LastVisitText }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        lastVisit = parts[1].Trim();
                    }
                }

                // Извлекаем гражданство
                if (textContent.Contains(CitizenshipText))
                {
                    var parts = textContent.Split(new[] { CitizenshipText }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        citizenship = parts[1].Trim();
                    }
                }

                // Извлекаем дополнительную информацию (готовность к удаленной работе)
                if (textContent.Contains(AdditionalInfoText))
                {
                    var parts = textContent.Split(new[] { AdditionalInfoText }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        var additionalInfo = parts[1].Trim();
                        // Проверяем наличие ключевых слов, указывающих на готовность к удаленной работе
                        if (additionalInfo.Contains(RemoteWorkText, StringComparison.OrdinalIgnoreCase) || additionalInfo.Contains("remote", StringComparison.OrdinalIgnoreCase))
                        {
                            remoteWork = true;
                        }
                    }
                }
            }
        }

        return new ResumeRecord(
            Age: age,
            WorkExperience: experienceText,
            Registration: registration,
            LastVisit: lastVisit,
            Citizenship: citizenship,
            RemoteWork: remoteWork);
    }

    /// <summary>
    /// Извлекает имя пользователя из заголовка страницы
    /// </summary>
    /// <param>name="doc">Документ для парсинга</param>
    /// <param name="pageTitleSelector">Селектор для заголовка страницы</param>
    /// <returns>Имя пользователя или null</returns>
    public static string? ExtractUserName(IDocument doc, string pageTitleSelector = PageTitleSelector)
    {
        var pageTitleElement = doc.QuerySelector(pageTitleSelector);
        return pageTitleElement?.TextContent?.Trim();
    }

    /// <summary>
    /// Извлекает техническую информацию и уровень из метаданных профиля
    /// </summary>
    /// <param name="doc">Документ для парсинга</param>
    /// <param name="metaSelector">Селектор для метаданных</param>
    /// <param name="inlineListSelector">Селектор для списка</param>
    /// <returns>Кортеж (infoTech, levelTitle)</returns>
    public static (string? infoTech, string? levelTitle) ExtractInfoTechAndLevel(
        IDocument doc,
        string metaSelector = UserPageSidebarMetaSelector,
        string inlineListSelector = InlineListSelector)
    {
        string? infoTech = null;
        string? levelTitle = null;

        var metaElement = doc.QuerySelector(metaSelector);
        if (metaElement != null)
        {
            var inlineList = metaElement.QuerySelector(inlineListSelector);
            if (inlineList != null)
            {
                var directChildren = inlineList.Children.Where(c => c.TagName.ToLower() == "span");
                var textParts = new List<string>();

                foreach (var child in directChildren)
                {
                    var textSpan = child.QuerySelector(FirstChildSpanSelector);
                    if (textSpan != null)
                    {
                        var text = textSpan.TextContent?.Trim();
                        if (!string.IsNullOrWhiteSpace(text) && !text.Contains(BulletText))
                        {
                            textParts.Add(text);
                        }
                    }
                }

                if (textParts.Count > 0)
                {
                    var lastPart = textParts[textParts.Count - 1];
                    if (IsValidLevelTitle(lastPart))
                    {
                        levelTitle = lastPart;
                        if (textParts.Count > 1)
                        {
                            infoTech = string.Join(" • ", textParts.Take(textParts.Count - 1));
                        }
                    }
                    else
                    {
                        infoTech = string.Join(" • ", textParts);
                        levelTitle = null;
                    }
                }
            }

        }

        return (infoTech, levelTitle);
    }

    /// <summary>
    /// Извлекает зарплату и статус поиска работы из карьерной секции
    /// </summary>
    /// <param name="doc">Документ для парсинга</param>
    /// <param name="careerSelector">Селектор для карьерной секции</param>
    /// <param name="salaryRegex">Regex для извлечения зарплаты</param>
    /// <returns>Кортеж (salary, jobSearchStatus)</returns>
    public static (int? salary, string? jobSearchStatus) ExtractSalaryAndJobStatus(
        IDocument doc,
        string careerSelector = UserPageSidebarCareerSelector,
        string? salaryRegex = null)
    {
        int? salary = null;
        string? jobSearchStatus = null;

        var careerElement = doc.QuerySelector(careerSelector);
        if (careerElement != null)
        {
            var careerText = careerElement.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(careerText))
            {
                // Извлекаем зарплату
                var pattern = salaryRegex ?? SalaryRegexPattern;
                var salaryMatch = System.Text.RegularExpressions.Regex.Match(careerText, pattern);
                if (salaryMatch.Success && salaryMatch.Groups.Count >= 2)
                {
                    var salaryStr = salaryMatch.Groups[1].Value.Replace(" ", "");
                    if (int.TryParse(salaryStr, out var salaryValue))
                    {
                        salary = salaryValue;
                    }
                }

                // Извлекаем статус поиска работы
                if (careerText.Contains(SearchForWorkText))
                {
                    jobSearchStatus = SearchForWorkText;
                }
                else if (careerText.Contains(NotSearchingForWorkText))
                {
                    jobSearchStatus = NotSearchingForWorkText;
                }
                else if (careerText.Contains(ConsiderOffersText))
                {
                    jobSearchStatus = ConsiderOffersText;
                }
            }
        }

        return (salary, jobSearchStatus);
    }

    /// <summary>
    /// Извлекает имя, должности и уровень из секции профиля в списке резюме
    /// Парсит текст вида "Должность 1 • Должность 2 • Уровень"
    /// </summary>
    public static (string? name, string? infoTech, string? levelTitle) ExtractNameInfoTechAndLevel(
        IElement section,
        string profileLinkSelector = ProfileLinkSelector,
        string separatorSelector = InlineSeparatorSelector)
    {
        string? name = null;
        string? infoTech = null;
        string? levelTitle = null;

        var profileLink = section.QuerySelector(profileLinkSelector);
        if (profileLink != null)
        {
            name = profileLink.TextContent?.Trim();
        }

        var separators = section.QuerySelectorAll(separatorSelector);
        if (separators.Length > 0)
        {
            var parts = new List<string>();

            foreach (var separator in separators)
            {
                var prevSibling = separator.PreviousElementSibling;
                if (prevSibling != null)
                {
                    var text = prevSibling.TextContent?.Trim();
                    if (!string.IsNullOrWhiteSpace(text) && !parts.Contains(text))
                    {
                        parts.Add(text);
                    }
                }
            }

            var lastSeparator = separators[separators.Length - 1];
            var nextSibling = lastSeparator.NextElementSibling;
            if (nextSibling != null)
            {
                var text = nextSibling.TextContent?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && !parts.Contains(text))
                {
                    parts.Add(text);
                }
            }

            if (parts.Count > 0)
            {
                var lastPart = parts[^1].Trim();
                if (IsValidLevelTitle(lastPart))
                {
                    levelTitle = lastPart;
                    if (parts.Count > 1)
                    {
                        infoTech = string.Join(" • ", parts[..^1]);
                    }
                }
                else
                {
                    infoTech = string.Join(" • ", parts);
                    levelTitle = null;
                }
            }
        }

        return (name, infoTech, levelTitle);
    }

    /// <summary>
    /// Извлекает зарплату из секции профиля в списке резюме
    /// </summary>
    public static int? ExtractSalaryFromSection(
        IElement section,
        string? salaryRegex = null)
    {
        var pattern = salaryRegex ?? SalaryRegexPattern;
        var salarySpans = section.QuerySelectorAll("span");

        foreach (var span in salarySpans)
        {
            var text = span.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(text) && text.Contains(SalaryText) && text.Contains(RubleText))
            {
                var salaryMatch = System.Text.RegularExpressions.Regex.Match(text, pattern);
                if (salaryMatch.Success && salaryMatch.Groups.Count >= 2)
                {
                    var salaryStr = salaryMatch.Groups[1].Value.Replace(" ", "");
                    if (int.TryParse(salaryStr, out var salaryValue))
                    {
                        return salaryValue;
                    }
                }
                break;
            }
        }

        return null;
    }

    /// <summary>
    /// Извлекает статус поиска работы из секции профиля в списке резюме
    /// </summary>
    public static string? ExtractJobSearchStatusFromSection(IElement section)
    {
        var allSpans = section.QuerySelectorAll("span");

        foreach (var span in allSpans)
        {
            var text = span.TextContent?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (text.Contains(SearchForWorkText))
            {
                return SearchForWorkText;
            }
            else if (text.Contains(NotSearchingForWorkText))
            {
                return NotSearchingForWorkText;
            }
            else if (text.Contains(ConsiderOffersText))
            {
                return ConsiderOffersText;
            }
        }

        return null;
    }

    /// <summary>
    /// Извлекает данные о высшем образовании из профиля пользователя в виде записей <see cref="UserUniversityRecord"/>.
    /// </summary>
    /// <param name="doc">Документ для парсинга</param>
    /// <param name="userLink">Ссылка на пользователя (проставляется в UserLink каждой записи)</param>
    public static List<UserUniversityRecord> ExtractEducationData(IDocument doc, string userLink = "")
    {
        var result = new List<UserUniversityRecord>();

        try
        {
            var sections = doc.QuerySelectorAll(AppConfig.EducationSectionSelector);
            IElement? educationSection = null;

            foreach (var section in sections)
            {
                var titleElement = section.QuerySelector(AppConfig.EducationSectionTitleSelector);
                var titleText = titleElement?.TextContent?.Trim();

                if (titleText != null && titleText.Contains(AppConfig.EducationSectionTitleText, StringComparison.OrdinalIgnoreCase))
                {
                    educationSection = section;
                    break;
                }
            }

            if (educationSection == null)
            {
                return result;
            }

            var educationItems = educationSection.QuerySelectorAll(AppConfig.EducationItemSelector);

            foreach (var item in educationItems)
            {
                try
                {
                    var educationData = ExtractSingleEducationItem(item, userLink);
                    if (educationData != null)
                    {
                        result.Add(educationData.Value);
                    }
                }
                catch
                {
                    continue;
                }
            }
        }
        catch
        {
        }

        return result;
    }

    /// <summary>
    /// Извлекает данные одной записи о высшем образовании из DOM-элемента.
    /// </summary>
    /// <param name="item">DOM-элемент записи об образовании.</param>
    /// <param name="userLink">Ссылка на профиль пользователя, проставляется в UserLink записи.</param>
    /// <returns>Запись <see cref="UserUniversityRecord"/> или null, если элемент не содержит обязательных данных.</returns>
    private static UserUniversityRecord? ExtractSingleEducationItem(IElement item, string userLink)
    {
        var universityLink = item.QuerySelector(AppConfig.EducationUniversityLinkSelector);
        if (universityLink == null)
        {
            return null;
        }

        var href = universityLink.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var idMatch = System.Text.RegularExpressions.Regex.Match(href, AppConfig.UniversityIdRegex);
        if (!idMatch.Success || !int.TryParse(idMatch.Groups[1].Value, out var universityHabrId))
        {
            return null;
        }

        var universityName = universityLink.TextContent?.Trim();
        if (string.IsNullOrWhiteSpace(universityName))
        {
            var nameElement = item.QuerySelector(AppConfig.EducationUniversityNameSelector);
            universityName = nameElement?.TextContent?.Trim();
        }

        if (string.IsNullOrWhiteSpace(universityName))
        {
            return null;
        }

        string? city = null;
        int? graduateCount = null;

        var locationElement = item.QuerySelector(AppConfig.EducationLocationSelector);
        if (locationElement != null)
        {
            var locationText = locationElement.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(locationText))
            {
                var parts = locationText.Split('•', StringSplitOptions.TrimEntries);
                if (parts.Length > 0)
                {
                    city = parts[0].Trim();
                }

                if (parts.Length > 1)
                {
                    var graduateMatch = System.Text.RegularExpressions.Regex.Match(parts[1], AppConfig.GraduateCountRegex);
                    if (graduateMatch.Success && int.TryParse(graduateMatch.Groups[1].Value, out var count))
                    {
                        graduateCount = count;
                    }
                }
            }
        }

        var courses = ExtractCourses(item);

        string? description = null;
        var descriptionElement = item.QuerySelector(AppConfig.EducationDescriptionSelector);
        if (descriptionElement != null)
        {
            description = descriptionElement.TextContent?.Trim();
        }

        return new UserUniversityRecord(
            UserLink: userLink,
            University: new UniversityRecord(
                HabrId: universityHabrId,
                Name: universityName,
                City: city,
                GraduateCount: graduateCount),
            Courses: courses,
            Description: description);
    }

    /// <summary>
    /// Извлекает список курсов из DOM-элемента записи об образовании.
    /// </summary>
    /// <param name="item">DOM-элемент записи об образовании.</param>
    /// <returns>Список <see cref="CourseData"/>; пустой список, если курсов нет.</returns>
    private static List<CourseData> ExtractCourses(IElement item)
    {
        var courses = new List<CourseData>();

        var coursesContainer = item.QuerySelector(AppConfig.EducationCoursesContainerSelector);
        if (coursesContainer == null)
        {
            return courses;
        }

        var courseElements = coursesContainer.QuerySelectorAll(AppConfig.EducationCourseSelector);

        foreach (var courseElement in courseElements)
        {
            try
            {
                var course = ExtractCourse(courseElement);
                if (course != null)
                {
                    courses.Add(course);
                }
            }
            catch
            {
                continue;
            }
        }

        return courses;
    }

    /// <summary>
    /// Извлекает данные одного курса из DOM-элемента.
    /// </summary>
    /// <param name="courseElement">DOM-элемент курса.</param>
    /// <returns>Запись <see cref="CourseData"/> или null, если название курса не найдено.</returns>
    private static CourseData? ExtractCourse(IElement courseElement)
    {
        var nameElement = courseElement.QuerySelector(AppConfig.EducationCourseNameSelector);
        var courseName = nameElement?.TextContent?.Trim();

        if (string.IsNullOrWhiteSpace(courseName))
        {
            return null;
        }

        var periodElement = courseElement.QuerySelector(AppConfig.EducationCoursePeriodSelector);
        var periodText = periodElement?.TextContent?.Trim();

        var (startDate, endDate, duration, isCurrent) = ParseCoursePeriod(periodText);

        return new CourseData
        {
            Name = courseName,
            StartDate = startDate,
            EndDate = endDate,
            Duration = duration,
            IsCurrent = isCurrent
        };
    }


    /// <summary>
    /// Извлекает числовой идентификатор университета из URL-ссылки.
    /// </summary>
    /// <param name="url">URL профиля университета.</param>
    /// <returns>Числовой идентификатор или null, если URL не содержит идентификатора.</returns>
    public static int? ExtractUniversityIdFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(url, AppConfig.UniversityIdRegex);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
        {
            return id;
        }

        return null;
    }

    /// <summary>
    /// Извлекает данные о дополнительном образовании из профиля пользователя
    /// </summary>
    public static List<AdditionalEducationRecord> ExtractAdditionalEducationData(IDocument doc, string userLink = "")
    {
        var result = new List<AdditionalEducationRecord>();

        try
        {
            var sections = doc.QuerySelectorAll(AppConfig.EducationSectionSelector);
            IElement? additionalEducationSection = null;

            foreach (var section in sections)
            {
                var titleElement = section.QuerySelector(AppConfig.EducationSectionTitleSelector);
                var titleText = titleElement?.TextContent?.Trim();

                if (titleText != null && titleText.Contains(AppConfig.AdditionalEducationSectionTitleText, StringComparison.OrdinalIgnoreCase))
                {
                    additionalEducationSection = section;
                    break;
                }
            }

            if (additionalEducationSection == null)
            {
                return result;
            }

            var container = additionalEducationSection.QuerySelector(AppConfig.AdditionalEducationContainerSelector);
            if (container == null)
            {
                container = additionalEducationSection;
            }

            var educationItems = container.QuerySelectorAll(AppConfig.AdditionalEducationItemSelector);

            foreach (var item in educationItems)
            {
                try
                {
                    var educationData = ExtractAdditionalEducationItem(item, userLink);
                    if (educationData != null)
                    {
                        result.Add(educationData.Value);
                    }
                }
                catch
                {
                    continue;
                }
            }
        }
        catch
        {
        }

        return result;
    }

    /// <summary>
    /// Извлекает данные одной записи о дополнительном образовании из DOM-элемента.
    /// </summary>
    /// <param name="item">DOM-элемент записи о дополнительном образовании.</param>
    /// <param name="userLink">Ссылка на профиль пользователя, проставляется в UserLink записи.</param>
    /// <returns>Запись <see cref="AdditionalEducationRecord"/> или null, если название не найдено.</returns>
    private static AdditionalEducationRecord? ExtractAdditionalEducationItem(IElement item, string userLink)
    {
        var titleElement = item.QuerySelector(AppConfig.AdditionalEducationTitleSelector);
        var title = titleElement?.TextContent?.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        string? course = null;
        var courseElement = item.QuerySelector(AppConfig.AdditionalEducationCourseSelector);
        if (courseElement != null)
        {
            course = courseElement.TextContent?.Trim();
        }

        string? duration = null;
        var durationElement = item.QuerySelector(AppConfig.AdditionalEducationDurationSelector);
        if (durationElement != null)
        {
            duration = durationElement.TextContent?.Trim();
        }

        return new AdditionalEducationRecord(
            UserLink: userLink,
            Title: title,
            Course: course,
            Duration: duration
        );
    }

    /// <summary>
    /// Извлекает данные об участии в профсообществах из профиля пользователя
    /// </summary>
    public static List<CommunityParticipationData> ExtractCommunityParticipationRecords(IDocument doc)
    {
        var result = new List<CommunityParticipationData>();

        try
        {
            var sections = doc.QuerySelectorAll(AppConfig.EducationSectionSelector);
            IElement? communitySection = null;

            foreach (var section in sections)
            {
                var titleElement = section.QuerySelector(AppConfig.EducationSectionTitleSelector);
                var titleText = titleElement?.TextContent?.Trim();

                if (titleText != null && titleText.Contains(AppConfig.CommunityParticipationSectionTitleText, StringComparison.OrdinalIgnoreCase))
                {
                    communitySection = section;
                    break;
                }
            }

            if (communitySection == null)
            {
                return result;
            }

            var container = communitySection.QuerySelector(AppConfig.CommunityParticipationContainerSelector);
            if (container == null)
            {
                container = communitySection;
            }

            var items = container.QuerySelectorAll(AppConfig.CommunityParticipationItemSelector);

            foreach (var item in items)
            {
                try
                {
                    var data = ExtractSingleCommunityParticipationItem(item);
                    if (data != null)
                    {
                        result.Add(data);
                    }
                }
                catch
                {
                    continue;
                }
            }
        }
        catch
        {
        }

        return result;
    }

    /// <summary>
    /// Извлекает данные одной записи об участии в профсообществе из DOM-элемента.
    /// </summary>
    /// <param name="item">DOM-элемент записи участия в сообществе.</param>
    /// <returns>Запись <see cref="CommunityParticipationData"/> или null, если название сообщества не найдено.</returns>
    private static CommunityParticipationData? ExtractSingleCommunityParticipationItem(IElement item)
    {
        var nameElement = item.QuerySelector(AppConfig.CommunityParticipationNameSelector);
        var name = nameElement?.TextContent?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string? memberSince = null;
        var memberSinceElement = item.QuerySelector(AppConfig.CommunityParticipationMemberSinceSelector);
        if (memberSinceElement != null)
        {
            memberSince = memberSinceElement.TextContent?.Trim();
        }

        string? contribution = null;
        var contributionElement = item.QuerySelector(AppConfig.CommunityParticipationContributionSelector);
        if (contributionElement != null)
        {
            contribution = contributionElement.TextContent?.Trim();
        }

        string? topics = null;
        var topicsElement = item.QuerySelector(AppConfig.CommunityParticipationTopicsSelector);
        if (topicsElement != null)
        {
            var topicLinks = topicsElement.QuerySelectorAll(LinkCompSelector);
            var topicsList = new List<string>();

            foreach (var link in topicLinks)
            {
                var topicText = link.TextContent?.Trim();
                if (!string.IsNullOrWhiteSpace(topicText))
                {
                    topicText = topicText.Replace("\u200B", "").Trim();
                    if (!string.IsNullOrWhiteSpace(topicText))
                    {
                        topicsList.Add(topicText);
                    }
                }
            }

            if (topicsList.Count > 0)
            {
                topics = string.Join(" • ", topicsList);
            }
        }

        return new CommunityParticipationData
        {
            Name = name,
            MemberSince = memberSince,
            Contribution = contribution,
            Topics = topics
        };
    }

    /// <summary>
    /// Извлекает список навыков из профиля в виде записей <see cref="SkillsRecord"/>.
    /// </summary>
    public static List<SkillsRecord> ExtractSkills(IDocument doc)
    {
        var skills = new List<SkillsRecord>();
        var skillElements = doc.QuerySelectorAll(AppConfig.UserResumeDetailSkillSelector);
        foreach (var skillElement in skillElements)
        {
            var skillTitle = skillElement.TextContent?.Trim();
            if (string.IsNullOrWhiteSpace(skillTitle))
                continue;

            int? skillId = null;
            var href = skillElement.GetAttribute("href");
            if (!string.IsNullOrWhiteSpace(href))
            {
                var skillMatch = System.Text.RegularExpressions.Regex.Match(
                    href, AppConfig.UserResumeDetailSkillIdRegex);
                if (skillMatch.Success && int.TryParse(skillMatch.Groups[1].Value, out var id))
                    skillId = id;
            }

            skills.Add(new SkillsRecord(SkillId: skillId, SkillTitle: skillTitle));
        }
        return skills;
    }

    /// <summary>
    /// Извлекает данные об опыте работы и возвращает количество записей и список.
    /// </summary>
    public static (int Count, List<UserExperienceRecord> Experiences) ExtractExperience(
        IDocument doc, string userLink)
    {
        var experiences = new List<UserExperienceRecord>();
        var experienceContainer = doc.QuerySelector(AppConfig.UserResumeDetailExperienceContainerSelector);
        if (experienceContainer == null)
            return (0, experiences);

        var experienceItems = experienceContainer.QuerySelectorAll(AppConfig.UserResumeDetailExperienceItemSelector);
        var isFirst = true;
        foreach (var item in experienceItems)
        {
            try
            {
                experiences.Add(ExtractExperienceItem(item, userLink, isFirst));
                isFirst = false;
            }
            catch
            {
                // Подавляем ошибки парсинга отдельных записей — они не критичны для всего профиля.
            }
        }
        return (experiences.Count, experiences);
    }


    /// <summary>
    /// Извлекает данные о высшем образовании.
    /// </summary>
    public static (int Count, List<UserUniversityRecord> Universities) ExtractEducation(
        IDocument doc, string userLink)
    {
        var universities = ExtractEducationData(doc, userLink);
        return (universities.Count, universities);
    }

    /// <summary>
    /// Извлекает данные о дополнительном образовании.
    /// </summary>
    public static (int Count, List<AdditionalEducationRecord> Educations) ExtractAdditionalEducation(
        IDocument doc, string userLink)
    {
        var data = ExtractAdditionalEducationData(doc, userLink);
        var educations = new List<AdditionalEducationRecord>(data.Count);
        foreach (var item in data)
        {
            educations.Add(new AdditionalEducationRecord(
                UserLink: item.UserLink,
                Title: item.Title,
                Course: item.Course,
                Duration: item.Duration));
        }
        return (educations.Count, educations);
    }

    /// <summary>
    /// Извлекает значения атрибута href со ссылок на друзей пользователя со страницы /friends.
    /// Скрапер сразу получает готовые href-строки, без необходимости работать с DOM-элементами.
    /// </summary>
    /// <param name="doc">Документ страницы друзей.</param>
    /// <param name="friendLinkSelector">CSS-селектор ссылок на друзей.</param>
    /// <returns>Массив значений href (может содержать null/empty для ссылок без атрибута).</returns>
    /// <summary>
    /// Извлекает из страницы /friends список пар (href, userCode) для каждого друга.
    /// Href — относительный путь из атрибута href; userCode — код пользователя (href без ведущего '/').
    /// Если href пустой, оба значения будут null.
    /// </summary>
    /// <param name="doc">Документ страницы друзей.</param>
    /// <param name="friendLinkSelector">CSS-селектор ссылок на друзей.</param>
    /// <returns>Список кортежей (Href, UserCode) в порядке появления ссылок на странице.</returns>
    public static IReadOnlyList<(string? Href, string? UserCode)> ExtractFriends(
        IDocument doc,
        string friendLinkSelector)
    {
        if (doc == null || string.IsNullOrWhiteSpace(friendLinkSelector))
        {
            return Array.Empty<(string?, string?)>();
        }

        return doc.QuerySelectorAll(friendLinkSelector)
            .Select(e =>
            {
                var href = e.GetAttribute("href");
                string? userCode = string.IsNullOrWhiteSpace(href) ? null : href.TrimStart('/');
                return (Href: href, UserCode: userCode);
            })
            .ToArray();
    }



    /// <summary>
    /// Извлекает данные одной карточки эксперта: резюме и связанную компанию.
    /// </summary>
    /// <param name="card">DOM-элемент карточки эксперта.</param>
    /// <returns>Кортеж (Resume, Company) или null, если карточка не содержит обязательных данных (ссылки или имени).</returns>
    private static (ResumeRecord Resume, CompanyRecord? Company)? ExtractExpertCard(IElement card)
    {
        var titleLink = card.QuerySelector(AppConfig.ExpertsTitleLinkSelector);
        if (titleLink == null)
        {
            return null;
        }

        var name = titleLink.TextContent?.Trim();
        var href = titleLink.GetAttribute("href");

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(href, AppConfig.ExpertsUserCodeRegex);
        var code = match.Success ? match.Groups[1].Value : null;
        var fullUrl = UrlManager.ToAbsolute(href);
        var workExperience = ExtractExpertWorkExperience(card);
        var company = ExtractExpertCompany(card);

        var resume = new ResumeRecord(
            Link: fullUrl,
            Title: name,
            Code: code,
            Expert: true,
            WorkExperience: workExperience,
            Mode: InsertMode.UpdateIfExists);

        return (resume, company);
    }

    /// <summary>
    /// Извлекает текстовое значение стажа работы из карточки эксперта.
    /// </summary>
    /// <param name="card">DOM-элемент карточки эксперта.</param>
    /// <returns>Строка со стажем (без префикса «Стаж ») или null, если данные не найдены.</returns>
    private static string? ExtractExpertWorkExperience(IElement card)
    {
        const string workExperiencePrefix = JobSearchStatusesPrefixText;

        var spans = card.QuerySelectorAll(AppConfig.ExpertsSpanSelector);
        foreach (var span in spans)
        {
            var text = span.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(text) && text.StartsWith(workExperiencePrefix))
            {
                return text.Replace(workExperiencePrefix, "").Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Извлекает данные компании из карточки эксперта.
    /// </summary>
    /// <param name="card">DOM-элемент карточки эксперта.</param>
    /// <returns>Запись <see cref="CompanyRecord"/> или null, если ссылка на компанию не найдена или не содержит валидного кода.</returns>
    private static CompanyRecord? ExtractExpertCompany(IElement card)
    {
        var companyLink = card.QuerySelector(AppConfig.ExpertsCompanyLinkSelector);
        if (companyLink == null)
        {
            return null;
        }

        var companyName = companyLink.TextContent?.Trim();
        var companyHref = companyLink.GetAttribute("href");

        if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(companyHref))
        {
            return null;
        }

        var companyCodeMatch = System.Text.RegularExpressions.Regex.Match(companyHref, AppConfig.ExpertsCompanyCodeRegex);
        if (!companyCodeMatch.Success)
        {
            return null;
        }

        var companyCode = companyCodeMatch.Groups[1].Value;
        var companyUrl = UrlManager.ToAbsolute(companyHref);

        return new CompanyRecord(companyCode, companyUrl, CompanyTitle: companyName);
    }

    /// <summary>
    /// Формирует запись об опыте работы из DOM-элемента позиции на странице резюме.
    /// </summary>
    /// <param name="item">DOM-элемент записи об опыте работы.</param>
    /// <param name="userLink">Ссылка на профиль пользователя.</param>
    /// <param name="isFirst">true, если это первая (наиболее актуальная) запись об опыте работы.</param>
    /// <returns>Заполненная запись <see cref="UserExperienceRecord"/>.</returns>
    private static UserExperienceRecord ExtractExperienceItem(
        IElement item, string userLink, bool isFirst)
    {
        string? companyCode = null;
        string? companyUrl = null;
        string? companyTitle = null;

        var companyLink = item.QuerySelector(AppConfig.UserResumeDetailCompanyLinkSelector);
        if (companyLink != null)
        {
            companyUrl = companyLink.GetAttribute("href");
            companyTitle = companyLink.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(companyUrl))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    companyUrl, AppConfig.UserResumeDetailCompanyCodeRegex);
                if (match.Success)
                {
                    companyCode = match.Groups[1].Value;
                    companyUrl = string.Format(AppConfig.UserResumeDetailCompanyUrlTemplate, companyCode);
                }
            }
        }

        string? companyAbout = item.QuerySelector(AppConfig.UserResumeDetailCompanyAboutSelector)?.TextContent?.Trim();

        string? companySize = null;
        foreach (var link in item.QuerySelectorAll(AppConfig.UserResumeDetailCompanyLinkSelector))
        {
            var href = link.GetAttribute("href");
            if (!string.IsNullOrWhiteSpace(href) && href.Contains(AppConfig.UserResumeDetailCompanySizeUrlPattern))
            {
                companySize = link.TextContent?.Trim();
                break;
            }
        }

        string? position = item.QuerySelector(AppConfig.UserResumeDetailPositionSelector)?.TextContent?.Trim();
        if (!string.IsNullOrWhiteSpace(position))
        {
            position = System.Text.RegularExpressions.Regex.Replace(position, @"\s+", " ");
        }

        string? duration = item.QuerySelector(AppConfig.UserResumeDetailDurationSelector)?.TextContent?.Trim();
        string? description = item.QuerySelector(AppConfig.UserResumeDetailDescriptionSelector)?.TextContent?.Trim();

        var experienceSkills = new List<SkillsRecord>();
        var tagsContainer = item.QuerySelector(AppConfig.UserResumeDetailTagsSelector);
        if (tagsContainer != null)
        {
            foreach (var skillLink in tagsContainer.QuerySelectorAll(AppConfig.UserResumeDetailCompanyLinkSelector))
            {
                var skillName = skillLink.TextContent?.Trim();
                if (string.IsNullOrWhiteSpace(skillName)) continue;

                int? skillId = null;
                var skillHref = skillLink.GetAttribute("href");
                if (!string.IsNullOrWhiteSpace(skillHref))
                {
                    var skillMatch = System.Text.RegularExpressions.Regex.Match(skillHref, AppConfig.UserResumeDetailSkillIdRegex);
                    if (skillMatch.Success && int.TryParse(skillMatch.Groups[1].Value, out var id))
                        skillId = id;
                }
                experienceSkills.Add(new SkillsRecord(SkillId: skillId, SkillTitle: skillName));
            }
        }

        return new UserExperienceRecord(
            UserLink: userLink,
            Company: new CompanyRecord(
                CompanyCode: companyCode ?? string.Empty,
                CompanyUrl: companyUrl ?? string.Empty,
                CompanyTitle: companyTitle,
                About: companyAbout,
                EmployeesCount: companySize),
            Position: position,
            Duration: duration,
            Description: description,
            Skills: experienceSkills,
            IsFirstRecord: isFirst);
    }

    /// <summary>
    /// Чистый парсинг: извлекает из HTML-документа данные профилей, найденных на странице списка резюме.
    /// Не выполняет никаких операций с БД — только разбирает DOM.
    /// Возвращает готовые ResumeRecord (с Mode = UpdateIfExists), которые вызывающий код кладёт в очередь через EnqueueResume.
    /// </summary>
    /// <param name="doc">Распарсенный HTML-документ страницы списка резюме.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <param name="logger">Опциональный логгер для записи ошибок парсинга отдельных профилей.</param>
    /// <returns>Список распарсенных ResumeRecord (без постановки в очередь БД).</returns>
    public static List<ResumeRecord> ParseProfilesFromPage(
        IDocument doc,
        CancellationToken ct,
        ConsoleLogger? logger = null)
    {
        var profiles = new List<ResumeRecord>();
        var sections = doc.QuerySelectorAll(AppConfig.ResumeListProfileSectionSelector);

        foreach (var section in sections)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // 1) Извлекаем ссылку и имя
                var profileLink = section.QuerySelector(AppConfig.ResumeListProfileLinkSelector);
                if (profileLink == null) continue;

                var href = profileLink.GetAttribute("href");

                if (string.IsNullOrWhiteSpace(href))
                    continue;

                // Проверяем и извлекаем код пользователя с помощью regex
                // Валидные: /username, https://career.habr.com/username
                // Невалидные: https://habr.com/users/username, /some/path
                var cleanHref = href.TrimStart('/');
                var match = System.Text.RegularExpressions.Regex.Match(cleanHref, AppConfig.ResumeListProfileLinkRegex);
                if (!match.Success)
                    continue;

                var code = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                var link = UrlManager.Format(AppConfig.ResumeListProfileUrlTemplate, code);

                // 2) Проверяем признак эксперта
                var expertIcon = section.QuerySelector(AppConfig.ResumeListExpertIconSelector);
                var isExpert = expertIcon != null;

                // 3) Извлекаем имя, должности и уровень используя UserDataExtractor
                var (name, infoTech, levelTitle) = ExtractNameInfoTechAndLevel(
                    section,
                    AppConfig.ResumeListProfileLinkSelector,
                    AppConfig.ResumeListSeparatorSelector);

                // 4) Извлекаем зарплату используя UserDataExtractor
                var salary = ExtractSalaryFromSection(
                    section,
                    AppConfig.ResumeListSalaryRegex);

                // 5) Извлекаем навыки
                var skills = new List<SkillsRecord>();
                var skillsSection = section.QuerySelector(AppConfig.ResumeListSkillsSectionSelector);
                if (skillsSection != null)
                {
                    var skillButtons = skillsSection.QuerySelectorAll(AppConfig.ResumeListSkillButtonSelector);
                    foreach (var skillSpan in skillButtons)
                    {
                        var skillName = skillSpan.TextContent?.Trim();
                        if (!string.IsNullOrWhiteSpace(skillName))
                        {
                            skills.Add(new SkillsRecord(SkillId: null, SkillTitle: skillName));
                        }
                    }
                }

                // Проверяем, что имя не null
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                profiles.Add(new ResumeRecord(
                    Link: link,
                    Title: name,
                    Code: code,
                    Expert: isExpert,
                    UserCode: code,
                    UserName: name,
                    IsExpert: isExpert,
                    LevelTitle: levelTitle,
                    InfoTech: infoTech,
                    Salary: salary,
                    Skills: skills.Count > 0 ? skills : null,
                    Mode: InsertMode.UpdateIfExists
                ));
            }
            catch (Exception ex)
            {
                LogProfileParsingError(logger, ex);
            }
        }

        return profiles;
    }

    /// <summary>
    /// Разбирает текст периода курса и извлекает дату начала, дату окончания, продолжительность и признак текущего курса.
    /// </summary>
    /// <param name="periodText">Текст периода в формате, принятом на сайте (например, «Январь 2020 — Май 2021, 1 год 4 месяца»).</param>
    /// <returns>Кортеж (startDate, endDate, duration, isCurrent).</returns>
    public static (string? startDate, string? endDate, string? duration, bool isCurrent) ParseCoursePeriod(string? periodText)
    {
        if (string.IsNullOrWhiteSpace(periodText))
        {
            return (null, null, null, false);
        }

        string? startDate = null;
        string? endDate = null;
        string? duration = null;
        bool isCurrent = false;

        if (periodText.Contains(AppConfig.CurrentPeriodText, StringComparison.OrdinalIgnoreCase))
        {
            isCurrent = true;
        }

        var match = System.Text.RegularExpressions.Regex.Match(periodText, AppConfig.CoursePeriodRegex);

        if (match.Success)
        {
            startDate = match.Groups[1].Value.Trim();

            var endPart = match.Groups[2].Value.Trim();
            if (!endPart.Contains(AppConfig.CurrentPeriodText, StringComparison.OrdinalIgnoreCase))
            {
                endDate = endPart;
            }

            if (match.Groups.Count > 3 && !string.IsNullOrWhiteSpace(match.Groups[3].Value))
            {
                duration = match.Groups[3].Value.Trim();
            }
        }

        return (startDate, endDate, duration, isCurrent);
    }

    /// <summary>
    /// Извлекает данные экспертов и связанных компаний со страницы списка экспертов.
    /// Скрапер получает уже готовые данные и не работает с DOM-элементами напрямую.
    /// </summary>
    public static (int CardCount, IReadOnlyList<(ResumeRecord Resume, CompanyRecord? Company)> Experts, int FailedCards) ParseExpertsFromPage(
        IDocument doc)
    {
        if (doc == null)
        {
            return (0, Array.Empty<(ResumeRecord Resume, CompanyRecord? Company)>(), 0);
        }

        var experts = new List<(ResumeRecord Resume, CompanyRecord? Company)>();
        var failedCards = 0;
        var expertCards = doc.QuerySelectorAll(AppConfig.ExpertsExpertCardSelector);

        foreach (var card in expertCards)
        {
            try
            {
                var expert = ExtractExpertCard(card);
                if (expert.HasValue)
                {
                    experts.Add(expert.Value);
                }
            }
            catch
            {
                failedCards++;
            }
        }

        return (expertCards.Length, experts, failedCards);
    }

    /// <summary>
    /// Преобразует HTML-фрагмент в читаемый текст с сохранением переносов строк.
    /// </summary>
    private static string NormalizeHtmlToText(string html)
    {
        var text = html;
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<br\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"</p>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"</li>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }


    /// <summary>
    /// Записывает сообщение об ошибке парсинга профиля в логгер или в fallback ConsoleLogger.
    /// </summary>
    /// <param name="logger">Опциональный логгер; если null, используется DefaultLogger.</param>
    /// <param name="ex">Перехваченное исключение.</param>
    private static void LogProfileParsingError(ConsoleLogger? logger, Exception ex)
    {
        var message = $"{ProfileParsingErrorText} {ex.Message}";
        (logger ?? DefaultLogger).WriteLine(message);
    }
}