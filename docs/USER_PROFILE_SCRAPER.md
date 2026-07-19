# UserProfileScraper - Парсинг профилей пользователей

## Описание

`UserProfileScraper` - это скрапер для сбора детальной информации о профилях пользователей и их друзьях с career.habr.com.

## Возможности

### 👤 Информация о пользователе
- **Имя** - полное имя пользователя из `<h1 class="page-title__title">`
- **Эксперт** - флаг наличия статуса эксперта (`.user-page-sidebar__is-expert`)
- **Уровень** - уровень специалиста (например, "Старший (Senior)", "Средний (Middle)")
- **Техническая информация** - специализация без уровня (например, "Product manager | B2B SaaS • Менеджер продукта")
- **Зарплата** - желаемая зарплата в рублях (только число)



## Конфигурация

```xml
<!-- Включить/отключить скрапер -->
<add key="UserProfile:Enabled" value="false" />

<!-- Таймаут HTTP-запроса (секунды) -->
<add key="UserProfile:TimeoutSeconds" value="60" />

<!-- Автоматические повторы при ошибках -->
<add key="UserProfile:EnableRetry" value="true" />

<!-- Измерение трафика -->
<add key="UserProfile:EnableTrafficMeasuring" value="true" />

<!-- Режим вывода: ConsoleOnly, FileOnly, Both -->
<add key="UserProfile:OutputMode" value="Both" />

<!-- Шаблон URL для страницы друзей -->
<add key="UserProfile:FriendsUrlTemplate" value="https://career.habr.com/{0}/friends" />
```

## Периодичность

Скрапер запускается **раз в 30 дней** и обходит всех пользователей из таблицы `habr_resumes` (где `code IS NOT NULL`).

## Структура данных

### Таблица habr_resumes (обновленная)

Добавлены новые поля:

```sql
ALTER TABLE habr_resumes 
ADD COLUMN level_id INTEGER REFERENCES habr_levels(id),
ADD COLUMN info_tech TEXT,
ADD COLUMN salary INTEGER;
```

### Таблица habr_levels

Справочник уровней специалистов:

```sql
CREATE TABLE habr_levels (
    id SERIAL PRIMARY KEY,
    title VARCHAR(255) NOT NULL UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```



## Примеры использования

### Получить пользователей по уровню

```sql
SELECT r.code, r.title, l.title as level, r.salary
FROM habr_resumes r
JOIN habr_levels l ON r.level_id = l.id
WHERE l.title = 'Старший (Senior)'
ORDER BY r.salary DESC NULLS LAST;
```

### Топ уровней по количеству пользователей

```sql
SELECT l.title, COUNT(r.id) as user_count
FROM habr_levels l
JOIN habr_resumes r ON l.id = r.level_id
GROUP BY l.title
ORDER BY user_count DESC;
```

### Средняя зарплата по уровням

```sql
SELECT l.title, 
       AVG(r.salary) as avg_salary,
       MIN(r.salary) as min_salary,
       MAX(r.salary) as max_salary,
       COUNT(r.salary) as count
FROM habr_levels l
JOIN habr_resumes r ON l.id = r.level_id
WHERE r.salary IS NOT NULL
GROUP BY l.title
ORDER BY avg_salary DESC;
```



## Логика работы

1. **Загрузка списка пользователей** из таблицы `habr_resumes` (WHERE code IS NOT NULL)
2. **Последовательный обход** каждого пользователя:
   - Открытие страницы `/friends`
   - Парсинг информации о пользователе
   - Сохранение в БД
3. **Обработка данных**:
   - Уровень → создание/получение записи в `habr_levels`
   - Профиль → обновление `habr_resumes` (COALESCE)

## Особенности

### Парсинг уровня и технической информации

Из элемента `.user-page-sidebar__meta` извлекаются все `<span>` элементы:
- **Последний элемент** = уровень (сохраняется в `habr_levels`)
- **Остальные элементы** = техническая информация (объединяются через " • ")

Пример:
```
Input: "Product manager | B2B SaaS • Менеджер продукта • Старший (Senior)"
Level: "Старший (Senior)"
Info Tech: "Product manager | B2B SaaS • Менеджер продукта"
```

### Парсинг зарплаты

Из текста извлекается только число:
```
Input: "От 350 000 ₽ • Не ищу работу"
Salary: 350000
```

Регулярное выражение: `От\s+([\d\s]+)\s*₽`

### Обработка NULL значений

При обновлении используется `COALESCE` - сохраняются существующие значения если новые NULL.

### Задержка между запросами

Между запросами добавлена задержка **500 мс** для снижения нагрузки на сервер.

## Установка

```bash
# 1. Создать таблицу уровней
psql -U postgres -d jobs -f sql/create_levels_table.sql

# 2. Добавить поля в таблицу resumes
psql -U postgres -d jobs -f sql/add_user_profile_columns.sql
```

## Отладка

HTML последней обработанной страницы сохраняется в:
```
./logs/UserProfileScraper/last_page.html
```

## Логирование

```
[UserProfileScraper] Обработка пользователя: nikjalet -> https://career.habr.com/nikjalet/friends
[UserProfileScraper] HTML сохранён: ./logs/UserProfileScraper/last_page.html
[UserProfileScraper] Пользователь nikjalet: Имя = Никита Крицкий, Эксперт = True, Уровень = Старший (Senior), Зарплата = 350000
```

## Производительность

- **Скорость**: ~2 пользователя в секунду (с учетом задержки 500 мс)
- **Трафик**: ~30-50 KB на пользователя
- **Время полного обхода**: зависит от количества пользователей в БД

Для 1000 пользователей: ~8-10 минут

## См. также

- [README](../README.md) - основная документация
- [DB_SCHEMA.md](DB_SCHEMA.md) - схема базы данных
