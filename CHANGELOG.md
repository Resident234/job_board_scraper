# Changelog

Все значимые изменения в проекте JobBoardScraper документируются в этом файле.

## [Unreleased]

### Добавлено

#### Сохранение извлеченных данных профиля в БД

- `UserResumeDetailScraper` теперь передает в `DatabaseClient.EnqueueUserResumeDetail()` полный набор извлеченных данных профиля:
  - имя пользователя;
  - техническую информацию и должности;
  - уровень специалиста;
  - желаемую зарплату;
  - текстовый опыт работы;
  - дату последнего визита;
  - возраст, регистрацию, гражданство и готовность к удаленной работе;
  - статус поиска работы.
- `DatabaseClient` сохраняет основные данные профиля в `habr_resumes` через существующие очереди записи.
- `job_search_status` вынесен в отдельное поле таблицы `habr_resumes`.
- Добавлена SQL-миграция `sql/alter_resumes_add_job_search_status.sql`:
  - добавляет колонку `job_search_status`;
  - добавляет комментарий к колонке;
  - создает индекс `idx_habr_resumes_job_search_status`.
- Обновлено логирование очереди `UserResumeDetail`, чтобы в одном сообщении были видны все сохраняемые поля: `UserName`, `InfoTech`, `Level`, `Salary`, `JobStatus`, `About`, `Skills`, `Age`, `ExperienceText`, `Registration`, `LastVisit`, `Citizenship`, `RemoteWork`.
- Заполняемые поля `habr_resumes`: `title`, `info_tech`, `level_id`, `salary`, `work_experience`, `last_visit`, `job_search_status`, `about`.
- Поддерживаемые значения `job_search_status`: `Ищу работу`, `Не ищу работу`, `Рассматриваю предложения`.

#### Сводка выполненных implementation tasks

- Перенесена в changelog сводка task-документов из `docs/tasks*.md`; сами task-файлы удалены как промежуточные артефакты планирования.
- Закрыт план по парсингу высшего образования:
  - созданы SQL-скрипты `habr_universities` и `habr_resumes_universities`;
  - добавлены модели `UniversityData`, `CourseData`, `UserUniversityData`, `UniversityEducationData`;
  - добавлены CSS-селекторы и regex-настройки образования в `AppConfig`;
  - реализован `ProfileDataExtractor.ExtractEducationData()` и разбор периода обучения, включая `По настоящее время`;
  - добавлены очереди `EnqueueUniversity()` и `EnqueueUserUniversity()` в `DatabaseClient`;
  - `UserResumeDetailScraper` интегрирован с извлечением и сохранением образования;
  - по исходному task-плану property-based проверки для образования проходили, сборка была успешной.
- Зафиксирован план по free-proxy инфраструктуре:
  - добавлены `FreeProxyPool`, `ProxyInfo`, `FreeProxyListScraper`, HTML-парсинг прокси и фильтрация по качеству;
  - реализованы refresh-циклы, dedup, лимиты пула и логирование;
  - добавлены настройки `FreeProxy:*` в `App.config` и `AppConfig`;
  - `UserResumeDetailScraper` получил поддержку получения прокси перед HTTP-запросами;
  - добавлен factory-метод создания `HttpClient` с прокси.
- Зафиксирован план по унифицированной статистике:
  - расширен `ScraperStatistics` счетчиками `TotalFound`, `TotalNotFound`, `TotalItemsCollected` и потокобезопасными increment/update методами;
  - `ScraperStatistics` интегрирован в `ResumeListPageScraper`, `UserProfileScraper`, `CompanyDetailScraper`, `UserResumeDetailScraper`, `UserFriendsScraper`, `CategoryScraper`, `ExpertsScraper` и связанные вызовы `ParallelScraperLogger`;
  - итоговый вывод статистики переведен на единый формат `ScraperStatistics.ToString()`;
  - unit-тесты для `ScraperStatistics` остались отмечены как незавершенный пункт исходного task-плана.
- Зафиксирован план namespace-рефакторинга:
  - скраперы перенесены в `Scrapers`;
  - прокси-инфраструктура перенесена в `Infrastructure/Proxy`;
  - throttling перенесен в `Infrastructure/Throttling`;
  - `AdaptiveConcurrencyController` перенесен в `Core`;
  - `HtmlParser` перенесен в `Parsing`;
  - целевая структура папок проверена, но часть пунктов исходного плана по удалению старых helper-дубликатов и запуску тестов оставалась не закрыта.
- Зафиксирован план proxy whitelist:
  - добавлены настройки `ProxyWhitelist:*` в `App.config` и `AppConfig`;
  - добавлены модели `WhitelistProxyEntry` и `WhitelistData`;
  - реализованы `IWhitelistStorage`, `JsonWhitelistStorage` и `ProxyWhitelistManager`;
  - реализована обработка успешного прокси, ошибок, суточного лимита и fallback на general pool;
  - `UserResumeDetailScraper` интегрирован с детекцией сообщения о суточном лимите;
  - часть property/integration-тестов whitelist в исходном task-плане оставалась незавершенной.
- Перенесена в changelog сводка из `docs/USERRESUME_PROXY_SUMMARY.md` и `docs/USERRESUME_PROXY_SUMMARY_1.md`; оба файла были дубликатами и удалены как промежуточные артефакты.
- Зафиксирована интеграция `ProxyRotator` в `UserResumeDetailScraper`:
  - `Program.cs` создает `ProxyRotator`, передает его в `SmartHttpClient` и логирует состояние прокси при запуске;
  - `UserResumeDetailScraper` вызывает `_httpClient.RotateProxy()` перед обработкой каждой страницы резюме;
  - `HttpClientFactory` автоматически загружает прокси из ProxyScrape API и GeoNode API, если `Proxy:List` пуст;
  - `App.config` поддерживает включение прокси через `Proxy:Enabled=true`, пустой `Proxy:List` для автозагрузки и включенный `UserResumeDetail:Enabled=true`;
  - ротация предназначена для обхода IP-лимита career.habr.com на просмотр профилей специалистов и распределения запросов по разным прокси;
  - для production-сценариев рекомендованы коммерческие прокси и настройки `UserResumeDetail:EnableRetry`, `UserResumeDetail:TimeoutSeconds`, `UserResumeDetail:EnableTrafficMeasuring`.

#### Извлечение дополнительных данных профиля

- Новая функциональность извлечения дополнительных данных из профилей пользователей career.habr.com
- Извлекаемые поля:
  - **Возраст** (например: "37 лет")
  - **Опыт работы (текст)** (например: "9 лет и 1 месяц")
  - **Регистрация** (например: "30.08.2022")
  - **Гражданство** (например: "Россия")
  - **Удаленная работа** (например: "готов к удаленной работе")

##### Подробная информация о поле "Опыт работы (текст)"

**Новое поле:** `experience_text` в таблице `habr_resumes`
- **Тип:** `text COLLATE pg_catalog."default"`
- **Описание:** Текстовое описание опыта работы (например: "9 лет и 1 месяц")
- **SQL миграция:** `sql/alter_resumes_add_additional_fields.sql`

**Изменения в коде:**

1. **ProfileDataExtractor.cs** - обновлена сигнатура метода:
   ```csharp
   // Было:
   public static (string? age, string? registration, string? citizenship, string? remoteWork)
       ExtractAdditionalProfileData(...)

   // Стало:
   public static (string? age, string? experienceText, string? registration, string? citizenship, string? remoteWork)
       ExtractAdditionalProfileData(...)
   ```

2. **DatabaseClient.cs** - обновлены методы:
   ```csharp
   // Добавлен параметр experienceText в EnqueueUserResumeDetail()
   public bool EnqueueUserResumeDetail(
       string userLink,
       string? about,
       List<string>? skills,
       string? age,
       string? experienceText,  // Новый параметр
       string? registration,
       string? citizenship,
       string? remoteWork)
   ```

