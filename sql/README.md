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
- `company_code` - уникальный код компании (например, "standartpark")
- `company_url` - полный URL компании
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
