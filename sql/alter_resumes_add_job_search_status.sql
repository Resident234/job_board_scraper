-- Добавление поля job_search_status в таблицу habr_resumes

ALTER TABLE habr_resumes
ADD COLUMN IF NOT EXISTS job_search_status text COLLATE pg_catalog."default";

-- Комментарий к столбцу
COMMENT ON COLUMN habr_resumes.job_search_status IS 'Статус поиска работы: "Ищу работу", "Не ищу работу", "Рассматриваю предложения"';

-- Индекс для быстрого поиска по статусу
CREATE INDEX IF NOT EXISTS idx_habr_resumes_job_search_status
    ON habr_resumes USING btree
    (job_search_status COLLATE pg_catalog."default" ASC NULLS LAST)
    TABLESPACE pg_default;
