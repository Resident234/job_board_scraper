# Сохранение извлеченных данных в базу данных

## Обзор

Добавлена функциональность для сохранения извлеченных данных профиля (имя, техническая информация, уровень, зарплата, статус поиска работы) в таблицу `habr_resumes`.

## Изменения

### 1. Обновлен метод EnqueueUserResumeDetail в DatabaseClient

Добавлены новые параметры для сохранения данных профиля:

```csharp
public bool EnqueueUserResumeDetail(
    string userLink, 
    string? about, 
    List<string>? skills,
    string? age,
    string? experienceText,
    string? registration,
    string? lastVisit,
    string? citizenship,
    bool? remoteWork,
    string? userName = null,           // НОВЫЙ
    string? infoTech = null,           // НОВЫЙ
    string? levelTitle = null,         // НОВЫЙ
    int? salary = null,                // НОВЫЙ
    string? jobSearchStatus = null)    // НОВЫЙ
```

#### Что сохраняется:

**Основные данные профиля** (через DbRecordType.UserProfile):
- `userName` - имя пользователя
- `infoTech` - техническая информация (должности)
- `levelTitle` - уровень специалиста
- `salary` - желаемая зарплата
- `experienceText` - опыт работы (текстовое описание)
- `lastVisit` - последний визит

**Дополнительные данные** (через DbRecordType.UserAdditionalData):
- `age` - возраст
- `registration` - дата регистрации
- `citizenship` - гражданство
- `remoteWork` - готовность к удаленной работе
- `jobSearchStatus` - статус поиска работы (НОВЫЙ)

### 2. Обновлен UserResumeDetailScraper

Теперь передает все извлеченные данные в `EnqueueUserResumeDetail`:

```csharp
// Извлекаем имя пользователя
var userName = Helper.Dom.ProfileDataExtractor.ExtractUserName(doc);

// Извлекаем техническую информацию и уровень
var (infoTech, levelTitle) = Helper.Dom.ProfileDataExtractor.ExtractInfoTechAndLevel(doc);

// Извлекаем зарплату и статус поиска работы
var (salary, jobSearchStatus) = Helper.Dom.ProfileDataExtractor.ExtractSalaryAndJobStatus(doc);

// Извлекаем дополнительные данные профиля
var (age, experienceText, registration, lastVisit, citizenship, remoteWork) = 
    Helper.Dom.ProfileDataExtractor.ExtractAdditionalProfileData(doc);

// Сохраняем все данные
_db.EnqueueUserResumeDetail(
    userLink, 
    about, 
    skills, 
    age, 
    experienceText, 
    registration, 
    lastVisit, 
    citizenship, 
    remoteWork,
    userName,           // НОВЫЙ
    infoTech,           // НОВЫЙ
    levelTitle,         // НОВЫЙ
    salary,             // НОВЫЙ
    jobSearchStatus);   // НОВЫЙ
```

## Поля в таблице habr_resumes

Теперь заполняются следующие поля:

| Поле | Источник | Описание |
|------|----------|----------|
| `title` | `userName` | Имя пользователя |
| `info_tech` | `infoTech` | Техническая информация (должности) |
| `level_id` | `levelTitle` | ID уровня специалиста (FK на habr_levels) |
| `salary` | `salary` | Желаемая зарплата в рублях |
| `work_experience` | `experienceText` | Опыт работы (текстовое описание) |
| `last_visit` | `lastVisit` | Последний визит |
| `job_search_status` | `jobSearchStatus` | Статус поиска работы |
| `about` | `about` | Информация "О себе" |

## Статус поиска работы

Статус поиска работы (`jobSearchStatus`) теперь сохраняется в отдельном поле `job_search_status` таблицы `habr_resumes`.

Возможные значения:
- "Ищу работу"
- "Не ищу работу"
- "Рассматриваю предложения"

### SQL миграция

Создан файл `sql/alter_resumes_add_job_search_status.sql` для добавления поля в таблицу:

```sql
ALTER TABLE habr_resumes
ADD COLUMN IF NOT EXISTS job_search_status text COLLATE pg_catalog."default";

COMMENT ON COLUMN habr_resumes.job_search_status IS 'Статус поиска работы: "Ищу работу", "Не ищу работу", "Рассматриваю предложения"';

CREATE INDEX IF NOT EXISTS idx_habr_resumes_job_search_status
    ON habr_resumes USING btree
    (job_search_status COLLATE pg_catalog."default" ASC NULLS LAST)
    TABLESPACE pg_default;
```

## Логирование

Обновлено логирование для отображения всех сохраняемых данных:

```
[DB Queue] UserResumeDetail: https://career.habr.com/username -> 
  UserName=Вячеслав Нечаев, 
  InfoTech=Бэкенд разработчик • Веб-разработчик, 
  Level=Младший (Junior), 
  Salary=80000, 
  JobStatus=Ищу работу, 
  About=True, 
  Skills=15, 
  Age=25 лет, 
  ExperienceText=2 года, 
  Registration=2020-01-01, 
  LastVisit=5 дней назад, 
  Citizenship=Россия, 
  RemoteWork=True
```

## Пример данных

### Входные данные (HTML):
```html
<h1 class="page-title__title">Вячеслав Нечаев</h1>
<div class="inline-list">
  <span><span>Бэкенд разработчик</span></span>
  <span><span>Веб-разработчик</span></span>
  <span><span>Младший (Junior)</span></span>
</div>
<div class="user-page-sidebar__career">
  От 80 000 ₽ • Ищу работу
</div>
```

### Сохраненные данные в БД:
```sql
INSERT INTO habr_resumes (
  link, 
  title, 
  info_tech, 
  level_id, 
  salary, 
  last_visit,
  job_search_status
) VALUES (
  'https://career.habr.com/username',
  'Вячеслав Нечаев',
  'Бэкенд разработчик • Веб-разработчик',
  (SELECT id FROM habr_levels WHERE title = 'Младший (Junior)'),
  80000,
  '5 дней назад',
  'Ищу работу'
);
```

## Проверка

Код успешно компилируется:
```
dotnet build
Сборка успешно выполнено с предупреждениями (6) через 7,5 с
```

## Связанные файлы

- `JobBoardScraper/DatabaseClient.cs` - обновлен метод `EnqueueUserResumeDetail` и `DatabaseInsert`
- `JobBoardScraper/Models/UserProfileData.cs` - добавлено поле `JobSearchStatus`
- `JobBoardScraper/WebScraper/UserResumeDetailScraper.cs` - обновлен вызов метода
- `JobBoardScraper/Helper.Dom/ProfileDataExtractor.cs` - методы извлечения данных
- `sql/create_resumes_table.sql` - структура таблицы
- `sql/alter_resumes_add_job_search_status.sql` - миграция для добавления поля `job_search_status`
