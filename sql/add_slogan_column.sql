-- Добавление столбца slogan в таблицу habr_resumes
ALTER TABLE habr_resumes 
ADD COLUMN IF NOT EXISTS slogan TEXT;

-- Добавление комментария к столбцу
COMMENT ON COLUMN habr_resumes.slogan IS 'Слоган/специализация пользователя';
