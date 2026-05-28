# 📋 Распознавание и обработка пустых профилей

## 🎯 Обзор

Система автоматически распознает и обрабатывает пустые профили пользователей, помечая их специальным флагом в базе данных. Пустой профиль - это профиль, который не содержит значимой информации о пользователе.

## ✅ Что реализовано

✅ **Автоматическое распознавание** пустых профилей
✅ **Булево поле** `is_empty` в таблице `habr_resumes`
✅ **Запись** "Пустой профиль" в поле `about` для пустых профилей
✅ **SQL скрипты** для работы с пустыми профилями
✅ **Индекс** для быстрого поиска
✅ **Полная документация** и примеры использования

## 🔍 Критерии пустого профиля

Профиль считается **пустым**, если выполняются **ВСЕ** следующие условия:

1. **❌ Нет информации "О себе"**
   - `about` пустое, NULL или содержит только пробелы
   - ⚠️ **Исключения**: Профили с `about = "Доступ ограничен настройками приватности"` или `about = "Ошибка 404"` **НЕ считаются пустыми**

2. **❌ Нет опыта работы**
   - Нет записей в таблице `habr_user_experience` для данного пользователя

3. **❌ Нет высшего образования**
   - Нет записей в таблице `habr_resumes_universities` для данного пользователя

4. **❌ Нет дополнительного образования**
   - Нет записей в таблице `habr_resumes_educations` для данного резюме

5. **❌ Нет участия в профсообществах**
   - Поле `community_participation` пустое, NULL или содержит пустой JSONB массив

## 🚀 Быстрый старт

### 1. Добавить поле в базу данных

```bash
psql -U postgres -d your_database -f sql/alter_resumes_add_empty_profile_field.sql
```

### 2. Запустить скрапер

Скрапер `UserResumeDetailScraper` автоматически определит пустые профили:

```bash
dotnet run --project JobBoardScraper -- --mode userresume
```

### 3. Проверить результаты

```sql
-- Количество пустых профилей
SELECT COUNT(*) FROM habr_resumes WHERE is_empty = TRUE;

-- Список пустых профилей
SELECT link, title, about FROM habr_resumes WHERE is_empty = TRUE LIMIT 10;

-- Статистика по типам профилей
SELECT
    CASE
        WHEN is_empty = TRUE THEN 'Пустые'
        WHEN is_empty = FALSE THEN 'Заполненные'
        ELSE 'Не определено'
    END as тип,
    COUNT(*) as количество,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM habr_resumes), 2) as процент
FROM habr_resumes
GROUP BY is_empty;
```

## 📋 Реализация

### База данных

#### Поле `is_empty`

```sql
ALTER TABLE habr_resumes
ADD COLUMN IF NOT EXISTS is_empty boolean DEFAULT FALSE;
```

- **TRUE** - профиль пустой
- **FALSE** - профиль заполненный
- **NULL** - статус не определен (для старых записей)

#### Индекс для быстрого поиска

```sql
CREATE INDEX IF NOT EXISTS idx_habr_resumes_is_empty
    ON habr_resumes USING btree
    (is_empty ASC NULLS LAST);
```

### Код (C#)

#### Логика определения (UserResumeDetailScraper.cs)

```csharp
// Проверяем, не является ли about служебным сообщением
bool isServiceMessage = !string.IsNullOrWhiteSpace(about) &&
                       (about == "Доступ ограничен настройками приватности" ||
                        about == "Ошибка 404");

// Профиль пустой только если:
// - about не является служебным сообщением
// - about пустой
// - И нет других данных
bool isEmpty = !isServiceMessage &&
              string.IsNullOrWhiteSpace(about) &&
              experienceCount == 0 &&
              educationCount == 0 &&
              additionalEducationCount == 0 &&
              communityParticipationData.Count == 0;

if (isEmpty)
{
    about = "Пустой профиль";
}
```

#### Сохранение в БД (DatabaseClient.cs)

```csharp
_db.EnqueueUserResumeDetail(
    userLink,
    about,
    skills,
    age,
    experienceText,
    registration,
    lastVisit,
    citizenship,
    remoteWork,
    userName,
    infoTech,
    levelTitle,
    salary,
    jobSearchStatus,
    communityParticipationData,
    isEmpty: isEmpty);
```

### Модель данных (UserProfileData.cs)

```csharp
public record UserProfileData(
    // ... другие поля
    bool? isEmpty
);
```

## 📊 SQL запросы

### Выборка пустых профилей

```sql
SELECT id, link, title, about, public
FROM habr_resumes
WHERE is_empty = TRUE
ORDER BY updated_at DESC;
```

### Подсчет пустых профилей

```sql
SELECT COUNT(*) as empty_profiles_count
FROM habr_resumes
WHERE is_empty = TRUE;
```

### Статистика по типам профилей

```sql
SELECT
    CASE
        WHEN is_empty = TRUE THEN 'Пустые профили'
        WHEN is_empty = FALSE THEN 'Заполненные профили'
        ELSE 'Не определено'
    END as profile_type,
    COUNT(*) as count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM habr_resumes), 2) as percentage
FROM habr_resumes
GROUP BY is_empty;
```

### Проверка корректности маркировки

