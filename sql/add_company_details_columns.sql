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

-- Добавляем столбец current_employees (текущие сотрудники)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS current_employees INTEGER;

-- Добавляем столбец past_employees (все сотрудники)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS past_employees INTEGER;

-- Добавляем столбец followers (подписчики)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS followers INTEGER;

-- Добавляем столбец want_work (хотят работать)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS want_work INTEGER;

-- Добавляем столбец employees_count (размер компании)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS employees_count TEXT;

-- Добавляем столбец description (детальное описание компании)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS description TEXT;

-- Добавляем столбец habr (ведет ли компания блог на Хабре)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS habr BOOLEAN DEFAULT FALSE;

-- Добавляем комментарии к столбцам
COMMENT ON COLUMN habr_companies.company_id IS 'Числовой ID компании из элемента company_fav_button';
COMMENT ON COLUMN habr_companies.about IS 'Описание компании из элемента company_about';
COMMENT ON COLUMN habr_companies.site IS 'Ссылка на сайт компании из элемента company_site';
COMMENT ON COLUMN habr_companies.rating IS 'Рейтинг компании из элемента span.rating';
COMMENT ON COLUMN habr_companies.current_employees IS 'Текущие сотрудники (первое число из "847 / 1622")';
COMMENT ON COLUMN habr_companies.past_employees IS 'Все сотрудники (второе число из "847 / 1622")';
COMMENT ON COLUMN habr_companies.followers IS 'Подписчики (первое число из "253 / 318")';
COMMENT ON COLUMN habr_companies.want_work IS 'Хотят работать (второе число из "253 / 318")';
COMMENT ON COLUMN habr_companies.employees_count IS 'Размер компании из элемента .employees (например, "Более 5000 человек")';
COMMENT ON COLUMN habr_companies.description IS 'Детальное описание компании из элемента .description (очищено от HTML тегов)';
COMMENT ON COLUMN habr_companies.habr IS 'Ведет ли компания блог на Хабре (проверяется по наличию элемента с текстом "Ведет блог на «Хабре»")';

-- Создаём индекс на company_id для быстрого поиска
CREATE INDEX IF NOT EXISTS idx_habr_companies_company_id ON habr_companies(company_id);

-- Создаём уникальное ограничение на company_id (если ID не NULL, он должен быть уникальным)
CREATE UNIQUE INDEX IF NOT EXISTS idx_habr_companies_company_id_unique 
ON habr_companies(company_id) 
WHERE company_id IS NOT NULL;
