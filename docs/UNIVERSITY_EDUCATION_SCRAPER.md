# Парсинг высшего образования (University Education Scraper)

## Описание

Функциональность парсинга блока "Высшее образование" из профилей резюме на Habr Career. Извлекает информацию об университетах, курсах и периодах обучения.

## Извлекаемые данные

### Университет
- **Название** — название ВУЗа (например, "ВАГС")
- **Habr ID** — уникальный идентификатор на Habr Career (например, 6081)
- **Город** — город расположения (например, "Волгоград")
- **Количество выпускников** — число выпускников на платформе

### Курсы
- **Название курса** — факультет/специальность
- **Дата начала** — начало обучения (например, "Сентябрь 2020")
- **Дата окончания** — конец обучения или null если "По настоящее время"
- **Продолжительность** — длительность обучения (например, "4 года")
- **Текущее обучение** — флаг, если обучение продолжается

### Описание
- Дополнительная информация об образовании (специальность, средний балл и т.д.)

## Структура базы данных

### Таблица habr_universities

```sql
CREATE TABLE IF NOT EXISTS habr_universities (
    id SERIAL PRIMARY KEY,
    habr_id INTEGER NOT NULL UNIQUE,
    name TEXT NOT NULL,
    city TEXT,
    graduate_count INTEGER,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_habr_universities_habr_id ON habr_universities(habr_id);
CREATE INDEX IF NOT EXISTS idx_habr_universities_city ON habr_universities(city);
CREATE INDEX IF NOT EXISTS idx_habr_universities_name ON habr_universities(name);
```

### Таблица habr_resumes_universities

```sql
CREATE TABLE IF NOT EXISTS habr_resumes_universities (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES habr_resumes(id) ON DELETE CASCADE,
    university_id INTEGER NOT NULL REFERENCES habr_universities(id) ON DELETE CASCADE,
    courses JSONB,
    description TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(user_id, university_id)
);

CREATE INDEX IF NOT EXISTS idx_habr_resumes_universities_user_id ON habr_resumes_universities(user_id);
CREATE INDEX IF NOT EXISTS idx_habr_resumes_universities_university_id ON habr_resumes_universities(university_id);
CREATE INDEX IF NOT EXISTS idx_habr_resumes_universities_courses ON habr_resumes_universities USING GIN (courses);
```

### Формат JSON для курсов

```json
[
    {
        "name": "Государственного и муниципального управления",
        "start_date": "Сентябрь 2023",
        "end_date": null,
        "duration": "2 года и 3 месяца",
        "is_current": true
    },
    {
        "name": "Информатика и вычислительная техника",
        "start_date": "Сентябрь 2019",
        "end_date": "Июль 2023",
        "duration": "3 года и 10 месяцев",
        "is_current": false
    }
]
```

## Настройка

### App.config

```xml
<!-- Education Section Settings -->
<add key="Education:SectionTitleText" value="Высшее образование" />
<add key="Education:SectionSelector" value=".content-section" />
<add key="Education:SectionTitleSelector" value=".content-section__title" />
<add key="Education:ItemSelector" value=".resume-education-item" />
<add key="Education:UniversityLinkSelector" value="a[href*='/universities/']" />
<add key="Education:UniversityNameSelector" value=".resume-education-item__title" />
<add key="Education:LocationSelector" value=".resume-education-item__location" />
<add key="Education:CoursesContainerSelector" value=".resume-education-item__courses" />
<add key="Education:CourseSelector" value=".education-course" />
<add key="Education:CourseNameSelector" value=".education-course__title span" />
<add key="Education:CoursePeriodSelector" value=".education-course__duration" />
<add key="Education:DescriptionSelector" value=".resume-education-item__description" />
<add key="Education:UniversityIdRegex" value="/universities/(\d+)" />
<add key="Education:GraduateCountRegex" value="(\d+)\s*выпускник" />
<add key="Education:CoursePeriodRegex" value="^(.+?)\s*—\s*(.+?)(?:\s*\((.+?)\))?$" />
<add key="Education:CurrentPeriodText" value="По настоящее время" />
```

## Модели данных

### UniversityData

```csharp
public readonly record struct UniversityData(
    int HabrId,
    string Name,
    string? City = null,
    int? GraduateCount = null
);
```

### CourseData

```csharp
public class CourseData
{
    public string Name { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Duration { get; set; }
    public bool IsCurrent { get; set; }
}
```

### UserUniversityData

```csharp
public class UserUniversityData
{
    public string UserLink { get; set; }
    public int UniversityHabrId { get; set; }
    public List<CourseData> Courses { get; set; }
    public string? Description { get; set; }
}
```

