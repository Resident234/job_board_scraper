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
}
