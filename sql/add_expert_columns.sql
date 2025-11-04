-- Добавление столбцов для экспертов в таблицу habr_resumes

-- Добавляем столбец code (код пользователя из URL)
ALTER TABLE habr_resumes 
ADD COLUMN IF NOT EXISTS code TEXT;

-- Добавляем столбец expert (флаг эксперта)
ALTER TABLE habr_resumes 
ADD COLUMN IF NOT EXISTS expert BOOLEAN DEFAULT FALSE;

-- Добавляем столбец work_experience (стаж работы)
ALTER TABLE habr_resumes 
ADD COLUMN IF NOT EXISTS work_experience TEXT;

-- Добавляем комментарии к столбцам
COMMENT ON COLUMN habr_resumes.code IS 'Код пользователя из URL профиля';
COMMENT ON COLUMN habr_resumes.expert IS 'Флаг: является ли пользователь экспертом';
COMMENT ON COLUMN habr_resumes.work_experience IS 'Стаж работы (например: "9 лет и 9 месяцев")';

-- Создаём индекс на code для быстрого поиска
CREATE INDEX IF NOT EXISTS idx_habr_resumes_code ON habr_resumes(code);

-- Создаём индекс на expert для фильтрации экспертов
CREATE INDEX IF NOT EXISTS idx_habr_resumes_expert ON habr_resumes(expert) WHERE expert = TRUE;
