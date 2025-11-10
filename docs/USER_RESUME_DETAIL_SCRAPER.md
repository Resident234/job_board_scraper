# UserResumeDetailScraper

## Описание

`UserResumeDetailScraper` — это компонент системы JobBoardScraper, который извлекает детальную информацию из резюме пользователей на платформе Habr Career. Скрапер обходит страницы профилей пользователей и собирает:

- **О себе (about)** — текстовое описание пользователя
- **Навыки (skills)** — список технологий и навыков
- **Опыт работы (experience)** — детальная информация о местах работы с компаниями, должностями и навыками

## Основные возможности

- ✅ Периодический обход резюме пользователей (по умолчанию раз в месяц)
- ✅ Извлечение текста "О себе"
- ✅ Извлечение списка навыков
- ✅ Адаптивное управление параллелизмом запросов
- ✅ Сохранение данных в PostgreSQL
- ✅ Гибкая настройка через App.config
- ✅ Поддержка различных режимов вывода (консоль, файл, оба)

## Архитектура

### Основной процесс

1. Получение списка ссылок пользователей из БД
2. Параллельный обход страниц резюме
3. Парсинг HTML для извлечения информации
4. Добавление данных в очередь записи в БД
5. Асинхронная запись в PostgreSQL

### Логика обновления данных

- **О себе и навыки**: Обновляются при каждом обходе
- **Опыт работы**: При добавлении первой записи опыта все старые записи пользователя удаляются (каскадное удаление включает связанные навыки)
- **Компании**: Создаются или обновляются автоматически при парсинге опыта работы

### Извлекаемые данные

#### О себе (about)
- Селектор: `.content-section.content-section--appearance-resume`
- Тип: TEXT
- Таблица: `habr_resumes.about`

#### Навыки (skills)
- Селектор: `.skills-list-show-item`
- Тип: LIST
- Таблицы: `habr_skills`, `habr_user_skills`

#### Опыт работы (experience)
- Контейнер: `.job-experience-item__positions`
- Элементы: `.job-experience-item`
- Извлекаемые данные:
  - Компания (код, URL, название, описание, размер)
  - Должность
  - Продолжительность работы
  - Описание работы
  - Навыки (с ID и названиями)
- Таблицы: `habr_user_experience`, `habr_user_experience_skills`, `habr_companies`, `habr_skills`

## Настройка

### App.config

```xml
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

### Параметры

| Параметр | Тип | По умолчанию | Описание |
|----------|-----|--------------|----------|
| `Enabled` | bool | false | Включить/выключить скрапер |
| `TimeoutSeconds` | int | 60 | Таймаут HTTP-запросов в секундах |
| `EnableRetry` | bool | true | Включить повторные попытки при ошибках |
| `EnableTrafficMeasuring` | bool | true | Включить измерение трафика |
| `OutputMode` | enum | Both | Режим вывода: ConsoleOnly, FileOnly, Both |
| `ContentSelector` | string | .content-section... | CSS-селектор для текста "О себе" |
| `SkillSelector` | string | .skills-list-show-item | CSS-селектор для навыков |
| `ExperienceContainerSelector` | string | .job-experience-item__positions | CSS-селектор контейнера опыта работы |
| `ExperienceItemSelector` | string | .job-experience-item | CSS-селектор элемента опыта |
| `CompanyLinkSelector` | string | a.link-comp... | CSS-селектор ссылки на компанию |
| `CompanyAboutSelector` | string | .job-experience-item__subtitle | CSS-селектор описания компании |
| `PositionSelector` | string | .job-position__title | CSS-селектор должности |
| `DurationSelector` | string | .job-position__duration | CSS-селектор продолжительности |
| `DescriptionSelector` | string | .job-position__message | CSS-селектор описания работы |
| `TagsSelector` | string | .job-position__tags | CSS-селектор контейнера навыков |
| `CompanyCodeRegex` | string | /companies/([^/?]+) | Regex для извлечения кода компании |
| `SkillIdRegex` | string | skills%5B%5D=(\d+) | Regex для извлечения ID навыка |
| `CompanyUrlTemplate` | string | https://career.habr.com/companies/{0} | Шаблон URL компании |
| `CompanySizeUrlPattern` | string | /companies?sz= | Паттерн URL для определения размера компании |

## Структура базы данных

### Таблица habr_resumes

```sql
ALTER TABLE habr_resumes 
ADD COLUMN IF NOT EXISTS about TEXT;
```

### Таблица habr_user_skills

```sql
CREATE TABLE IF NOT EXISTS habr_user_skills (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES habr_resumes(id) ON DELETE CASCADE,
    skill_id INTEGER NOT NULL REFERENCES habr_skills(id) ON DELETE CASCADE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(user_id, skill_id)
);
```

### Таблица habr_user_experience

```sql
CREATE TABLE IF NOT EXISTS habr_user_experience (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES habr_resumes(id) ON DELETE CASCADE,
    company_id INTEGER REFERENCES habr_companies(id) ON DELETE SET NULL,
    position TEXT,
    duration TEXT,
    description TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);
