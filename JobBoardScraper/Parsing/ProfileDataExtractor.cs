using AngleSharp.Dom;
using JobBoardScraper.Data;
using JobBoardScraper.Domain.Models;

namespace JobBoardScraper.Parsing;

/// <summary>
/// Вспомогательные методы для извлечения данных профиля из DOM
/// </summary>
public static class ProfileDataExtractor
{
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
                                (about == "Доступ ограничен настройками приватности" || about == "Ошибка 404");
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
    /// Извлекает текст "О себе" из секции с заголовком "Обо мне".
    /// </summary>
    public static string? ExtractAboutSection(IDocument doc)
    {
        var contentSections = doc.QuerySelectorAll(AppConfig.UserResumeDetailContentSelector);
        foreach (var section in contentSections)
        {
            var titleElement = section.QuerySelector(".content-section__title");
            var titleText = titleElement?.TextContent?.Trim();
            if (titleText != null && titleText.Contains("Обо мне", StringComparison.OrdinalIgnoreCase))
            {
                var ugcContent = section.QuerySelector(".style-ugc");
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

        const string deletedMarker1 = "Профиль удален";
        const string deletedMarker2 = "user-profile__deleted";
        const string deletedMarker3 = "Страница удалена";

        // Проверяем CSS-класс через DOM-запрос
        if (!string.IsNullOrEmpty(doc.QuerySelector($".{deletedMarker2}")?.ClassName))
        {
            return true;
        }

        // Проверяем текстовые маркеры в содержимом документа
        var documentText = doc.DocumentElement?.TextContent ?? string.Empty;
        return documentText.Contains(deletedMarker1, StringComparison.OrdinalIgnoreCase) ||
               documentText.Contains(deletedMarker3, StringComparison.OrdinalIgnoreCase);
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

        const string privateProfileText1 = "Доступ ограничен настройками приватности";
        const string privateProfileText2 = "Информация скрыта";
        const string privateProfileClass = "user-page-sidebar--status-hidden";

        // Проверяем CSS-класс через DOM-запрос
        if (doc.QuerySelector($".{privateProfileClass}") != null)
        {
            return true;
        }

        // Проверяем текстовые маркеры в содержимом документа
        var documentText = doc.DocumentElement?.TextContent ?? string.Empty;
        return documentText.Contains(privateProfileText1, StringComparison.OrdinalIgnoreCase) ||
               documentText.Contains(privateProfileText2, StringComparison.OrdinalIgnoreCase);
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
    /// Извлекает опыт работы и последний визит из секций .basic-section
    /// </summary>
    /// <param name="doc">Документ для парсинга</param>
    /// <param name="basicSectionSelector">Селектор для .basic-section</param>
    /// <returns>Кортеж (workExperience, lastVisit)</returns>
    public static (string? workExperience, string? lastVisit) ExtractWorkExperienceAndLastVisit(
        IDocument doc, 
        string basicSectionSelector = ".basic-section")
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
                if (textContent.Contains("Опыт работы:"))
                {
                    // Извлекаем текст после "Опыт работы:"
                    var parts = textContent.Split(new[] { "Опыт работы:" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        workExperience = parts[1].Trim();
                    }
                }
                
                // Проверяем на последний визит
                if (textContent.Contains("Последний визит:"))
                {
                    // Извлекаем текст после "Последний визит:"
                    var parts = textContent.Split(new[] { "Последний визит:" }, StringSplitOptions.None);
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
        string basicSectionSelector = ".basic-section")
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
                if (textContent.Contains("Возраст:"))
                {
                    var parts = textContent.Split(new[] { "Возраст:" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        age = parts[1].Trim();
                    }
                }

                // Извлекаем опыт работы (текстовое описание)
                if (textContent.Contains("Опыт работы:"))
                {
                    var parts = textContent.Split(new[] { "Опыт работы:" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        experienceText = parts[1].Trim();
                    }
                }

                // Извлекаем регистрацию
                if (textContent.Contains("Регистрация:"))
                {
                    var parts = textContent.Split(new[] { "Регистрация:" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        registration = parts[1].Trim();
                    }
                }

                // Извлекаем последний визит
                if (textContent.Contains("Последний визит:"))
                {
                    var parts = textContent.Split(new[] { "Последний визит:" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        lastVisit = parts[1].Trim();
                    }
                }

                // Извлекаем гражданство
                if (textContent.Contains("Гражданство:"))
                {
                    var parts = textContent.Split(new[] { "Гражданство:" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        citizenship = parts[1].Trim();
                    }
                }

                // Извлекаем дополнительную информацию (готовность к удаленной работе)
                if (textContent.Contains("Дополнительно:"))
                {
                    var parts = textContent.Split(new[] { "Дополнительно:" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        var additionalInfo = parts[1].Trim().ToLower();
                        // Проверяем наличие ключевых слов, указывающих на готовность к удаленной работе
                        if (additionalInfo.Contains("удаленн") || additionalInfo.Contains("remote"))
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
    /// <param name="doc">Документ для парсинга</param>
    /// <param name="pageTitleSelector">Селектор для заголовка страницы</param>
    /// <returns>Имя пользователя или null</returns>
    public static string? ExtractUserName(IDocument doc, string pageTitleSelector = "h1.page-title__title")
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
        string metaSelector = ".user-page-sidebar__meta",
        string inlineListSelector = ".inline-list")
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
                    var textSpan = child.QuerySelector("span:first-child");
                    if (textSpan != null)
                    {
                        var text = textSpan.TextContent?.Trim();
                        if (!string.IsNullOrWhiteSpace(text) && !text.Contains("•"))
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
        string careerSelector = ".user-page-sidebar__career",
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
                var pattern = salaryRegex ?? @"От\s+([\d\s]+)\s*₽";
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
                if (careerText.Contains("Ищу работу"))
                {
                    jobSearchStatus = "Ищу работу";
                }
                else if (careerText.Contains("Не ищу работу"))
                {
                    jobSearchStatus = "Не ищу работу";
                }
                else if (careerText.Contains("Рассматриваю предложения"))
                {
                    jobSearchStatus = "Рассматриваю предложения";
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
        string profileLinkSelector = "a[href^='/']",
        string separatorSelector = "span.inline-separator")
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
        var pattern = salaryRegex ?? @"От\s+([\d\s]+)\s*₽";
        var salarySpans = section.QuerySelectorAll("span");

        foreach (var span in salarySpans)
        {
            var text = span.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(text) && text.Contains("От") && text.Contains("₽"))
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
            
            if (text.Contains("Ищу работу"))
            {
                return "Ищу работу";
            }
            else if (text.Contains("Не ищу работу"))
            {
                return "Не ищу работу";
            }
            else if (text.Contains("Рассматриваю предложения"))
            {
                return "Рассматриваю предложения";
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
                var course = ExtractSingleCourse(courseElement);
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

    private static CourseData? ExtractSingleCourse(IElement courseElement)
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
                    var educationData = ExtractSingleAdditionalEducationItem(item, userLink);
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

    private static AdditionalEducationRecord? ExtractSingleAdditionalEducationItem(IElement item, string userLink)
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
            var topicLinks = topicsElement.QuerySelectorAll("a.link-comp");
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
                experiences.Add(BuildExperienceRecord(item, userLink, isFirst));
                isFirst = false;
            }
            catch
            {
                // Подавляем ошибки парсинга отдельных записей — они не критичны для всего профиля.
            }
        }
        return (experiences.Count, experiences);
    }

    private static UserExperienceRecord BuildExperienceRecord(
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
}
