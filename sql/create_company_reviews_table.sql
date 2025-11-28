-- Таблица для хранения отзывов о компаниях

-- DROP TABLE IF EXISTS company_reviews;

CREATE TABLE IF NOT EXISTS habr_company_reviews
(
    id BIGINT NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
    company_id INTEGER NOT NULL,
    review_hash TEXT NOT NULL,
    review_text TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT company_reviews_pkey PRIMARY KEY (id),
    CONSTRAINT company_reviews_review_hash_unique UNIQUE (review_hash),
    CONSTRAINT fk_company FOREIGN KEY (company_id) REFERENCES habr_companies(id) ON DELETE CASCADE
)
TABLESPACE pg_default;

ALTER TABLE IF EXISTS habr_company_reviews
    OWNER to postgres;

-- Комментарии к столбцам
COMMENT ON TABLE habr_company_reviews IS 'Отзывы о компаниях с сайта career.habr.com';
COMMENT ON COLUMN habr_company_reviews.id IS 'Уникальный идентификатор отзыва';
COMMENT ON COLUMN habr_company_reviews.company_id IS 'ID компании из таблицы habr_companies (FK)';
COMMENT ON COLUMN habr_company_reviews.review_hash IS 'Контрольная сумма (хеш) текста отзыва для предотвращения дубликатов';
COMMENT ON COLUMN habr_company_reviews.review_text IS 'Текст отзыва о компании (очищенный от HTML тегов)';
COMMENT ON COLUMN habr_company_reviews.created_at IS 'Дата и время создания записи';
COMMENT ON COLUMN habr_company_reviews.updated_at IS 'Дата и время последнего обновления записи';

-- Индексы
CREATE INDEX IF NOT EXISTS idx_company_reviews_company_id 
    ON habr_company_reviews USING btree (company_id ASC NULLS LAST)
    TABLESPACE pg_default;

CREATE INDEX IF NOT EXISTS idx_company_reviews_review_hash 
    ON habr_company_reviews USING btree (review_hash COLLATE pg_catalog."default" ASC NULLS LAST)
    TABLESPACE pg_default;

CREATE INDEX IF NOT EXISTS idx_company_reviews_created_at 
    ON habr_company_reviews USING btree (created_at DESC NULLS LAST)
    TABLESPACE pg_default;
