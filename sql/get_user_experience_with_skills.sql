WITH experience_rows AS (
    SELECT
        ue.id AS experience_id,
        ue.user_id,
        ue.company_id,
        ue.position,
        ue.duration,
        ue.description,
        ARRAY_TO_STRING(
            ARRAY(
                SELECT DISTINCT s.title
                FROM habr_user_experience_skills ues
                JOIN habr_skills s ON ues.skill_id = s.id
                WHERE ues.experience_id = ue.id
                ORDER BY s.title
            ),
            ' • '
        ) AS skills
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
combined AS (
    SELECT
        user_id,
        company_id,
        position,
        duration,
        description,
        skills,
        experience_id,
        0 AS sort_offset
    FROM numbered

    UNION ALL

    SELECT
        user_id,
        NULL::INTEGER AS company_id,
        '---' AS position,
        NULL::TEXT AS duration,
        NULL::TEXT AS description,
        NULL::TEXT AS skills,
        experience_id,
        1 AS sort_offset
    FROM numbered
    WHERE next_user_id IS NOT NULL
      AND next_user_id IS DISTINCT FROM user_id
)
SELECT
    user_id,
    company_id,
    position,
    duration,
    description,
    skills
FROM combined
ORDER BY user_id, experience_id, sort_offset;