3. **UserResumeDetailScraper.cs** - обновлено извлечение и сохранение:
   ```csharp
   // Извлечение данных
   var (age, experienceText, registration, citizenship, remoteWork) =
       Helper.Dom.ProfileDataExtractor.ExtractAdditionalProfileData(doc);

   // Сохранение в БД
   _db.EnqueueUserResumeDetail(userLink, about, skills, age, experienceText, registration, citizenship, remoteWork);
   ```

**Примеры SQL запросов:**

```sql
-- Просмотр данных с опытом работы
SELECT link, title, age, experience_text, registration, citizenship
FROM habr_resumes
WHERE experience_text IS NOT NULL
LIMIT 10;

-- Статистика по опыту
SELECT experience_text, COUNT(*) as count
FROM habr_resumes
WHERE experience_text IS NOT NULL AND public = true
GROUP BY experience_text
ORDER BY count DESC;
```

**Применение изменений:**
1. Выполнить миграцию БД: `psql -U postgres -d habr_career -f sql/alter_resumes_add_additional_fields.sql`
2. Пересобрать проект: `dotnet build`
3. Запустить скрапер: `dotnet run`

✅ **Обратная совместимость:** Полностью обратно совместимо. Старый код продолжит работать, новое поле опционально.

### Сводка реализации: Извлечение дополнительных данных профиля

#### Дата: 2024

#### Задача
Добавить извлечение дополнительных данных из профилей пользователей career.habr.com:
- Возраст
- Дата регистрации
- Гражданство
- Готовность к удаленной работе

#### Выполненные работы

##### 1. База данных
**Создан SQL скрипт миграции**
**Файл:** `sql/alter_resumes_add_additional_fields.sql`

Добавлены поля в таблицу `habr_resumes`:
- `age` (text) - возраст пользователя
- `registration` (text) - дата регистрации
- `citizenship` (text) - гражданство
- `remote_work` (text) - готовность к удаленной работе

Создан индекс для поиска по гражданству.

##### 2. Helper.Dom.ProfileDataExtractor
**Добавлен новый метод**
**Файл:** `JobBoardScraper/Helper.Dom/ProfileDataExtractor.cs`

```csharp
public static (string? age, string? registration, string? citizenship, string? remoteWork)
    ExtractAdditionalProfileData(IDocument doc, string basicSectionSelector = ".basic-section")
```

Метод извлекает дополнительные данные из секций `.basic-section` HTML документа.

##### 3. DatabaseClient
**Обновления структур данных**
**Файл:** `JobBoardScraper/DatabaseClient.cs`

1. **Добавлен новый тип записи:**
   ```csharp
   public enum DbRecordType
   {
       // ...
       UserAdditionalData  // Новый тип
   }
   ```

2. **Расширена структура DbRecord:**
   ```csharp
   public readonly record struct DbRecord(
       // ...
       Dictionary<string, string?>? AdditionalData = null  // Новое поле
   );
   ```

3. **Добавлена перегрузка метода:**
   ```csharp
   public bool EnqueueUserResumeDetail(
       string userLink,
       string? about,
       List<string>? skills,
       string? age,
       string? registration,
       string? citizenship,
       string? remoteWork)
   ```

4. **Добавлен новый метод обновления:**
   ```csharp
   public void DatabaseUpdateUserAdditionalData(
       NpgsqlConnection conn,
       string userLink,
       Dictionary<string, string?> additionalData)
   ```

5. **Добавлена обработка в switch statement:**
   ```csharp
   case DbRecordType.UserAdditionalData:
       if (record.AdditionalData != null)
       {
           DatabaseUpdateUserAdditionalData(conn, userLink: record.PrimaryValue, additionalData: record.AdditionalData);
       }
       break;
   ```

##### 4. UserResumeDetailScraper
**Интеграция извлечения данных**
**Файл:** `JobBoardScraper/WebScraper/UserResumeDetailScraper.cs`

Добавлено извлечение и сохранение дополнительных данных:

```csharp
// Извлекаем дополнительные данные профиля
var (age, registration, citizenship, remoteWork) =
    Helper.Dom.ProfileDataExtractor.ExtractAdditionalProfileData(doc);

// Сохраняем информацию для публичного профиля
_db.EnqueueUserResumeDetail(userLink, about, skills, age, registration, citizenship, remoteWork);
```

Обновлен вывод в лог для отображения новых данных.

##### 5. UserProfileScraper
**Рефакторинг кода**
**Файл:** `JobBoardScraper/WebScraper/UserProfileScraper.cs`

Заменен дублирующийся код на использование метода из `ProfileDataExtractor`:

```csharp
// Было: ~30 строк дублирующегося кода
// Стало:
var (workExperience, lastVisit) = Helper.Dom.ProfileDataExtractor.ExtractWorkExperienceAndLastVisit(
    doc,
    AppConfig.UserProfileBasicSectionSelector);
```

##### 6. Документация
Документация по извлечению дополнительных данных профиля перенесена в `CHANGELOG.md`.

Ранее подготовленные материалы:

1. **ADDITIONAL_DATA_SUMMARY.md**
   - Краткая сводка изменений
   - Список измененных файлов
   - Инструкции по использованию

2. **QUICK_START_ADDITIONAL_DATA.md**
   - Быстрый старт для новой функциональности
   - Пошаговые инструкции
   - Примеры SQL запросов
   - Troubleshooting

3. **IMPLEMENTATION_SUMMARY.md**
   - Полная сводка реализации

4. **Обновлен README.md**
   - Добавлена информация о новых данных
   - Добавлены ссылки на документацию

##### Архитектурные улучшения

1. **Централизация логики парсинга**
   - Методы извлечения данных вынесены в `Helper.Dom.ProfileDataExtractor`
   - Устранено дублирование кода между скраперами

2. **Переиспользование кода**
   - `UserProfileScraper` теперь использует общие методы
   - Упрощена поддержка и тестирование

3. **Обратная совместимость**
   - Добавлена перегрузка метода `EnqueueUserResumeDetail`
   - Старый код продолжит работать без изменений

4. **Расширяемость**
   - Легко добавить новые поля в будущем
   - Гибкая структура `AdditionalData` с Dictionary

5. **Типобезопасность**
   - Использование кортежей для возврата данных
   - Строгая типизация параметров

##### Статистика изменений

**Файлы созданы: 4**
- `sql/alter_resumes_add_additional_fields.sql`
- `ADDITIONAL_DATA_SUMMARY.md`
- `QUICK_START_ADDITIONAL_DATA.md`
- `IMPLEMENTATION_SUMMARY.md`

**Файлы изменены: 4**
- `JobBoardScraper/Helper.Dom/ProfileDataExtractor.cs` (+60 строк)
- `JobBoardScraper/DatabaseClient.cs` (+100 строк)
- `JobBoardScraper/WebScraper/UserResumeDetailScraper.cs` (+10 строк, рефакторинг)
- `JobBoardScraper/WebScraper/UserProfileScraper.cs` (-30 строк, рефакторинг)
- `README.md` (обновлена документация)

**Строк кода добавлено: ~170**
**Строк кода удалено: ~30 (рефакторинг)**
**Чистое добавление: ~140 строк**

##### Примеры использования

**Извлечение данных:**
```csharp
var (age, registration, citizenship, remoteWork) =
    ProfileDataExtractor.ExtractAdditionalProfileData(doc);
```

**Сохранение в БД:**
```csharp
_db.EnqueueUserResumeDetail(
    userLink,
    about,
    skills,
    age,
    registration,
    citizenship,
    remoteWork);
```

**SQL запросы:**
```sql
-- Поиск по гражданству
SELECT * FROM habr_resumes
WHERE citizenship = 'Россия'
  AND public = true;

-- Статистика по возрасту
SELECT age, COUNT(*) as count
FROM habr_resumes
WHERE age IS NOT NULL
GROUP BY age
ORDER BY count DESC;

-- Пользователи, готовые к удаленной работе
SELECT link, title, experience_text, citizenship, remote_work
FROM habr_resumes
WHERE remote_work LIKE '%удаленной работе%'
  AND public = true;
```

