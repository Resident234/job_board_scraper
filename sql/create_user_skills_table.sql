-- Создание таблицы для связи пользователей и навыков
-- Эта таблица связывает пользователей (habr_resumes) с навыками (habr_skills)

CREATE TABLE IF NOT EXISTS habr_user_skills (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES habr_resumes(id) ON DELETE CASCADE,
    skill_id INTEGER NOT NULL REFERENCES habr_skills(id) ON DELETE CASCADE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(user_id, skill_id)
);

-- Создание индексов для оптимизации запросов
CREATE INDEX IF NOT EXISTS idx_habr_user_skills_user_id ON habr_user_skills(user_id);
CREATE INDEX IF NOT EXISTS idx_habr_user_skills_skill_id ON habr_user_skills(skill_id);

-- Комментарии к таблице и колонкам
COMMENT ON TABLE habr_user_skills IS 'Связь между пользователями и их навыками';
COMMENT ON COLUMN habr_user_skills.user_id IS 'ID пользователя из таблицы habr_resumes';
COMMENT ON COLUMN habr_user_skills.skill_id IS 'ID навыка из таблицы habr_skills';
COMMENT ON COLUMN habr_user_skills.created_at IS 'Дата и время добавления связи';
