-- Таблица навыков
CREATE TABLE IF NOT EXISTS habr_skills (
    id SERIAL PRIMARY KEY,
    title TEXT NOT NULL UNIQUE,
    skill_id INTEGER UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Комментарии к столбцам
COMMENT ON COLUMN habr_skills.title IS 'Название навыка (без ограничения длины)';
COMMENT ON COLUMN habr_skills.skill_id IS 'ID навыка из URL Habr Career (например, 352 из /resumes?skills[]=352)';

-- Индексы для быстрого поиска
CREATE INDEX IF NOT EXISTS idx_habr_skills_title ON habr_skills(title);
CREATE INDEX IF NOT EXISTS idx_habr_skills_skill_id ON habr_skills(skill_id);

-- Связующая таблица для связи многие-ко-многим между компаниями и навыками
CREATE TABLE IF NOT EXISTS habr_company_skills (
    id SERIAL PRIMARY KEY,
    company_id INTEGER NOT NULL,
    skill_id INTEGER NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_company FOREIGN KEY (company_id) REFERENCES habr_companies(id) ON DELETE CASCADE,
    CONSTRAINT fk_skill FOREIGN KEY (skill_id) REFERENCES habr_skills(id) ON DELETE CASCADE,
    CONSTRAINT unique_company_skill UNIQUE (company_id, skill_id)
);

-- Комментарии к столбцам
COMMENT ON COLUMN habr_company_skills.company_id IS 'ID компании из таблицы habr_companies';
COMMENT ON COLUMN habr_company_skills.skill_id IS 'ID навыка из таблицы habr_skills';

-- Индексы для быстрого поиска
CREATE INDEX IF NOT EXISTS idx_habr_company_skills_company ON habr_company_skills(company_id);
CREATE INDEX IF NOT EXISTS idx_habr_company_skills_skill ON habr_company_skills(skill_id);

-- Миграция: изменение типа столбца title с VARCHAR(255) на TEXT (для существующих таблиц)
-- Если таблица уже существовала с VARCHAR(255), эта команда обновит тип
ALTER TABLE habr_skills 
ALTER COLUMN title TYPE TEXT;
