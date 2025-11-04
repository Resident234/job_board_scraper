-- Table: public.habr_resumes

-- DROP TABLE IF EXISTS habr_resumes;

CREATE TABLE IF NOT EXISTS habr_resumes
(
    link text COLLATE pg_catalog."default" NOT NULL,
    title text COLLATE pg_catalog."default",
    slogan text COLLATE pg_catalog."default",
    code text COLLATE pg_catalog."default",
    expert boolean DEFAULT FALSE,
    work_experience text COLLATE pg_catalog."default",
    viewed bit(1),
    id bigint NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
    CONSTRAINT habr_resumes_pkey PRIMARY KEY (id),
    CONSTRAINT habr_resumes_link_unique UNIQUE (link)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS habr_resumes
    OWNER to postgres;

-- Комментарии к столбцам
COMMENT ON COLUMN habr_resumes.link IS 'Полный URL профиля пользователя';
COMMENT ON COLUMN habr_resumes.title IS 'Имя пользователя (username)';
COMMENT ON COLUMN habr_resumes.slogan IS 'Слоган/специализация пользователя';
COMMENT ON COLUMN habr_resumes.code IS 'Код пользователя из URL профиля';
COMMENT ON COLUMN habr_resumes.expert IS 'Флаг: является ли пользователь экспертом';
COMMENT ON COLUMN habr_resumes.work_experience IS 'Стаж работы (например: "9 лет и 9 месяцев")';
COMMENT ON COLUMN habr_resumes.viewed IS 'Флаг просмотра записи';

-- Index: ix_habr_resumes_viewed_id

-- DROP INDEX IF EXISTS ix_habr_resumes_viewed_id;

CREATE INDEX IF NOT EXISTS ix_habr_resumes_viewed_id
    ON habr_resumes USING btree
    (viewed ASC NULLS LAST, id ASC NULLS LAST)
    TABLESPACE pg_default;

-- Index: idx_habr_resumes_code
CREATE INDEX IF NOT EXISTS idx_habr_resumes_code
    ON habr_resumes USING btree
    (code COLLATE pg_catalog."default" ASC NULLS LAST)
    TABLESPACE pg_default;

-- Index: idx_habr_resumes_expert (partial index for experts only)
CREATE INDEX IF NOT EXISTS idx_habr_resumes_expert
    ON habr_resumes USING btree
    (expert ASC NULLS LAST)
    WHERE expert = TRUE
    TABLESPACE pg_default;