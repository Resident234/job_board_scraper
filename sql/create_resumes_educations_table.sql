-- Создание таблицы для хранения дополнительного образования пользователей
-- Секция "Дополнительное образование" в профиле резюме

CREATE TABLE IF NOT EXISTS habr_resumes_educations (
    id SERIAL PRIMARY KEY,
    resume_id INTEGER NOT NULL REFERENCES habr_resumes(id) ON DELETE CASCADE,
    title TEXT NOT NULL,
    course TEXT,
    duration TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Создание индексов для оптимизации запросов
CREATE INDEX IF NOT EXISTS idx_habr_resumes_educations_resume_id ON habr_resumes_educations(resume_id);
CREATE INDEX IF NOT EXISTS idx_habr_resumes_educations_title ON habr_resumes_educations(title);

-- Комментарии к таблице и колонкам
COMMENT ON TABLE habr_resumes_educations IS 'Дополнительное образование пользователей (курсы, тренинги)';
COMMENT ON COLUMN habr_resumes_educations.id IS 'Внутренний ID записи';
COMMENT ON COLUMN habr_resumes_educations.resume_id IS 'ID пользователя из таблицы habr_resumes';
COMMENT ON COLUMN habr_resumes_educations.title IS 'Название организации/платформы (например, index-tech)';
COMMENT ON COLUMN habr_resumes_educations.course IS 'Название курса (например, Рекрутмент)';
COMMENT ON COLUMN habr_resumes_educations.duration IS 'Период обучения (например, Март 2024 — Март 2024 (1 месяц))';
COMMENT ON COLUMN habr_resumes_educations.created_at IS 'Дата и время создания записи';
COMMENT ON COLUMN habr_resumes_educations.updated_at IS 'Дата и время последнего обновления';
