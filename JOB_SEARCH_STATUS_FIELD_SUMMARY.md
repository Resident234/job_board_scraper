# Добавление поля job_sear
ch_status в таблицу habr_resumes

## Обзор

Добавлено новое поле `job_search_status` в таблицу `habr_resumes` для хранения статуса поиска работы пользователя.

## Выполненные изменения

### 1. SQL миграция

Создан файл `sql/alter_resumes_add_job_search_status.sql`:
- Добавлено поле `job_search_status` типа `text`
- Создан индекс для быстрого поиска по статусу
- Добавлен комментарий к полю

### 2. Модель данных

Обновлен `JobBoardScraper/Models/UserProfileData.cs`:
- Добавлено поле `JobSearchStatus` в структуру

### 3. DatabaseClient

Обновлен `JobBoardScraper/DatabaseClient.cs`:
- Добавлен параметр `jobSearchStatus` в метод `DatabaseInsert`
- Обновлены SQL запросы INSERT и UPDATE для включения поля `job_search_status`
- Добавлено логирование статуса поиска работы
- Обновлены все места создания `UserProfileData` для передачи `JobSearchStatus`

### 4. Извлечение данных

Данные извлекаются методом `ProfileDataExtractor.ExtractSalaryAndJobStatus()` и передаются через `EnqueueUserResumeDetail()`.

## Возможные значения

- "Ищу работу"
- "Не ищу работу"
- "Рассматриваю предложения"

## Применение миграции

```bash
psql -U postgres -d jobs -f sql/alter_resumes_add_job_search_status.sql
```

## Проверка

Код успешно компилируется:
```
dotnet build
Сборка успешно выполнено с предупреждениями (6) через 2,7 с
```

## Итог

Статус поиска работы теперь сохраняется в отдельном поле таблицы `habr_resumes`, что позволяет эффективно фильтровать и анализировать пользователей по их статусу поиска работы.
