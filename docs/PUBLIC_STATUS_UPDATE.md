# Автоматическое обновление статуса публичности профиля

## Описание

UserResumeDetailScraper теперь автоматически устанавливает флаг `public` в таблице `habr_resumes` в зависимости от доступности профиля:
- `public = true` - если удалось успешно извлечь данные со страницы резюме
- `public = false` - если профиль закрыт настройками приватности

## Логика

### Условия

**Публичный профиль:**
Если скрапер смог извлечь данные со страницы резюме (текст "О себе", навыки, опыт работы), это означает, что профиль пользователя **публичный**.

**Приватный профиль:**
Если на странице (в HTML) найден текст "Доступ ограничен настройками приватности", это означает, что профиль **приватный**. Проверка выполняется до парсинга HTML для экономии ресурсов.

### Действия

**Для публичного профиля:**
1. Сохраняются данные резюме (`about`, `skills`, `experience`)
2. Автоматически устанавливается `public = true`
3. Обновляется `updated_at = NOW()`

**Для приватного профиля:**
1. Проверка выполняется сразу в HTML (до парсинга)
2. В поле `about` записывается текст "Доступ ограничен настройками приватности"
3. Автоматически устанавливается `public = false`
4. Навыки и опыт работы не извлекаются и не сохраняются
5. Обновляется `updated_at = NOW()`
6. Сразу переходим к следующему профилю (экономия ресурсов)

## Реализация

### В UserResumeDetailScraper.cs

```csharp
// Получаем HTML страницы
var html = encoding.GetString(htmlBytes);

// Проверяем на приватный профиль сразу в HTML (до парсинга)
const string privateProfileText = "Доступ ограничен настройками приватности";
if (html.Contains(privateProfileText))
{
    // Профиль приватный - сохраняем статус и переходим к следующему
    _db.EnqueueUserResumeDetail(userLink, privateProfileText, new List<string>());
    _db.EnqueueUpdateUserPublicStatus(userLink, isPublic: false);
    
    _logger.WriteLine($"Пользователь {userLink}:");
    _logger.WriteLine($"  Статус: приватный профиль");
    _logger.WriteLine($"  Сообщение: {privateProfileText}");
    
    _statistics.IncrementSuccess();
    return; // Переходим к следующему профилю
}

// Парсим HTML только для публичных профилей
var doc = await HtmlParser.ParseDocumentAsync(html, ct);

// Извлекаем данные...
// Сохраняем информацию для публичного профиля
_db.EnqueueUserResumeDetail(userLink, about, skills);
_db.EnqueueUpdateUserPublicStatus(userLink, isPublic: true);

_logger.WriteLine($"Пользователь {userLink}:");
_logger.WriteLine($"  О себе: ...");
_logger.WriteLine($"  Навыки: {skills.Count} шт.");
_logger.WriteLine($"  Опыт работы: {experienceCount} записей");
_logger.WriteLine($"  Статус: публичный профиль");
```

### В DatabaseClient.cs

Добавлены методы:

#### 1. EnqueueUpdateUserPublicStatus
```csharp
public bool EnqueueUpdateUserPublicStatus(string userLink, bool isPublic)
{
    // Добавляет запись в очередь для обновления статуса
    var record = new DbRecord(
        Type: DbRecordType.UserProfile,
        PrimaryValue: userLink,
        SecondaryValue: isPublic.ToString(),
        Mode: InsertMode.UpdateIfExists
    );
    _saveQueue.Enqueue(record);
    return true;
}
```

#### 2. DatabaseUpdateUserPublicStatus
```csharp
public void DatabaseUpdateUserPublicStatus(NpgsqlConnection conn, string userLink, bool isPublic)
{
    // Выполняет UPDATE в базе данных
    UPDATE habr_resumes 
    SET public = @public,
        updated_at = NOW()
    WHERE link = @link
}
```

## SQL-запрос

