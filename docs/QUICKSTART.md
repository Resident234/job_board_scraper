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

## Быстрый старт: дополнительные данные профиля

Этот раздел объединяет инструкции из `QUICK_START_ADDITIONAL_DATA.md` и `QUICK_START_ADDITIONAL_DATA_1.md`.

### Шаг 1: Миграция базы данных

Выполните SQL-скрипт для добавления новых полей:

```bash
psql -U postgres -d habr_career -f sql/alter_resumes_add_additional_fields.sql
```

Или вручную:

```sql
-- Добавляем новые поля
ALTER TABLE habr_resumes ADD COLUMN IF NOT EXISTS age text;
ALTER TABLE habr_resumes ADD COLUMN IF NOT EXISTS registration text;
ALTER TABLE habr_resumes ADD COLUMN IF NOT EXISTS citizenship text;
ALTER TABLE habr_resumes ADD COLUMN IF NOT EXISTS remote_work text;

-- Создаем индекс для поиска по гражданству
CREATE INDEX IF NOT EXISTS idx_habr_resumes_citizenship
    ON habr_resumes USING btree (citizenship);
```

### Шаг 2: Сборка проекта

```bash
dotnet build
```

### Шаг 3: Запуск скрапера

Скрапер автоматически начнет извлекать дополнительные данные:

```bash
dotnet run
```

### Шаг 4: Проверка данных

#### Просмотр извлеченных данных

```sql
SELECT
    link,
    title,
    age,
    experience_text,
    registration,
    citizenship,
    remote_work
FROM habr_resumes
WHERE age IS NOT NULL
ORDER BY created_at DESC
LIMIT 10;
```

#### Статистика по гражданству

```sql
SELECT
    citizenship,
    COUNT(*) as count
FROM habr_resumes
WHERE citizenship IS NOT NULL
GROUP BY citizenship
ORDER BY count DESC;
```

#### Пользователи готовые к удаленной работе

```sql
SELECT
    link,
    title,
    citizenship,
    remote_work
FROM habr_resumes
WHERE remote_work LIKE '%удаленной%'
  AND public = true;
```

### Что извлекается

Из профиля пользователя теперь извлекаются:

```
Возраст: 37 лет
Опыт работы (текст): 9 лет и 1 месяц
Регистрация: 30.08.2022
Последний визит: 2 дня назад
Гражданство: Россия
Дополнительно: готов к удаленной работе
```

### Пример вывода в консоль

```
Пользователь https://career.habr.com/username:
  О себе: Опытный разработчик с 9-летним опытом...
  Навыки: 15 шт.
  Опыт работы: 3 записей
  Возраст: 37 лет
  Опыт работы (текст): 9 лет и 1 месяц
  Регистрация: 30.08.2022
  Гражданство: Россия
  Удаленная работа: готов к удаленной работе
  Статус: публичный профиль
```

### Обратная совместимость

Старый код продолжит работать без изменений. Новые поля опциональны и допускают NULL значения.

### Полезные запросы

#### Поиск по возрасту

```sql
SELECT * FROM habr_resumes
WHERE age LIKE '%37%'
  AND public = true;
```

#### Поиск по дате регистрации

```sql
SELECT * FROM habr_resumes
WHERE registration LIKE '%2022%'
  AND public = true;
```

#### Экспорт данных в CSV

```sql
COPY (
    SELECT
        link,
        title,
        age,
        experience_text,
        registration,
        citizenship,
        remote_work
    FROM habr_resumes
    WHERE public = true
      AND age IS NOT NULL
) TO '/tmp/users_with_additional_data.csv'
WITH CSV HEADER;
```

### Troubleshooting: дополнительные данные

#### Поля не заполняются

1. Проверьте, что миграция выполнена успешно:
   ```sql
   \d habr_resumes
   ```

2. Проверьте логи скрапера на наличие ошибок

3. Убедитесь, что профили публичные

#### Данные извлекаются некорректно

1. Проверьте HTML структуру страницы
2. Убедитесь, что селекторы актуальны
3. Проверьте логи на предмет ошибок парсинга

### Дополнительная информация

Подробная документация перенесена в [CHANGELOG.md](../CHANGELOG.md), раздел "Извлечение дополнительных данных профиля".

## Быстрый запуск UserResumeDetailScraper

Этот раздел объединяет инструкции из `QUICK_START_USERRESUME.md` и `QUICK_START_USERRESUME_1.md`.

### ✅ Текущая конфигурация

**Включен:** UserResumeDetailScraper с ротацией прокси  
**Отключены:** Все остальные скраперы

#### 🌐 Прокси

**Статус:** ✅ Включены  
**Режим:** Автоматическая загрузка из публичных источников  
**Ротация:** Для каждой страницы резюме

### 🚀 Запуск UserResumeDetailScraper

#### 1. Проверьте базу данных

Убедитесь, что PostgreSQL запущен и база данных создана:

```bash
psql -U postgres -c "SELECT 1 FROM pg_database WHERE datname='jobs'"
```

Если база не создана:

```bash
psql -U postgres -c "CREATE DATABASE jobs;"
```

