-- Проверка логики определения пустых профилей
-- Этот запрос сравнивает флаг is_empty с фактическим состоянием данных

-- 1. Профили, помеченные как пустые, но имеющие данные (потенциальные ошибки)
SELECT 
    'Помечены как пустые, но имеют данные' as проверка,
    r.id,
    r.link,
    r.title,
    r.is_empty,
    CASE 
        WHEN r.about IS NOT NULL AND TRIM(r.about) != '' AND r.about != 'Пустой профиль' THEN 'Есть about'
        ELSE NULL
    END as about_status,
    CASE 
        WHEN EXISTS (SELECT 1 FROM habr_user_experience ue WHERE ue.user_id = r.id) THEN 'Есть опыт'
        ELSE NULL
    END as experience_status,
    CASE 
        WHEN EXISTS (SELECT 1 FROM habr_resumes_universities ru WHERE ru.user_id = r.id) THEN 'Есть образование'
        ELSE NULL
    END as education_status,
    CASE 
        WHEN EXISTS (SELECT 1 FROM habr_resumes_educations re WHERE re.resume_id = r.id) THEN 'Есть доп. образование'
        ELSE NULL
    END as additional_education_status,
    CASE 
        WHEN r.community_participation IS NOT NULL AND jsonb_array_length(r.community_participation) > 0 THEN 'Есть сообщества'
        ELSE NULL
    END as community_status
FROM habr_resumes r
WHERE r.is_empty = TRUE
  AND (
      -- Есть не пустой about (кроме "Пустой профиль")
      (r.about IS NOT NULL AND TRIM(r.about) != '' AND r.about != 'Пустой профиль' 
       AND r.about != 'Доступ ограничен настройками приватности' AND r.about != 'Ошибка 404')
      OR
      -- Есть опыт работы
      EXISTS (SELECT 1 FROM habr_user_experience ue WHERE ue.user_id = r.id)
      OR
      -- Есть высшее образование
      EXISTS (SELECT 1 FROM habr_resumes_universities ru WHERE ru.user_id = r.id)
      OR
      -- Есть дополнительное образование
      EXISTS (SELECT 1 FROM habr_resumes_educations re WHERE re.resume_id = r.id)
      OR
      -- Есть участие в профсообществах
      (r.community_participation IS NOT NULL AND jsonb_array_length(r.community_participation) > 0)
  );

-- 2. Профили, помеченные как заполненные, но не имеющие данных (потенциальные ошибки)
-- Исключаем профили с "Доступ ограничен" и "Ошибка 404" - они не считаются пустыми
SELECT 
    'Помечены как заполненные, но не имеют данных' as проверка,
    r.id,
    r.link,
    r.title,
    r.is_empty,
    r.about
FROM habr_resumes r
WHERE r.is_empty = FALSE
  AND (r.about IS NULL OR TRIM(r.about) = '' OR r.about = 'Пустой профиль')
  AND r.about != 'Доступ ограничен настройками приватности'
  AND r.about != 'Ошибка 404'
  AND NOT EXISTS (SELECT 1 FROM habr_user_experience ue WHERE ue.user_id = r.id)
  AND NOT EXISTS (SELECT 1 FROM habr_resumes_universities ru WHERE ru.user_id = r.id)
  AND NOT EXISTS (SELECT 1 FROM habr_resumes_educations re WHERE re.resume_id = r.id)
  AND (r.community_participation IS NULL OR jsonb_array_length(r.community_participation) = 0);

-- 3. Профили с неопределенным статусом (is_empty = NULL)
SELECT 
    'Статус не определен (NULL)' as проверка,
    COUNT(*) as количество
FROM habr_resumes
WHERE is_empty IS NULL;

-- 4. Общая статистика корректности
WITH validation_stats AS (
    SELECT 
        COUNT(*) FILTER (WHERE is_empty = TRUE) as marked_empty,
        COUNT(*) FILTER (WHERE is_empty = FALSE) as marked_filled,
        COUNT(*) FILTER (WHERE is_empty IS NULL) as marked_null,
        COUNT(*) as total
    FROM habr_resumes
)
SELECT 
    'Общая статистика' as проверка,
    marked_empty as "Помечено пустых",
    marked_filled as "Помечено заполненных",
    marked_null as "Не определено",
    total as "Всего",
    ROUND(marked_empty::numeric / total::numeric * 100, 2) as "% пустых",
    ROUND(marked_filled::numeric / total::numeric * 100, 2) as "% заполненных"
FROM validation_stats;
