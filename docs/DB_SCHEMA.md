# Модель данных `JobBoardScraper` (ER-диаграмма)

Документ описывает все таблицы PostgreSQL, используемые парсером `JobBoardScraper`, и связи между ними.
ER-диаграмма оформлена в формате [Mermaid](https://mermaid.js.org/) — она рендерится в GitHub/GitLab/VS Code и поддерживается большинством Markdown-просмотрщиков.

## Сквозные обозначения

- **PK** — первичный ключ (`id`, обычно `SERIAL`/`BIGSERIAL`).
- **FK** — внешний ключ (ссылка на PK другой таблицы).
- **UQ** — уникальный ключ (`UNIQUE` constraint).
- Все таблицы имеют `created_at`/`updated_at TIMESTAMPTZ` (там, где встречается в коде).
- Поле `is_deleted` в `habr_resumes` используется как мягкое удаление (см. `sql/add_is_deleted_column.sql`).

## ER-диаграмма

```mermaid
erDiagram
    habr_resumes {
        BIGSERIAL id PK
        TEXT      link UQ "ON CONFLICT (link)"
        TEXT      title
        TEXT      slogan
        TEXT      code
        BOOLEAN   expert
        TEXT      work_experience
        INTEGER   level_id FK
        TEXT      info_tech
        INTEGER   salary
        TIMESTAMPTZ last_visit
        TEXT      age
        TEXT      registration
        TEXT      citizenship
        BOOLEAN   remote_work
        BOOLEAN   public
        TEXT      job_search_status
        BOOLEAN   is_empty
        BOOLEAN   is_deleted "soft delete"
        TEXT      about
        JSONB     community_participation
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_levels {
        BIGSERIAL id PK
        TEXT      title UQ "ON CONFLICT (title)"
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_companies {
        BIGSERIAL id PK
        TEXT      code UQ "ON CONFLICT (code)"
        TEXT      url
        BIGINT    company_id "из career.habr.com"
        TEXT      title
        TEXT      about
        TEXT      description
        TEXT      site
        NUMERIC   rating
        INTEGER   current_employees
        INTEGER   past_employees
        INTEGER   followers
        INTEGER   want_work
        TEXT      employees_count
        BOOLEAN   habr
        TEXT      city
        TEXT[]    awards
        NUMERIC   scores
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_skills {
        BIGSERIAL id PK
        INTEGER   skill_id "из career.habr.com, не NULL после insert"
        TEXT      title UQ "ON CONFLICT (title)"
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_user_skills {
        BIGSERIAL id PK
        INTEGER   user_id FK
        INTEGER   skill_id FK
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_company_skills {
        BIGSERIAL id PK
        INTEGER   company_id FK
        INTEGER   skill_id FK
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_company_reviews {
        BIGSERIAL id PK
        INTEGER   company_id FK
        TEXT      review_hash UQ "хеш текста отзыва"
        TEXT      review_text
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_user_experience {
        BIGSERIAL id PK
        INTEGER   user_id FK
        INTEGER   company_id FK
        TEXT      position
        TEXT      duration
        TEXT      description
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_user_experience_skills {
        BIGSERIAL id PK
        INTEGER   experience_id FK
        INTEGER   skill_id FK
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_universities {
        BIGSERIAL id PK
        INTEGER   habr_id UQ "ON CONFLICT (habr_id)"
        TEXT      name
        TEXT      city
        INTEGER   graduate_count
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_resumes_universities {
        BIGSERIAL id PK
        INTEGER   user_id FK
        INTEGER   university_id FK
        JSONB     courses
        TEXT      description
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_resumes_educations {
        BIGSERIAL id PK
        INTEGER   resume_id FK
        TEXT      title
        TEXT      course
        TEXT      duration
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_category_root_ids {
        BIGSERIAL id PK
        TEXT      category_id UQ "ON CONFLICT (category_id)"
        TEXT      category_name
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    habr_resumes       ||--o| habr_levels              : "level_id"
    habr_resumes       ||--o{ habr_user_skills          : "user_id"
    habr_resumes       ||--o{ habr_user_experience      : "user_id"
    habr_resumes       ||--o{ habr_resumes_universities  : "user_id"
    habr_resumes       ||--o{ habr_resumes_educations    : "resume_id"
    habr_resumes       }o..o{ habr_skills               : "through habr_user_skills"

    habr_user_experience ||--|| habr_companies          : "company_id"
    habr_user_experience ||--o{ habr_user_experience_skills : "experience_id"

    habr_user_experience_skills }o--|| habr_skills         : "skill_id"
    habr_user_skills            }o--|| habr_skills         : "skill_id"

    habr_companies    ||--o{ habr_company_skills        : "company_id"
    habr_companies    ||--o{ habr_company_reviews        : "company_id"
    habr_company_skills }o--|| habr_skills               : "skill_id"

    habr_resumes_universities }o--|| habr_universities   : "university_id"
```

## Пояснения к сущностям

### Основные таблицы

- **`habr_resumes`** — главная таблица. Одно резюме = один пользователь (`link` уникален).
  Содержит всю «карточку»: имя, должности, навыки (на уровне списком через `habr_user_skills`), зарплата, контакты.
  При парсинге списка резюме с career.habr.com сюда попадают только `link`, `code`, `name`, `is_expert`, `info_tech`, `level_title`, `salary`, `skills`.
  Полное наполнение (`about`, `community_participation`, `experience`, `universities`, `educations`) собирается `UserResumeDetailScraper`.

- **`habr_companies`** — компании из раздела компаний и те, что упоминаются в опыте работы (`code` уникален, приходит с career.habr.com).
- **`habr_levels`** — справочник грейдов (Junior/Middle/Senior), ссылается `habr_resumes.level_id`.
- **`habr_category_root_ids`** — справочник корневых категорий резюме.
- **`habr_skills`** — справочник навыков с уникальным `title`; `skill_id` подтягивается из URL страницы навыка на career.habr.com.

### Связующие таблицы (M:N)

- **`habr_user_skills`** — навыки конкретного пользователя (`user_id` + `skill_id`).
- **`habr_company_skills`** — навыки конкретной компании.
- **`habr_user_experience_skills`** — навыки внутри конкретной записи опыта работы.
- **`habr_resumes_universities`** — высшее образование пользователя + JSON со списком курсов `courses`.

### Зависимые таблицы

- **`habr_user_experience`** — одна запись = одно место работы пользователя; ссылается на резюме и компанию. Содержит JSON-курсы и skills через `habr_user_experience_skills`.
- **`habr_company_reviews`** — отзывы о компании с дедупликацией по хешу текста.
- **`habr_resumes_educations`** — дополнительное образование пользователя.

## Особенности дизайна

1. **`ON CONFLICT ... DO UPDATE`** — все основные таблицы используют `INSERT ... ON CONFLICT(...) DO UPDATE SET ... COALESCE(EXCLUDED.x, table.x)`. Это значит, что повторный парсинг обновляет только заполненные поля, не затирая уже имеющиеся данные значением `NULL`.

2. **JSONB для коллекций** — `community_participation`, `courses` хранятся как JSONB-массивы. Это упрощает схему (нет отдельных таблиц для Community/Course) ценой потери удобства SQL-запросов по содержимому.

3. **Мягкое удаление** — только в `habr_resumes` через `is_deleted` (см. `sql/add_is_deleted_column.sql`). Остальные таблицы жёстко удаляют записи через `DELETE`.

4. **`habr_user_skills` vs `habr_resumes.skills`** — исторически `skills` хранились и в JSON-виде внутри `habr_resumes`, и в отдельной таблице `habr_user_skills` (для быстрого поиска/фильтрации по навыкам). При парсинге списка заполняется только `skills`-поле, при детальном парсинге — обе.

## Соответствие скрейперов и таблиц

| Скрейпер | Заполняемые таблицы |
|---|---|
| `ResumeListPageScraper` | `habr_resumes` (только базовые поля), `habr_skills` |
| `UserResumeDetailScraper` | `habr_resumes` (полностью), `habr_user_experience`, `habr_user_experience_skills`, `habr_resumes_universities`, `habr_resumes_educations`, `habr_user_skills` |
| `UserFriendsScraper` | (friend-relations — отсутствует в БД, если есть — отдельная таблица) |
| `CompanyListScraper` | `habr_companies`, `habr_company_reviews`, `habr_company_skills` |
| `CompanyDetailScraper` | `habr_companies`, `habr_company_skills`, `habr_company_reviews` |
| `CompanyRatingScraper` | `habr_company_reviews` (через хеш) |
| `CompanyFollowersScraper` | (если используется) `habr_companies.followers` |
| `UserProfileScraper` | `habr_resumes.about`, `habr_resumes.community_participation` |
| `ExpertsScraper` | (через `habr_resumes` с фильтром `is_expert=true`) |
| `BruteForceUsernameScraper` | `habr_resumes` |

## Как рендерить

### В GitHub / GitLab
Просто откройте этот файл — они автоматически рендерят Mermaid в Preview.

### В VS Code
Установите расширение **Markdown Preview Mermaid Support** (bierner.markdown-mermaid) и откройте Preview (`Ctrl+Shift+V`).

### Локально (mermaid-cli)
```bash
npm install -g @mermaid-js/mermaid-cli
mmdc -i docs/DB_SCHEMA.md -o docs/DB_SCHEMA.svg
```

### Онлайн
Скопируйте блок `mermaid` в [mermaid.live](https://mermaid.live/).