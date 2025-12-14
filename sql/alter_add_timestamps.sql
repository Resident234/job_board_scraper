-- Миграция: добавление полей created_at и updated_at во все таблицы
-- Для таблиц, где уже есть created_at, добавляем только updated_at
-- Для таблиц, где нет ни одного поля, добавляем оба

-- ============================================
-- habr_levels: добавляем updated_at
-- ============================================
ALTER TABLE habr_levels 
ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

COMMENT ON COLUMN habr_levels.updated_at IS 'Дата и время последнего обновления записи';

-- ============================================
-- habr_skills: добавляем updated_at
-- ============================================
ALTER TABLE habr_skills 
ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

COMMENT ON COLUMN habr_skills.updated_at IS 'Дата и время последнего обновления записи';

-- ============================================
-- habr_company_skills: добавляем updated_at
-- ============================================
ALTER TABLE habr_company_skills 
ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

COMMENT ON COLUMN habr_company_skills.updated_at IS 'Дата и время последнего обновления записи';

-- ============================================
-- habr_user_skills: добавляем updated_at
-- ============================================
ALTER TABLE habr_user_skills 
ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

COMMENT ON COLUMN habr_user_skills.updated_at IS 'Дата и время последнего обновления записи';

-- ============================================
-- habr_user_experience_skills: добавляем updated_at
-- ============================================
ALTER TABLE habr_user_experience_skills 
ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

COMMENT ON COLUMN habr_user_experience_skills.updated_at IS 'Дата и время последнего обновления записи';

-- ============================================
-- Обновление существующих записей: установка updated_at = created_at
-- ============================================
UPDATE habr_levels SET updated_at = created_at WHERE updated_at IS NULL;
UPDATE habr_skills SET updated_at = created_at WHERE updated_at IS NULL;
UPDATE habr_company_skills SET updated_at = created_at WHERE updated_at IS NULL;
UPDATE habr_user_skills SET updated_at = created_at WHERE updated_at IS NULL;
UPDATE habr_user_experience_skills SET updated_at = created_at WHERE updated_at IS NULL;

-- ============================================
-- Создание триггерной функции для автоматического обновления updated_at
-- ============================================
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- ============================================
-- Создание триггеров для автоматического обновления updated_at
-- ============================================

-- habr_resumes
DROP TRIGGER IF EXISTS update_habr_resumes_updated_at ON habr_resumes;
CREATE TRIGGER update_habr_resumes_updated_at
    BEFORE UPDATE ON habr_resumes
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- habr_companies
DROP TRIGGER IF EXISTS update_habr_companies_updated_at ON habr_companies;
CREATE TRIGGER update_habr_companies_updated_at
    BEFORE UPDATE ON habr_companies
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- habr_levels
DROP TRIGGER IF EXISTS update_habr_levels_updated_at ON habr_levels;
CREATE TRIGGER update_habr_levels_updated_at
    BEFORE UPDATE ON habr_levels
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- habr_skills
DROP TRIGGER IF EXISTS update_habr_skills_updated_at ON habr_skills;
CREATE TRIGGER update_habr_skills_updated_at
    BEFORE UPDATE ON habr_skills
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- habr_company_skills
DROP TRIGGER IF EXISTS update_habr_company_skills_updated_at ON habr_company_skills;
CREATE TRIGGER update_habr_company_skills_updated_at
    BEFORE UPDATE ON habr_company_skills
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- habr_user_skills
DROP TRIGGER IF EXISTS update_habr_user_skills_updated_at ON habr_user_skills;
CREATE TRIGGER update_habr_user_skills_updated_at
    BEFORE UPDATE ON habr_user_skills
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- habr_user_experience
DROP TRIGGER IF EXISTS update_habr_user_experience_updated_at ON habr_user_experience;
CREATE TRIGGER update_habr_user_experience_updated_at
    BEFORE UPDATE ON habr_user_experience
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- habr_user_experience_skills
DROP TRIGGER IF EXISTS update_habr_user_experience_skills_updated_at ON habr_user_experience_skills;
CREATE TRIGGER update_habr_user_experience_skills_updated_at
    BEFORE UPDATE ON habr_user_experience_skills
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- habr_company_reviews
DROP TRIGGER IF EXISTS update_habr_company_reviews_updated_at ON habr_company_reviews;
CREATE TRIGGER update_habr_company_reviews_updated_at
    BEFORE UPDATE ON habr_company_reviews
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- habr_universities
DROP TRIGGER IF EXISTS update_habr_universities_updated_at ON habr_universities;
CREATE TRIGGER update_habr_universities_updated_at
    BEFORE UPDATE ON habr_universities
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- habr_resumes_universities
DROP TRIGGER IF EXISTS update_habr_resumes_universities_updated_at ON habr_resumes_universities;
CREATE TRIGGER update_habr_resumes_universities_updated_at
    BEFORE UPDATE ON habr_resumes_universities
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- habr_resumes_educations
DROP TRIGGER IF EXISTS update_habr_resumes_educations_updated_at ON habr_resumes_educations;
CREATE TRIGGER update_habr_resumes_educations_updated_at
    BEFORE UPDATE ON habr_resumes_educations
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- habr_category_root_ids
DROP TRIGGER IF EXISTS update_habr_category_root_ids_updated_at ON habr_category_root_ids;
CREATE TRIGGER update_habr_category_root_ids_updated_at
    BEFORE UPDATE ON habr_category_root_ids
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();
