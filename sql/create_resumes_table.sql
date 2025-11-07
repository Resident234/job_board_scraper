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
    level_id integer,
    info_tech text COLLATE pg_catalog."default",
    salary integer,
    last_visit text COLLATE pg_catalog."default",
    viewed bit(1),
    id bigint NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
    CONSTRAINT habr_resumes_pkey PRIMARY KEY (id),
    CONSTRAINT habr_resumes_link_unique UNIQUE (link),
    CONSTRAINT fk_level FOREIGN KEY (level_id) REFERENCES habr_levels(id)
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
COMMENT ON COLUMN habr_resumes.level_id IS 'ID уровня специалиста из таблицы habr_levels (FK)';
COMMENT ON COLUMN habr_resumes.info_tech IS 'Техническая информация о специализации (например, "Product manager | B2B SaaS • Менеджер продукта")';
COMMENT ON COLUMN habr_resumes.salary IS 'Желаемая зарплата в рублях (только число)';
COMMENT ON COLUMN habr_resumes.last_visit IS 'Последний визит (например, "5 дней назад")';
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

-- Index: idx_habr_resumes_level_id
CREATE INDEX IF NOT EXISTS idx_habr_resumes_level_id
    ON habr_resumes USING btree
    (level_id ASC NULLS LAST)
    TABLESPACE pg_default;

-- Index: idx_habr_resumes_salary
CREATE INDEX IF NOT EXISTS idx_habr_resumes_salary
    ON habr_resumes USING btree
    (salary ASC NULLS LAST)
    TABLESPACE pg_default;