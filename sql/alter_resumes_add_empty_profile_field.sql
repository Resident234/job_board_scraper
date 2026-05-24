-- Добавление поля is_empty в таблицу habr_resumes
-- Поле обозначает, что профиль пустой (не содержит данных)

ALTER TABLE IF EXISTS habr_resumes
ADD COLUMN IF NOT EXISTS is_empty boolean DEFAULT FALSE;

-- Комментарий к столбцу
COMMENT ON COLUMN habr_resumes.is_empty IS 'Флаг: является ли профиль пустым (не содержит данных)';

-- Индекс для быстрого поиска пустых профилей
CREATE INDEX IF NOT EXISTS idx_habr_resumes_is_empty
    ON habr_resumes USING btree
    (is_empty ASC NULLS LAST)
    TABLESPACE pg_default;
