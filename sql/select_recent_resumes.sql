-- Выборка резюме, обновленных за последние 2 суток
-- Использование: выполните этот запрос для получения недавно обновленных записей

-- Вариант 1: Все поля
SELECT *
FROM habr_resumes
WHERE updated_at >= NOW() - INTERVAL '2 days'
ORDER BY updated_at DESC;

-- Вариант 2: Только основные поля
SELECT 
    id,
    code,
    title,
    link,
    expert,
    salary,
    level_id,
    info_tech,
    created_at,
    updated_at
FROM habr_resumes
WHERE updated_at >= NOW() - INTERVAL '2 days'
ORDER BY updated_at DESC;

-- Вариант 3: Точно 48 часов
SELECT *
FROM habr_resumes
WHERE updated_at >= NOW() - INTERVAL '48 hours'
ORDER BY updated_at DESC;

-- Вариант 4: Подсчет количества обновленных записей
SELECT COUNT(*) as total_records
FROM habr_resumes
WHERE updated_at >= NOW() - INTERVAL '2 days';

-- Вариант 5: С группировкой по дате обновления
SELECT 
    DATE(updated_at) as update_date,
    COUNT(*) as records_count
FROM habr_resumes
WHERE updated_at >= NOW() - INTERVAL '2 days'
GROUP BY DATE(updated_at)
ORDER BY update_date DESC;

-- Вариант 6: С информацией о навыках (JOIN с habr_user_skills)
SELECT 
    r.id,
    r.code,
    r.title,
    r.link,
    r.expert,
    r.salary,
    r.updated_at,
    COUNT(us.skill_id) as skills_count
FROM habr_resumes r
LEFT JOIN habr_user_skills us ON r.code = us.user_code
WHERE r.updated_at >= NOW() - INTERVAL '2 days'
GROUP BY r.id, r.code, r.title, r.link, r.expert, r.salary, r.updated_at
ORDER BY r.updated_at DESC;