##### Известные ограничения
1. Данные извлекаются только из публичных профилей
2. Формат данных зависит от структуры HTML страницы
3. При изменении структуры сайта потребуется обновление селекторов
4. Поля допускают NULL значения (не все профили содержат все данные)

##### Следующие шаги
1. Выполнить миграцию базы данных
2. Протестировать на реальных данных
3. Собрать статистику по заполненности полей
4. При необходимости скорректировать селекторы
5. Добавить unit-тесты для новых методов
6. Рассмотреть возможность добавления других полей

✅ **Обратная совместимость:** Полностью обратно совместимо. Старый код продолжит работать, новые поля опциональны.

### Файлы изменены

#### SQL
- `sql/alter_resumes_add_additional_fields.sql` - миграция для добавления новых полей

#### Helper.Dom
- `JobBoardScraper/Helper.Dom/ProfileDataExtractor.cs` - добавлен метод `ExtractAdditionalProfileData()`

#### DatabaseClient
- `JobBoardScraper/DatabaseClient.cs`:
  - Добавлен тип `DbRecordType.UserAdditionalData`
  - Добавлено поле `AdditionalData` в структуру `DbRecord`
  - Добавлена перегрузка `EnqueueUserResumeDetail()` с дополнительными параметрами
  - Добавлен метод `DatabaseUpdateUserAdditionalData()`
  - Добавлена обработка `UserAdditionalData` в switch statement

#### Scrapers
- `JobBoardScraper/WebScraper/UserResumeDetailScraper.cs`:
  - Использует `ExtractAdditionalProfileData()` для извлечения данных
  - Передает дополнительные данные в `EnqueueUserResumeDetail()`
  - Выводит дополнительные данные в лог

- `JobBoardScraper/WebScraper/UserProfileScraper.cs`:
  - Рефакторинг: использует `ExtractWorkExperienceAndLastVisit()` вместо дублирования кода

### Архитектурные улучшения

1. **Централизация логики парсинга** - методы вынесены в `Helper.Dom.ProfileDataExtractor`
2. **Переиспользование кода** - `UserProfileScraper` теперь использует общие методы
3. **Обратная совместимость** - старый код продолжит работать
4. **Расширяемость** - легко добавить новые поля в будущем

### Использование

```bash
# 1. Выполнить миграцию базы данных
psql -U postgres -d your_database -f sql/alter_resumes_add_additional_fields.sql

# 2. Запустить скрапер
dotnet run

# 3. Проверить данные
SELECT link, age, registration, citizenship, remote_work
FROM habr_resumes
WHERE age IS NOT NULL
LIMIT 10;
```

### Пример вывода

```
Пользователь https://career.habr.com/username:
  О себе: Опытный разработчик...
  Навыки: 15 шт.
  Опыт работы: 3 записей
  Возраст: 37 лет
  Опыт работы (текст): 9 лет и 1 месяц
  Регистрация: 30.08.2022
  Гражданство: Россия
  Удаленная работа: готов к удаленной работе
  Статус: публичный профиль
```

---

## [2.5.0] - 2024-12-18

### Добавлено

#### Новая архитектура управления прокси (Proxy Coordinator)
- Разделение ответственности между компонентами прокси-системы
- Новые классы:
  - `IProxyManager` - общий интерфейс для менеджеров прокси
  - `ProxyCoordinator` - координатор между whitelist и general pool
  - `GeneralPoolManager` - менеджер общего пула с blacklist и событиями верификации
- Обмен данными через события:
  - `OnProxyVerified` - прокси из general pool подтверждён как рабочий
  - `OnProxyBlacklisted` - прокси забанен после превышения лимита ошибок

### Изменено

#### ProxyWhitelistManager
- Теперь отвечает только за whitelist (без управления general pool)
- Убрана зависимость от `FreeProxyPool` в конструкторе
- Добавлен метод `AddProxy()` для ручного добавления прокси
- Реализует интерфейс `IProxyManager`

#### UserResumeDetailScraper
- Использует `ProxyCoordinator` вместо отдельных `FreeProxyPool` и `ProxyWhitelistManager`
- Упрощена логика получения и ротации прокси
- Исправлена ошибка: при HTTP ошибках (5xx, 403, 429) теперь вызывается `ReportFailure()` для смены прокси

### Исправлено

#### Ротация прокси при ошибках
- Исправлена проблема повторного использования одного прокси при ошибках 530
- Теперь при любой HTTP ошибке прокси меняется на следующий

---

## [2.4.0] - 2024-12-11

### Добавлено

#### Парсинг участия в профсообществах (Community Participation)
- Извлечение блока "Участие в профсообществах" из профилей резюме (Хабр, GitHub и др.)
- Новая модель данных:
  - `CommunityParticipationData` - данные об участии (Name, MemberSince, Contribution, Topics)
- Новые методы в `ProfileDataExtractor`:
  - `ExtractCommunityParticipationData()` - извлечение данных об участии в сообществах
  - `ExtractSingleCommunityParticipationItem()` - парсинг одного элемента
- Новые методы в `DatabaseClient`:
  - `DatabaseUpdateUserCommunityParticipation()` - сохранение данных в JSONB поле
- Новое поле в таблице `habr_resumes`:
  - `community_participation` (JSONB) - массив объектов с данными об участии
- CSS-селекторы для участия в профсообществах в `AppConfig`
- Property-based тесты (FsCheck) для валидации JSON сериализации

#### SQL миграции
- `sql/alter_resumes_add_community_participation.sql` - добавление поля community_participation

### Изменено

#### UserResumeDetailScraper
- Интеграция извлечения участия в профсообществах в основной процесс обхода
- Автоматическое сохранение данных в JSONB поле
- Логирование количества записей участия в сообществах

#### DatabaseClient
- Добавлен новый тип `DbRecordType.UserCommunityParticipation`
- Расширена структура `DbRecord` полем `CommunityParticipation`
- Обновлён метод `EnqueueUserResumeDetail()` с параметром `communityParticipation`

---

## [2.3.0] - 2024-12-10

### Добавлено

#### Парсинг высшего образования (University Education Scraper)
- Извлечение блока "Высшее образование" из профилей резюме
- Новые модели данных:
  - `UniversityData` - данные университета (HabrId, Name, City, GraduateCount)
  - `CourseData` - данные курса с JSON-сериализацией
  - `UserUniversityData` - связь пользователь-университет
  - `UniversityEducationData` - комбинированная модель
- Новые методы в `ProfileDataExtractor`:
  - `ExtractEducationData()` - извлечение данных об образовании
  - `ParseCoursePeriod()` - парсинг периода курса
  - `ExtractUniversityIdFromUrl()` - извлечение ID из URL
- Новые методы в `DatabaseClient`:
  - `EnqueueUniversity()` / `FlushUniversityQueue()` - очередь университетов
  - `EnqueueUserUniversity()` / `FlushUserUniversityQueue()` - очередь связей
- Новые таблицы БД:
  - `habr_universities` - справочник университетов
  - `habr_resumes_universities` - связь резюме с университетами (курсы в JSONB)
- CSS-селекторы для образования в `AppConfig`
- Property-based тесты (FsCheck) для валидации

#### Документация
- [UNIVERSITY_EDUCATION_SCRAPER.md](docs/UNIVERSITY_EDUCATION_SCRAPER.md) - полная документация
- Обновлён [USER_RESUME_DETAIL_SCRAPER.md](docs/USER_RESUME_DETAIL_SCRAPER.md)

#### SQL миграции
- `sql/create_universities_table.sql` - таблица университетов
- `sql/create_resumes_universities_table.sql` - связь резюме-университеты

### Изменено

#### UserResumeDetailScraper
- Интеграция извлечения образования в основной процесс обхода
- Автоматическое сохранение университетов и связей
- Логирование количества записей образования

---

## [2.2.0] - 2024-12-03

### Добавлено

#### ExponentialBackoff - умная стратегия повторов
- Класс `ExponentialBackoff` в `Helper.Utils/ExponentialBackoff.cs`
- Алгоритм Exponential Backoff with Jitter для HTTP ошибок
- Специализированные методы для разных типов ошибок:
  - `CalculateServerErrorDelay()` - для ошибок сервера (5xx): baseDelay=2с, maxDelay=60с
  - `CalculateProxyErrorDelay()` - для ошибок прокси/сети: baseDelay=0.5с, maxDelay=10с
