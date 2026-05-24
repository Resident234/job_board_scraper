-- Выборка пустых профилей из таблицы habr_resumes
-- Пустые профили - это профили, у которых is_empty = TRUE

SELECT 
    r.id,
    r.link,
    r.title,
    r.code,
    r.about,
    r.public,
    r.created_at,
    r.updated_at
FROM habr_resumes r
WHERE r.is_empty = TRUE
ORDER BY r.updated_at DESC;

-- Подсчёт количества пустых профилей
SELECT COUNT(*) as empty_profiles_count
FROM habr_resumes r
WHERE r.is_empty = TRUE;

-- Статистика по пустым и заполненным профилям
SELECT 
    CASE 
        WHEN is_empty = TRUE THEN 'Пустые профили'
        WHEN is_empty = FALSE THEN 'Заполненные профили'
        ELSE 'Не определено'
    END as profile_type,
    COUNT(*) as count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM habr_resumes), 2) as percentage
FROM habr_resumes
GROUP BY is_empty
ORDER BY is_empty DESC NULLS LAST;
