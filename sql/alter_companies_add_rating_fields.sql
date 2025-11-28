-- Добавление новых полей в таблицу habr_companies для хранения данных рейтингов

-- Добавляем поле city для хранения города компании
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS city TEXT;

-- Добавляем поле awards для хранения списка наград (используем TEXT[] - массив строк)
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS awards TEXT[];

-- Добавляем поле scores для хранения средней оценки
ALTER TABLE habr_companies 
ADD COLUMN IF NOT EXISTS scores DECIMAL(4,2);

-- Комментарии к новым столбцам
COMMENT ON COLUMN habr_companies.city IS 'Город компании из страницы рейтингов';
COMMENT ON COLUMN habr_companies.awards IS 'Список наград компании (например: ["Современные технологии #1", "Интересные задачи #2"])';
COMMENT ON COLUMN habr_companies.scores IS 'Средняя оценка компании из раздела рейтингов (например: 4.82)';

-- Создаем индекс для поля city для быстрого поиска по городам
CREATE INDEX IF NOT EXISTS idx_habr_companies_city ON habr_companies(city);

-- Создаем индекс для поля scores для сортировки по оценкам
CREATE INDEX IF NOT EXISTS idx_habr_companies_scores ON habr_companies(scores DESC NULLS LAST);
