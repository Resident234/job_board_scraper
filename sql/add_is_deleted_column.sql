-- Добавление столбца is_deleted в таблицу habr_companies
ALTER TABLE habr_companies
ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN DEFAULT FALSE;

-- Комментарий к столбцу
COMMENT ON COLUMN habr_companies.is_deleted IS 'Флаг мягкого удаления (TRUE - запись удалена, FALSE - активная запись)';
