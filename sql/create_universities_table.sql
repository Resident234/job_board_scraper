-- Создание таблицы для хранения справочника университетов
-- Эта таблица хранит информацию об университетах с Habr Career

CREATE TABLE IF NOT EXISTS habr_universities (
    id SERIAL PRIMARY KEY,
    habr_id INTEGER NOT NULL UNIQUE,
    name TEXT NOT NULL,
    city TEXT,
    graduate_count INTEGER,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Создание индексов для оптимизации запросов
CREATE INDEX IF NOT EXISTS idx_habr_universities_habr_id ON habr_universities(habr_id);
CREATE INDEX IF NOT EXISTS idx_habr_universities_city ON habr_universities(city);
CREATE INDEX IF NOT EXISTS idx_habr_universities_name ON habr_universities(name);

-- Комментарии к таблице и колонкам
COMMENT ON TABLE habr_universities IS 'Справочник университетов с Habr Career';
COMMENT ON COLUMN habr_universities.id IS 'Внутренний ID записи';
COMMENT ON COLUMN habr_universities.habr_id IS 'ID университета на Habr Career (например, 6081)';
COMMENT ON COLUMN habr_universities.name IS 'Название университета (например, ВАГС)';
COMMENT ON COLUMN habr_universities.city IS 'Город расположения университета (например, Волгоград)';
COMMENT ON COLUMN habr_universities.graduate_count IS 'Количество выпускников на Habr Career';
COMMENT ON COLUMN habr_universities.created_at IS 'Дата и время создания записи';
COMMENT ON COLUMN habr_universities.updated_at IS 'Дата и время последнего обновления';
