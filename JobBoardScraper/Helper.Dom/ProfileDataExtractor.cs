using AngleSharp.Dom;

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
                var spans = inlineList.QuerySelectorAll("span > span:first-child");
                var textParts = new List<string>();
                
                foreach (var span in spans)
                {
                    var text = span.TextContent?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        textParts.Add(text);
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
}
