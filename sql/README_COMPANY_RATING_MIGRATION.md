# Миграция базы данных для CompanyRatingScraper

## Описание

Эта миграция добавляет поддержку хранения данных о рейтингах компаний и отзывах.

## Порядок выполнения

### 1. Добавление новых полей в таблицу habr_companies

Выполните скрипт:
```bash
psql -U postgres -d jobs -f sql/alter_companies_add_rating_fields.sql
```

Этот скрипт добавит следующие поля:
- `city` (TEXT) - город компании
- `awards` (TEXT[]) - массив наград компании
- `scores` (DECIMAL(4,2)) - средняя оценка компании

### 2. Создание таблицы company_reviews

Выполните скрипт:
```bash
psql -U postgres -d jobs -f sql/create_company_reviews_table.sql
```

Этот скрипт создаст таблицу `company_reviews` со следующими полями:
- `id` (BIGINT) - первичный ключ
- `company_id` (INTEGER) - внешний ключ к habr_companies.id
- `review_hash` (TEXT) - уникальный SHA256 хеш текста отзыва
- `review_text` (TEXT) - текст отзыва
- `created_at` (TIMESTAMP) - дата создания
- `updated_at` (TIMESTAMP) - дата обновления

### 3. Проверка миграции

Проверьте, что новые поля добавлены:
```sql
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'habr_companies' 
AND column_name IN ('city', 'awards', 'scores');
```

Проверьте, что таблица создана:
```sql
SELECT table_name 
FROM information_schema.tables 
WHERE table_name = 'company_reviews';
```

## Откат миграции

Если необходимо откатить изменения:

```sql
-- Удалить таблицу отзывов
DROP TABLE IF EXISTS company_reviews;

-- Удалить новые поля из habr_companies
ALTER TABLE habr_companies 
DROP COLUMN IF EXISTS city,
DROP COLUMN IF EXISTS awards,
DROP COLUMN IF EXISTS scores;

-- Удалить индексы
DROP INDEX IF EXISTS idx_habr_companies_city;
DROP INDEX IF EXISTS idx_habr_companies_scores;
```

## Примечания

- Миграция безопасна и не влияет на существующие данные
- Новые поля допускают NULL значения
- Таблица company_reviews имеет каскадное удаление при удалении компании
- Уникальный индекс на review_hash предотвращает дубликаты отзывов
