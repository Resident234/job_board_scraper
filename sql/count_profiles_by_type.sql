-- Подсчёт профилей по типам с использованием поля is_empty
-- Результат: статистика по заполненным, пустым и приватным профилям

WITH profile_stats AS (
    SELECT 
        CASE 
            WHEN public = false AND about = 'Доступ ограничен настройками приватности' THEN 'Приватные профили'
            WHEN is_empty = TRUE THEN 'Пустые профили'
            WHEN is_empty = FALSE THEN 'Заполненные профили'
            ELSE 'Не определено'
        END as profile_type,
        COUNT(*) as count
    FROM habr_resumes
    GROUP BY 
        CASE 
            WHEN public = false AND about = 'Доступ ограничен настройками приватности' THEN 'Приватные профили'
            WHEN is_empty = TRUE THEN 'Пустые профили'
            WHEN is_empty = FALSE THEN 'Заполненные профили'
            ELSE 'Не определено'
        END
),
total_count AS (
    SELECT COUNT(*) as total FROM habr_resumes
)
SELECT 
    ps.profile_type as "Категория",
    ps.count as "Количество",
    tc.total as "Всего записей",
    ROUND(ps.count::numeric / tc.total::numeric * 100, 2) as "Процент"
FROM profile_stats ps
CROSS JOIN total_count tc
ORDER BY 
    CASE ps.profile_type
        WHEN 'Заполненные профили' THEN 1
        WHEN 'Пустые профили' THEN 2
        WHEN 'Приватные профили' THEN 3
        ELSE 4
    END;

-- Дополнительная статистика: количество публичных пустых профилей
SELECT 
    'Публичные пустые профили' as "Категория",
    COUNT(*) as "Количество"
FROM habr_resumes
WHERE is_empty = TRUE 
  AND public = TRUE;

-- Дополнительная статистика: количество публичных заполненных профилей
SELECT 
    'Публичные заполненные профили' as "Категория",
    COUNT(*) as "Количество"
FROM habr_resumes
WHERE is_empty = FALSE 
  AND public = TRUE;
