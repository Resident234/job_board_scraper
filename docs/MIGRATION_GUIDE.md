# Руководство по миграции JobBoardScraper

Это руководство поможет обновить существующую установку JobBoardScraper до последней версии с поддержкой экспертов и улучшенной системой логирования.

## Что нового

### Версия 2.0 (Ноябрь 2024)

1. **ExpertsScraper** - новый скрапер для обхода экспертов с career.habr.com
2. **CompanyDetailScraper** - новый скрапер для детальной информации о компаниях (ID, рейтинг, навыки)
3. **UserProfileScraper** - новый скрапер для профилей пользователей (уровень, зарплата, опыт)
4. **UserResumeDetailScraper** - новый скрапер для извлечения "О себе" и навыков из резюме
5. **Расширенная структура БД** - добавлены столбцы для экспертов, компаний, профилей и навыков
6. **SmartHttpClient** - универсальная обёртка с retry и измерением трафика
7. **Улучшенное логирование** - независимые логи для каждого скрапера
8. **Статистика трафика** - автоматический подсчёт и сохранение статистики HTTP-трафика

## Шаги миграции

### 1. Обновление базы данных

Выполните SQL-скрипты для добавления новых столбцов:

```bash
# Добавление столбца slogan (если ещё не добавлен)
psql -U postgres -d jobs -f sql/add_slogan_column.sql

# Добавление уникального ограничения на link (если ещё не добавлено)
psql -U postgres -d jobs -f sql/add_unique_link_constraint.sql

# Добавление столбцов для экспертов (НОВОЕ)
psql -U postgres -d jobs -f sql/add_expert_columns.sql

# Добавление столбцов для детальной информации о компаниях (НОВОЕ)
psql -U postgres -d jobs -f sql/add_company_details_columns.sql

# Создание таблицы уровней (НОВОЕ)
psql -U postgres -d jobs -f sql/create_levels_table.sql

# Добавление столбцов для профилей пользователей (НОВОЕ)
psql -U postgres -d jobs -f sql/add_user_profile_columns.sql

# Добавление столбца "О себе" для резюме (НОВОЕ)
psql -U postgres -d jobs -f sql/add_user_about_column.sql

# Создание таблицы навыков пользователей (НОВОЕ)
psql -U postgres -d jobs -f sql/create_user_skills_table.sql

# Создание таблицы опыта работы (НОВОЕ)
psql -U postgres -d jobs -f sql/create_user_experience_table.sql

# Создание таблицы связи опыта работы и навыков (НОВОЕ)
psql -U postgres -d jobs -f sql/create_user_experience_skills_table.sql
```

### 2. Обновление конфигурации

Добавьте в `App.config` новые настройки для всех новых скраперов:

```xml
<!-- ExpertsScraper Settings -->
<add key="Experts:Enabled" value="true" />
<add key="Experts:ListUrl" value="https://career.habr.com/experts?order=lastActive" />
<add key="Experts:EnableTrafficMeasuring" value="true" />
<add key="Experts:OutputMode" value="Both" />

<!-- CompanyDetailScraper Settings -->
<add key="CompanyDetail:Enabled" value="false" />
<add key="CompanyDetail:TimeoutSeconds" value="60" />
<add key="CompanyDetail:EnableRetry" value="true" />
<add key="CompanyDetail:EnableTrafficMeasuring" value="true" />
<add key="CompanyDetail:OutputMode" value="Both" />

<!-- UserProfileScraper Settings -->
<add key="UserProfile:Enabled" value="false" />
<add key="UserProfile:TimeoutSeconds" value="60" />
<add key="UserProfile:EnableRetry" value="true" />
<add key="UserProfile:EnableTrafficMeasuring" value="true" />
<add key="UserProfile:OutputMode" value="Both" />

<!-- UserResumeDetailScraper Settings -->
<add key="UserResumeDetail:Enabled" value="false" />
<add key="UserResumeDetail:TimeoutSeconds" value="60" />
<add key="UserResumeDetail:EnableRetry" value="true" />
<add key="UserResumeDetail:EnableTrafficMeasuring" value="true" />
<add key="UserResumeDetail:OutputMode" value="Both" />
<add key="UserResumeDetail:ContentSelector" value=".content-section.content-section--appearance-resume" />
<add key="UserResumeDetail:SkillSelector" value=".skills-list-show-item" />
<add key="UserResumeDetail:ExperienceContainerSelector" value=".job-experience-item__positions" />
<add key="UserResumeDetail:ExperienceItemSelector" value=".job-experience-item" />
<add key="UserResumeDetail:CompanyLinkSelector" value="a.link-comp.link-comp--appearance-dark" />
<add key="UserResumeDetail:CompanyAboutSelector" value=".job-experience-item__subtitle" />
<add key="UserResumeDetail:PositionSelector" value=".job-position__title" />
<add key="UserResumeDetail:DurationSelector" value=".job-position__duration" />
<add key="UserResumeDetail:DescriptionSelector" value=".job-position__message" />
<add key="UserResumeDetail:TagsSelector" value=".job-position__tags" />
<add key="UserResumeDetail:CompanyCodeRegex" value="/companies/([^/?]+)" />
<add key="UserResumeDetail:SkillIdRegex" value="skills%5B%5D=(\d+)" />
<add key="UserResumeDetail:CompanyUrlTemplate" value="https://career.habr.com/companies/{0}" />
<add key="UserResumeDetail:CompanySizeUrlPattern" value="/companies?sz=" />
```