- Метод `GetDelayDescription()` для форматированного вывода задержки
- Потокобезопасная реализация с использованием `lock`
- Поддержка Retry-After заголовка

#### Поле job_search_status
- Новое поле `job_search_status` в таблице `habr_resumes`
- SQL миграция `sql/alter_resumes_add_job_search_status.sql`
- Поддержка значений: "Ищу работу", "Не ищу работу", "Рассматриваю предложения"
- Индекс для быстрого поиска по статусу

##### Сводка реализации: Поле job_search_status

**Обзор**
Добавлено новое поле `job_search_status` в таблицу `habr_resumes` для хранения статуса поиска работы пользователя.

**Выполненные изменения**

1. **SQL миграция**
Создан файл `sql/alter_resumes_add_job_search_status.sql`:
- Добавлено поле `job_search_status` типа `text`
- Создан индекс для быстрого поиска по статусу
- Добавлен комментарий к полю

2. **Модель данных**
Обновлен `JobBoardScraper/Models/UserProfileData.cs`:
- Добавлено поле `JobSearchStatus` в структуру

3. **DatabaseClient**
Обновлен `JobBoardScraper/DatabaseClient.cs`:
- Добавлен параметр `jobSearchStatus` в метод `DatabaseInsert`
- Обновлены SQL запросы INSERT и UPDATE для включения поля `job_search_status`
- Добавлено логирование статуса поиска работы
- Обновлены все места создания `UserProfileData` для передачи `JobSearchStatus`

4. **Извлечение данных**
Данные извлекаются методом `ProfileDataExtractor.ExtractSalaryAndJobStatus()` и передаются через `EnqueueUserResumeDetail()`.

**Возможные значения**
- "Ищу работу"
- "Не ищу работу"
- "Рассматриваю предложения"

**Применение миграции**
```bash
psql -U postgres -d jobs -f sql/alter_resumes_add_job_search_status.sql
```

**Проверка**
Код успешно компилируется:
```
dotnet build
Сборка успешно выполнено с предупреждениями (6) через 2,7 с
```

**Итог**
Статус поиска работы теперь сохраняется в отдельном поле таблицы `habr_resumes`, что позволяет эффективно фильтровать и анализировать пользователей по их статусу поиска работы.

#### Документация
- [JOB_SEARCH_STATUS_FIELD_SUMMARY.md](JOB_SEARCH_STATUS_FIELD_SUMMARY.md) - полная документация
- [BACKOFF_ALGORITHMS.md](docs/BACKOFF_ALGORITHMS.md) - полное описание алгоритмов задержки
- [HTTP_ERROR_RETRY_STRATEGY.md](docs/HTTP_ERROR_RETRY_STRATEGY.md) - стратегия повторов для HTTP ошибок

### Изменено

#### Рефакторинг ProfileDataExtractor
- Перенос кода извлечения имени, должностей, уровня, зарплаты и статуса поиска работы из `ResumeListPageScraper` в переиспользуемый класс `Helper.Dom.ProfileDataExtractor`
- Устранено дублирование кода между скраперами
- `UserResumeDetailScraper` уже использует методы `ProfileDataExtractor`:
  - `ExtractUserName()` - имя пользователя
  - `ExtractInfoTechAndLevel()` - техническая информация и уровень
  - `ExtractSalaryAndJobStatus()` - зарплата и статус поиска работы

##### Новые методы для списков резюме

###### ExtractNameInfoTechAndLevel
- Извлекает имя, должности и уровень из секции профиля в списке резюме
- Парсит текст вида `Должность 1 • Должность 2 • Уровень`

```csharp
public static (string? name, string? infoTech, string? levelTitle) ExtractNameInfoTechAndLevel(
    IElement section,
    string profileLinkSelector = "a[href^='/']",
    string separatorSelector = "span.bullet")
```

Пример использования:

```csharp
var (name, infoTech, levelTitle) = Helper.Dom.ProfileDataExtractor.ExtractNameInfoTechAndLevel(
    section,
    AppConfig.ResumeListProfileLinkSelector,
    AppConfig.ResumeListSeparatorSelector);
```

###### ExtractSalaryFromSection
- Извлекает зарплату из секции профиля в списке резюме
- Парсит текст вида `От 80 000 ₽`

```csharp
public static int? ExtractSalaryFromSection(
    IElement section,
    string? salaryRegex = null)
```

Пример использования:

```csharp
var salary = Helper.Dom.ProfileDataExtractor.ExtractSalaryFromSection(
    section,
    AppConfig.ResumeListSalaryRegex);
```

###### ExtractJobSearchStatusFromSection
- Извлекает статус поиска работы из секции профиля в списке резюме
- Поддерживает значения `Ищу работу`, `Не ищу работу`, `Рассматриваю предложения`

```csharp
public static string? ExtractJobSearchStatusFromSection(IElement section)
```

Пример использования:

```csharp
var jobSearchStatus = Helper.Dom.ProfileDataExtractor.ExtractJobSearchStatusFromSection(section);
```

##### Обновление ResumeListPageScraper

Код извлечения данных был заменён на вызовы методов из `ProfileDataExtractor`.

Было:

```csharp
// Извлечение имени
var name = profileLink.TextContent?.Trim();

// Извлечение должностей и уровня
var parts = allText.Split('•', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
if (parts.Length > 0)
{
    levelTitle = parts[^1].Trim();
    if (parts.Length > 1)
    {
        infoTech = string.Join(" • ", parts[..^1]);
    }
}

// Извлечение зарплаты
var salaryMatch = System.Text.RegularExpressions.Regex.Match(text, AppConfig.ResumeListSalaryRegex);
if (salaryMatch.Success)
{
    var salaryStr = salaryMatch.Groups[1].Value.Replace(" ", "");
    if (int.TryParse(salaryStr, out var salaryValue))
    {
        salary = salaryValue;
    }
}
```

Стало:

```csharp
// Извлекаем имя, должности и уровень используя Helper.Dom.ProfileDataExtractor
var (name, infoTech, levelTitle) = Helper.Dom.ProfileDataExtractor.ExtractNameInfoTechAndLevel(
    section,
    AppConfig.ResumeListProfileLinkSelector,
    AppConfig.ResumeListSeparatorSelector);

// Извлекаем зарплату используя Helper.Dom.ProfileDataExtractor
var salary = Helper.Dom.ProfileDataExtractor.ExtractSalaryFromSection(
    section,
    AppConfig.ResumeListSalaryRegex);
```

##### Структура ProfileDataExtractor

Методы для детальных страниц профиля:
- `ExtractWorkExperienceAndLastVisit()` - опыт работы и последний визит
- `ExtractTextAfterPrefix()` - текст после префикса
- `ExtractAdditionalProfileData()` - дополнительные данные (возраст, гражданство и т.д.)
- `ExtractUserName()` - имя пользователя
- `ExtractInfoTechAndLevel()` - техническая информация и уровень
- `ExtractSalaryAndJobStatus()` - зарплата и статус поиска работы

Методы для списков резюме:
- `ExtractNameInfoTechAndLevel()` - имя, должности и уровень
- `ExtractSalaryFromSection()` - зарплата
- `ExtractJobSearchStatusFromSection()` - статус поиска работы

##### Преимущества
- Переиспользование кода: логика извлечения данных теперь находится в одном месте
- Упрощение поддержки: изменения в логике парсинга нужно делать только в одном месте
- Консистентность: скраперы используют одинаковую логику извлечения данных
- Тестируемость: методы извлечения можно тестировать независимо от скраперов

##### Проверка
Код успешно компилируется без ошибок:

```text
dotnet build
Сборка успешно выполнено с предупреждениями (6) через 1,4 с
```

#### Улучшение UserResumeDetailScraper
- Интеграция ExponentialBackoff для HTTP ошибок
- Разные параметры задержки для server errors и proxy errors
- Улучшенное логирование с отображением задержки

