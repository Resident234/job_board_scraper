-- Изменение типа столбца title в таблице habr_skills с VARCHAR(255) на TEXT
-- Это необходимо для поддержки длинных названий навыков

ALTER TABLE habr_skills 
ALTER COLUMN title TYPE TEXT;

-- Комментарий
COMMENT ON COLUMN habr_skills.title IS 'Название навыка (без ограничения длины)';
