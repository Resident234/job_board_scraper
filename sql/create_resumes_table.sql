-- Table: public.habr_resumes

-- DROP TABLE IF EXISTS habr_resumes;

CREATE TABLE IF NOT EXISTS habr_resumes
(
    link text COLLATE pg_catalog."default",
    title text COLLATE pg_catalog."default",
    slogan text COLLATE pg_catalog."default",
    viewed bit(1),
    id bigint NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
    CONSTRAINT habr_resumes_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS habr_resumes
    OWNER to postgres;

-- Комментарии к столбцам
COMMENT ON COLUMN habr_resumes.link IS 'Полный URL профиля пользователя';
COMMENT ON COLUMN habr_resumes.title IS 'Имя пользователя (username)';
COMMENT ON COLUMN habr_resumes.slogan IS 'Слоган/специализация пользователя';
COMMENT ON COLUMN habr_resumes.viewed IS 'Флаг просмотра записи';

-- Index: ix_habr_resumes_viewed_id

-- DROP INDEX IF EXISTS ix_habr_resumes_viewed_id;

CREATE INDEX IF NOT EXISTS ix_habr_resumes_viewed_id
    ON habr_resumes USING btree
    (viewed ASC NULLS LAST, id ASC NULLS LAST)
    TABLESPACE pg_default;