#### Обновление DatabaseClient
- Метод `EnqueueUserResumeDetail` принимает параметр `jobSearchStatus`
- Метод `DatabaseInsert` поддерживает поле `job_search_status`
- Обновлены SQL запросы INSERT и UPDATE

#### Исправление ParallelScraperLogger
- Удалены дублирующиеся префиксы имени класса в логах

### Исправлено

- Дублирование кода извлечения данных между скраперами
- Фиксированная задержка при HTTP ошибках (заменена на экспоненциальную)
- Дублирование имени класса в логах ParallelScraperLogger

---

## [2.1.0] - 2024-11-28

### Добавлено

#### ProxyRotator - система ротации прокси-серверов
- Автоматическая ротация прокси-серверов для распределения нагрузки
- Поддержка множественных прокси с циклическим переключением
- Поддержка различных типов прокси: HTTP, HTTPS, SOCKS5
- Поддержка аутентификации (username:password)
- Ручная и автоматическая ротация
- Опциональное использование (можно отключить для конкретных скраперов)
- Настройки в `App.config`:
  - `Proxy:Enabled` - включить/выключить прокси
  - `Proxy:List` - список прокси-серверов (через ; или ,)
  - `Proxy:RotationIntervalSeconds` - интервал автоматической ротации
  - `Proxy:AutoRotate` - автоматическая ротация при каждом запросе

#### HttpClientFactory - фабрика для создания HttpClient
- Метод `CreateHttpClient()` - создание HttpClient с опциональной поддержкой прокси
- Метод `CreateProxyRotator()` - создание ProxyRotator из конфигурации
- Метод `CreateDefaultClient()` - создание HttpClient по умолчанию (обратная совместимость)
- Автоматическая настройка compression (GZip, Deflate)

#### Расширение SmartHttpClient
- Поддержка ProxyRotator в конструкторе
- Метод `GetProxyStatus()` - получение информации о текущем прокси
- Метод `RotateProxy()` - ручная ротация прокси
- Полная обратная совместимость (прокси опциональны)

#### ProxyProvider - динамическое получение прокси
- Автоматическая загрузка прокси из публичных источников
- Поддержка ProxyScrape API
- Поддержка GeoNode API
- Загрузка/сохранение из файла
- Проверка работоспособности прокси
- Автоматическое удаление нерабочих прокси

#### DynamicProxyRotator - автоматическое обновление
- Автоматическое обновление списка прокси по расписанию
- Принудительное обновление по команде
- Интеграция с ProxyProvider
- Настраиваемый интервал обновления

#### Документация по прокси
- [PROXY_ROTATION.md](docs/PROXY_ROTATION.md) - полная документация
- [PROXY_USAGE_EXAMPLE.md](docs/PROXY_USAGE_EXAMPLE.md) - примеры использования
- [PROXY_QUICKSTART.md](docs/PROXY_QUICKSTART.md) - быстрый старт
- [PROXY_CONFIG_EXAMPLES.md](docs/PROXY_CONFIG_EXAMPLES.md) - примеры конфигураций
- [DYNAMIC_PROXY.md](docs/DYNAMIC_PROXY.md) - динамическое обновление прокси
- [PROXY_SERVICES.md](docs/PROXY_SERVICES.md) - коммерческие прокси-сервисы
- [FREE_PROXY_SOURCES.md](docs/FREE_PROXY_SOURCES.md) - бесплатные источники прокси

---

## [2.0.0] - 2024-11-04

### Добавлено

#### ExpertsScraper - новый скрапер для обхода экспертов
- Обход страниц экспертов с `https://career.habr.com/experts`
- Извлечение данных эксперта:
  - Имя и ссылка на профиль
  - Код пользователя из URL
  - Стаж работы (например, "9 лет и 9 месяцев")
  - Флаг эксперта (`expert = true`)
- Извлечение компаний из карточек экспертов
- Автоматическая пагинация
- Режим `UpdateIfExists` для обновления существующих записей
- Настраиваемый интервал обхода (по умолчанию: 4 дня)
- Поддержка всех режимов вывода (`ConsoleOnly`, `FileOnly`, `Both`)

#### Расширение структуры базы данных
- Новый столбец `code` - код пользователя из URL профиля
- Новый столбец `expert` - флаг эксперта (boolean)
- Новый столбец `work_experience` - стаж работы (text)
- Индексы для быстрого поиска по `code` и `expert`
- SQL-скрипт `add_expert_columns.sql` для миграции

#### SmartHttpClient - универсальная обёртка над HttpClient
- Объединяет функциональность `HttpRetry` и `TrafficMeasuringHttpClient`
- Автоматические повторы с экспоненциальной задержкой
- Измерение HTTP-трафика для каждого скрапера
- Настраиваемый timeout для каждого скрапера
- Поддержка заголовка `Retry-After`
- Индивидуальные настройки для каждого скрапера

#### Система измерения HTTP-трафика
- Класс `TrafficStatistics` для сбора статистики
- Автоматический подсчёт размера каждого HTTP-ответа
- Статистика по каждому скраперу отдельно
- Общая статистика по всем скраперам
- Сохранение в файл с настраиваемым интервалом (по умолчанию: 5 минут)
- Настройки в `App.config`:
  - `Traffic:OutputFile` - путь к файлу статистики
  - `Traffic:SaveIntervalMinutes` - интервал сохранения

#### Управление скраперами через конфигурацию
- Каждый скрапер можно включить/отключить через `App.config`
- Настройки `{Scraper}:Enabled` для всех скраперов:
  - `BruteForce:Enabled`
  - `ResumeList:Enabled`
  - `Companies:Enabled`
  - `Category:Enabled`
  - `CompanyFollowers:Enabled`
  - `Experts:Enabled`
- Вывод статуса каждого скрапера при запуске

#### Независимое логирование для каждого скрапера
- Класс `ConsoleLogger` для управления выводом
- Три режима вывода: `ConsoleOnly`, `FileOnly`, `Both`
- Отдельные лог-файлы для каждого скрапера
- Формат имени файла: `{ScraperName}_{timestamp}.log`
- Настройка режима через `App.config`:
  - `Companies:OutputMode`
  - `CompanyFollowers:OutputMode`
  - `Experts:OutputMode`

#### Документация
- `QUICKSTART.md` - быстрый старт для новых пользователей
- `CHANGELOG.md` - история изменений (включает руководство по миграции)
- Обновлён `README.md` с описанием ExpertsScraper
- Обновлён `sql/README.md` с примерами запросов для экспертов
- `docs/TRAFFIC_OPTIMIZATION.md` - оптимизация трафика

#### Руководство по миграции

##### Шаги миграции

###### 1. Обновление базы данных

Выполните SQL-скрипты для добавления новых столбцов:

```bash
# Добавление столбца slogan (если ещё не добавлен)
psql -U postgres -d jobs -f sql/add_slogan_column.sql

# Добавление уникального ограничения на link (если ещё не добавлено)
psql -U postgres -d jobs -f sql/add_unique_link_constraint.sql

# Добавление столбцов для экспертов (НОВОЕ)
psql -U postgres -d jobs -f sql/add_expert_columns.sql

# Добавление столбцов для детальной информации о компаниях (НОВОЕ)
psql -U postgres -d jobs -f sql/add_company_details_columns.sql

# Создание таблицы уровней (НОВОЕ)
psql -U postgres -d jobs -f sql/create_levels_table.sql

# Добавление столбцов для профилей пользователей (НОВОЕ)
psql -U postgres -d jobs -f sql/add_user_profile_columns.sql

# Добавление столбца "О себе" для резюме (НОВОЕ)
psql -U postgres -d jobs -f sql/add_user_about_column.sql

# Создание таблицы навыков пользователей (НОВОЕ)
psql -U postgres -d jobs -f sql/create_user_skills_table.sql

# Создание таблицы опыта работы (НОВОЕ)
psql -U postgres -d jobs -f sql/create_user_experience_table.sql

# Создание таблицы связи опыта работы и навыков (НОВОЕ)
psql -U postgres -d jobs -f sql/create_user_experience_skills_table.sql
```

