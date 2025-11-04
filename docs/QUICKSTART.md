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
\c jobs

# Выход
\q
```

## Шаг 2: Создание таблиц

```bash
# Таблица резюме
psql -U postgres -d jobs -f sql/create_resumes_table.sql

# Таблица компаний
psql -U postgres -d jobs -f sql/create_companies_table.sql

# Таблица категорий
psql -U postgres -d jobs -f sql/create_category_root_ids_table.sql

# Индексы
psql -U postgres -d jobs -f sql/create_index.sql

# Дополнительные столбцы
psql -U postgres -d jobs -f sql/add_slogan_column.sql
psql -U postgres -d jobs -f sql/add_unique_link_constraint.sql
psql -U postgres -d jobs -f sql/add_expert_columns.sql
```

## Шаг 3: Настройка конфигурации

Отредактируйте `JobBoardScraper/App.config`:

```xml
<!-- Включите нужные скраперы -->
<add key="Experts:Enabled" value="true" />
<add key="CompanyFollowers:Enabled" value="false" />
<add key="Companies:Enabled" value="false" />
<add key="Category:Enabled" value="false" />
<add key="ResumeList:Enabled" value="false" />
<add key="BruteForce:Enabled" value="false" />

<!-- Настройте подключение к БД -->
<add key="Database:ConnectionString" value="Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;" />
```

## Шаг 4: Создание директории для логов

```bash
mkdir logs
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
[Program] Статистика трафика будет сохраняться в: ./logs/traffic_stats.txt
[Program] Интервал сохранения статистики: 5 минут
[Program] ResumeListPageScraper: ОТКЛЮЧЕН
[Program] CompanyListScraper: ОТКЛЮЧЕН
[Program] CategoryScraper: ОТКЛЮЧЕН
[Program] CompanyFollowersScraper: ОТКЛЮЧЕН
[Program] ExpertsScraper: ВКЛЮЧЕН
[Program] Режим вывода ExpertsScraper: Both
[Program] BruteForceUsernameScraper: ОТКЛЮЧЕН
```

### Логи

```bash
# Просмотр логов ExpertsScraper
tail -f logs/ExpertsScraper_*.log

# Статистика трафика
cat logs/traffic_stats.txt
```

### База данных

```sql
-- Подключение к БД
psql -U postgres -d jobs

-- Проверка данных
SELECT COUNT(*) FROM habr_resumes WHERE expert = TRUE;
SELECT * FROM habr_resumes WHERE expert = TRUE LIMIT 5;
```

## Рекомендуемая конфигурация для начала

### Вариант 1: Только эксперты (быстрый старт)

```xml
<add key="Experts:Enabled" value="true" />
<add key="Experts:OutputMode" value="Both" />
```

**Результат:** Обход экспертов каждые 4 дня, логи в консоль и файл.

### Вариант 2: Эксперты + Компании

```xml
<add key="Experts:Enabled" value="true" />
<add key="Companies:Enabled" value="true" />
<add key="Category:Enabled" value="true" />
```

**Результат:** Сбор экспертов, компаний и категорий.

### Вариант 3: Полный набор (кроме BruteForce)

```xml
<add key="Experts:Enabled" value="true" />
<add key="CompanyFollowers:Enabled" value="true" />
<add key="Companies:Enabled" value="true" />
<add key="Category:Enabled" value="true" />
<add key="ResumeList:Enabled" value="true" />
```

**Результат:** Все скраперы работают параллельно.

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

1. Изучите [README.md](JobBoardScraper/README.md) для подробной документации
2. Настройте режимы вывода для каждого скрапера
3. Оптимизируйте параметры в `App.config`
4. Изучите [TRAFFIC_OPTIMIZATION.md](docs/TRAFFIC_OPTIMIZATION.md) для экономии трафика

## Полезные команды

```bash
# Просмотр всех логов
ls -la logs/

# Очистка логов
rm logs/*.log

# Проверка размера БД
psql -U postgres -d jobs -c "SELECT pg_size_pretty(pg_database_size('jobs'));"

# Экспорт данных
psql -U postgres -d jobs -c "COPY (SELECT * FROM habr_resumes WHERE expert = TRUE) TO '/tmp/experts.csv' CSV HEADER;"
```