#### 2. Выполните SQL-скрипты

Если еще не выполнены:

```bash
psql -U postgres -d jobs -f sql/create_resumes_table.sql
psql -U postgres -d jobs -f sql/create_user_skills_table.sql
psql -U postgres -d jobs -f sql/create_companies_table.sql
```

#### 3. Запустите приложение

```bash
dotnet run --project JobBoardScraper
```

### 📊 Что будет происходить

UserResumeDetailScraper будет:

1. **Загрузить прокси** (если список пуст):
   - Из ProxyScrape API
   - Из GeoNode API
   - Создать пул для ротации

2. **Читать пользователей** из таблицы `habr_resumes`

3. **Для каждого пользователя**:
   - Переключиться на новый прокси
   - Загрузить страницу резюме
   - Извлечь текст "О себе"
   - Извлечь список навыков
   - Извлечь опыт работы
   - Сохранить в базу данных

### 📝 Логи UserResumeDetailScraper

Логи будут выводиться в консоль и файл:

```
[HttpClientFactory] Список прокси пуст. Загрузка из публичных источников...
[HttpClientFactory] ProxyScrape: загружено 50 прокси
[HttpClientFactory] GeoNode: загружено 100 прокси
[HttpClientFactory] ✓ Загружено 100 прокси
[Program] UserResumeDetailScraper: ВКЛЮЧЕН
[Program] UserResumeDetailScraper: Прокси ВКЛЮЧЕНЫ (100 серверов)
[Program] UserResumeDetailScraper: Ротация прокси для каждой страницы
[UserResumeDetailScraper] Начало обработки пользователей...
[UserResumeDetailScraper] Обработка пользователя: username
[UserResumeDetailScraper] Найдено навыков: 15
[UserResumeDetailScraper] Найдено опыта работы: 3
[UserResumeDetailScraper] Сохранено в БД
```

### 🔧 Настройки UserResumeDetailScraper

#### Изменить режим вывода

В `App.config`:

```xml
<!-- Both = консоль + файл, ConsoleOnly = только консоль, FileOnly = только файл -->
<add key="UserResumeDetail:OutputMode" value="Both" />
```

#### Изменить таймаут

```xml
<add key="UserResumeDetail:TimeoutSeconds" value="60" />
```

#### Включить/отключить retry

```xml
<add key="UserResumeDetail:EnableRetry" value="true" />
```

#### Включить/отключить измерение трафика

```xml
<add key="UserResumeDetail:EnableTrafficMeasuring" value="true" />
```

### 🛑 Остановка UserResumeDetailScraper

Нажмите `Ctrl+C` для остановки приложения.

### 📈 Проверка результатов UserResumeDetailScraper

#### Проверить количество обработанных резюме

```sql
SELECT COUNT(*) FROM habr_resumes WHERE about IS NOT NULL;
```

#### Проверить навыки

```sql
SELECT COUNT(*) FROM habr_user_skills;
```

#### Проверить опыт работы

```sql
SELECT COUNT(*) FROM habr_user_experience;
```

#### Посмотреть последние обработанные резюме

```sql
SELECT username, about, updated_at
FROM habr_resumes
WHERE about IS NOT NULL
ORDER BY updated_at DESC
LIMIT 10;
```

### 🔄 Включить другие скраперы

Если нужно включить другие скраперы, отредактируйте `App.config`:

```xml
<!-- Включить UserProfileScraper -->
<add key="UserProfile:Enabled" value="true" />

<!-- Включить CompanyDetailScraper -->
<add key="CompanyDetail:Enabled" value="true" />
```

### 📚 Документация UserResumeDetailScraper

- [USER_RESUME_DETAIL_SCRAPER.md](USER_RESUME_DETAIL_SCRAPER.md) - Полная документация
- [USERRESUME_WITH_PROXY.md](USERRESUME_WITH_PROXY.md) - Использование с прокси
- [CURRENT_CONFIG.md](../CURRENT_CONFIG.md) - Текущая конфигурация
- [App.config](../JobBoardScraper/App.config) - Файл конфигурации
- [PROXY_SERVICES.md](PROXY_SERVICES.md) - Коммерческие прокси-сервисы

### ⚠️ Важно для UserResumeDetailScraper

- Убедитесь, что в таблице `habr_resumes` есть пользователи для обработки
- Проверьте строку подключения к БД в `App.config`
- Скрапер обрабатывает только пользователей с `public = true`

### 🆘 Проблемы UserResumeDetailScraper

#### База данных не подключается

Проверьте строку подключения в `App.config`:

```xml
<add key="Database:ConnectionString" value="Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;" />
```

#### Нет пользователей для обработки

Сначала запустите другие скраперы для сбора пользователей:

```xml
<add key="ResumeList:Enabled" value="true" />
<add key="UserProfile:Enabled" value="true" />
```

#### Ошибки HTTP

Проверьте доступность сайта career.habr.com и настройки прокси (если используются).

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
4. Изучите [TRAFFIC_OPTIMIZATION.md](TRAFFIC_OPTIMIZATION.md) для экономии трафика

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