###### 2. Обновление конфигурации

Добавьте в `App.config` новые настройки для всех новых скраперов:

```xml
<!-- ExpertsScraper Settings -->
<add key="Experts:Enabled" value="true" />
<add key="Experts:ListUrl" value="https://career.habr.com/experts?order=lastActive" />
<add key="Experts:EnableTrafficMeasuring" value="true" />
<add key="Experts:OutputMode" value="Both" />

<!-- CompanyDetailScraper Settings -->
<add key="CompanyDetail:Enabled" value="false" />
<add key="CompanyDetail:TimeoutSeconds" value="60" />
<add key="CompanyDetail:EnableRetry" value="true" />
<add key="CompanyDetail:EnableTrafficMeasuring" value="true" />
<add key="CompanyDetail:OutputMode" value="Both" />

<!-- UserProfileScraper Settings -->
<add key="UserProfile:Enabled" value="false" />
<add key="UserProfile:TimeoutSeconds" value="60" />
<add key="UserProfile:EnableRetry" value="true" />
<add key="UserProfile:EnableTrafficMeasuring" value="true" />
<add key="UserProfile:OutputMode" value="Both" />

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

Также добавьте настройки статистики трафика (если их нет):

```xml
<!-- Traffic Statistics Settings -->
<add key="Traffic:OutputFile" value="./logs/traffic_stats.txt" />
<add key="Traffic:SaveIntervalMinutes" value="5" />
```

###### 3. Создание директории для логов

```bash
mkdir logs
```

###### 4. Пересборка проекта

```bash
dotnet build JobBoardScraper/JobBoardScraper.csproj -c Release
```

###### 5. Запуск приложения

```bash
dotnet run --project JobBoardScraper
```

При запуске вы увидите статус всех скраперов:

```
[Program] ResumeListPageScraper: ОТКЛЮЧЕН
[Program] CompanyListScraper: ОТКЛЮЧЕН
[Program] CategoryScraper: ОТКЛЮЧЕН
[Program] CompanyFollowersScraper: ВКЛЮЧЕН
[Program] ExpertsScraper: ВКЛЮЧЕН
[Program] BruteForceUsernameScraper: ОТКЛЮЧЕН
```

##### Проверка миграции

###### Проверка структуры БД

```sql
-- Проверка наличия новых столбцов
\d habr_resumes

-- Должны быть столбцы:
-- - code (text)
-- - expert (boolean)
-- - work_experience (text)
```

###### Проверка работы ExpertsScraper

После запуска проверьте логи:

```bash
# Консольный вывод
tail -f logs/ExpertsScraper_*.log

# Статистика трафика
cat logs/traffic_stats.txt
```

###### Проверка данных в БД

```sql
-- Количество экспертов
SELECT COUNT(*) FROM habr_resumes WHERE expert = TRUE;

-- Примеры записей экспертов
SELECT title, code, work_experience, link
FROM habr_resumes
WHERE expert = TRUE
LIMIT 10;
```

##### Откат изменений

Если что-то пошло не так, вы можете откатить изменения:

###### Откат изменений в БД

```sql
-- Удаление столбцов экспертов
ALTER TABLE habr_resumes DROP COLUMN IF EXISTS code;
ALTER TABLE habr_resumes DROP COLUMN IF EXISTS expert;
ALTER TABLE habr_resumes DROP COLUMN IF EXISTS work_experience;

-- Удаление индексов
DROP INDEX IF EXISTS idx_habr_resumes_code;
DROP INDEX IF EXISTS idx_habr_resumes_expert;
```

###### Откат конфигурации

Просто отключите ExpertsScraper в `App.config`:

```xml
<add key="Experts:Enabled" value="false" />
```

##### Часто задаваемые вопросы

###### Q: Нужно ли останавливать приложение для миграции?

**A:** Да, рекомендуется остановить приложение перед выполнением SQL-скриптов.

###### Q: Что делать, если скрипт add_expert_columns.sql выдаёт ошибку?

**A:** Скрипт использует `IF NOT EXISTS`, поэтому безопасен для повторного выполнения. Если ошибка сохраняется, проверьте права доступа к БД.

###### Q: Можно ли запустить только ExpertsScraper?

**A:** Да, установите `Experts:Enabled = true` и отключите остальные скраперы.

###### Q: Как часто ExpertsScraper обходит страницы?

**A:** По умолчанию каждые 4 дня. Интервал задаётся в коде (`TimeSpan.FromDays(4)`).

###### Q: Где хранятся логи ExpertsScraper?

**A:** В директории `./logs/` с именем `ExpertsScraper_{timestamp}.log` (если `OutputMode = Both` или `FileOnly`).

###### Q: Сколько трафика потребляет ExpertsScraper?

**A:** Зависит от количества страниц. Статистика сохраняется в `./logs/traffic_stats.txt`.

### Изменено

#### Рефакторинг системы логирования
- Удалён глобальный `Console.SetOut()`
- Каждый скрапер использует свой экземпляр `ConsoleLogger`
- Логи больше не смешиваются между скраперами
- Улучшена читаемость логов

#### Рефакторинг HTTP-клиентов
- Удалён класс `HttpRetry` (заменён на `SmartHttpClient`)
- Удалён класс `TrafficMeasuringHttpClient` (заменён на `SmartHttpClient`)
- Удалён класс `DualWriter` (заменён на `ConsoleLogger`)
- Все скраперы используют `SmartHttpClient`

#### Улучшение DatabaseClient
- Добавлена поддержка новых полей (`code`, `expert`, `work_experience`)
- Метод `EnqueueResume` принимает дополнительные параметры
- Метод `DatabaseInsert` поддерживает новые поля
- Улучшена обработка режима `UpdateIfExists`

#### Обновление Program.cs
- Инициализация `TrafficStatistics`
- Создание `SmartHttpClient` для каждого скрапера
- Вывод статуса каждого скрапера при запуске
- Добавлен ExpertsScraper в список процессов

### Удалено
- Класс `HttpRetry` (заменён на `SmartHttpClient`)
- Класс `TrafficMeasuringHttpClient` (заменён на `SmartHttpClient`)
- Класс `DualWriter` (заменён на `ConsoleLogger`)
- Глобальное перенаправление `Console.SetOut()`

### Исправлено
- Проблема смешанных логов от разных скраперов
- Дублирование кода HTTP-запросов
- Отсутствие контроля трафика
- Невозможность отключить отдельные скраперы
- Проблемы с кодировкой HTML (правильное определение из заголовков)

---

## 📋 Implementation Checklist for Version 2.0

### ✅ 1. Logging System Refactoring

#### 1.1 ConsoleLogger
- [x] Created class `ConsoleLogger` in `Helper.ConsoleHelper/ConsoleLogger.cs`
- [x] Supports three modes: `ConsoleOnly`, `FileOnly`, `Both`
- [x] Automatic log file creation with timestamp
- [x] Implements `IDisposable` for proper file closing
- [x] Method `SetOutputMode()` for changing output mode

#### 1.2 OutputMode
- [x] Created enum `OutputMode` in `Helper.ConsoleHelper/OutputMode.cs`
- [x] Three values: `ConsoleOnly`, `FileOnly`, `Both`

#### 1.3 Removed old code
- [x] Removed class `DualWriter`
- [x] Removed global `Console.SetOut()`
- [x] All scrapers migrated to `ConsoleLogger`

### ✅ 2. SmartHttpClient - Universal Wrapper

#### 2.1 SmartHttpClient Creation
- [x] Created class `SmartHttpClient.cs`
- [x] Combines retry functionality and traffic measurement
- [x] Configurable timeout for each scraper
- [x] Supports `Retry-After` header
- [x] Exponential delay between attempts
- [x] Handles transient errors (408, 429, 500, 502, 503, 504)

#### 2.2 Removed old classes
- [x] Removed class `HttpRetry`
- [x] Removed class `TrafficMeasuringHttpClient`
- [x] All scrapers migrated to `SmartHttpClient`

#### 2.3 Integration with scrapers
- [x] BruteForceUsernameScraper uses SmartHttpClient
- [x] ResumeListPageScraper uses SmartHttpClient
- [x] CompanyListScraper uses SmartHttpClient
- [x] CategoryScraper uses SmartHttpClient
- [x] CompanyFollowersScraper uses SmartHttpClient
- [x] ExpertsScraper uses SmartHttpClient

### ✅ 3. HTTP Traffic Measurement System

#### 3.1 TrafficStatistics
- [x] Created class `TrafficStatistics.cs`
- [x] Traffic counting for each scraper separately
- [x] Overall statistics for all scrapers
- [x] Automatic saving to file
- [x] Configurable save interval
- [x] Formatted statistics output

#### 3.2 App.config Settings
- [x] `Traffic:OutputFile` - path to statistics file
- [x] `Traffic:SaveIntervalMinutes` - save interval
- [x] `{Scraper}:EnableTrafficMeasuring` for each scraper

#### 3.3 Program.cs Integration
- [x] Initialization of `TrafficStatistics` at startup
- [x] Passing to each `SmartHttpClient`
- [x] Automatic statistics saving

### ✅ 4. Scraper Management via Configuration

#### 4.1 App.config Settings
- [x] `BruteForce:Enabled`
- [x] `ResumeList:Enabled`
- [x] `Companies:Enabled`
- [x] `Category:Enabled`
- [x] `CompanyFollowers:Enabled`
- [x] `Experts:Enabled`
- [x] `CompanyDetail:Enabled`
- [x] `UserProfile:Enabled`

#### 4.2 AppConfig.cs Updates
- [x] Added properties for all `{Scraper}:Enabled`
- [x] Added properties for ExpertsScraper settings
- [x] Added properties for CompanyDetailScraper settings
- [x] Added properties for UserProfileScraper settings
- [x] Added properties for traffic statistics

#### 4.3 Program.cs Updates
- [x] Check `{Scraper}:Enabled` before starting
- [x] Output status of each scraper at startup
- [x] Conditional scraper startup

### ✅ 5. ExpertsScraper - New Scraper

#### 5.1 Scraper Creation
- [x] Created class `ExpertsScraper.cs`
- [x] Crawls pages from `https://career.habr.com/experts`
- [x] Automatic pagination
- [x] Extracts expert data (name, link, code, experience)
- [x] Extracts companies from cards
- [x] Uses `ConsoleLogger`
- [x] Uses `SmartHttpClient`
- [x] Implements `IDisposable`

