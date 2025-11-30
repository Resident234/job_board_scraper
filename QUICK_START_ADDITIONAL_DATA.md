# Быстрый старт: Дополнительные данные профиля

## Шаг 1: Миграция базы данных

Выполните SQL скрипт для добавления новых полей:

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

## Шаг 2: Сборка проекта

```bash
dotnet build
```

## Шаг 3: Запуск скрапера

Скрапер автоматически начнет извлекать дополнительные данные:

```bash
dotnet run
```

## Шаг 4: Проверка данных

### Просмотр извлеченных данных

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

### Статистика по гражданству

```sql
SELECT 
    citizenship,
    COUNT(*) as count
FROM habr_resumes
WHERE citizenship IS NOT NULL
GROUP BY citizenship
ORDER BY count DESC;
```

### Пользователи готовые к удаленной работе

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

## Что извлекается

Из профиля пользователя теперь извлекаются:

```
Возраст: 37 лет
Опыт работы (текст): 9 лет и 1 месяц
Регистрация: 30.08.2022
Последний визит: 2 дня назад
Гражданство: Россия
Дополнительно: готов к удаленной работе
```

## Пример вывода в консоль

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

## Обратная совместимость

Старый код продолжит работать без изменений. Новые поля опциональны и допускают NULL значения.

## Полезные запросы

### Поиск по возрасту

```sql
SELECT * FROM habr_resumes
WHERE age LIKE '%37%'
  AND public = true;
```

### Поиск по дате регистрации

```sql
SELECT * FROM habr_resumes
WHERE registration LIKE '%2022%'
  AND public = true;
```

### Экспорт данных в CSV

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

## Troubleshooting

### Поля не заполняются

1. Проверьте, что миграция выполнена успешно:
   ```sql
   \d habr_resumes
   ```

2. Проверьте логи скрапера на наличие ошибок

3. Убедитесь, что профили публичные

### Данные извлекаются некорректно

1. Проверьте HTML структуру страницы
2. Убедитесь, что селекторы актуальны
3. Проверьте логи на предмет ошибок парсинга

## Дополнительная информация

Подробная документация: `docs/USER_ADDITIONAL_DATA_EXTRACTION.md`
