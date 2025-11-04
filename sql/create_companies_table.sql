CREATE TABLE IF NOT EXISTS habr_companies (
    id SERIAL PRIMARY KEY,
    code VARCHAR(255) NOT NULL UNIQUE,
    url VARCHAR(500) NOT NULL,
    title VARCHAR(500),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Комментарии к столбцам
COMMENT ON COLUMN habr_companies.code IS 'Уникальный код компании';
COMMENT ON COLUMN habr_companies.url IS 'URL страницы компании';
COMMENT ON COLUMN habr_companies.title IS 'Название компании';

CREATE INDEX IF NOT EXISTS idx_habr_companies_code ON habr_companies(code);
CREATE INDEX IF NOT EXISTS idx_habr_companies_created_at ON habr_companies(created_at);