#### 5.2 Data Extraction
- [x] Parses expert cards (`.expert-card`)
- [x] Extracts name and link (`a.expert-card__title-link`)
- [x] Extracts code from URL (regex `/([^/]+)$`)
- [x] Extracts work experience (`span` with text "Стаж ")
- [x] Extracts company (`a.link-comp`)
- [x] Extracts company code (regex `/companies/([^/]+)`)

#### 5.3 Program.cs Integration
- [x] Creates `SmartHttpClient` for ExpertsScraper
- [x] Initializes ExpertsScraper
- [x] Runs in background mode
- [x] Configurable interval (default: 4 days)

### ✅ 7. CompanyDetailScraper - Company Detail Scraper

#### 7.1 Scraper Creation
- [x] Created class `CompanyDetailScraper.cs`
- [x] Crawls company detail pages
- [x] Extracts company_id from favorite button
- [x] Extracts title, about, description, site, rating
- [x] Extracts employee and follower counts
- [x] Extracts company skills
- [x] Extracts public employees
- [x] Uses `ConsoleLogger`
- [x] Uses `SmartHttpClient`

#### 7.2 Database Structure Expansion
- [x] Created `sql/add_company_details_columns.sql`
- [x] Added columns: company_id, title, about, description, site, rating
- [x] Added columns: current_employees, past_employees, followers, want_work
- [x] Added columns: employees_count, habr
- [x] Created habr_skills table
- [x] Created habr_company_skills table (many-to-many relationship)

#### 7.3 Program.cs Integration
- [x] Creates `SmartHttpClient` for CompanyDetailScraper
- [x] Initializes CompanyDetailScraper
- [x] Runs in background mode
- [x] Configurable interval (default: 30 days)

### ✅ 8. UserProfileScraper - User Profile Scraper

#### 8.1 Scraper Creation
- [x] Created class `UserProfileScraper.cs`
- [x] Crawls user profiles via /friends URL
- [x] Extracts username
- [x] Determines expert status
- [x] Extracts level (Junior, Middle, Senior, etc.)
- [x] Extracts technical information
- [x] Extracts salary expectations
- [x] Extracts work experience
- [x] Extracts last visit date
- [x] Determines profile publicity
- [x] Uses `ConsoleLogger`
- [x] Uses `SmartHttpClient`

#### 8.2 Database Structure Expansion
- [x] Created habr_levels table for storing levels
- [x] Created `sql/create_levels_table.sql`
- [x] Created `sql/add_user_profile_columns.sql`
- [x] Added columns: user_name, level_id, info_tech, salary
- [x] Added columns: last_visit, public
- [x] Created foreign key to habr_levels

#### 8.3 DatabaseClient Updates
- [x] Added `UserProfileData` structure
- [x] Method `EnqueueUserProfile` for adding to queue
- [x] Method `DatabaseUpdateUserProfile` for updating profile
- [x] Method `GetAllUsernames` for getting user list
- [x] Automatic level creation when needed

#### 8.4 Program.cs Integration
- [x] Creates `SmartHttpClient` for UserProfileScraper
- [x] Initializes UserProfileScraper
- [x] Runs in background mode
- [x] Configurable interval (default: 30 days)

### ✅ 9. UserResumeDetailScraper - User Resume Detail Scraper

#### 9.1 Scraper Creation
- [x] Created class `UserResumeDetailScraper.cs`
- [x] Crawls user resume pages
- [x] Extracts "About" text
- [x] Extracts skills list
- [x] Extracts work experience
- [x] Parses company information (code, URL, name, description, size)
- [x] Parses positions and work duration
- [x] Parses skills from experience
- [x] Uses `ConsoleLogger`
- [x] Uses `SmartHttpClient`
- [x] Uses `AdaptiveConcurrencyController`

#### 9.2 Database Structure Expansion
- [x] Created `sql/add_user_about_column.sql`
- [x] Added `about` column (TEXT) to habr_resumes
- [x] Created `sql/create_user_skills_table.sql`
- [x] Created habr_user_skills table (many-to-many relationship)
- [x] Created foreign key to habr_resumes
- [x] Created foreign key to habr_skills
- [x] Created unique index (user_id, skill_id)
- [x] Created `sql/create_user_experience_table.sql`
- [x] Created habr_user_experience table for work experience
- [x] Created `sql/create_user_experience_skills_table.sql`
- [x] Created habr_user_experience_skills table (many-to-many relationship)

#### 9.3 DatabaseClient Updates
- [x] Added types `DbRecordType.UserAbout`, `DbRecordType.UserSkills`, and `DbRecordType.UserExperience`
- [x] Added `UserExperienceData` structure
- [x] Method `EnqueueUserResumeDetail` for adding to queue
- [x] Method `DatabaseUpdateUserAbout` for updating "About" text
- [x] Method `DatabaseInsertUserSkills` for inserting skills
- [x] Method `EnqueueUserExperience` for adding work experience to queue
- [x] Method `DatabaseInsertUserExperience` for inserting work experience
- [x] Processing of UserAbout, UserSkills, and UserExperience in `StartWriterTask`
- [x] Automatic creation/update of companies when adding experience
- [x] Cascading deletion of old experience records before inserting new ones
- [x] `IsFirstRecord` flag for optimization (only for first record)

#### 9.4 AppConfig.cs Updates
- [x] Added settings `UserResumeDetail:Enabled`
- [x] Added settings `UserResumeDetail:TimeoutSeconds`
- [x] Added settings `UserResumeDetail:EnableRetry`
- [x] Added settings `UserResumeDetail:EnableTrafficMeasuring`
- [x] Added settings `UserResumeDetail:OutputMode`
- [x] Added selectors `UserResumeDetail:ContentSelector`
- [x] Added selectors `UserResumeDetail:SkillSelector`

