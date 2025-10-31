-- Таблица для хранения category_root_id из career.habr.com/companies
CREATE TABLE IF NOT EXISTS habr_category_root_ids (
    id SERIAL PRIMARY KEY,
    category_id VARCHAR(255) NOT NULL UNIQUE,
    category_name TEXT,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

-- Индекс для быстрого поиска по category_id
CREATE INDEX IF NOT EXISTS idx_category_id ON habr_category_root_ids(category_id);

-- Комментарии к таблице и колонкам
COMMENT ON TABLE habr_category_root_ids IS 'Категории компаний (category_root_id) с сайта career.habr.com';
COMMENT ON COLUMN habr_category_root_ids.category_id IS 'Значение category_root_id из select элемента';
COMMENT ON COLUMN habr_category_root_ids.category_name IS 'Название категории (текст из option)';
COMMENT ON COLUMN habr_category_root_ids.created_at IS 'Дата первого добавления записи';
COMMENT ON COLUMN habr_category_root_ids.updated_at IS 'Дата последнего обновления записи';
