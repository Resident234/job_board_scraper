-- Добавление дополнительных столбцов в таблицу habr_companies
-- Этот скрипт добавляет все поля, извлекаемые CompanyDetailScraper

-- Добавляем столбец company_id (числовой ID компании из кнопки избранного)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS company_id BIGINT;

-- Добавляем столбец about (описание компании)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS about TEXT;

-- Добавляем столбец site (ссылка на сайт компании)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS site TEXT;

-- Добавляем столбец rating (рейтинг компании)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS rating DECIMAL(3,2);

-- Добавляем комментарии к столбцам
COMMENT ON COLUMN habr_companies.company_id IS 'Числовой ID компании из элемента company_fav_button';
COMMENT ON COLUMN habr_companies.about IS 'Описание компании из элемента company_about';
COMMENT ON COLUMN habr_companies.site IS 'Ссылка на сайт компании из элемента company_site';
COMMENT ON COLUMN habr_companies.rating IS 'Рейтинг компании из элемента span.rating';

-- Создаём индекс на company_id для быстрого поиска
CREATE INDEX IF NOT EXISTS idx_habr_companies_company_id ON habr_companies(company_id);

-- Создаём уникальное ограничение на company_id (если ID не NULL, он должен быть уникальным)
CREATE UNIQUE INDEX IF NOT EXISTS idx_habr_companies_company_id_unique 
ON habr_companies(company_id) 
WHERE company_id IS NOT NULL;
