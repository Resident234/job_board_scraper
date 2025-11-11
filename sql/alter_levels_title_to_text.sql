-- Изменение типа столбца title в таблице habr_levels с VARCHAR(255) на TEXT

ALTER TABLE habr_levels 
ALTER COLUMN title TYPE TEXT;

-- Комментарий
COMMENT ON COLUMN habr_levels.title IS 'Название уровня (без ограничения длины)';
