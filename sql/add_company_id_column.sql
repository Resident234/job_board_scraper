-- Добавление столбца company_id в таблицу habr_companies

-- Добавляем столбец company_id (числовой ID компании из кнопки избранного)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS company_id BIGINT;

-- Добавляем комментарий к столбцу
COMMENT ON COLUMN habr_companies.company_id IS 'Числовой ID компании из элемента company_fav_button';

-- Создаём индекс на company_id для быстрого поиска
CREATE INDEX IF NOT EXISTS idx_habr_companies_company_id ON habr_companies(company_id);

-- Создаём уникальное ограничение на company_id (если ID не NULL, он должен быть уникальным)
CREATE UNIQUE INDEX IF NOT EXISTS idx_habr_companies_company_id_unique 
ON habr_companies(company_id) 
WHERE company_id IS NOT NULL;
