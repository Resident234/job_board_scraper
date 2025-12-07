-- Выборка навыков пользователя по ID профиля (резюме)
-- Параметр: :profile_id - ID профиля из таблицы habr_resumes

SELECT * FROM habr_skills;

SELECT * FROM habr_user_skills WHERE user_id = 103203;

-- Вариант 1: Получить навыки с полной информацией
SELECT 
    hs.id AS skill_id,
    hs.title AS skill_name,
    hs.skill_id AS habr_skill_id,
    hus.created_at AS added_at
FROM habr_user_skills hus
JOIN habr_skills hs ON hus.skill_id = hs.id
WHERE hus.user_id = 103203
ORDER BY hs.title;

-- Вариант 2: Получить только названия навыков (простой список)
-- SELECT hs.title
-- FROM habr_user_skills hus
-- JOIN habr_skills hs ON hus.skill_id = hs.id
-- WHERE hus.user_id = :profile_id
-- ORDER BY hs.title;

-- Вариант 3: Получить навыки с информацией о профиле
-- SELECT 
--     hr.code AS user_code,
--     hr.title AS user_name,
--     hs.title AS skill_name
-- FROM habr_user_skills hus
-- JOIN habr_skills hs ON hus.skill_id = hs.id
-- JOIN habr_resumes hr ON hus.user_id = hr.id
-- WHERE hus.user_id = :profile_id
-- ORDER BY hs.title;

-- Пример использования:
-- SELECT * FROM habr_user_skills hus
-- JOIN habr_skills hs ON hus.skill_id = hs.id
-- WHERE hus.user_id = 12345;
