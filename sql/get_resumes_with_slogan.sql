SELECT *
FROM habr_resumes
WHERE slogan IS NOT NULL
  AND TRIM(slogan) <> '';