Также добавьте настройки статистики трафика (если их нет):

```xml
<!-- Traffic Statistics Settings -->
<add key="Traffic:OutputFile" value="./logs/traffic_stats.txt" />
<add key="Traffic:SaveIntervalMinutes" value="5" />
```

### 3. Создание директории для логов

```bash
mkdir logs
```

### 4. Пересборка проекта

```bash
dotnet build JobBoardScraper/JobBoardScraper.csproj -c Release
```

### 5. Запуск приложения

```bash
dotnet run --project JobBoardScraper
```

При запуске вы увидите статус всех скраперов:

```
[Program] ResumeListPageScraper: ОТКЛЮЧЕН
[Program] CompanyListScraper: ОТКЛЮЧЕН
[Program] CategoryScraper: ОТКЛЮЧЕН
[Program] CompanyFollowersScraper: ВКЛЮЧЕН
[Program] ExpertsScraper: ВКЛЮЧЕН
[Program] BruteForceUsernameScraper: ОТКЛЮЧЕН
```

## Проверка миграции

### Проверка структуры БД

```sql
-- Проверка наличия новых столбцов
\d habr_resumes

-- Должны быть столбцы:
-- - code (text)
-- - expert (boolean)
-- - work_experience (text)
```

### Проверка работы ExpertsScraper

После запуска проверьте логи:

```bash
# Консольный вывод
tail -f logs/ExpertsScraper_*.log

# Статистика трафика
cat logs/traffic_stats.txt
```

### Проверка данных в БД

```sql
-- Количество экспертов
SELECT COUNT(*) FROM habr_resumes WHERE expert = TRUE;

-- Примеры записей экспертов
SELECT title, code, work_experience, link 
FROM habr_resumes 
WHERE expert = TRUE 
LIMIT 10;
```

## Откат изменений

Если что-то пошло не так, вы можете откатить изменения:

### Откат изменений в БД

```sql
-- Удаление столбцов экспертов
ALTER TABLE habr_resumes DROP COLUMN IF EXISTS code;
ALTER TABLE habr_resumes DROP COLUMN IF EXISTS expert;
ALTER TABLE habr_resumes DROP COLUMN IF EXISTS work_experience;

-- Удаление индексов
DROP INDEX IF EXISTS idx_habr_resumes_code;
DROP INDEX IF EXISTS idx_habr_resumes_expert;
```

### Откат конфигурации

Просто отключите ExpertsScraper в `App.config`:

```xml
<add key="Experts:Enabled" value="false" />
```

## Часто задаваемые вопросы

### Q: Нужно ли останавливать приложение для миграции?

**A:** Да, рекомендуется остановить приложение перед выполнением SQL-скриптов.

### Q: Что делать, если скрипт add_expert_columns.sql выдаёт ошибку?

**A:** Скрипт использует `IF NOT EXISTS`, поэтому безопасен для повторного выполнения. Если ошибка сохраняется, проверьте права доступа к БД.

### Q: Можно ли запустить только ExpertsScraper?

**A:** Да, установите `Experts:Enabled = true` и отключите остальные скраперы.

### Q: Как часто ExpertsScraper обходит страницы?

**A:** По умолчанию каждые 4 дня. Интервал задаётся в коде (`TimeSpan.FromDays(4)`).

### Q: Где хранятся логи ExpertsScraper?

**A:** В директории `./logs/` с именем `ExpertsScraper_{timestamp}.log` (если `OutputMode = Both` или `FileOnly`).

### Q: Сколько трафика потребляет ExpertsScraper?

**A:** Зависит от количества страниц. Статистика сохраняется в `./logs/traffic_stats.txt`.

## Поддержка

Если у вас возникли проблемы с миграцией:

1. Проверьте логи в директории `./logs/`
2. Проверьте статус БД: `psql -U postgres -d jobs`
3. Убедитесь, что все SQL-скрипты выполнены успешно
4. Проверьте конфигурацию в `App.config`

## Дополнительные ресурсы

- [README.md](JobBoardScraper/README.md) - основная документация
- [sql/README.md](sql/README.md) - документация по SQL-скриптам
- [TRAFFIC_OPTIMIZATION.md](docs/TRAFFIC_OPTIMIZATION.md) - оптимизация трафика
