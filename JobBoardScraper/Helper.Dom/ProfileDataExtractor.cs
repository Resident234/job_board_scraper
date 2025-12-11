using AngleSharp.Dom;
using JobBoardScraper.Models;

namespace JobBoardScraper.Helper.Dom;

/// <summary>
/// Вспомогательные методы для извлечения данных профиля из DOM
/// </summary>
public static class ProfileDataExtractor
{
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
    /// </summary>
    /// <param name="doc">Документ для парсинга</param>
    /// <param name="basicSectionSelector">Селектор для .basic-section</param>
    /// <returns>Кортеж (age, experienceText, registration, lastVisit, citizenship, remoteWork)</returns>
    public static (string? age, string? experienceText, string? registration, string? lastVisit, string? citizenship, bool? remoteWork) ExtractAdditionalProfileData(
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
        
        return (age, experienceText, registration, lastVisit, citizenship, remoteWork);
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
                // Выбираем только прямые дочерние span элементы inline-list,
                // затем берём первый span внутри каждого (который содержит текст, а не разделитель)
                // Структура: .inline-list > span > span:first-child (текст) + span.inline-separator
                var directChildren = inlineList.Children.Where(c => c.TagName.ToLower() == "span");
                var textParts = new List<string>();
                
                foreach (var child in directChildren)
                {
                    // Берём первый вложенный span (который содержит текст)
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
                
                // Последний элемент - это уровень
                if (textParts.Count > 0)
                {
                    levelTitle = textParts[textParts.Count - 1];
                    
                    // Остальные элементы - техническая информация
                    if (textParts.Count > 1)
                    {
                        infoTech = string.Join(" • ", textParts.Take(textParts.Count - 1));
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
    /// <param name="section">Секция профиля</param>
    /// <param name="profileLinkSelector">Селектор для ссылки на профиль</param>
    /// <param name="separatorSelector">Селектор для разделителя</param>
    /// <returns>Кортеж (name, infoTech, levelTitle)</returns>
    public static (string? name, string? infoTech, string? levelTitle) ExtractNameInfoTechAndLevel(
        IElement section,
        string profileLinkSelector = "a[href^='/']",
        string separatorSelector = "span.bullet")
    {
        string? name = null;
        string? infoTech = null;
        string? levelTitle = null;
        
        // Извлекаем имя из ссылки
        var profileLink = section.QuerySelector(profileLinkSelector);
        if (profileLink != null)
        {
            name = profileLink.TextContent?.Trim();
        }
        
        // Извлекаем должности и уровень
        var positionsDiv = section.QuerySelector("div");
        if (positionsDiv != null)
        {
            var separators = positionsDiv.QuerySelectorAll(separatorSelector);
            if (separators.Length > 0)
            {
                var allText = positionsDiv.TextContent?.Trim();
                if (!string.IsNullOrWhiteSpace(allText))
                {
                    // Разбиваем по разделителю •
                    var parts = allText.Split('•', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        // Последний элемент - уровень
                        levelTitle = parts[^1].Trim();
                        
                        // Остальные - должности
                        if (parts.Length > 1)
                        {
                            infoTech = string.Join(" • ", parts[..^1]);
                        }
                    }
                }
            }
        }
        
        return (name, infoTech, levelTitle);
    }
    
    /// <summary>
    /// Извлекает зарплату из секции профиля в списке резюме
    /// Парсит текст вида "От 80 000 ₽"
    /// </summary>
    /// <param name="section">Секция профиля</param>
    /// <param name="salaryRegex">Regex для извлечения зарплаты</param>
    /// <returns>Зарплата или null</returns>
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
    /// Парсит текст вида "Ищу работу", "Не ищу работу", "Рассматриваю предложения"
    /// </summary>
    /// <param name="section">Секция профиля</param>
    /// <returns>Статус поиска работы или null</returns>
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
    /// Извлекает данные о высшем образовании из профиля пользователя
    /// </summary>
    /// <param name="doc">Документ для парсинга</param>
    /// <returns>Список данных об образовании или пустой список если секция не найдена</returns>
    public static List<UniversityEducationData> ExtractEducationData(IDocument doc)
    {
        var result = new List<UniversityEducationData>();
        
        try
        {
            // Ищем секцию "Высшее образование"
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
                return result; // Секция не найдена - возвращаем пустой список
            }
            
            // Ищем элементы образования внутри секции
            var educationItems = educationSection.QuerySelectorAll(AppConfig.EducationItemSelector);
            
            foreach (var item in educationItems)
            {
                try
                {
                    var educationData = ExtractSingleEducationItem(item);
                    if (educationData != null)
                    {
                        result.Add(educationData);
                    }
                }
                catch
                {
                    // Пропускаем элемент при ошибке парсинга
                    continue;
                }
            }
        }
        catch
        {
            // При любой ошибке возвращаем пустой список
        }
        
        return result;
    }

    /// <summary>
    /// Извлекает данные из одного элемента образования
    /// </summary>
    private static UniversityEducationData? ExtractSingleEducationItem(IElement item)
    {
        // Извлекаем ссылку на университет и его ID
        var universityLink = item.QuerySelector(AppConfig.EducationUniversityLinkSelector);
        if (universityLink == null)
        {
            return null; // Без ссылки на университет не можем получить ID
        }
        
        var href = universityLink.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }
        
        // Извлекаем ID университета из URL
        var idMatch = System.Text.RegularExpressions.Regex.Match(href, AppConfig.UniversityIdRegex);
        if (!idMatch.Success || !int.TryParse(idMatch.Groups[1].Value, out var universityHabrId))
        {
            return null;
        }
        
        // Извлекаем название университета
        var universityName = universityLink.TextContent?.Trim();
        if (string.IsNullOrWhiteSpace(universityName))
        {
            // Пробуем альтернативный селектор
            var nameElement = item.QuerySelector(AppConfig.EducationUniversityNameSelector);
            universityName = nameElement?.TextContent?.Trim();
        }
        
        if (string.IsNullOrWhiteSpace(universityName))
        {
            return null; // Название обязательно
        }
        
        // Извлекаем город и количество выпускников
        string? city = null;
        int? graduateCount = null;
        
        var locationElement = item.QuerySelector(AppConfig.EducationLocationSelector);
        if (locationElement != null)
        {
            var locationText = locationElement.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(locationText))
            {
                // Формат: "Волгоград • 63 выпускника"
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
        
        // Извлекаем курсы
        var courses = ExtractCourses(item);
        
        // Извлекаем описание
        string? description = null;
        var descriptionElement = item.QuerySelector(AppConfig.EducationDescriptionSelector);
        if (descriptionElement != null)
        {
            description = descriptionElement.TextContent?.Trim();
        }
        
        return new UniversityEducationData
        {
            University = new UniversityData(
                HabrId: universityHabrId,
                Name: universityName,
                City: city,
                GraduateCount: graduateCount
            ),
            Courses = courses,
            Description = description
        };
    }

    /// <summary>
    /// Извлекает список курсов из элемента образования
    /// </summary>
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
                // Пропускаем курс при ошибке
                continue;
            }
        }
        
        return courses;
    }

