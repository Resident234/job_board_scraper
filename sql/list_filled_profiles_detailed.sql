-- Полный список заполненных профилей с детальной информацией
-- Заполненные профили определяются по той же логике, что и в count_filled_profiles.sql

WITH filled_profiles AS (
    SELECT DISTINCT r.id
    FROM habr_resumes r
    WHERE 
        -- Не пустой about (не NULL и не пустая строка)
        (r.about IS NOT NULL AND TRIM(r.about) != '' AND about != 'Доступ ограничен настройками приватности' AND about != 'Ошибка 404')
        OR
        -- Есть опыт работы в habr_user_experience
        EXISTS (SELECT 1 FROM habr_user_experience ue WHERE ue.user_id = r.id)
        OR
        -- Есть высшее образование в habr_resumes_universities (колонка user_id)
        EXISTS (SELECT 1 FROM habr_resumes_universities ru WHERE ru.user_id = r.id)
        OR
        -- Есть дополнительное образование в habr_resumes_educations
        EXISTS (SELECT 1 FROM habr_resumes_educations re WHERE re.resume_id = r.id)
        OR
        -- Есть участие в профсообществах (JSONB массив не пустой)
        (r.community_participation IS NOT NULL AND jsonb_array_length(r.community_participation) > 0)
)
SELECT 
    -- 1) Общая информация о профиле
    r.title AS "Имя пользователя",
    r.slogan AS "Специализация",
    r.info_tech AS "Техническая информация",
    CASE 
        WHEN r.salary IS NOT NULL THEN 'От ' || r.salary::text || ' ₽'
        ELSE 'Не указана'
    END AS "Зарплата",
    CASE 
        WHEN r.public = true THEN 'Ищу работу'
        ELSE 'Не ищу работу'
    END AS "Статус поиска",
    r.age AS "Возраст",
    COALESCE(r.experience_text, r.work_experience) AS "Опыт работы",
    r.registration AS "Регистрация",
    r.last_visit AS "Последний визит",
    r.citizenship AS "Гражданство",
    CASE 
        WHEN r.remote_work = true THEN 'готов к удаленной работе'
        ELSE 'не указана готовность к удаленной работе'
    END AS "Удаленная работа",
    
    -- 2) Блок "Обо мне"
    r.about AS "Обо мне",
    
    -- 3) Список навыков
    ARRAY_TO_STRING(
        ARRAY(
            SELECT DISTINCT s.title
            FROM habr_user_skills us
            JOIN habr_skills s ON us.skill_id = s.id
            WHERE us.user_id = r.id
            ORDER BY s.title
        ),
        ' • '
    ) AS "Навыки",
    
    -- 4) Опыт работы (форматированный)
    ARRAY_TO_STRING(
        ARRAY(
            SELECT 
                COALESCE(c.title, ue.company_id::text) || '\n' ||
                COALESCE(c.description, '') || '\n' ||
                COALESCE(ue.position, '') || '\n' ||
                COALESCE(ue.duration, '') || '\n' ||
                COALESCE(ue.description, '') || '\n' ||
                ARRAY_TO_STRING(
                    ARRAY(
                        SELECT DISTINCT s2.title
                        FROM habr_user_experience_skills ues
                        JOIN habr_skills s2 ON ues.skill_id = s2.id
                        WHERE ues.experience_id = ue.id
                        ORDER BY s2.title
                    ),
                    ' • '
                )
            FROM habr_user_experience ue
            LEFT JOIN habr_companies c ON ue.company_id = c.id
            WHERE ue.user_id = r.id
            ORDER BY 
                CASE 
                    WHEN ue.duration LIKE '%По настоящее время%' THEN 1
                    ELSE 2
                END,
                ue.duration DESC
        ),
        '\n\n'
    ) AS "Опыт работы детально",
    
    -- 5) Участие в профсообществах
    CASE 
        WHEN r.community_participation IS NOT NULL THEN
            ARRAY_TO_STRING(
                ARRAY(
                    SELECT 
                        cp->>'name' || '\n' ||
                        COALESCE(cp->>'member_since', '') || '\n' ||
                        COALESCE(cp->>'contribution', '') || '\n' ||
                        COALESCE(cp->>'topics', '')
                    FROM JSONB_ARRAY_ELEMENTS(r.community_participation) cp
                ),
                '\n\n'
            )
        ELSE ''
    END AS "Профсообщества",
    
    -- Ссылка на профиль
    r.link AS "Ссылка на профиль"
    
FROM habr_resumes r
JOIN filled_profiles fp ON r.id = fp.id
WHERE r.public = true  -- Только публичные профили
ORDER BY r.created_at DESC
LIMIT 10000;