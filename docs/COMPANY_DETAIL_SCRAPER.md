# CompanyDetailScraper - Детальный парсинг компаний

## Описание

`CompanyDetailScraper` - это скрапер для сбора детальной информации о компаниях с их страниц на career.habr.com. Он извлекает максимум доступных данных о каждой компании.

## Возможности

### 📊 Основная информация
- **company_id** - числовой ID компании из Habr
- **title** - название компании
- **about** - краткое описание
- **description** - детальное описание (очищено от HTML)
- **site** - ссылка на официальный сайт
- **rating** - рейтинг компании (0.00 - 5.00)
- **employees_count** - размер компании текстом (например, "Более 5000 человек")
- **habr** - флаг наличия блога на Хабре

### 📈 Статистика
- **current_employees** - количество текущих сотрудников
- **past_employees** - общее количество сотрудников (включая бывших)
- **followers** - количество подписчиков
- **want_work** - количество желающих работать в компании

### 👥 Люди
- **Контактные лица** - HR и другие представители компании (с именами)
- **Сотрудники** - список всех сотрудников компании (только коды)

### 🏢 Связи
- **Связанные компании** - другие компании, связанные с текущей
- **Навыки** - технологии и навыки, используемые в компании

## Конфигурация

```xml
<!-- Включить/отключить скрапер -->
<add key="CompanyDetail:Enabled" value="false" />

<!-- Таймаут HTTP-запроса (секунды) -->
<add key="CompanyDetail:TimeoutSeconds" value="60" />

<!-- Автоматические повторы при ошибках -->
<add key="CompanyDetail:EnableRetry" value="true" />

<!-- Измерение трафика -->
<add key="CompanyDetail:EnableTrafficMeasuring" value="true" />

<!-- Режим вывода: ConsoleOnly, FileOnly, Both -->
<add key="CompanyDetail:OutputMode" value="Both" />
```

## Периодичность

Скрапер запускается **раз в 30 дней** и обходит все компании из таблицы `habr_companies`.

## Структура данных

### Таблица habr_companies

Основная таблица с информацией о компаниях:

```sql
CREATE TABLE habr_companies (
    id SERIAL PRIMARY KEY,
    code VARCHAR(255) NOT NULL UNIQUE,
    url VARCHAR(500) NOT NULL,
    title VARCHAR(500),
    company_id BIGINT UNIQUE,
    about TEXT,
    description TEXT,
    site TEXT,
    rating DECIMAL(3,2),
    current_employees INTEGER,
    past_employees INTEGER,
    followers INTEGER,
    want_work INTEGER,
    employees_count TEXT,
    habr BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### Таблица habr_skills

Справочник навыков:

```sql
CREATE TABLE habr_skills (
    id SERIAL PRIMARY KEY,
    title VARCHAR(255) NOT NULL UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### Таблица habr_company_skills

Связь многие-ко-многим между компаниями и навыками:

```sql
CREATE TABLE habr_company_skills (
    id SERIAL PRIMARY KEY,
    company_id INTEGER NOT NULL REFERENCES habr_companies(id),
    skill_id INTEGER NOT NULL REFERENCES habr_skills(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(company_id, skill_id)
);
```

## Примеры использования

### Получить компании с высоким рейтингом

```sql
SELECT code, title, rating, followers, employees_count
FROM habr_companies
WHERE rating >= 4.0
ORDER BY rating DESC, followers DESC
LIMIT 10;
```

### Получить навыки компании

```sql
SELECT s.title
FROM habr_skills s
JOIN habr_company_skills cs ON s.id = cs.skill_id
JOIN habr_companies c ON cs.company_id = c.id
WHERE c.code = 'yandex'
ORDER BY s.title;
```

### Топ навыков по популярности

```sql
SELECT s.title, COUNT(cs.company_id) as company_count
FROM habr_skills s
JOIN habr_company_skills cs ON s.id = cs.skill_id
GROUP BY s.title
ORDER BY company_count DESC
LIMIT 20;
```

### Компании с блогом на Хабре

```sql
SELECT code, title, rating, site
FROM habr_companies
WHERE habr = TRUE
ORDER BY rating DESC NULLS LAST;
```

### Статистика по размерам компаний

```sql
SELECT 
    employees_count,
    COUNT(*) as count
FROM habr_companies
WHERE employees_count IS NOT NULL
GROUP BY employees_count
ORDER BY count DESC;
```

## Логика работы

1. **Загрузка списка компаний** из таблицы `habr_companies`
2. **Последовательный обход** каждой компании:
   - Загрузка HTML страницы
   - Парсинг всех доступных данных
   - Сохранение в БД
3. **Извлечение связанных данных**:
   - Контактные лица → `habr_resumes` (UpdateIfExists)
   - Сотрудники → `habr_resumes` (SkipIfExists)
   - Связанные компании → `habr_companies` (UpdateIfExists)
   - Навыки → `habr_skills` + `habr_company_skills`

## Особенности

### Режимы сохранения

- **UpdateIfExists** - обновляет существующие записи (для контактных лиц и связанных компаний)
- **SkipIfExists** - пропускает если запись существует (для сотрудников)

### Обработка NULL значений

При обновлении используется `COALESCE` - сохраняются существующие значения если новые NULL:

```sql
UPDATE habr_companies 
SET title = COALESCE(@title, title),
    rating = COALESCE(@rating, rating),
    ...
```

### Задержка между запросами

Между запросами к страницам компаний добавлена задержка **500 мс** для снижения нагрузки на сервер.

## Отладка

HTML последней обработанной страницы сохраняется в:
```
./logs/CompanyDetailScraper/last_page.html
```

Это помогает при отладке селекторов и проверке структуры страницы.

## Логирование

Скрапер выводит подробную информацию о процессе:

```
[CompanyDetailScraper] Обработка компании: yandex -> https://career.habr.com/companies/yandex
[CompanyDetailScraper] HTML сохранён: ./logs/CompanyDetailScraper/last_page.html (кодировка: utf-8)
[CompanyDetailScraper] Найдено контактных лиц: 5
[CompanyDetailScraper] Найдено сотрудников: 847
[CompanyDetailScraper] Найдено связанных компаний: 3
[CompanyDetailScraper] Найдено навыков: 24
[CompanyDetailScraper] Компания yandex: ID = 123456, Название = Яндекс, Рейтинг = 4.85, ...
```

## Производительность

- **Скорость**: ~2 компании в секунду (с учетом задержки 500 мс)
- **Трафик**: ~50-100 KB на компанию
- **Время полного обхода**: зависит от количества компаний в БД

Для 1000 компаний: ~8-10 минут

## См. также

- [JobBoardScraper README](../JobBoardScraper/README.md) - основная документация
- [SQL README](../sql/README.md) - работа с базой данных
- [TRAFFIC_OPTIMIZATION](TRAFFIC_OPTIMIZATION.md) - оптимизация трафика
