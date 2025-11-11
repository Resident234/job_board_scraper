# SQL Scripts для JobBoardScraper

Скрипты для инициализации и обслуживания базы данных PostgreSQL.

## Создание таблиц

### 1. Таблица для резюме
```bash
# См. create_index.sql для индексов
```

### 2. Таблица для компаний
```bash
psql -U postgres -d jobs -f sql/create_companies_table.sql
```

**Структура:**
- `id` - первичный ключ
- `code` - уникальный код компании (например, "standartpark")
- `url` - полный URL компании
- `title` - название компании
- `company_id` - числовой ID компании из Habr
- `about` - краткое описание компании
- `description` - детальное описание компании
- `site` - ссылка на сайт компании
- `rating` - рейтинг компании (DECIMAL 3,2)
- `current_employees` - текущие сотрудники
- `past_employees` - все сотрудники
- `followers` - подписчики
- `want_work` - хотят работать
- `employees_count` - размер компании (текст, например "Более 5000 человек")
- `habr` - ведет ли компания блог на Хабре (boolean)
- `created_at` - дата создания записи
- `updated_at` - дата последнего обновления

### 3. Таблица для category_root_id
```bash
psql -U postgres -d jobs -f sql/create_category_root_ids_table.sql
```

**Структура:**
- `id` - первичный ключ
- `category_id` - уникальный идентификатор категории
- `category_name` - название категории
- `created_at` - дата создания записи
- `updated_at` - дата последнего обновления

### 4. Добавление столбца slogan
```bash
psql -U postgres -d jobs -f sql/add_slogan_column.sql
```

Добавляет столбец `slogan` в таблицу `habr_resumes` для хранения специализации/слогана пользователя.

### 5. Добавление уникального ограничения на link
```bash
psql -U postgres -d jobs -f sql/add_unique_link_constraint.sql
```

Добавляет уникальное ограничение на столбец `link` для поддержки режима `UpdateIfExists` (UPSERT).

### 6. Добавление столбцов для экспертов
```bash
psql -U postgres -d jobs -f sql/add_expert_columns.sql
```

Добавляет столбцы для хранения данных экспертов:
- `code` - код пользователя из URL профиля (например, "apstenku")
- `expert` - флаг, является ли пользователь экспертом (boolean)
- `work_experience` - стаж работы (например, "9 лет и 9 месяцев")

Также создаёт индексы для быстрого поиска по этим полям.

### 7. Добавление детальных полей для компаний
```bash
psql -U postgres -d jobs -f sql/add_company_details_columns.sql
```

Добавляет дополнительные столбцы в таблицу `habr_companies`:
- `company_id` - числовой ID компании
- `about` - краткое описание
- `description` - детальное описание
- `site` - ссылка на сайт
- `rating` - рейтинг компании
- `current_employees` - текущие сотрудники
- `past_employees` - все сотрудники
- `followers` - подписчики
- `want_work` - хотят работать
- `employees_count` - размер компании (текст)
- `habr` - ведет ли блог на Хабре

Также создаёт индексы и уникальное ограничение на `company_id`.

### 8. Таблицы для навыков компаний
```bash
psql -U postgres -d jobs -f sql/create_skills_table.sql
```

Создаёт две таблицы для хранения навыков:

**habr_skills:**
- `id` - первичный ключ
- `title` - название навыка (уникальное)
- `created_at` - дата создания

**habr_company_skills:**
- `id` - первичный ключ
- `company_id` - ID компании (FK → habr_companies.id)
- `skill_id` - ID навыка (FK → habr_skills.id)
- `created_at` - дата создания
- Уникальное ограничение на пару (company_id, skill_id)

Связь многие-ко-многим между компаниями и навыками.

## Использование CategoryScraper

```csharp
using var httpClient = new HttpClient();
var dbClient = new DatabaseClient(AppConfig.ConnectionString);
var conn = dbClient.DatabaseConnectionInit();

// Запуск фоновой задачи записи в БД
var cts = new CancellationTokenSource();
dbClient.StartWriterTask(conn, cts.Token);

// Создание и запуск скрапера
var categoryScraper = new CategoryScraper(
    httpClient,
    dbClient.EnqueueCategoryRootId,
    interval: TimeSpan.FromDays(7),
    outputMode: OutputMode.ConsoleAndFile
);

await categoryScraper.StartAsync(cts.Token);
```

## Обслуживание БД

### create_index.sql
Создание индексов для таблицы резюме.

### remove_doubles.sql
Удаление дубликатов из базы данных.

## Подключение к БД

```bash
psql -U postgres -d jobs
```

Строка подключения из AppConfig.cs:
```
Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;
```

## Примеры запросов

### Получить все категории
```sql
SELECT * FROM habr_category_root_ids ORDER BY category_name;
```

### Получить количество категорий
```sql
SELECT COUNT(*) FROM habr_category_root_ids;
```

### Найти категорию по ID
```sql
SELECT * FROM habr_category_root_ids WHERE category_id = 'your_category_id';
```

### Получить всех экспертов
```sql
SELECT * FROM habr_resumes WHERE expert = TRUE ORDER BY title;
```

### Получить экспертов с указанным стажем
```sql
SELECT title, code, work_experience, link 
FROM habr_resumes 
WHERE expert = TRUE AND work_experience IS NOT NULL
ORDER BY work_experience DESC;
```

### Найти пользователя по коду
```sql
SELECT * FROM habr_resumes WHERE code = 'apstenku';
```

### Статистика по экспертам
```sql
SELECT 
    COUNT(*) as total_experts,
    COUNT(work_experience) as with_experience,
    COUNT(slogan) as with_slogan
FROM habr_resumes 
WHERE expert = TRUE;
```

### Получить компании с детальной информацией
```sql
SELECT code, title, rating, employees_count, habr, site
FROM habr_companies 
WHERE company_id IS NOT NULL
ORDER BY rating DESC NULLS LAST;
```

### Получить навыки компании
```sql
SELECT c.code, c.title, s.title as skill
FROM habr_companies c
JOIN habr_company_skills cs ON c.id = cs.company_id
JOIN habr_skills s ON cs.skill_id = s.id
WHERE c.code = 'yandex'
ORDER BY s.title;
```

### Топ навыков по количеству компаний
```sql
SELECT s.title, COUNT(cs.company_id) as company_count
FROM habr_skills s
JOIN habr_company_skills cs ON s.id = cs.skill_id
GROUP BY s.title
ORDER BY company_count DESC
LIMIT 20;
```

### Компании с блогом на Хабре
```sql
SELECT code, title, rating, followers
FROM habr_companies
WHERE habr = TRUE
ORDER BY followers DESC NULLS LAST;
```

### Статистика по компаниям
```sql
SELECT 
    COUNT(*) as total_companies,
    COUNT(company_id) as with_details,
    COUNT(rating) as with_rating,
    COUNT(habr) FILTER (WHERE habr = TRUE) as with_habr_blog,
    AVG(rating) as avg_rating
FROM habr_companies;
```


## Миграции для исправления ограничений длины

### Изменение типа title в habr_skills и habr_levels
Если вы столкнулись с ошибкой "value too long for type character varying(255)", примените следующие миграции:

```bash
# Изменить тип title в habr_skills с VARCHAR(255) на TEXT
psql -U postgres -d jobs -f sql/alter_skills_title_to_text.sql

# Изменить тип title в habr_levels с VARCHAR(255) на TEXT
psql -U postgres -d jobs -f sql/alter_levels_title_to_text.sql
```

Эти миграции убирают ограничение на длину названий навыков и уровней, что позволяет хранить длинные строки без обрезки.
