-- Опыт работы: по 4 строки на запись + разделитель
-- Формат: id/user_id/company_id → position → duration → description
-- Между записями одного user_id: ----------- ; между разными user_id: ===========

WITH experience_rows AS (
    SELECT
        ue.id AS experience_id,
        ue.user_id,
        ue.company_id::text AS company_id,
        ue.position,
        ue.duration,
        ue.description
    FROM habr_user_experience ue
    WHERE company_id IS NOT NULL
       OR (position IS NOT NULL AND TRIM(position) <> '')
       OR (duration IS NOT NULL AND TRIM(duration) <> '')
       OR (description IS NOT NULL AND TRIM(description) <> '')
),
numbered AS (
    SELECT
        *,
        LEAD(user_id) OVER (ORDER BY user_id, experience_id) AS next_user_id
    FROM experience_rows
),
formatted_output AS (
    SELECT
        user_id,
        experience_id,
        1 AS line_order,
        experience_id::text || ', ' || user_id::text || ', ' || COALESCE(company_id, '') AS output_line
    FROM numbered

    UNION ALL

    SELECT
        user_id,
        experience_id,
        2,
        COALESCE(position, '')
    FROM numbered

    UNION ALL

    SELECT
        user_id,
        experience_id,
        3,
        COALESCE(duration, '')
    FROM numbered

    UNION ALL

    SELECT
        user_id,
        experience_id,
        4,
        COALESCE(description, '')
    FROM numbered

    UNION ALL

    SELECT
        user_id,
        experience_id,
        5,
        CASE
            WHEN next_user_id IS NOT NULL
             AND next_user_id IS DISTINCT FROM user_id
            THEN '==========='
            ELSE '-----------'
        END
    FROM numbered
)
SELECT output_line
FROM formatted_output
ORDER BY user_id, experience_id, line_order;
