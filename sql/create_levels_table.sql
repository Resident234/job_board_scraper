-- Таблица уровней специалистов
CREATE TABLE IF NOT EXISTS habr_levels (
    id SERIAL PRIMARY KEY,
    title TEXT NOT NULL UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Комментарии к столбцам
COMMENT ON COLUMN habr_levels.title IS 'Название уровня (например, "Старший (Senior)", "Средний (Middle)")';

-- Индекс для быстрого поиска по названию
CREATE INDEX IF NOT EXISTS idx_habr_levels_title ON habr_levels(title);