#### 9.5 App.config Updates
- [x] Added UserResumeDetailScraper Settings section
- [x] Configured all parameters with default values
- [x] Added CSS selectors for parsing

#### 9.6 Program.cs Integration
- [x] Creates `SmartHttpClient` for UserResumeDetailScraper
- [x] Initializes UserResumeDetailScraper
- [x] Runs in background mode
- [x] Configurable interval (default: 30 days)
- [x] Updated comment (10 processes instead of 9)

#### 9.7 Documentation
- [x] Created `docs/USER_RESUME_DETAIL_SCRAPER.md`
- [x] Updated `README.md` with UserResumeDetailScraper description
- [x] Updated `IMPLEMENTATION_CHECKLIST.md`

### ✅ 10. Database Structure Expansion for Experts

#### 6.1 New Columns in habr_resumes
- [x] Column `code` (TEXT) - user code
- [x] Column `expert` (BOOLEAN) - expert flag
- [x] Column `work_experience` (TEXT) - work experience
- [x] Index on `code`
- [x] Index on `expert` (WHERE expert = TRUE)

#### 6.2 SQL Scripts
- [x] Created `sql/add_expert_columns.sql`
- [x] Uses `IF NOT EXISTS` for safety
- [x] Column comments
- [x] Index creation

#### 6.3 DatabaseClient Updates
- [x] Method `EnqueueResume` accepts new parameters
- [x] Method `DatabaseInsert` supports new fields
- [x] Support for `UpdateIfExists` mode for experts
- [x] Outputs expert information in logs

### ✅ 11. Documentation

#### 11.1 Main Documentation
- [x] Updated `README.md` with ExpertsScraper description
- [x] Added section about SmartHttpClient
- [x] Added section about traffic measurement system
- [x] Added section about scraper management
- [x] Updated architecture (8 processes instead of 5)

#### 11.2 SQL Documentation
- [x] Updated `sql/README.md`
- [x] Added section about expert columns
- [x] Added SQL query examples for experts
- [x] Added expert statistics

#### 11.3 Guides
- [x] Created `MIGRATION_GUIDE.md` - migration guide
- [x] Created `QUICKSTART.md` - quick start guide
- [x] Created `CHANGELOG.md` - change log
- [x] Created `IMPLEMENTATION_CHECKLIST.md` - this document

#### 11.4 Technical Documentation
- [x] `docs/TRAFFIC_OPTIMIZATION.md` - traffic optimization

### ✅ 12. Testing and Verification

#### 12.1 Compilation
- [x] Verified compilation of all files
- [x] No errors in getDiagnostics
- [x] Verified all dependencies

#### 12.2 Configuration
- [x] All settings added to `App.config`
- [x] All settings added to `AppConfig.cs`
- [x] Default values are correct

#### 12.3 SQL Scripts
- [x] All scripts use `IF NOT EXISTS`
- [x] Scripts are safe for repeated execution
- [x] Indexes created correctly

### 📊 Change Statistics

#### Created Files
1. `JobBoardScraper/Helper.ConsoleHelper/ConsoleLogger.cs`
2. `JobBoardScraper/Helper.ConsoleHelper/OutputMode.cs`
3. `JobBoardScraper/SmartHttpClient.cs`
4. `JobBoardScraper/TrafficStatistics.cs`
5. `JobBoardScraper/WebScraper/ExpertsScraper.cs`
6. `JobBoardScraper/WebScraper/CompanyDetailScraper.cs`
7. `JobBoardScraper/WebScraper/UserProfileScraper.cs`
8. `JobBoardScraper/WebScraper/UserResumeDetailScraper.cs`
9. `sql/add_expert_columns.sql`
10. `sql/add_company_details_columns.sql`
11. `sql/create_levels_table.sql`
12. `sql/add_user_profile_columns.sql`
13. `sql/add_user_about_column.sql`
14. `sql/create_user_skills_table.sql`
15. `sql/create_user_experience_table.sql`
16. `sql/create_user_experience_skills_table.sql`
17. `docs/USER_PROFILE_SCRAPER.md`
18. `docs/USER_RESUME_DETAIL_SCRAPER.md`
19. `MIGRATION_GUIDE.md`
20. `QUICKSTART.md`
21. `CHANGELOG.md`
22. `IMPLEMENTATION_CHECKLIST.md`

#### Removed Files
1. `JobBoardScraper/HttpRetry.cs`
2. `JobBoardScraper/TrafficMeasuringHttpClient.cs`
3. `JobBoardScraper/Helper.ConsoleHelper/DualWriter.cs`

#### Modified Files
1. `JobBoardScraper/Program.cs`
2. `JobBoardScraper/DatabaseClient.cs`
3. `JobBoardScraper/AppConfig.cs`
4. `JobBoardScraper/App.config`
5. `JobBoardScraper/README.md`
6. `sql/README.md`
7. `JobBoardScraper/WebScraper/BruteForceUsernameScraper.cs`
8. `JobBoardScraper/WebScraper/CompanyListScraper.cs`
9. `JobBoardScraper/WebScraper/CategoryScraper.cs`
10. `JobBoardScraper/WebScraper/CompanyFollowersScraper.cs`
11. `JobBoardScraper/WebScraper/ResumeListPageScraper.cs`

### 🎯 Final Results

#### Solved Problems
1. ✅ Mixed logs from different scrapers
2. ✅ Code duplication in HTTP requests
3. ✅ Lack of traffic control
4. ✅ Inability to disable individual scrapers
5. ✅ HTML encoding issues
6. ✅ Missing expert data
7. ✅ Limited database structure
8. ✅ Missing detailed company information
9. ✅ Missing user profile information
10. ✅ Missing "About" and skills from resumes

#### New Capabilities
1. ✅ Collecting expert data from career.habr.com
2. ✅ Collecting detailed company information (company_id, rating, skills, etc.)
3. ✅ Collecting user profile information (level, salary, experience, etc.)
4. ✅ Collecting "About" text and skills from user resumes
5. ✅ HTTP traffic measurement and statistics
6. ✅ Flexible scraper management
7. ✅ Independent logging for each scraper
8. ✅ Universal SmartHttpClient wrapper
9. ✅ Extended database structure with support for experts, companies, profiles, and skills
10. ✅ Determining user profile publicity
11. ✅ Many-to-many relationship table for user skills

#### Code Quality Improvements
1. ✅ Eliminated code duplication
2. ✅ Improved logging architecture
3. ✅ Uniform approach to HTTP requests
4. ✅ Better error handling
5. ✅ More readable logs
6. ✅ Complete documentation

### 🚀 Release Readiness
- [x] All features implemented
- [x] Code compiles without errors
- [x] Documentation updated
- [x] SQL scripts ready
- [x] Migration guides created
- [x] Changelog filled
- [x] Configuration set up

**Status: READY FOR RELEASE 2.0.0** ✨

---

## [1.0.0] - 2024-10-29

### Добавлено

- Первоначальный релиз
- BruteForceUsernameScraper - перебор имён пользователей
- ResumeListPageScraper - обход списка резюме
- CompanyListScraper - обход списка компаний
- CategoryScraper - сбор категорий
- CompanyFollowersScraper - обход подписчиков компаний
- AdaptiveConcurrencyController - адаптивное управление параллелизмом
- DatabaseClient - работа с PostgreSQL
- Базовая система логирования
- Конфигурация через App.config

---

## Формат

Этот changelog следует принципам [Keep a Changelog](https://keepachangelog.com/ru/1.0.0/),
и проект придерживается [Semantic Versioning](https://semver.org/lang/ru/).

### Типы изменений

- **Добавлено** - для новой функциональности
- **Изменено** - для изменений в существующей функциональности
- **Устарело** - для функциональности, которая скоро будет удалена
- **Удалено** - для удалённой функциональности
- **Исправлено** - для исправления багов
- **Безопасность** - для изменений, связанных с безопасностью
