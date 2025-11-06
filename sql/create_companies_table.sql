CREATE TABLE IF NOT EXISTS habr_companies (
    id SERIAL PRIMARY KEY,
    code VARCHAR(255) NOT NULL UNIQUE,
    url VARCHAR(500) NOT NULL,
    title VARCHAR(500),
    company_id BIGINT,
    about TEXT,
    site TEXT,
    rating DECIMAL(3,2),
    current_employees INTEGER,
    past_employees INTEGER,
    followers INTEGER,
    want_work INTEGER,
    employees_count TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Комментарии к столбцам
COMMENT ON COLUMN habr_companies.code IS 'Уникальный код компании';
COMMENT ON COLUMN habr_companies.url IS 'URL страницы компании';
COMMENT ON COLUMN habr_companies.title IS 'Название компании';
COMMENT ON COLUMN habr_companies.company_id IS 'Числовой ID компании из элемента company_fav_button';
COMMENT ON COLUMN habr_companies.about IS 'Описание компании из элемента company_about';
COMMENT ON COLUMN habr_companies.site IS 'Ссылка на сайт компании из элемента company_site';
COMMENT ON COLUMN habr_companies.rating IS 'Рейтинг компании из элемента span.rating';
COMMENT ON COLUMN habr_companies.current_employees IS 'Текущие сотрудники (первое число из "847 / 1622")';
COMMENT ON COLUMN habr_companies.past_employees IS 'Все сотрудники (второе число из "847 / 1622")';
COMMENT ON COLUMN habr_companies.followers IS 'Подписчики (первое число из "253 / 318")';
COMMENT ON COLUMN habr_companies.want_work IS 'Хотят работать (второе число из "253 / 318")';
COMMENT ON COLUMN habr_companies.employees_count IS 'Размер компании из элемента .employees (например, "Более 5000 человек")';

-- Индексы
CREATE INDEX IF NOT EXISTS idx_habr_companies_code ON habr_companies(code);
CREATE INDEX IF NOT EXISTS idx_habr_companies_created_at ON habr_companies(created_at);
CREATE INDEX IF NOT EXISTS idx_habr_companies_company_id ON habr_companies(company_id);

-- Уникальное ограничение на company_id (если ID не NULL, он должен быть уникальным)
CREATE UNIQUE INDEX IF NOT EXISTS idx_habr_companies_company_id_unique 
ON habr_companies(company_id) 
WHERE company_id IS NOT NULL;
