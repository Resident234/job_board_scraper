CREATE TABLE IF NOT EXISTS habr_companies (
    id SERIAL PRIMARY KEY,
    company_code VARCHAR(255) NOT NULL UNIQUE,
    company_url VARCHAR(500) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_habr_companies_code ON habr_companies(company_code);
CREATE INDEX IF NOT EXISTS idx_habr_companies_created_at ON habr_companies(created_at);
