-- Добавление колонки about в таблицу habr_resumes
-- Эта колонка будет хранить информацию "О себе" из резюме пользователя

ALTER TABLE habr_resumes 
ADD COLUMN IF NOT EXISTS about TEXT;

-- Комментарий к колонке
COMMENT ON COLUMN habr_resumes.about IS 'Информация "О себе" из резюме пользователя';
