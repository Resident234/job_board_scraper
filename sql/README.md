# SQL Scripts

Скрипты для инициализации и обслуживания базы данных PostgreSQL.

## Файлы

### create_companies_table.sql
Создание таблицы `habr_companies` для хранения кодов компаний с career.habr.com.

**Структура:**
- `id` - первичный ключ
- `company_code` - уникальный код компании (например, "standartpark")
- `company_url` - полный URL компании
- `created_at` - дата создания записи
- `updated_at` - дата последнего обновления

**Использование:**
```bash
psql -U postgres -d jobs -f sql/create_companies_table.sql
```

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