```

### Таблица habr_user_experience_skills

```sql
CREATE TABLE IF NOT EXISTS habr_user_experience_skills (
    id SERIAL PRIMARY KEY,
    experience_id INTEGER NOT NULL REFERENCES habr_user_experience(id) ON DELETE CASCADE,
    skill_id INTEGER NOT NULL REFERENCES habr_skills(id) ON DELETE CASCADE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(experience_id, skill_id)
);
```

## Использование

### Запуск

1. Убедитесь, что выполнены SQL-миграции:
   ```bash
   psql -U postgres -d jobs -f sql/add_user_about_column.sql
   psql -U postgres -d jobs -f sql/create_user_skills_table.sql
   psql -U postgres -d jobs -f sql/create_user_experience_table.sql
   psql -U postgres -d jobs -f sql/create_user_experience_skills_table.sql
   ```

2. Включите скрапер в `App.config`:
   ```xml
   <add key="UserResumeDetail:Enabled" value="true" />
   ```

3. Запустите приложение:
   ```bash
   dotnet run
   ```

### Пример вывода

```
[Program] UserResumeDetailScraper: ВКЛЮЧЕН
[Program] Режим вывода UserResumeDetailScraper: Both
[Program] Timeout UserResumeDetailScraper: 60 секунд
Инициализация UserResumeDetailScraper с режимом вывода: Both
Начало обхода резюме пользователей...
Загружено 1500 пользователей из БД.
HTTP запрос https://career.habr.com/username: 0.234 сек. Код ответа 200. Обработано: 1/1500 (0.07%). Параллельных процессов: 5.
Пользователь https://career.habr.com/username:
  О себе: Опытный разработчик с 5+ годами опыта в веб-разработке...
  Навыки: 15 шт.
  Опыт работы: 3 записей
[DB Queue] UserResumeDetail: https://career.habr.com/username -> About=True, Skills=15
[DB Queue] UserExperience: https://career.habr.com/username -> Company=doczilla, Position=Менеджер по персоналу (Средний), Skills=5
[DB] Добавлен опыт работы для https://career.habr.com/username: Company=Doczilla, Position=Менеджер по персоналу (Средний), Skills=5
```

## Производительность

### Рекомендуемые настройки

- **Параллелизм**: Управляется `AdaptiveConcurrencyController`
- **Интервал обхода**: 30 дней (настраивается в Program.cs)
- **Timeout**: 60 секунд для медленных страниц

### Оптимизация

1. **Адаптивный параллелизм**: Автоматически подстраивается под нагрузку сервера
2. **Очередь записи**: Асинхронная запись в БД не блокирует обход
3. **Повторные попытки**: Автоматические retry при временных ошибках
4. **Каскадное удаление**: При обновлении опыта работы старые записи удаляются автоматически вместе со связанными навыками

## Обработка ошибок

Скрапер обрабатывает следующие типы ошибок:

- ❌ HTTP-ошибки (404, 500, timeout)
- ❌ Ошибки парсинга HTML
- ❌ Ошибки записи в БД
- ❌ Отмена операции (CancellationToken)

Все ошибки логируются с указанием ссылки пользователя и типа ошибки.

## Интеграция с другими компонентами

### DatabaseClient

- `EnqueueUserResumeDetail()` — добавление данных в очередь
- `DatabaseUpdateUserAbout()` — обновление текста "О себе"
- `DatabaseInsertUserSkills()` — вставка навыков
- `EnqueueUserExperience()` — добавление опыта работы в очередь
- `DatabaseInsertUserExperience()` — вставка опыта работы с автоматическим удалением старых записей

### SmartHttpClient

- Управление HTTP-запросами
- Измерение трафика
- Повторные попытки

### AdaptiveConcurrencyController

- Динамическое управление параллелизмом
- Мониторинг задержек
- Оптимизация нагрузки

## Примеры запросов

### Получить пользователей с навыками

```sql
SELECT 
    r.link,
    r.title,
    r.about,
    COUNT(us.skill_id) as skills_count
