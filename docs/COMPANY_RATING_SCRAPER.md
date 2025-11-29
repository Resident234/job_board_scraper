# CompanyRatingScraper Documentation

## Описание

`CompanyRatingScraper` - скрапер для сбора данных о рейтингах компаний с сайта career.habr.com. Скрапер перебирает страницы рейтингов с различными комбинациями параметров (размер компании, год) и извлекает информацию о компаниях, их рейтингах, наградах и отзывах.

## Функциональность

### Параметры URL

Скрапер генерирует все возможные комбинации следующих параметров:

- **sz** (размер компании): 2, 3, 4, 5
- **y** (год): 2024, 2023, 2022, 2021, 2020, 2019, 2018

### Типы URL

1. **Базовый URL**: `https://career.habr.com/companies/ratings`
2. **Только sz**: `https://career.habr.com/companies/ratings?sz=5`
3. **Только y**: `https://career.habr.com/companies/ratings?y=2024`
4. **Комбинация sz + y**: `https://career.habr.com/companies/ratings?sz=5&y=2024`

### Пагинация

Скрапер автоматически обрабатывает пагинацию, добавляя параметр `page=2`, `page=3` и т.д. до тех пор, пока не будут найдены компании на странице.

## Извлекаемые данные

Для каждой компании извлекаются следующие данные:

1. **Код компании** (`code`) - из href ссылки (например, "tensor" из "/companies/tensor")
2. **URL компании** (`url`) - полный URL в формате "https://career.habr.com/companies/{code}"
3. **Название** (`title`) - из элемента `<h2 class="rating-card__company-title-text">`
4. **Рейтинг** (`rating`) - из элемента `.rating-card__company-rating`
5. **Описание** (`about`) - из элемента `.rating-card__company-description`
6. **Город** (`city`) - из первой ссылки в `.rating-card__company-meta` до разделителя
7. **Награды** (`awards`) - список наград из `alt` атрибутов изображений в `.rating-card__company-awards`
8. **Средняя оценка** (`scores`) - из элемента `.rating-card__scores-value`
9. **Текст отзыва** (`review_text`) - из элемента `.rating-card__review-message` (очищенный от HTML)

## Структура базы данных

### Таблица habr_companies

Добавлены новые поля:

```sql
city TEXT                -- Город компании
awards TEXT[]            -- Массив наград
scores DECIMAL(4,2)      -- Средняя оценка
```

### Таблица company_reviews

Новая таблица для хранения отзывов:

```sql
id BIGINT PRIMARY KEY
company_id INTEGER       -- FK к habr_companies.id
review_hash TEXT UNIQUE  -- SHA256 хеш текста отзыва
review_text TEXT         -- Текст отзыва
created_at TIMESTAMP
updated_at TIMESTAMP
```

## Логика работы

### Сохранение данных компании

1. Поиск компании по полю `code`
2. Если компания найдена - обновление полей: `title`, `rating`, `about`, `city`, `awards`, `scores`, `url`
3. Если компания не найдена - вставка новой записи со всеми полями

### Сохранение отзывов

1. Вычисление SHA256 хеша текста отзыва
2. Проверка существования отзыва с таким хешем в таблице `company_reviews`
3. Если хеш не найден - вставка нового отзыва с привязкой к `company_id`
4. Если хеш найден - пропуск вставки (предотвращение дубликатов)

## Конфигурация

Настройки в `App.config`:

```xml
<add key="CompanyRating:Enabled" value="true" />
<add key="CompanyRating:OutputMode" value="Both" />
<add key="CompanyRating:TimeoutSeconds" value="60" />
<add key="CompanyRating:EnableRetry" value="true" />
<add key="CompanyRating:EnableTrafficMeasuring" value="true" />
```

## Использование

```csharp
var companyRatingHttpClient = new SmartHttpClient(
    httpClient, 
    "CompanyRatingScraper", 
    trafficStats,
    enableRetry: AppConfig.CompanyRatingEnableRetry,
    enableTrafficMeasuring: AppConfig.CompanyRatingEnableTrafficMeasuring,
    timeout: AppConfig.CompanyRatingTimeout);

var companyRatingScraper = new CompanyRatingScraper(
    companyRatingHttpClient,
    db,
    controller: controller,
    interval: TimeSpan.FromDays(30),
    outputMode: AppConfig.CompanyRatingOutputMode);

_ = companyRatingScraper.StartAsync(cts.Token);
```

## Примеры данных

### Пример компании

```json
{
  "code": "alfabank",
  "url": "https://career.habr.com/companies/alfabank",
  "title": "Альфа-Банк",
  "rating": 4.5,
  "about": "Digital-подразделение Альфа-Банка",
  "city": "Москва",
  "awards": [
    "Средняя оценка #1",
    "Интересные задачи #1",
    "Адекватная зарплата #1",
    "Современные технологии #1"
  ],
  "scores": 4.82
}
```

### Пример отзыва

```json
{
  "company_id": 123,
  "review_hash": "a1b2c3d4e5f6...",
  "review_text": "Отличная компания с интересными задачами..."
}
```

## Миграция базы данных

Для использования скрапера необходимо выполнить SQL-скрипты:

1. `sql/alter_companies_add_rating_fields.sql` - добавление новых полей в habr_companies
2. `sql/create_company_reviews_table.sql` - создание таблицы company_reviews

## Статистика

Скрапер собирает статистику:

- Общее количество обработанных URL
- Количество найденных компаний
- Время начала и окончания работы
- Количество обработанных страниц

## Примечания

- Скрапер использует `AdaptiveConcurrencyController` для оптимизации нагрузки
- Отзывы дедуплицируются по SHA256 хешу текста
- Награды хранятся как массив строк PostgreSQL (TEXT[])
- Скрапер автоматически останавливает пагинацию при отсутствии результатов