```sql
UPDATE habr_resumes 
SET public = true,
    updated_at = NOW()
WHERE link = 'https://career.habr.com/username';
```

## Логи

### Публичный профиль

```
[UserResumeDetailScraper] Пользователь https://career.habr.com/username:
[UserResumeDetailScraper]   О себе: Опытный разработчик...
[UserResumeDetailScraper]   Навыки: 15 шт.
[UserResumeDetailScraper]   Опыт работы: 3 записей
[UserResumeDetailScraper]   Статус: публичный профиль
[DB Queue] UpdateUserPublicStatus: https://career.habr.com/username -> public=true
[DB] Обновлен статус публичности для https://career.habr.com/username: public=true
```

### Приватный профиль

```
[UserResumeDetailScraper] Пользователь https://career.habr.com/private_user:
[UserResumeDetailScraper]   Статус: приватный профиль
[UserResumeDetailScraper]   Сообщение: Доступ ограничен настройками приватности
[DB Queue] UpdateUserPublicStatus: https://career.habr.com/private_user -> public=false
[DB] Обновлен статус публичности для https://career.habr.com/private_user: public=false
```

## Проверка в базе данных

### Проверить количество публичных профилей

```sql
SELECT COUNT(*) 
FROM habr_resumes 
WHERE public = true;
```

### Посмотреть последние обновленные профили

```sql
SELECT link, public, updated_at 
FROM habr_resumes 
WHERE public = true 
ORDER BY updated_at DESC 
LIMIT 10;
```

### Найти профили с данными, но без флага публичности

```sql
SELECT link, about, public 
FROM habr_resumes 
WHERE about IS NOT NULL 
  AND public IS NULL;
```

### Найти приватные профили

```sql
SELECT link, about, public, updated_at
FROM habr_resumes 
WHERE public = false
ORDER BY updated_at DESC;
```

## Преимущества

1. **Автоматическая маркировка** - не нужно вручную отмечать публичные/приватные профили
2. **Точность** - если данные извлечены, профиль точно публичный
3. **Фильтрация** - легко найти только публичные профили для дальнейшей обработки
4. **Статистика** - можно подсчитать соотношение публичных/приватных профилей
5. **Пропуск приватных** - при повторной обработке приватные профили автоматически пропускаются

## Использование

### Получить профили для обработки

```csharp
// В DatabaseClient.GetAllUserLinks()
// Автоматически исключает уже обработанные приватные профили
var query = @"SELECT link FROM habr_resumes 
              WHERE link IS NOT NULL 
              AND NOT (public = false AND about = 'Доступ ограничен настройками приватности')
              ORDER BY link";
```

**Логика фильтрации:**
- Включаются все профили с `public = true`
- Включаются профили с `public = NULL` (еще не обработаны)
- Исключаются профили с `public = false` и `about = 'Доступ ограничен настройками приватности'`

### Получить только публичные профили

```csharp
// В DatabaseClient
var links = db.GetAllUserLinks(conn, onlyPublic: true);
// SELECT link FROM habr_resumes WHERE link IS NOT NULL AND public = true
```

### Статистика

```sql
-- Общая статистика
SELECT 
    COUNT(*) as total,
    COUNT(CASE WHEN public = true THEN 1 END) as public_count,
    COUNT(CASE WHEN public = false THEN 1 END) as private_count,
    COUNT(CASE WHEN public IS NULL THEN 1 END) as unknown_count
FROM habr_resumes;
```

## Обратная совместимость

- ✅ Существующие записи не затрагиваются
- ✅ Флаг обновляется только при успешном извлечении данных
- ✅ Если профиль стал приватным, флаг не сбрасывается автоматически

## Дополнительная информация

- [USER_RESUME_DETAIL_SCRAPER.md](USER_RESUME_DETAIL_SCRAPER.md) - Документация по скраперу
- [USERRESUME_WITH_PROXY.md](USERRESUME_WITH_PROXY.md) - Использование с прокси
