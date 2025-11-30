-- Добавление дополнительных полей в таблицу habr_resumes
-- Возраст, регистрация, гражданство, готовность к удаленной работе

-- Добавляем поле для возраста
ALTER TABLE IF EXISTS habr_resumes
ADD COLUMN IF NOT EXISTS age text COLLATE pg_catalog."default";

COMMENT ON COLUMN habr_resumes.age IS 'Возраст пользователя (например: "37 лет")';

-- Добавляем поле для даты регистрации
ALTER TABLE IF EXISTS habr_resumes
ADD COLUMN IF NOT EXISTS registration text COLLATE pg_catalog."default";

COMMENT ON COLUMN habr_resumes.registration IS 'Дата регистрации на платформе (например: "30.08.2022")';

-- Добавляем поле для гражданства
ALTER TABLE IF EXISTS habr_resumes
ADD COLUMN IF NOT EXISTS citizenship text COLLATE pg_catalog."default";

COMMENT ON COLUMN habr_resumes.citizenship IS 'Гражданство пользователя (например: "Россия")';

-- Добавляем поле для готовности к удаленной работе (булевое)
ALTER TABLE IF EXISTS habr_resumes
ADD COLUMN IF NOT EXISTS remote_work boolean;

COMMENT ON COLUMN habr_resumes.remote_work IS 'Готовность к удаленной работе (true - готов, false/null - не указано)';

-- Добавляем поле для текстового описания опыта работы
ALTER TABLE IF EXISTS habr_resumes
ADD COLUMN IF NOT EXISTS experience_text text COLLATE pg_catalog."default";

COMMENT ON COLUMN habr_resumes.experience_text IS 'Текстовое описание опыта работы (например: "9 лет и 1 месяц")';

-- Создаем индекс для поиска по гражданству
CREATE INDEX IF NOT EXISTS idx_habr_resumes_citizenship
    ON habr_resumes USING btree
    (citizenship COLLATE pg_catalog."default" ASC NULLS LAST)
    TABLESPACE pg_default;

-- Создаем индекс для поиска по готовности к удаленной работе
CREATE INDEX IF NOT EXISTS idx_habr_resumes_remote_work
    ON habr_resumes USING btree
    (remote_work ASC NULLS LAST)
    TABLESPACE pg_default
    WHERE remote_work = TRUE;
