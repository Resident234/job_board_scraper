# Рефакторинг извлечения данных профиля

## Обзор

Код извлечения имени, должностей, уровня, зарплаты и статуса поиска работы был перенесен из `ResumeListPageScraper` в переиспользуемый класс `Helper.Dom.ProfileDataExtractor`.

## Изменения

### 1. Добавлены новые методы в ProfileDataExtractor

#### ExtractNameInfoTechAndLevel
Извлекает имя, должности и уровень из секции профиля в списке резюме.
Парсит текст вида "Должность 1 • Должность 2 • Уровень".

```csharp
public static (string? name, string? infoTech, string? levelTitle) ExtractNameInfoTechAndLevel(
    IElement section,
    string profileLinkSelector = "a[href^='/']",
    string separatorSelector = "span.bullet")
```

**Пример использования:**
```csharp
var (name, infoTech, levelTitle) = Helper.Dom.ProfileDataExtractor.ExtractNameInfoTechAndLevel(
    section, 
    AppConfig.ResumeListProfileLinkSelector, 
    AppConfig.ResumeListSeparatorSelector);
```

#### ExtractSalaryFromSection
Извлекает зарплату из секции профиля в списке резюме.
Парсит текст вида "От 80 000 ₽".

```csharp
public static int? ExtractSalaryFromSection(
    IElement section,
    string? salaryRegex = null)
```

**Пример использования:**
```csharp
var salary = Helper.Dom.ProfileDataExtractor.ExtractSalaryFromSection(
    section, 
    AppConfig.ResumeListSalaryRegex);
```

#### ExtractJobSearchStatusFromSection
Извлекает статус поиска работы из секции профиля в списке резюме.
Парсит текст вида "Ищу работу", "Не ищу работу", "Рассматриваю предложения".

```csharp
public static string? ExtractJobSearchStatusFromSection(IElement section)
```

**Пример использования:**
```csharp
var jobSearchStatus = Helper.Dom.ProfileDataExtractor.ExtractJobSearchStatusFromSection(section);
```

### 2. Обновлен ResumeListPageScraper

Код извлечения данных был заменен на вызовы методов из `ProfileDataExtractor`:

**Было:**
```csharp
// Извлечение имени
var name = profileLink.TextContent?.Trim();

// Извлечение должностей и уровня
var parts = allText.Split('•', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
if (parts.Length > 0)
{
    levelTitle = parts[^1].Trim();
    if (parts.Length > 1)
    {
        infoTech = string.Join(" • ", parts[..^1]);
    }
}

// Извлечение зарплаты
var salaryMatch = System.Text.RegularExpressions.Regex.Match(text, AppConfig.ResumeListSalaryRegex);
if (salaryMatch.Success)
{
    var salaryStr = salaryMatch.Groups[1].Value.Replace(" ", "");
    if (int.TryParse(salaryStr, out var salaryValue))
    {
        salary = salaryValue;
    }
}
```

**Стало:**
```csharp
// Извлекаем имя, должности и уровень используя Helper.Dom.ProfileDataExtractor
var (name, infoTech, levelTitle) = Helper.Dom.ProfileDataExtractor.ExtractNameInfoTechAndLevel(
    section, 
    AppConfig.ResumeListProfileLinkSelector, 
    AppConfig.ResumeListSeparatorSelector);

// Извлекаем зарплату используя Helper.Dom.ProfileDataExtractor
var salary = Helper.Dom.ProfileDataExtractor.ExtractSalaryFromSection(
    section, 
    AppConfig.ResumeListSalaryRegex);
```

### 3. UserResumeDetailScraper уже использует ProfileDataExtractor

`UserResumeDetailScraper` уже использует методы из `ProfileDataExtractor`:
- `ExtractUserName` - для извлечения имени
- `ExtractInfoTechAndLevel` - для извлечения технической информации и уровня
- `ExtractSalaryAndJobStatus` - для извлечения зарплаты и статуса поиска работы

## Преимущества

1. **Переиспользование кода**: Логика извлечения данных теперь находится в одном месте
2. **Упрощение поддержки**: Изменения в логике парсинга нужно делать только в одном месте
3. **Консистентность**: Оба скрапера используют одинаковую логику извлечения данных
4. **Тестируемость**: Методы извлечения можно тестировать независимо от скраперов

## Структура ProfileDataExtractor

Класс `Helper.Dom.ProfileDataExtractor` теперь содержит следующие методы:

### Для детальных страниц профиля:
- `ExtractWorkExperienceAndLastVisit` - опыт работы и последний визит
- `ExtractTextAfterPrefix` - текст после префикса
- `ExtractAdditionalProfileData` - дополнительные данные (возраст, гражданство и т.д.)
- `ExtractUserName` - имя пользователя
- `ExtractInfoTechAndLevel` - техническая информация и уровень
- `ExtractSalaryAndJobStatus` - зарплата и статус поиска работы

### Для списков резюме:
- `ExtractNameInfoTechAndLevel` - имя, должности и уровень
- `ExtractSalaryFromSection` - зарплата
- `ExtractJobSearchStatusFromSection` - статус поиска работы

## Проверка

Код успешно компилируется без ошибок:
```
dotnet build
Сборка успешно выполнено с предупреждениями (6) через 1,4 с
```