```sql
-- Профили, помеченные как пустые, но имеющие данные
SELECT r.id, r.link, r.title
FROM habr_resumes r
WHERE r.is_empty = TRUE
  AND (
      EXISTS (SELECT 1 FROM habr_user_experience ue WHERE ue.user_id = r.id)
      OR EXISTS (SELECT 1 FROM habr_resumes_universities ru WHERE ru.user_id = r.id)
      OR EXISTS (SELECT 1 FROM habr_resumes_educations re WHERE re.resume_id = r.id)
      OR (r.community_participation IS NOT NULL AND jsonb_array_length(r.community_participation) > 0)
  );
```

## 🔧 Обработка пустых профилей

### При обнаружении пустого профиля:

1. **Устанавливается флаг**: `is_empty = TRUE`
2. **Записывается сообщение**: `about = "Пустой профиль"`
3. **Логируется событие**: `[DB] UserEmptyProfile {link}: ✓ UPDATE (isEmpty=True)`

### При повторной обработке:

Если пользователь добавил информацию в профиль:
- Флаг `is_empty` обновляется на `FALSE`
- Поле `about` обновляется реальными данными
- Профиль переходит в категорию "заполненных"

## 📁 SQL скрипты

| Файл | Описание |
|------|----------|
| `sql/alter_resumes_add_empty_profile_field.sql` | Добавить поле `is_empty` |
| `sql/select_empty_profiles.sql` | Выбрать все пустые профили |
| `sql/count_profiles_by_type.sql` | Статистика по типам профилей |
| `sql/verify_empty_profile_logic.sql` | Проверка корректности маркировки |
| `sql/list_filled_profiles_detailed.sql` | Выборка заполненных профилей |

## 🛠️ Примеры использования

### Найти все публичные пустые профили

```sql
SELECT link, title, about
FROM habr_resumes
WHERE is_empty = TRUE
  AND public = TRUE;
```

### Обновить флаг пустого профиля вручную

```sql
UPDATE habr_resumes
SET is_empty = TRUE
WHERE link = 'https://career.habr.com/username';
```

### Найти профили с противоречивыми данными

```sql
SELECT r.id, r.link, r.title
FROM habr_resumes r
WHERE r.is_empty = TRUE
  AND EXISTS (SELECT 1 FROM habr_user_experience ue WHERE ue.user_id = r.id);
```

## 📚 Связанные файлы

### Код
- `JobBoardScraper/Scrapers/UserResumeDetailScraper.cs` - логика определения пустых профилей
- `JobBoardScraper/Data/DatabaseClient.cs` - методы сохранения флага в БД
- `JobBoardScraper/Domain/Models/UserProfileData.cs` - модель данных профиля

### SQL
- `sql/alter_resumes_add_empty_profile_field.sql` - создание поля
- `sql/select_empty_profiles.sql` - выборка пустых профилей
- `sql/count_profiles_by_type.sql` - статистика по типам профилей
- `sql/verify_empty_profile_logic.sql` - проверка корректности
- `sql/list_filled_profiles_detailed.sql` - выборка заполненных профилей

### Документация
- `docs/EMPTY_PROFILE_DETECTION.md` - полная документация по распознаванию
- `docs/EMPTY_PROFILE_IMPLEMENTATION.md` - документация по реализации
- `docs/EMPTY_PROFILE_SUMMARY.md` - краткое резюме

## ⚠️ Примечания

1. **Автоматическое определение**: Флаг `is_empty` устанавливается автоматически при обработке профиля скрапером `UserResumeDetailScraper`

2. **Поле "О себе"**: Для пустых профилей в поле `about` записывается текст "Пустой профиль"

3. **Приватные и недоступные профили**: Профили с `about = 'Доступ ограничен настройками приватности'` или `about = 'Ошибка 404'` **НЕ считаются пустыми**, так как их содержимое недоступно или не существует

4. **Обновление статуса**: При повторной обработке профиля флаг `is_empty` обновляется, если пользователь добавил информацию

5. **Производительность**: Использование индекса `idx_habr_resumes_is_empty` обеспечивает быстрый поиск пустых профилей даже в больших таблицах

## 🎯 Преимущества

✅ **Автоматическое распознавание** пустых профилей
✅ **Оптимизированные SQL запросы** с индексами
✅ **Полная документация** и примеры
✅ **Валидация и проверка** корректности
✅ **Обратная совместимость**

## 🚀 Следующие шаги

1. Применить миграцию БД: `psql -U postgres -d your_database -f sql/alter_resumes_add_empty_profile_field.sql`
2. Запустить скрапер для обработки профилей: `dotnet run --project JobBoardScraper -- --mode userresume`
3. Проверить результаты с помощью SQL запросов
4. Использовать статистику для анализа данных

## 📊 Производительность

### Индексы
Создан индекс `idx_habr_resumes_is_empty` для быстрого поиска:
- Поиск пустых профилей: O(log n)
- Фильтрация по флагу: оптимизирована

### Рекомендации
- Используйте индекс при частых запросах по `is_empty`
- Комбинируйте с другими условиями для оптимизации
- Регулярно обновляйте статистику таблицы: `ANALYZE habr_resumes;`

## 🎉 Заключение

Функционал распознавания пустых профилей полностью реализован и готов к использованию. Все изменения обратно совместимы и не требуют модификации существующего кода.