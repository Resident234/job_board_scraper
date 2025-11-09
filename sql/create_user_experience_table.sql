-- Создание таблицы для хранения опыта работы пользователей
-- Эта таблица связывает пользователей с компаниями и хранит информацию о должностях

CREATE TABLE IF NOT EXISTS habr_user_experience (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES habr_resumes(id) ON DELETE CASCADE,
    company_id INTEGER REFERENCES habr_companies(id) ON DELETE SET NULL,
    position TEXT,
    duration TEXT,
    description TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Создание индексов для оптимизации запросов
CREATE INDEX IF NOT EXISTS idx_habr_user_experience_user_id ON habr_user_experience(user_id);
CREATE INDEX IF NOT EXISTS idx_habr_user_experience_company_id ON habr_user_experience(company_id);

-- Комментарии к таблице и колонкам
COMMENT ON TABLE habr_user_experience IS 'Опыт работы пользователей';
COMMENT ON COLUMN habr_user_experience.user_id IS 'ID пользователя из таблицы habr_resumes';
COMMENT ON COLUMN habr_user_experience.company_id IS 'ID компании из таблицы habr_companies';
COMMENT ON COLUMN habr_user_experience.position IS 'Должность';
COMMENT ON COLUMN habr_user_experience.duration IS 'Продолжительность работы';
COMMENT ON COLUMN habr_user_experience.description IS 'Описание работы';
COMMENT ON COLUMN habr_user_experience.created_at IS 'Дата и время создания записи';
COMMENT ON COLUMN habr_user_experience.updated_at IS 'Дата и время последнего обновления';
