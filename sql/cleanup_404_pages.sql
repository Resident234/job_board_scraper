-- ============================================================
-- Периодическая очистка 404 страниц из таблицы habr_resumes
-- ============================================================

-- 1. Функция для удаления 404 записей
CREATE OR REPLACE FUNCTION cleanup_404_resumes()
RETURNS INTEGER AS $$
DECLARE
    deleted_count INTEGER;
BEGIN
    DELETE FROM habr_resumes
    WHERE title LIKE '%Ошибка 404%'
       OR about LIKE '%Ошибка 404%';

    GET DIAGNOSTICS deleted_count = ROW_COUNT;

    -- Логируем результат (можно увидеть в логах PostgreSQL)
    RAISE NOTICE 'Очистка 404: удалено % записей на %', deleted_count, NOW();

    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

-- 2. Представление для просмотра текущих 404 записей
CREATE OR REPLACE VIEW v_404_resumes AS
SELECT id, link, title, about, created_at, updated_at
FROM habr_resumes
WHERE title LIKE '%Ошибка 404%'
   OR about LIKE '%Ошибка 404%';

-- 3. Для периодического выполнения используйте pg_cron (если установлен):
-- SELECT cron.schedule('cleanup-404-resumes', '0 3 * * *', 'SELECT cleanup_404_resumes()');
-- Это запустит очистку каждый день в 3:00 UTC

-- Если pg_cron не установлен, используйте системный cron:
-- 0 3 * * * psql -d jobs -c "SELECT cleanup_404_resumes();"

-- Для ручного запуска:
-- SELECT cleanup_404_resumes();

-- Для просмотра 404 записей:
-- SELECT * FROM v_404_resumes;
