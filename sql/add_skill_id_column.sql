-- Добавление столбца skill_id в таблицу habr_skills
-- Этот столбец хранит ID навыка из URL Habr Career

ALTER TABLE habr_skills 
ADD COLUMN IF NOT EXISTS skill_id INTEGER UNIQUE;

-- Создание индекса для оптимизации поиска по skill_id
CREATE INDEX IF NOT EXISTS idx_habr_skills_skill_id ON habr_skills(skill_id);

-- Комментарий к столбцу
COMMENT ON COLUMN habr_skills.skill_id IS 'ID навыка из URL Habr Career (например, 352 из /resumes?skills[]=352)';