    /// <summary>
    /// Извлекает данные одного курса
    /// </summary>
    private static CourseData? ExtractSingleCourse(IElement courseElement)
    {
        // Извлекаем название курса
        var nameElement = courseElement.QuerySelector(AppConfig.EducationCourseNameSelector);
        var courseName = nameElement?.TextContent?.Trim();
        
        if (string.IsNullOrWhiteSpace(courseName))
        {
            return null;
        }
        
        // Извлекаем период
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
    /// Парсит строку периода курса
    /// Формат: "Сентябрь 2023 — По настоящее время (2 года и 3 месяца)"
    /// или: "Сентябрь 2019 — Июль 2023 (3 года и 10 месяцев)"
    /// </summary>
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
        
        // Проверяем на "По настоящее время"
        if (periodText.Contains(AppConfig.CurrentPeriodText, StringComparison.OrdinalIgnoreCase))
        {
            isCurrent = true;
        }
        
        // Парсим с помощью regex
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
    /// Извлекает ID университета из URL
    /// </summary>
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
    /// Секция "Дополнительное образование" (курсы, тренинги)
    /// </summary>
    /// <param name="doc">Документ для парсинга</param>
    /// <returns>Список данных о дополнительном образовании или пустой список если секция не найдена</returns>
    public static List<AdditionalEducationData> ExtractAdditionalEducationData(IDocument doc)
    {
        var result = new List<AdditionalEducationData>();
        
        try
        {
            // Ищем секцию "Дополнительное образование"
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
                return result; // Секция не найдена - возвращаем пустой список
            }
            
            // Ищем контейнер с элементами образования
            var container = additionalEducationSection.QuerySelector(AppConfig.AdditionalEducationContainerSelector);
            if (container == null)
            {
                // Пробуем искать элементы напрямую в секции
                container = additionalEducationSection;
            }
            
            // Ищем элементы образования
            var educationItems = container.QuerySelectorAll(AppConfig.AdditionalEducationItemSelector);
            
            foreach (var item in educationItems)
            {
                try
                {
                    var educationData = ExtractSingleAdditionalEducationItem(item);
                    if (educationData != null)
                    {
                        result.Add(educationData);
                    }
                }
                catch
                {
                    // Пропускаем элемент при ошибке парсинга
                    continue;
                }
            }
        }
        catch
        {
            // При любой ошибке возвращаем пустой список
        }
        
        return result;
    }

    /// <summary>
    /// Извлекает данные из одного элемента дополнительного образования
    /// </summary>
    private static AdditionalEducationData? ExtractSingleAdditionalEducationItem(IElement item)
    {
        // Извлекаем название организации/платформы
        var titleElement = item.QuerySelector(AppConfig.AdditionalEducationTitleSelector);
        var title = titleElement?.TextContent?.Trim();
        
        if (string.IsNullOrWhiteSpace(title))
        {
            return null; // Название обязательно
        }
        
        // Извлекаем название курса
        string? course = null;
        var courseElement = item.QuerySelector(AppConfig.AdditionalEducationCourseSelector);
        if (courseElement != null)
        {
            course = courseElement.TextContent?.Trim();
        }
        
        // Извлекаем период обучения
        string? duration = null;
        var durationElement = item.QuerySelector(AppConfig.AdditionalEducationDurationSelector);
        if (durationElement != null)
        {
            duration = durationElement.TextContent?.Trim();
        }
        
        return new AdditionalEducationData
        {
            Title = title,
            Course = course,
            Duration = duration
        };
    }
}
