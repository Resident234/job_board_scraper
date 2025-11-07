-- Добавление дополнительных столбцов в таблицу habr_resumes
-- Этот скрипт добавляет поля для детальной информации профиля

-- Добавляем столбец level_id (уровень специалиста)
ALTER TABLE habr_resumes 
ADD COLUMN IF NOT EXISTS level_id INTEGER REFERENCES habr_levels(id);

-- Добавляем столбец info_tech (техническая информация о специализации)
ALTER TABLE habr_resumes 
ADD COLUMN IF NOT EXISTS info_tech TEXT;

-- Добавляем столбец salary (желаемая зарплата)
ALTER TABLE habr_resumes 
ADD COLUMN IF NOT EXISTS salary INTEGER;

-- Добавляем комментарии к столбцам
COMMENT ON COLUMN habr_resumes.level_id IS 'ID уровня специалиста из таблицы habr_levels (FK)';
COMMENT ON COLUMN habr_resumes.info_tech IS 'Техническая информация о специализации (например, "Product manager | B2B SaaS • Менеджер продукта")';
COMMENT ON COLUMN habr_resumes.salary IS 'Желаемая зарплата в рублях (только число)';

-- Создаём индекс на level_id для быстрого поиска
CREATE INDEX IF NOT EXISTS idx_habr_resumes_level_id ON habr_resumes(level_id);

-- Создаём индекс на salary для фильтрации по зарплате
CREATE INDEX IF NOT EXISTS idx_habr_resumes_salary ON habr_resumes(salary);
