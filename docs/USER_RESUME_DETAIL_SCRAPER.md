# UserResumeDetailScraper

## Описание

`UserResumeDetailScraper` — это компонент системы JobBoardScraper, который извлекает детальную информацию из резюме пользователей на платформе Habr Career. Скрапер обходит страницы профилей пользователей и собирает:

- **О себе (about)** — текстовое описание пользователя
- **Навыки (skills)** — список технологий и навыков

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

### Извлекаемые данные

#### О себе (about)
- Селектор: `.content-section.content-section--appearance-resume`
- Тип: TEXT
- Таблица: `habr_resumes.about`

#### Навыки (skills)
- Селектор: `.skills-list-show-item`
- Тип: LIST
- Таблицы: `habr_skills`, `habr_user_skills`

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

## Использование

### Запуск

1. Убедитесь, что выполнены SQL-миграции:
   ```bash
   psql -U postgres -d jobs -f sql/add_user_about_column.sql
   psql -U postgres -d jobs -f sql/create_user_skills_table.sql
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
[DB Queue] UserResumeDetail: https://career.habr.com/username -> About=True, Skills=15
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