FROM habr_resumes r
LEFT JOIN habr_user_skills us ON r.id = us.user_id
WHERE r.about IS NOT NULL
GROUP BY r.id, r.link, r.title, r.about
ORDER BY skills_count DESC
LIMIT 10;
```

### Найти пользователей с конкретным навыком

```sql
SELECT 
    r.link,
    r.title,
    s.title as skill
FROM habr_resumes r
JOIN habr_user_skills us ON r.id = us.user_id
JOIN habr_skills s ON us.skill_id = s.id
WHERE s.title = 'Python'
ORDER BY r.title;
```

### Топ навыков среди пользователей

```sql
SELECT 
    s.title,
    COUNT(us.user_id) as users_count
FROM habr_skills s
JOIN habr_user_skills us ON s.id = us.skill_id
GROUP BY s.id, s.title
ORDER BY users_count DESC
LIMIT 20;
```

### Получить опыт работы пользователя с компаниями

```sql
SELECT 
    r.link,
    r.title as user_name,
    c.title as company_name,
    ue.position,
    ue.duration,
    ue.description
FROM habr_user_experience ue
JOIN habr_resumes r ON ue.user_id = r.id
LEFT JOIN habr_companies c ON ue.company_id = c.id
WHERE r.link = 'https://career.habr.com/username'
ORDER BY ue.created_at DESC;
```

### Получить навыки по опыту работы

```sql
SELECT 
    r.link,
    c.title as company_name,
    ue.position,
    s.title as skill
FROM habr_user_experience ue
JOIN habr_resumes r ON ue.user_id = r.id
LEFT JOIN habr_companies c ON ue.company_id = c.id
JOIN habr_user_experience_skills ues ON ue.id = ues.experience_id
JOIN habr_skills s ON ues.skill_id = s.id
WHERE r.link = 'https://career.habr.com/username'
ORDER BY ue.created_at DESC, s.title;
```

### Топ компаний по количеству сотрудников в базе

```sql
SELECT 
    c.title,
    c.code,
    COUNT(ue.id) as employees_count
FROM habr_companies c
JOIN habr_user_experience ue ON c.id = ue.company_id
GROUP BY c.id, c.title, c.code
ORDER BY employees_count DESC
LIMIT 20;
```

## Логирование

### Режимы вывода

- **ConsoleOnly**: Вывод только в консоль
- **FileOnly**: Вывод только в файл
- **Both**: Вывод в консоль и файл

### Файлы логов

Логи сохраняются в директории, указанной в `Logging:OutputDirectory` (по умолчанию `./logs`).

Формат имени файла: `UserResumeDetailScraper_YYYYMMDD_HHmmss.log`

## Troubleshooting

### Проблема: Скрапер не находит данные

**Решение**: Проверьте селекторы в App.config. Структура HTML может измениться.

### Проблема: Ошибки записи в БД

**Решение**: Убедитесь, что выполнены все SQL-миграции и таблицы созданы.

### Проблема: Медленная работа

**Решение**: 
- Увеличьте timeout
- Проверьте настройки `AdaptiveConcurrencyController`
- Убедитесь, что БД не перегружена

## См. также

- [USER_PROFILE_SCRAPER.md](USER_PROFILE_SCRAPER.md) — Скрапер профилей пользователей
- [TRAFFIC_OPTIMIZATION.md](TRAFFIC_OPTIMIZATION.md) — Оптимизация трафика
- [IMPLEMENTATION_CHECKLIST.md](../IMPLEMENTATION_CHECKLIST.md) — Чеклист реализации
