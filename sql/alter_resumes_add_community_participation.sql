-- Добавление поля для хранения участия в профсообществах
-- Поле community_participation хранит массив JSON объектов

ALTER TABLE habr_resumes 
ADD COLUMN IF NOT EXISTS community_participation JSONB;

-- Создание индекса для поиска по полю
CREATE INDEX IF NOT EXISTS idx_habr_resumes_community_participation 
ON habr_resumes USING GIN (community_participation);

-- Комментарий к полю
COMMENT ON COLUMN habr_resumes.community_participation IS 'Участие в профсообществах (Хабр, GitHub и др.) в формате JSON массива: [{"name": "Хабр", "member_since": "c мая 2009 (16 лет и 7 месяцев)", "contribution": "2 публикации, 93 комментария", "topics": "Управление e-commerce • Компьютерное железо"}]';

-- Пример данных:
-- [
--   {
--     "name": "Хабр",
--     "member_since": "c мая 2009 (16 лет и 7 месяцев)",
--     "contribution": "2 публикации, 93 комментария, пишет в хабы:",
--     "topics": "Управление e-commerce • Компьютерное железо"
--   },
--   {
--     "name": "GitHub",
--     "member_since": "c мая 2012 (13 лет и 7 месяцев)",
--     "contribution": "4229 вкладов в 7 репозиториев, связан с языками:",
--     "topics": "PHP • CSS"
--   }
-- ]
