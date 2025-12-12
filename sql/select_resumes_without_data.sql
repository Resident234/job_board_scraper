-- Выборка профилей без заполненных данных для UserResumeDetailScraper
-- Противоположный фильтр к count_filled_profiles.sql
-- Выбирает профили, которые:
-- 1. НЕ приватные
-- 2. НЕ имеют заполненных данных (нет about, нет опыта, нет образования, нет участия в сообществах)

SELECT r.id, r.link, r.title, r.code, r.updated_at
FROM habr_resumes r
WHERE r.link IS NOT NULL
  -- НЕ приватный профиль
  AND NOT (r.public = false AND r.about = 'Доступ ограничен настройками приватности')
  -- НЕТ заполненного about
  AND (r.about IS NULL OR TRIM(r.about) = '')
  -- НЕТ опыта работы
  AND NOT EXISTS (SELECT 1 FROM habr_user_experience ue WHERE ue.user_id = r.id)
  -- НЕТ высшего образования (колонка user_id в habr_resumes_universities)
  AND NOT EXISTS (SELECT 1 FROM habr_resumes_universities ru WHERE ru.user_id = r.id)
  -- НЕТ дополнительного образования (колонка resume_id в habr_resumes_educations)
  AND NOT EXISTS (SELECT 1 FROM habr_resumes_educations re WHERE re.resume_id = r.id)
  -- НЕТ участия в профсообществах
  AND (r.community_participation IS NULL OR jsonb_array_length(r.community_participation) = 0)
ORDER BY r.updated_at ASC NULLS FIRST;

-- Подсчёт количества таких профилей
SELECT COUNT(*) as profiles_without_data
FROM habr_resumes r
WHERE r.link IS NOT NULL
  AND NOT (r.public = false AND r.about = 'Доступ ограничен настройками приватности')
  AND (r.about IS NULL OR TRIM(r.about) = '')
  AND NOT EXISTS (SELECT 1 FROM habr_user_experience ue WHERE ue.user_id = r.id)
  AND NOT EXISTS (SELECT 1 FROM habr_resumes_universities ru WHERE ru.user_id = r.id)
  AND NOT EXISTS (SELECT 1 FROM habr_resumes_educations re WHERE re.resume_id = r.id)
  AND (r.community_participation IS NULL OR jsonb_array_length(r.community_participation) = 0);
