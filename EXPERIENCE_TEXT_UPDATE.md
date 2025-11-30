# Обновление: Добавлено поле experience_text

## Что добавлено

Добавлено новое поле `experience_text` для хранения текстового описания опыта работы пользователя.

## Пример данных

```
Опыт работы (текст): 9 лет и 1 месяц
```

## Изменения

### 1. SQL миграция

**Файл:** `sql/alter_resumes_add_additional_fields.sql`

Добавлено поле:
```sql
ALTER TABLE IF EXISTS habr_resumes
ADD COLUMN IF NOT EXISTS experience_text text COLLATE pg_catalog."default";

COMMENT ON COLUMN habr_resumes.experience_text IS 'Текстовое описание опыта работы (например: "9 лет и 1 месяц")';
```

### 2. ProfileDataExtractor

**Файл:** `JobBoardScraper/Helper.Dom/ProfileDataExtractor.cs`

Обновлена сигнатура метода:
```csharp
// Было:
public static (string? age, string? registration, string? citizenship, string? remoteWork) 
    ExtractAdditionalProfileData(...)

// Стало:
public static (string? age, string? experienceText, string? registration, string? citizenship, string? remoteWork) 
    ExtractAdditionalProfileData(...)
```

Добавлено извлечение:
```csharp
// Извлекаем опыт работы (текстовое описание)
if (textContent.Contains("Опыт работы:"))
{
    var parts = textContent.Split(new[] { "Опыт работы:" }, StringSplitOptions.None);
    if (parts.Length > 1)
    {
        experienceText = parts[1].Trim();
    }
}
```

### 3. DatabaseClient

**Файл:** `JobBoardScraper/DatabaseClient.cs`

#### Обновлена сигнатура метода:
```csharp
public bool EnqueueUserResumeDetail(
    string userLink, 
    string? about, 
    List<string>? skills,
    string? age,
    string? experienceText,  // Новый параметр
    string? registration,
    string? citizenship,
    string? remoteWork)
```

#### Добавлено в AdditionalData:
```csharp
AdditionalData: new Dictionary<string, string?>
{
    { "age", age },
    { "experience_text", experienceText },  // Новое поле
    { "registration", registration },
    { "citizenship", citizenship },
    { "remote_work", remoteWork }
}
```

#### Обновлен DatabaseUpdateUserAdditionalData:
```csharp
if (additionalData.TryGetValue("experience_text", out var experienceText) && !string.IsNullOrWhiteSpace(experienceText))
{
    setClauses.Add("experience_text = @experience_text");
    cmd.Parameters.AddWithValue("@experience_text", experienceText);
}
```

### 4. UserResumeDetailScraper

**Файл:** `JobBoardScraper/WebScraper/UserResumeDetailScraper.cs`

Обновлено извлечение и сохранение:
```csharp
// Извлекаем дополнительные данные профиля
var (age, experienceText, registration, citizenship, remoteWork) = 
    Helper.Dom.ProfileDataExtractor.ExtractAdditionalProfileData(doc);

// Сохраняем информацию для публичного профиля
_db.EnqueueUserResumeDetail(userLink, about, skills, age, experienceText, registration, citizenship, remoteWork);
```

Добавлен вывод в лог:
```csharp
_logger.WriteLine($"  Опыт работы (текст): {experienceText ?? "(не найдено)"}");
```

## Применение изменений

### 1. Выполнить миграцию БД

```bash
psql -U postgres -d habr_career -f sql/alter_resumes_add_additional_fields.sql
```

### 2. Пересобрать проект

```bash
dotnet build
```

### 3. Запустить скрапер

```bash
dotnet run
```

## Примеры SQL запросов

### Просмотр данных

```sql
SELECT 
    link,
    title,
    age,
    experience_text,
    registration,
    citizenship
FROM habr_resumes
WHERE experience_text IS NOT NULL
LIMIT 10;
```

### Статистика по опыту

```sql
SELECT 
    experience_text,
    COUNT(*) as count
FROM habr_resumes
WHERE experience_text IS NOT NULL
  AND public = true
GROUP BY experience_text
ORDER BY count DESC;
```

### Поиск по опыту

```sql
SELECT * FROM habr_resumes
WHERE experience_text LIKE '%9 лет%'
  AND public = true;
```

## Пример вывода

```
Пользователь https://career.habr.com/username:
  О себе: Опытный разработчик...
  Навыки: 15 шт.
  Опыт работы: 3 записей
  Возраст: 37 лет
  Опыт работы (текст): 9 лет и 1 месяц
  Регистрация: 30.08.2022
  Гражданство: Россия
  Удаленная работа: готов к удаленной работе
  Статус: публичный профиль
```

## Статус

✅ SQL миграция обновлена  
✅ Код обновлен  
✅ Компиляция успешна  
✅ Документация обновлена  
⏳ Требуется выполнить миграцию БД  
⏳ Требуется тестирование  

## Обратная совместимость

Полностью обратно совместимо. Старый код продолжит работать, новое поле опционально.
