-- Добавление уникального ограничения на столбец link
-- Необходимо для работы ON CONFLICT в режиме UpdateIfExists

-- Сначала делаем столбец NOT NULL (если еще не сделано)
ALTER TABLE habr_resumes 
ALTER COLUMN link SET NOT NULL;

-- Добавляем уникальное ограничение
ALTER TABLE habr_resumes 
ADD CONSTRAINT habr_resumes_link_unique UNIQUE (link);

-- Комментарий
COMMENT ON CONSTRAINT habr_resumes_link_unique ON habr_resumes IS 'Уникальность ссылки на профиль пользователя';
