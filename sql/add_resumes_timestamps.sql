-- Добавление столбцов created_at и updated_at в таблицу habr_resumes
-- Эти столбцы отслеживают время создания и обновления записей

ALTER TABLE habr_resumes 
ADD COLUMN IF NOT EXISTS created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

-- Комментарии к столбцам
COMMENT ON COLUMN habr_resumes.created_at IS 'Дата и время создания записи';
COMMENT ON COLUMN habr_resumes.updated_at IS 'Дата и время последнего обновления записи';
