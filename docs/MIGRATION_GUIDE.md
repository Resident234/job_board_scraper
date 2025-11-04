# Руководство по миграции JobBoardScraper

Это руководство поможет обновить существующую установку JobBoardScraper до последней версии с поддержкой экспертов и улучшенной системой логирования.

## Что нового

### Версия 2.0 (Ноябрь 2024)

1. **ExpertsScraper** - новый скрапер для обхода экспертов с career.habr.com
2. **Расширенная структура БД** - добавлены столбцы `code`, `expert`, `work_experience`
3. **SmartHttpClient** - универсальная обёртка с retry и измерением трафика
4. **Улучшенное логирование** - независимые логи для каждого скрапера
5. **Статистика трафика** - автоматический подсчёт и сохранение статистики HTTP-трафика

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
```

### 2. Обновление конфигурации

Добавьте в `App.config` новые настройки для ExpertsScraper:

```xml
<!-- ExpertsScraper Settings -->
<add key="Experts:Enabled" value="true" />
<add key="Experts:ListUrl" value="https://career.habr.com/experts?order=lastActive" />
<add key="Experts:EnableTrafficMeasuring" value="true" />
<add key="Experts:OutputMode" value="Both" />
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
