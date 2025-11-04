-- Переименование столбцов в таблице habr_companies и добавление title

-- Переименовываем company_code в code
ALTER TABLE habr_companies 
RENAME COLUMN company_code TO code;

-- Переименовываем company_url в url
ALTER TABLE habr_companies 
RENAME COLUMN company_url TO url;

-- Добавляем столбец title
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS title VARCHAR(500);

-- Добавляем комментарии
COMMENT ON COLUMN habr_companies.code IS 'Уникальный код компании';
COMMENT ON COLUMN habr_companies.url IS 'URL страницы компании';
COMMENT ON COLUMN habr_companies.title IS 'Название компании';
