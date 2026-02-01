-- Полный список заполненных профилей с детальной информацией
-- Заполненные профили определяются по той же логике, что и в count_filled_profiles.sql
-- Формат вывода: каждая строка с префиксом названия поля, разделитель между профилями

WITH filled_profiles AS (
    SELECT DISTINCT r.id, r.title
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
    -- LIMIT 100 -- раскомментировать для ограничения количества профилей
),
profile_data AS (
    SELECT 
        r.id,
        r.title,
        r.slogan,
        r.info_tech,
        r.salary,
        r.public,
        r.age,
        COALESCE(r.experience_text, r.work_experience) as experience,
        r.registration,
        r.last_visit,
        r.citizenship,
        r.remote_work,
        r.about,
        r.link,
        ARRAY_TO_STRING(
            ARRAY(
                SELECT DISTINCT s.title
                FROM habr_user_skills us
                JOIN habr_skills s ON us.skill_id = s.id
                WHERE us.user_id = r.id
                ORDER BY s.title
            ),
            ' • '
        ) as skills,
        ARRAY_TO_STRING(
            ARRAY(
                SELECT 
                    'Компания: ' || COALESCE(c.title, ue.company_id::text, 'Не указана') || ' | ' ||
                    'Должность: ' || COALESCE(ue.position, 'Не указана') || ' | ' ||
                    'Период: ' || COALESCE(ue.duration, 'Не указан') || ' | ' ||
                    'Описание: ' || COALESCE(ue.description, 'Не указано') || ' | ' ||
                    'Навыки: ' || COALESCE(
                        ARRAY_TO_STRING(
                            ARRAY(
                                SELECT DISTINCT s2.title
                                FROM habr_user_experience_skills ues
                                JOIN habr_skills s2 ON ues.skill_id = s2.id
                                WHERE ues.experience_id = ue.id
                                ORDER BY s2.title
                            ),
                            ' • '
                        ),
                        'Не указаны'
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
            ' || '
        ) as experience_details,
        CASE 
            WHEN r.community_participation IS NOT NULL THEN
                ARRAY_TO_STRING(
                    ARRAY(
                        SELECT 
                            'Сообщество: ' || COALESCE(cp->>'name', 'Не указано') || ' | ' ||
                            'Участвует: ' || COALESCE(cp->>'member_since', 'Не указано') || ' | ' ||
                            'Вклад: ' || COALESCE(cp->>'contribution', 'Не указан') || ' | ' ||
                            'Темы: ' || COALESCE(cp->>'topics', 'Не указаны')
                        FROM JSONB_ARRAY_ELEMENTS(r.community_participation) cp
                    ),
                    ' || '
                )
            ELSE 'Не указано'
        END as communities
    FROM habr_resumes r
    JOIN filled_profiles fp ON r.id = fp.id
    WHERE r.public = true
),
formatted_output AS (
    SELECT 
        id,
        title,
        1 as display_order,
        '=== ПРОФИЛЬ: ' || title || ' ===' AS output_line
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        2,
        'Имя: ' || title
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        3,
        'Специализация: ' || COALESCE(slogan, 'Не указана')
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        4,
        'Техническая информация: ' || COALESCE(info_tech, 'Не указана')
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        5,
        'Зарплата: ' || CASE 
            WHEN salary IS NOT NULL THEN 'От ' || salary::text || ' ₽'
            ELSE 'Не указана'
        END
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        6,
        'Статус поиска: ' || CASE 
            WHEN public = true THEN 'Ищу работу'
            ELSE 'Не ищу работу'
        END
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        7,
        'Возраст: ' || COALESCE(age, 'Не указан')
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        8,
        'Опыт работы: ' || COALESCE(experience, 'Не указан')
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        9,
        'Регистрация: ' || COALESCE(registration, 'Не указана')
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        10,
        'Последний визит: ' || COALESCE(last_visit, 'Не указан')
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        11,
        'Гражданство: ' || COALESCE(citizenship, 'Не указано')
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        12,
        'Удаленная работа: ' || CASE 
            WHEN remote_work = true THEN 'готов к удаленной работе'
            ELSE 'не указана готовность к удаленной работе'
        END
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        13,
        'Ссылка на профиль: ' || link
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        14,
        '--- Обо мне ---'
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        15,
        'Обо мне: ' || COALESCE(about, 'Не указано')
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        16,
        '--- Навыки ---'
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        17,
        'Навыки: ' || COALESCE(skills, 'Не указаны')
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        18,
        '--- Опыт работы ---'
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        19,
        'Опыт работы: ' || COALESCE(experience_details, 'Не указан')
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        20,
        '--- Профсообщества ---'
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        21,
        'Профсообщества: ' || communities
    FROM profile_data

    UNION ALL

    SELECT 
        id,
        title,
        22,
        '=============================================='
    FROM profile_data
)

SELECT output_line
FROM formatted_output
ORDER BY title, display_order
-- LIMIT убран для получения всех профилей;