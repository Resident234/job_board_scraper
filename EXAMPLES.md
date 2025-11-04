# Примеры использования JobBoardScraper

Практические примеры конфигурации и использования различных скраперов.

## Содержание

1. [Базовые сценарии](#базовые-сценарии)
2. [Конфигурация скраперов](#конфигурация-скраперов)
3. [SQL-запросы](#sql-запросы)
4. [Анализ данных](#анализ-данных)
5. [Оптимизация производительности](#оптимизация-производительности)

---

## Базовые сценарии

### Сценарий 1: Сбор только экспертов

**Цель:** Быстро собрать базу экспертов с минимальным трафиком.

**App.config:**
```xml
<add key="Experts:Enabled" value="true" />
<add key="Experts:OutputMode" value="Both" />
<add key="Experts:EnableTrafficMeasuring" value="true" />

<!-- Отключаем остальные -->
<add key="BruteForce:Enabled" value="false" />
<add key="ResumeList:Enabled" value="false" />
<add key="Companies:Enabled" value="false" />
<add key="Category:Enabled" value="false" />
<add key="CompanyFollowers:Enabled" value="false" />
```

**Результат:**
- Обход экспертов каждые 4 дня
- Логи в консоль и файл
- Статистика трафика в `./logs/traffic_stats.txt`

**Ожидаемый трафик:** ~5-10 MB за полный обход

---

### Сценарий 2: Сбор экспертов + компании

**Цель:** Собрать экспертов и обновить базу компаний.

**App.config:**
```xml
<add key="Experts:Enabled" value="true" />
<add key="Companies:Enabled" value="true" />
<add key="Category:Enabled" value="true" />
<add key="Companies:OutputMode" value="FileOnly" />
<add key="Experts:OutputMode" value="Both" />
```

**Результат:**
- ExpertsScraper: логи в консоль и файл
- CompanyListScraper: логи только в файл (меньше шума)
- CategoryScraper: обновление списка категорий

**Ожидаемый трафик:** ~50-100 MB за полный обход

---

### Сценарий 3: Полный сбор данных

**Цель:** Максимальный охват всех источников.

**App.config:**
```xml
<add key="Experts:Enabled" value="true" />
<add key="CompanyFollowers:Enabled" value="true" />
<add key="Companies:Enabled" value="true" />
<add key="Category:Enabled" value="true" />
<add key="ResumeList:Enabled" value="true" />
<add key="BruteForce:Enabled" value="false" />

<!-- Логи только в файлы для уменьшения шума -->
<add key="Companies:OutputMode" value="FileOnly" />
<add key="CompanyFollowers:OutputMode" value="FileOnly" />
<add key="Experts:OutputMode" value="FileOnly" />
```

**Результат:**
- Все скраперы работают параллельно
- Минимальный вывод в консоль
- Детальные логи в файлах

**Ожидаемый трафик:** ~500 MB - 1 GB за неделю

---

### Сценарий 4: Агрессивный перебор

**Цель:** Максимальный охват через BruteForce.

**App.config:**
```xml
<add key="BruteForce:Enabled" value="true" />
<add key="BruteForce:MinLength" value="3" />
<add key="BruteForce:MaxLength" value="6" />
<add key="BruteForce:MaxConcurrentRequests" value="10" />
<add key="BruteForce:EnableRetry" value="true" />
<add key="BruteForce:MaxRetries" value="300" />

<!-- Отключаем остальные для фокуса на BruteForce -->
<add key="Experts:Enabled" value="false" />
<add key="CompanyFollowers:Enabled" value="false" />
<add key="Companies:Enabled" value="false" />
```

**⚠️ Внимание:** Очень высокая нагрузка на сервер и большой трафик!

**Ожидаемый трафик:** Несколько GB в день

---

## Конфигурация скраперов

### ExpertsScraper - детальная настройка

```xml
<!-- Основные настройки -->
<add key="Experts:Enabled" value="true" />
<add key="Experts:ListUrl" value="https://career.habr.com/experts?order=lastActive" />

<!-- Режим вывода -->
<add key="Experts:OutputMode" value="Both" />
<!-- Варианты: ConsoleOnly, FileOnly, Both -->

<!-- Измерение трафика -->
<add key="Experts:EnableTrafficMeasuring" value="true" />
```

**Интервал обхода:** Задаётся в коде (`TimeSpan.FromDays(4)`)

**Что извлекается:**
- Имя эксперта
- Ссылка на профиль
- Код пользователя (из URL)
- Стаж работы
- Компания (если указана)

---

### CompanyFollowersScraper - детальная настройка

```xml
<!-- Основные настройки -->
<add key="CompanyFollowers:Enabled" value="true" />
<add key="CompanyFollowers:TimeoutSeconds" value="300" />

<!-- URL и селекторы -->
<add key="CompanyFollowers:UrlTemplate" value="https://career.habr.com/companies/{0}/followers" />
<add key="CompanyFollowers:UserItemSelector" value=".user_friends_item" />
<add key="CompanyFollowers:UsernameSelector" value=".username" />
<add key="CompanyFollowers:SloganSelector" value=".specialization" />

<!-- Режим вывода -->
<add key="CompanyFollowers:OutputMode" value="Both" />

<!-- Измерение трафика -->
<add key="CompanyFollowers:EnableTrafficMeasuring" value="true" />
```

**Интервал обхода:** Задаётся в коде (`TimeSpan.FromDays(5)`)

**Особенности:**
- Использует `AdaptiveConcurrencyController`
- Автоматически регулирует параллелизм
- Режим `UpdateIfExists` для обновления данных

---

### CompanyListScraper - детальная настройка

```xml
<!-- Основные настройки -->
<add key="Companies:Enabled" value="true" />
<add key="Companies:ListUrl" value="https://career.habr.com/companies" />
<add key="Companies:BaseUrl" value="https://career.habr.com/companies/" />

<!-- CSS селекторы -->
<add key="Companies:LinkSelector" value="a[href^='/companies/']" />
<add key="Companies:HrefRegex" value="/companies/([a-zA-Z0-9_-]+)" />
<add key="Companies:NextPageSelector" value="a.page[href*='page={0}']" />

<!-- Режим вывода -->
<add key="Companies:OutputMode" value="FileOnly" />

<!-- Измерение трафика -->
<add key="Companies:EnableTrafficMeasuring" value="true" />
```

**Интервал обхода:** Задаётся в коде (`TimeSpan.FromDays(7)`)

**Фильтры обхода:**
1. Без фильтров
2. По размеру (sz=1..5)
3. По категориям (из БД)
4. Дополнительные фильтры (with_vacancies, with_ratings, и т.д.)

---

## SQL-запросы

### Работа с экспертами

#### Получить всех экспертов
```sql
SELECT 
    id,
    title AS name,
    code,
    work_experience,
    link,
    slogan,
    created_at
FROM habr_resumes 
WHERE expert = TRUE 
ORDER BY created_at DESC;
```

#### Топ-10 экспертов по стажу
```sql
SELECT 
    title AS name,
    work_experience,
    link
FROM habr_resumes 
WHERE expert = TRUE 
  AND work_experience IS NOT NULL
ORDER BY 
    -- Извлекаем число лет из строки "X лет и Y месяцев"
    CAST(SUBSTRING(work_experience FROM '(\d+)') AS INTEGER) DESC
LIMIT 10;
```

#### Эксперты без указанного стажа
```sql
SELECT 
    title AS name,
    code,
    link
FROM habr_resumes 
WHERE expert = TRUE 
  AND work_experience IS NULL;
```

#### Эксперты с определённой специализацией
```sql
SELECT 
    title AS name,
    slogan,
    work_experience,
    link
FROM habr_resumes 
WHERE expert = TRUE 
  AND slogan ILIKE '%python%'
ORDER BY title;
```

---

### Статистика

#### Общая статистика по экспертам
```sql
SELECT 
    COUNT(*) AS total_experts,
    COUNT(work_experience) AS with_experience,
    COUNT(slogan) AS with_slogan,
    COUNT(code) AS with_code,
    ROUND(AVG(CAST(SUBSTRING(work_experience FROM '(\d+)') AS NUMERIC)), 2) AS avg_years_experience
FROM habr_resumes 
WHERE expert = TRUE;
```

#### Распределение экспертов по стажу
```sql
SELECT 
    CASE 
        WHEN work_experience IS NULL THEN 'Не указан'
        WHEN CAST(SUBSTRING(work_experience FROM '(\d+)') AS INTEGER) < 3 THEN '0-2 года'
        WHEN CAST(SUBSTRING(work_experience FROM '(\d+)') AS INTEGER) < 5 THEN '3-4 года'
        WHEN CAST(SUBSTRING(work_experience FROM '(\d+)') AS INTEGER) < 10 THEN '5-9 лет'
        ELSE '10+ лет'
    END AS experience_range,
    COUNT(*) AS count
FROM habr_resumes 
WHERE expert = TRUE
GROUP BY experience_range
ORDER BY 
    CASE experience_range
        WHEN 'Не указан' THEN 0
        WHEN '0-2 года' THEN 1
        WHEN '3-4 года' THEN 2
        WHEN '5-9 лет' THEN 3
        ELSE 4
    END;
```

#### Топ специализаций среди экспертов
```sql
SELECT 
    slogan,
    COUNT(*) AS count
FROM habr_resumes 
WHERE expert = TRUE 
  AND slogan IS NOT NULL
GROUP BY slogan
ORDER BY count DESC
LIMIT 20;
```

---

### Сравнение экспертов и обычных пользователей

#### Сравнение по наличию слогана
```sql
SELECT 
    expert,
    COUNT(*) AS total,
    COUNT(slogan) AS with_slogan,
    ROUND(COUNT(slogan)::NUMERIC / COUNT(*) * 100, 2) AS slogan_percentage
FROM habr_resumes
GROUP BY expert;
```

#### Средний стаж экспертов vs обычных пользователей
```sql
SELECT 
    expert,
    COUNT(*) AS total,
    COUNT(work_experience) AS with_experience,
    ROUND(AVG(CAST(SUBSTRING(work_experience FROM '(\d+)') AS NUMERIC)), 2) AS avg_years
FROM habr_resumes
WHERE work_experience IS NOT NULL
GROUP BY expert;
```

---

## Анализ данных

### Экспорт данных

#### Экспорт всех экспертов в CSV
```sql
COPY (
    SELECT 
        title AS name,
        code,
        work_experience,
        slogan,
        link,
        created_at
    FROM habr_resumes 
    WHERE expert = TRUE
    ORDER BY created_at DESC
) TO '/tmp/experts.csv' CSV HEADER;
```

#### Экспорт экспертов с компаниями
```sql
COPY (
    SELECT 
        r.title AS expert_name,
        r.code AS expert_code,
        r.work_experience,
        r.slogan,
        r.link AS expert_link,
        c.title AS company_name,
        c.code AS company_code,
        c.url AS company_url
    FROM habr_resumes r
    LEFT JOIN habr_companies c ON r.link LIKE '%' || c.code || '%'
    WHERE r.expert = TRUE
    ORDER BY r.title
) TO '/tmp/experts_with_companies.csv' CSV HEADER;
```

---

### Поиск дубликатов

#### Найти дубликаты по коду
```sql
SELECT 
    code,
    COUNT(*) AS count,
    STRING_AGG(title, ', ') AS names
FROM habr_resumes
WHERE code IS NOT NULL
GROUP BY code
HAVING COUNT(*) > 1
ORDER BY count DESC;
```

#### Найти дубликаты по имени
```sql
SELECT 
    title,
    COUNT(*) AS count,
    STRING_AGG(link, ', ') AS links
FROM habr_resumes
WHERE expert = TRUE
GROUP BY title
HAVING COUNT(*) > 1
ORDER BY count DESC;
```

---

## Оптимизация производительности

### Настройка параллелизма

#### Низкая нагрузка (для медленного интернета)
```xml
<add key="BruteForce:MaxConcurrentRequests" value="2" />
<add key="CompanyFollowers:TimeoutSeconds" value="600" />
```

#### Средняя нагрузка (рекомендуется)
```xml
<add key="BruteForce:MaxConcurrentRequests" value="5" />
<add key="CompanyFollowers:TimeoutSeconds" value="300" />
```

#### Высокая нагрузка (для быстрого интернета)
```xml
<add key="BruteForce:MaxConcurrentRequests" value="10" />
<add key="CompanyFollowers:TimeoutSeconds" value="120" />
```

---

### Экономия трафика

#### Отключить измерение трафика (экономия ~5% CPU)
```xml
<add key="BruteForce:EnableTrafficMeasuring" value="false" />
<add key="Companies:EnableTrafficMeasuring" value="false" />
<add key="CompanyFollowers:EnableTrafficMeasuring" value="false" />
<add key="Experts:EnableTrafficMeasuring" value="false" />
```

#### Увеличить интервал сохранения статистики
```xml
<add key="Traffic:SaveIntervalMinutes" value="30" />
<!-- Вместо 5 минут -->
```

---

### Оптимизация логирования

#### Минимальный вывод (только ошибки в консоль)
```xml
<add key="Companies:OutputMode" value="FileOnly" />
<add key="CompanyFollowers:OutputMode" value="FileOnly" />
<add key="Experts:OutputMode" value="FileOnly" />
```

#### Максимальная детализация (для отладки)
```xml
<add key="Companies:OutputMode" value="Both" />
<add key="CompanyFollowers:OutputMode" value="Both" />
<add key="Experts:OutputMode" value="Both" />
```

---

## Мониторинг

### Просмотр логов в реальном времени

#### Windows (PowerShell)
```powershell
Get-Content -Path "logs\ExpertsScraper_*.log" -Wait -Tail 50
```

#### Linux/Mac
```bash
tail -f logs/ExpertsScraper_*.log
```

---

### Анализ статистики трафика

```bash
# Просмотр последней статистики
cat logs/traffic_stats.txt

# Мониторинг изменений
watch -n 60 cat logs/traffic_stats.txt
```

---

### Проверка прогресса в БД

```sql
-- Количество записей за последний час
SELECT 
    COUNT(*) AS new_records
FROM habr_resumes
WHERE created_at > NOW() - INTERVAL '1 hour';

-- Количество экспертов за последний день
SELECT 
    COUNT(*) AS new_experts
FROM habr_resumes
WHERE expert = TRUE 
  AND created_at > NOW() - INTERVAL '1 day';

-- Прогресс по компаниям
SELECT 
    COUNT(*) AS total_companies,
    COUNT(CASE WHEN updated_at > NOW() - INTERVAL '1 week' THEN 1 END) AS updated_this_week
FROM habr_companies;
```

---

## Резервное копирование

### Бэкап базы данных

```bash
# Полный бэкап
pg_dump -U postgres -d jobs > backup_$(date +%Y%m%d).sql

# Только таблица экспертов
pg_dump -U postgres -d jobs -t habr_resumes > backup_resumes_$(date +%Y%m%d).sql

# Только эксперты (данные)
pg_dump -U postgres -d jobs -t habr_resumes --data-only --column-inserts \
  --where="expert = TRUE" > backup_experts_$(date +%Y%m%d).sql
```

### Восстановление

```bash
# Восстановление полного бэкапа
psql -U postgres -d jobs < backup_20241104.sql

# Восстановление только данных
psql -U postgres -d jobs < backup_experts_20241104.sql
```

---

## Автоматизация

### Запуск по расписанию (Windows Task Scheduler)

1. Создайте bat-файл `run_scraper.bat`:
```batch
@echo off
cd C:\path\to\JobBoardScraper
dotnet run --project JobBoardScraper
```

2. Добавьте задачу в Task Scheduler:
   - Триггер: При запуске системы
   - Действие: Запуск `run_scraper.bat`

### Запуск по расписанию (Linux cron)

```bash
# Редактировать crontab
crontab -e

# Добавить строку (запуск при перезагрузке)
@reboot cd /path/to/JobBoardScraper && dotnet run --project JobBoardScraper >> /var/log/scraper.log 2>&1
```

---

## Troubleshooting

### Проблема: Слишком много логов

**Решение:**
```xml
<add key="Companies:OutputMode" value="FileOnly" />
<add key="CompanyFollowers:OutputMode" value="FileOnly" />
```

### Проблема: Высокий трафик

**Решение:**
```xml
<!-- Отключить BruteForce -->
<add key="BruteForce:Enabled" value="false" />

<!-- Уменьшить параллелизм -->
<add key="BruteForce:MaxConcurrentRequests" value="2" />
```

### Проблема: Медленная работа

**Решение:**
```xml
<!-- Увеличить параллелизм -->
<add key="BruteForce:MaxConcurrentRequests" value="10" />

<!-- Уменьшить timeout -->
<add key="CompanyFollowers:TimeoutSeconds" value="120" />
```

### Проблема: Ошибки подключения к БД

**Решение:**
```sql
-- Проверить подключение
psql -U postgres -d jobs -c "SELECT 1;"

-- Проверить права
GRANT ALL PRIVILEGES ON DATABASE jobs TO postgres;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO postgres;
```
