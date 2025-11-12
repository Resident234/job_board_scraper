-- Таблица уровней специалистов
CREATE TABLE IF NOT EXISTS habr_levels (
    id SERIAL PRIMARY KEY,
    title TEXT NOT NULL UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Комментарии к столбцам
COMMENT ON COLUMN habr_levels.title IS 'Название уровня (без ограничения длины, например, "Старший (Senior)", "Средний (Middle)")';

-- Индекс для быстрого поиска по названию
CREATE INDEX IF NOT EXISTS idx_habr_levels_title ON habr_levels(title);

-- Миграция: изменение типа столбца title с VARCHAR(255) на TEXT (для существующих таблиц)
-- Если таблица уже существовала с VARCHAR(255), эта команда обновит тип
ALTER TABLE habr_levels 
ALTER COLUMN title TYPE TEXT;
