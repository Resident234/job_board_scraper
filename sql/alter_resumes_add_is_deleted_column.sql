-- Добавление столбца is_deleted в таблицу habr_resumes
ALTER TABLE habr_resumes
ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN DEFAULT FALSE;

-- Комментарий к столбцу
COMMENT ON COLUMN habr_resumes.is_deleted IS 'Флаг мягкого удаления (TRUE - запись удалена, FALSE - активная запись)';
