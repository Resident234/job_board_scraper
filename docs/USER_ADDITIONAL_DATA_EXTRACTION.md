# Извлечение дополнительных данных профиля пользователя

## Обзор

Добавлена функциональность извлечения дополнительных данных из профилей пользователей на Habr Career:
- Возраст
- Дата регистрации
- Гражданство
- Готовность к удаленной работе

## Изменения в базе данных

### Новые поля в таблице `habr_resumes`

```sql
-- Возраст пользователя (например: "37 лет")
age text

-- Текстовое описание опыта работы (например: "9 лет и 1 месяц")
experience_text text

-- Дата регистрации на платформе (например: "30.08.2022")
registration text

-- Гражданство пользователя (например: "Россия")
citizenship text

-- Готовность к удаленной работе (например: "готов к удаленной работе")
remote_work text
```

### Миграция

Для добавления новых полей выполните SQL скрипт:

```bash
psql -U postgres -d your_database -f sql/alter_resumes_add_additional_fields.sql
```

## Изменения в коде

### 1. Helper.Dom.ProfileDataExtractor

Добавлен новый метод для извлечения дополнительных данных:

```csharp
public static (string? age, string? experienceText, string? registration, string? citizenship, string? remoteWork) 
    ExtractAdditionalProfileData(IDocument doc, string basicSectionSelector = ".basic-section")
```

**Параметры:**
- `doc` - документ AngleSharp для парсинга
- `basicSectionSelector` - CSS селектор для секций с данными (по умолчанию `.basic-section`)

**Возвращает:**
- `age` - возраст пользователя
- `experienceText` - текстовое описание опыта работы
- `registration` - дата регистрации
- `citizenship` - гражданство
- `remoteWork` - готовность к удаленной работе

### 2. DatabaseClient

#### Новый тип записи

```csharp
public enum DbRecordType
{
    // ...
    UserAdditionalData  // Новый тип для дополнительных данных
}
```

#### Обновленная структура DbRecord

```csharp
public readonly record struct DbRecord(
    // ...
    Dictionary<string, string?>? AdditionalData = null  // Новое поле
);
```

#### Новые методы

**EnqueueUserResumeDetail (перегрузка)**

```csharp
public bool EnqueueUserResumeDetail(
    string userLink, 
    string? about, 
    List<string>? skills,
    string? age,
    string? experienceText,
    string? registration,
    string? citizenship,
    string? remoteWork)
```

Добавляет детальную информацию о резюме пользователя в очередь, включая дополнительные данные.

**DatabaseUpdateUserAdditionalData**

```csharp
public void DatabaseUpdateUserAdditionalData(
    NpgsqlConnection conn, 
    string userLink, 
    Dictionary<string, string?> additionalData)
```

Обновляет дополнительные данные профиля пользователя в базе данных.

### 3. UserResumeDetailScraper

Обновлен для извлечения и сохранения дополнительных данных:

```csharp
// Извлекаем дополнительные данные профиля
var (age, experienceText, registration, citizenship, remoteWork) = 
    Helper.Dom.ProfileDataExtractor.ExtractAdditionalProfileData(doc);

// Сохраняем информацию для публичного профиля
_db.EnqueueUserResumeDetail(userLink, about, skills, age, experienceText, registration, citizenship, remoteWork);
```

### 4. UserProfileScraper

Рефакторинг для использования метода из `ProfileDataExtractor`:

```csharp
// Извлекаем опыт работы и последний визит
var (workExperience, lastVisit) = Helper.Dom.ProfileDataExtractor.ExtractWorkExperienceAndLastVisit(
    doc, 
    AppConfig.UserProfileBasicSectionSelector);
```

## Пример использования

### Запуск скрапера

```csharp
var scraper = new UserResumeDetailScraper(
    httpClient,
    db,
    getUserCodes,
    controller,
    proxyPool,
    interval: TimeSpan.FromMinutes(20),
    outputMode: OutputMode.ConsoleOnly
);

await scraper.StartAsync(cancellationToken);
```

### Вывод в лог

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

## Запросы к базе данных

### Поиск пользователей по гражданству

```sql
SELECT link, title, age, experience_text, citizenship, remote_work
FROM habr_resumes
WHERE citizenship = 'Россия'
  AND public = true
ORDER BY created_at DESC;
```

### Статистика по возрасту

```sql
SELECT 
    age,
    COUNT(*) as count
FROM habr_resumes
WHERE age IS NOT NULL
  AND public = true
GROUP BY age
ORDER BY count DESC;
```

### Статистика по опыту работы

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

### Пользователи готовые к удаленной работе

```sql
SELECT link, title, experience_text, citizenship, remote_work
FROM habr_resumes
WHERE remote_work LIKE '%удаленной работе%'
  AND public = true;
```

## Преимущества

1. **Централизация логики** - методы извлечения данных вынесены в `Helper.Dom.ProfileDataExtractor`
2. **Переиспользование кода** - один метод используется в нескольких скраперах
3. **Расширяемость** - легко добавить новые поля в будущем
4. **Типобезопасность** - использование кортежей для возврата данных
5. **Гибкость** - поддержка null значений для отсутствующих данных

## Совместимость

- Обратная совместимость сохранена через перегрузку метода `EnqueueUserResumeDetail`
- Старый код продолжит работать без изменений
- Новые поля в базе данных допускают NULL значения

## Тестирование

После внедрения рекомендуется:

1. Выполнить миграцию базы данных
2. Запустить скрапер на небольшой выборке пользователей
3. Проверить корректность извлеченных данных
4. Убедиться, что все поля заполняются правильно

## Известные ограничения

- Данные извлекаются только из публичных профилей
- Формат данных зависит от структуры HTML страницы Habr Career
- При изменении структуры сайта может потребоваться обновление селекторов
