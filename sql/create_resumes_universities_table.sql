-- Создание таблицы для связи резюме и университетов
-- Эта таблица связывает пользователей (habr_resumes) с университетами (habr_universities)
-- и хранит информацию о пройденных курсах

CREATE TABLE IF NOT EXISTS habr_resumes_universities (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES habr_resumes(id) ON DELETE CASCADE,
    university_id INTEGER NOT NULL REFERENCES habr_universities(id) ON DELETE CASCADE,
    courses JSONB,
    description TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(user_id, university_id)
);

-- Создание индексов для оптимизации запросов
CREATE INDEX IF NOT EXISTS idx_habr_resumes_universities_user_id ON habr_resumes_universities(user_id);
CREATE INDEX IF NOT EXISTS idx_habr_resumes_universities_university_id ON habr_resumes_universities(university_id);
CREATE INDEX IF NOT EXISTS idx_habr_resumes_universities_courses ON habr_resumes_universities USING GIN (courses);

-- Комментарии к таблице и колонкам
COMMENT ON TABLE habr_resumes_universities IS 'Связь между резюме пользователей и университетами';
COMMENT ON COLUMN habr_resumes_universities.id IS 'Внутренний ID записи';
COMMENT ON COLUMN habr_resumes_universities.user_id IS 'ID пользователя из таблицы habr_resumes';
COMMENT ON COLUMN habr_resumes_universities.university_id IS 'ID университета из таблицы habr_universities';
COMMENT ON COLUMN habr_resumes_universities.courses IS 'JSON массив курсов: [{"name": "...", "start_date": "...", "end_date": "...", "duration": "...", "is_current": true/false}]';
COMMENT ON COLUMN habr_resumes_universities.description IS 'Описание образования (специальность, средний балл и т.д.)';
COMMENT ON COLUMN habr_resumes_universities.created_at IS 'Дата и время создания записи';
COMMENT ON COLUMN habr_resumes_universities.updated_at IS 'Дата и время последнего обновления';
