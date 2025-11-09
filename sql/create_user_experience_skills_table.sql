-- Создание таблицы для связи опыта работы и навыков (многие-ко-многим)
-- Эта таблица связывает записи опыта работы с навыками

CREATE TABLE IF NOT EXISTS habr_user_experience_skills (
    id SERIAL PRIMARY KEY,
    experience_id INTEGER NOT NULL REFERENCES habr_user_experience(id) ON DELETE CASCADE,
    skill_id INTEGER NOT NULL REFERENCES habr_skills(id) ON DELETE CASCADE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(experience_id, skill_id)
);

-- Создание индексов для оптимизации запросов
CREATE INDEX IF NOT EXISTS idx_habr_user_experience_skills_experience_id ON habr_user_experience_skills(experience_id);
CREATE INDEX IF NOT EXISTS idx_habr_user_experience_skills_skill_id ON habr_user_experience_skills(skill_id);

-- Комментарии к таблице и колонкам
COMMENT ON TABLE habr_user_experience_skills IS 'Связь между опытом работы и навыками';
COMMENT ON COLUMN habr_user_experience_skills.experience_id IS 'ID записи опыта работы из таблицы habr_user_experience';
COMMENT ON COLUMN habr_user_experience_skills.skill_id IS 'ID навыка из таблицы habr_skills';
COMMENT ON COLUMN habr_user_experience_skills.created_at IS 'Дата и время добавления связи';
