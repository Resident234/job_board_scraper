# Быстрый старт JobBoardScraper

Минимальная инструкция для запуска проекта с нуля.

## Предварительные требования

- ✅ .NET 9.0 SDK
- ✅ PostgreSQL 12+
- ✅ Git (опционально)

## Шаг 1: Настройка базы данных

```bash
# Подключение к PostgreSQL
psql -U postgres

# Создание базы данных
CREATE DATABASE jobs;

# Выход
\q
```

## Шаг 2: Создание таблиц

Выполните SQL-скрипты для создания всех необходимых таблиц:

```bash
# Основные таблицы
psql -U postgres -d jobs -f sql/create_resumes_table.sql
psql -U postgres -d jobs -f sql/create_companies_table.sql
psql -U postgres -d jobs -f sql/create_skills_table.sql
psql -U postgres -d jobs -f sql/create_levels_table.sql
psql -U postgres -d jobs -f sql/create_category_root_ids_table.sql
psql -U postgres -d jobs -f sql/create_user_skills_table.sql
psql -U postgres -d jobs -f sql/create_user_experience_table.sql
psql -U postgres -d jobs -f sql/create_user_experience_skills_table.sql
psql -U postgres -d jobs -f sql/create_universities_table.sql
psql -U postgres -d jobs -f sql/create_resumes_universities_table.sql
psql -U postgres -d jobs -f sql/create_company_reviews_table.sql

# Миграции (ALTER TABLE)
psql -U postgres -d jobs -f sql/alter_companies_add_rating_fields.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_additional_fields.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_community_participation.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_job_search_status.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_empty_profile_field.sql
psql -U postgres -d jobs -f sql/alter_add_timestamps.sql
psql -U postgres -d jobs -f sql/add_is_deleted_column.sql
psql -U postgres -d jobs -f sql/add_expert_columns.sql
psql -U postgres -d jobs -f sql/add_company_details_columns.sql
psql -U postgres -d jobs -f sql/add_user_profile_columns.sql
psql -U postgres -d jobs -f sql/add_unique_link_constraint.sql
psql -U postgres -d jobs -f sql/add_slogan_column.sql
```

## Шаг 3: Настройка конфигурации

Отредактируйте `JobBoardScraper/App.config`:

```xml
<!-- Строка подключения к БД -->
<add key="Database:ConnectionString" value="Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;" />

<!-- Включите нужные скраперы (по умолчанию включен только UserResumeDetailScraper) -->
<add key="UserResumeDetail:Enabled" value="true" />
<add key="CompanyRating:Enabled" value="false" />
<add key="Experts:Enabled" value="false" />
<add key="CompanyFollowers:Enabled" value="false" />
<add key="Companies:Enabled" value="false" />
<add key="Category:Enabled" value="false" />
<add key="ResumeList:Enabled" value="false" />
<add key="BruteForce:Enabled" value="false" />
<add key="UserProfile:Enabled" value="false" />
<add key="UserFriends:Enabled" value="false" />
<add key="CompanyDetail:Enabled" value="false" />
```

## Шаг 4: Создание директорий

```bash
mkdir logs data
```

## Шаг 5: Запуск приложения

```bash
# Запуск в режиме разработки
dotnet run --project JobBoardScraper

# Или сборка и запуск
dotnet build JobBoardScraper/JobBoardScraper.csproj -c Release
cd JobBoardScraper/bin/Release/net9.0
./JobBoardScraper.exe
```

## Шаг 6: Проверка работы

### Консольный вывод

Вы должны увидеть:

```
[Program] UserResumeDetailScraper: ВКЛЮЧЕН
[Program] CompanyRatingScraper: ОТКЛЮЧЕН
[Program] ResumeListPageScraper: ОТКЛЮЧЕН
...
```

### Логи

```bash
# Просмотр логов
ls logs/
cat logs/traffic_stats.txt
```

### База данных

```sql
-- Подключение к БД
psql -U postgres -d jobs

-- Проверка данных
SELECT COUNT(*) FROM habr_resumes;
SELECT * FROM habr_resumes LIMIT 5;
```

## Рекомендуемая конфигурация для начала

### Вариант 1: Только UserResumeDetailScraper (по умолчанию)

```xml
<add key="UserResumeDetail:Enabled" value="true" />
<add key="FreeProxy:Enabled" value="true" />
<add key="ProxyWhitelist:Enabled" value="true" />
```

**Результат:** Сбор детальных данных резюме с ротацией бесплатных прокси.

### Вариант 2: Все скраперы (полный сбор данных)

```xml
<add key="ResumeList:Enabled" value="true" />
<add key="Companies:Enabled" value="true" />
<add key="Category:Enabled" value="true" />
<add key="CompanyFollowers:Enabled" value="true" />
<add key="Experts:Enabled" value="true" />
<add key="CompanyDetail:Enabled" value="true" />
<add key="UserProfile:Enabled" value="true" />
<add key="UserFriends:Enabled" value="true" />
<add key="UserResumeDetail:Enabled" value="true" />
<add key="CompanyRating:Enabled" value="true" />
```

### Вариант 3: UserResumeDetailScraper без прокси

```xml
<add key="UserResumeDetail:Enabled" value="true" />
<add key="FreeProxy:Enabled" value="false" />
<add key="ProxyWhitelist:Enabled" value="false" />
```

## Остановка приложения

Нажмите `Ctrl+C` в консоли. Приложение корректно завершит работу:

```
Приложение остановлено пользователем.
Приложение завершено.
```

## Типичные проблемы

### Ошибка подключения к БД

```
Npgsql.NpgsqlException: Connection refused
```

**Решение:** Проверьте, что PostgreSQL запущен и доступен на `localhost:5432`.

### Ошибка "таблица не существует"

```
ERROR: relation "habr_resumes" does not exist
```

**Решение:** Выполните SQL-скрипты из Шага 2.

### Нет логов в файле

**Решение:** Проверьте настройку `OutputMode`:
- `ConsoleOnly` - только консоль
- `FileOnly` - только файл
- `Both` - консоль и файл

### Приложение не запускается

**Решение:** Проверьте версию .NET:

```bash
dotnet --version
# Должно быть 9.0.x
```

## Следующие шаги

1. Изучите [README.md](../README.md) для подробной документации
2. Настройте режимы вывода для каждого скрапера
3. Оптимизируйте параметры в `App.config`
4. Изучите [CONFIGURATION.md](CONFIGURATION.md) для полного списка настроек

## Полезные команды

```bash
# Просмотр всех логов
ls -la logs/

# Очистка логов
rm logs/*.log

# Проверка размера БД
psql -U postgres -d jobs -c "SELECT pg_size_pretty(pg_database_size('jobs'));"

# Экспорт данных
psql -U postgres -d jobs -c "COPY (SELECT * FROM habr_resumes WHERE public = true) TO '/tmp/resumes.csv' CSV HEADER;"