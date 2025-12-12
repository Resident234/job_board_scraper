-- Подсчёт записей с заполненными данными (about, навыки или опыт работы)
-- Результат: две строки - для заполненных профилей и для приватных профилей
WITH filled_profiles AS (
    SELECT DISTINCT r.id
    FROM habr_resumes r
    WHERE 
        -- Не пустой about (не NULL и не пустая строка)
        (r.about IS NOT NULL AND TRIM(r.about) != '')
        OR
        -- Есть опыт работы в habr_user_experience
        EXISTS (SELECT 1 FROM habr_user_experience ue WHERE ue.user_id = r.id)
        OR
        -- Есть высшее образование в habr_resumes_universities
        EXISTS (SELECT 1 FROM habr_resumes_universities ru WHERE ru.resume_id = r.id)
        OR
        -- Есть дополнительное образование в habr_resumes_educations
        EXISTS (SELECT 1 FROM habr_resumes_educations re WHERE re.resume_id = r.id)
        OR
        -- Есть участие в профсообществах (JSONB массив не пустой)
        (r.community_participation IS NOT NULL AND jsonb_array_length(r.community_participation) > 0)
),
private_profiles AS (
    SELECT id
    FROM habr_resumes
    WHERE public = false 
      AND about = 'Доступ ограничен настройками приватности'
),
total_count AS (
    SELECT COUNT(*) as total FROM habr_resumes
)
SELECT 
    'Заполненные профили' as "Категория",
    (SELECT COUNT(*) FROM filled_profiles) as "Количество",
    tc.total as "Всего записей",
    ROUND((SELECT COUNT(*) FROM filled_profiles)::numeric / tc.total::numeric * 100, 2) as "Процент"
FROM total_count tc

UNION ALL

SELECT 
    'Приватные профили' as "Категория",
    (SELECT COUNT(*) FROM private_profiles) as "Количество",
    tc.total as "Всего записей",
    ROUND((SELECT COUNT(*) FROM private_profiles)::numeric / tc.total::numeric * 100, 2) as "Процент"
FROM total_count tc;
