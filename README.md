# JobBoardScraper v2.0

Мощное приложение для автоматического сбора данных с career.habr.com с поддержкой экспертов, компаний и резюме.

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-12%2B-336791)](https://www.postgresql.org/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

---

## 🚀 Быстрый старт

```bash
# 1. Клонировать репозиторий
git clone <repository-url>
cd JobBoardScraper

# 2. Создать базу данных
psql -U postgres -c "CREATE DATABASE jobs;"

# 3. Выполнить SQL-скрипты
psql -U postgres -d jobs -f sql/create_resumes_table.sql
psql -U postgres -d jobs -f sql/create_companies_table.sql
psql -U postgres -d jobs -f sql/create_category_root_ids_table.sql
psql -U postgres -d jobs -f sql/create_skills_table.sql
psql -U postgres -d jobs -f sql/add_expert_columns.sql
psql -U postgres -d jobs -f sql/add_company_details_columns.sql
psql -U postgres -d jobs -f sql/create_levels_table.sql
psql -U postgres -d jobs -f sql/add_user_profile_columns.sql

# 4. Применить дополнительные миграции (если нужно)
psql -U postgres -d jobs -f sql/alter_resumes_add_additional_fields.sql
psql -U postgres -d jobs -f sql/alter_resumes_add_job_search_status.sql

# 5. Запустить приложение
dotnet run --project JobBoardScraper
```

**Подробнее:** [QUICKSTART.md](QUICKSTART.md)

---

## ✨ Что нового в v2.0

### 🎯 ExpertsScraper
Новый скрапер для сбора данных экспертов с расширенной информацией:
- Имя и ссылка на профиль
- Код пользователя
- Стаж работы
- Компания

### 🏢 CompanyDetailScraper
Новый скрапер для детального сбора информации о компаниях:
- Основная информация (ID, название, описание, сайт, рейтинг)
- Статистика (сотрудники, подписчики)
- Контактные лица и сотрудники
- Связанные компании
- Навыки компании (с таблицей связей многие-ко-многим)
- Флаг наличия блога на Хабре

### 👤 UserProfileScraper
Новый скрапер для сбора детальной информации о профилях пользователей:
- Имя пользователя
- Статус эксперта
- Уровень (Junior, Middle, Senior и т.д.)
- Техническая информация
- Зарплатные ожидания
- Опыт работы
- Дата последнего визита
- Определение публичности профиля

### 📝 UserResumeDetailScraper
Новый скрапер для извлечения детальной информации из резюме пользователей:
- Текст "О себе" (about)
- Список навыков (skills) с таблицей связей многие-ко-многим
- Опыт работы с детальной информацией о компаниях, должностях и навыках
- **Дополнительные данные профиля:**
  - Возраст (например: "37 лет")
  - Дата регистрации (например: "30.08.2022")
  - Гражданство (например: "Россия")
  - Готовность к удаленной работе
- Автоматическое создание/обновление компаний при парсинге опыта
- Интеграция с таблицами habr_skills, habr_user_skills, habr_user_experience и habr_user_experience_skills
- Поддержка прокси-ротации для обхода лимитов

### 🔧 Улучшения 
- **SmartHttpClient**: Умная обёртка над HttpClient с автоматическими повторами, измерением трафика и поддержкой прокси
- **ProxyRotator**: Автоматическая ротация прокси-серверов для распределения нагрузки
- **TrafficStatistics**: Детальная статистика трафика по скраперам
- **AdaptiveConcurrencyController**: Динамическое управление параллелизмом
- **DatabaseClient**: Универсальный клиент для работы с PostgreSQL
- **ExponentialBackoff**: Умная стратегия повторов с экспоненциальной задержкой и jitter для HTTP ошибок
- **ProfileDataExtractor**: Централизованный класс для извлечения данных профиля из HTML
- **job_search_status**: Новое поле для хранения статуса поиска работы пользователя

---

## 🌐 Поддержка прокси

Система поддерживает автоматическую ротацию прокси-серверов для обхода ограничений и распределения нагрузки.

### Быстрая настройка

```xml
<!-- В App.config -->
<add key="Proxy:Enabled" value="true" />
<add key="Proxy:List" value="http://proxy1:8080;http://proxy2:8080" />
```

### Использование

```csharp
// Создать ProxyRotator из конфигурации
var proxyRotator = HttpClientFactory.CreateProxyRotator();

// Создать HttpClient с прокси
var httpClient = HttpClientFactory.CreateHttpClient(proxyRotator);

// Создать SmartHttpClient с поддержкой прокси
var smartClient = new SmartHttpClient(
    httpClient,
    scraperName: "MyScraper",
    proxyRotator: proxyRotator
);
```

**Подробнее:** 
- [PROXY_ROTATION.md](docs/PROXY_ROTATION.md) - Полная документация
- [PROXY_USAGE_EXAMPLE.md](docs/PROXY_USAGE_EXAMPLE.md) - Примеры использования
- [DYNAMIC_PROXY.md](docs/DYNAMIC_PROXY.md) - Динамическое обновление прокси
- [PROXY_SERVICES.md](docs/PROXY_SERVICES.md) - Коммерческие прокси-сервисы

---

## 📚 Документация

- [QUICKSTART.md](docs/QUICKSTART.md) - Быстрый старт
- [EXAMPLES.md](docs/EXAMPLES.md) - Примеры использования
- [MIGRATION_GUIDE.md](docs/MIGRATION_GUIDE.md) - Миграция с v1.x
- [CHANGELOG.md](docs/CHANGELOG.md) - История изменений
- [TRAFFIC_OPTIMIZATION.md](docs/TRAFFIC_OPTIMIZATION.md) - Оптимизация трафика
- [PROXY_ROTATION.md](docs/PROXY_ROTATION.md) - Ротация прокси
- [PROXY_USAGE_EXAMPLE.md](docs/PROXY_USAGE_EXAMPLE.md) - Примеры использования прокси

### Скраперы

- [COMPANY_DETAIL_SCRAPER.md](docs/COMPANY_DETAIL_SCRAPER.md)
- [COMPANY_RATING_SCRAPER.md](docs/COMPANY_RATING_SCRAPER.md)
- [USER_PROFILE_SCRAPER.md](docs/USER_PROFILE_SCRAPER.md)
- [USER_RESUME_DETAIL_SCRAPER.md](docs/USER_RESUME_DETAIL_SCRAPER.md)
- [USER_ADDITIONAL_DATA_EXTRACTION.md](docs/USER_ADDITIONAL_DATA_EXTRACTION.md) - Извлечение дополнительных данных профиля

### Алгоритмы и стратегии

- [BACKOFF_ALGORITHMS.md](docs/BACKOFF_ALGORITHMS.md) - Алгоритмы задержки между повторами
- [HTTP_ERROR_RETRY_STRATEGY.md](HTTP_ERROR_RETRY_STRATEGY.md) - Стратегия повторов для HTTP ошибок

### Рефакторинг и изменения

- [REFACTORING_PROFILE_EXTRACTOR.md](REFACTORING_PROFILE_EXTRACTOR.md) - Рефакторинг извлечения данных профиля
- [SAVE_EXTRACTED_DATA_TO_DB.md](SAVE_EXTRACTED_DATA_TO_DB.md) - Сохранение извлечённых данных в БД
- [JOB_SEARCH_STATUS_FIELD_SUMMARY.md](JOB_SEARCH_STATUS_FIELD_SUMMARY.md) - Поле статуса поиска работы

### Быстрые старты

- [QUICK_START_ADDITIONAL_DATA.md](QUICK_START_ADDITIONAL_DATA.md) - Быстрый старт для дополнительных данных

---

## 🛠️ Технологии

- **.NET 9.0** - Современная платформа разработки
- **PostgreSQL 12+** - Надёжная база данных
- **AngleSharp** - Парсинг HTML
- **Npgsql** - Драйвер PostgreSQL для .NET
- **ProxyRotator** - Ротация прокси-серверов

---

## 📝 Лицензия

MIT License - см. [LICENSE](LICENSE)

---

## 🤝 Вклад

Приветствуются pull requests и issues!

---

## 📧 Контакты

Если у вас есть вопросы или предложения, создайте issue в репозитории.