## Использование

### Миграция базы данных

```bash
psql -U postgres -d jobs -f sql/create_universities_table.sql
psql -U postgres -d jobs -f sql/create_resumes_universities_table.sql
```

### Автоматическое извлечение

Парсинг образования выполняется автоматически при обходе профилей пользователей в `UserResumeDetailScraper`. Данные сохраняются через очереди:

```csharp
// Извлечение данных
var educationData = ProfileDataExtractor.ExtractEducationData(doc);

// Сохранение
foreach (var education in educationData)
{
    _db.EnqueueUniversity(education.University);
    _db.EnqueueUserUniversity(new UserUniversityData
    {
        UserLink = userLink,
        UniversityHabrId = education.University.HabrId,
        Courses = education.Courses,
        Description = education.Description
    });
}
```

## Примеры SQL-запросов

### Получить образование пользователя

```sql
SELECT 
    r.link,
    r.title as user_name,
    u.name as university_name,
    u.city,
    ru.courses,
    ru.description
FROM habr_resumes_universities ru
JOIN habr_resumes r ON ru.user_id = r.id
JOIN habr_universities u ON ru.university_id = u.id
WHERE r.link = 'https://career.habr.com/username';
```

### Топ университетов по количеству пользователей

```sql
SELECT 
    u.name,
    u.city,
    u.graduate_count,
    COUNT(ru.user_id) as users_in_db
FROM habr_universities u
LEFT JOIN habr_resumes_universities ru ON u.id = ru.university_id
GROUP BY u.id, u.name, u.city, u.graduate_count
ORDER BY users_in_db DESC
LIMIT 20;
```

### Найти пользователей из конкретного университета

```sql
SELECT 
    r.link,
    r.title as user_name,
    ru.courses,
    ru.description
FROM habr_resumes r
JOIN habr_resumes_universities ru ON r.id = ru.user_id
JOIN habr_universities u ON ru.university_id = u.id
WHERE u.name ILIKE '%МГУ%'
ORDER BY r.title;
```

### Университеты по городам

```sql
SELECT 
    city,
    COUNT(*) as universities_count,
    SUM(graduate_count) as total_graduates
FROM habr_universities
WHERE city IS NOT NULL
GROUP BY city
ORDER BY universities_count DESC;
```

### Пользователи с текущим обучением

```sql
SELECT 
    r.link,
    r.title as user_name,
    u.name as university_name,
    course->>'name' as course_name,
    course->>'start_date' as start_date
FROM habr_resumes_universities ru
JOIN habr_resumes r ON ru.user_id = r.id
JOIN habr_universities u ON ru.university_id = u.id,
LATERAL jsonb_array_elements(ru.courses) as course
WHERE (course->>'is_current')::boolean = true;
```

### Статистика по курсам

```sql
SELECT 
    course->>'name' as course_name,
    COUNT(*) as students_count
FROM habr_resumes_universities ru,
LATERAL jsonb_array_elements(ru.courses) as course
GROUP BY course->>'name'
ORDER BY students_count DESC
LIMIT 20;
```

## Тестирование

### Property-Based Tests (FsCheck)

Реализованы следующие property-based тесты:

1. **Property 1: Education Section Detection** — детекция секции образования
2. **Property 3: University ID Parsing from URL** — парсинг ID из URL
3. **Property 6: Course JSON Round-Trip** — сериализация/десериализация курсов
4. **Property 7: Course Period Parsing** — парсинг периода обучения
5. **Property 9: Null Safety for Optional Fields** — обработка опциональных полей

### Запуск тестов

```bash
dotnet test JobBoardScraper.Tests --filter "FullyQualifiedName~Education|FullyQualifiedName~CourseData"
```

## Обработка ошибок

- **Секция не найдена** — возвращается пустой список, обработка продолжается
- **Невалидный ID университета** — запись пропускается с логированием
- **Ошибка парсинга количества выпускников** — поле остаётся null
- **Ошибка сериализации курсов** — сохраняется пустой массив []

## Пример вывода

```
Пользователь https://career.habr.com/username:
  Имя: Иван Иванов
  ...
  Образование: 2 записей
[DB] Университет МГУ (ID=1234): сохранён
[DB] Связь пользователь-университет: user_id=567, university_id=1234, courses=3
```

## См. также

- [USER_RESUME_DETAIL_SCRAPER.md](USER_RESUME_DETAIL_SCRAPER.md) — Основной скрапер резюме
- [USER_ADDITIONAL_DATA_EXTRACTION.md](USER_ADDITIONAL_DATA_EXTRACTION.md) — Извлечение дополнительных данных
