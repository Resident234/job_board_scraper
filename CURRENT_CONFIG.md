# Текущая конфигурация скраперов

## ✅ Включенные скраперы

### UserResumeDetailScraper
- **Статус:** ✅ Включен
- **Настройка:** `UserResumeDetail:Enabled = true`
- **Описание:** Извлечение детальной информации из резюме пользователей
- **Функции:**
  - Текст "О себе"
  - Список навыков
  - Опыт работы с компаниями
  - Должности и навыки по позициям

## ❌ Отключенные скраперы

| Скрапер | Настройка | Описание |
|---------|-----------|----------|
| BruteForceUsernameScraper | `BruteForce:Enabled = false` | Перебор имен пользователей |
| CompanyListScraper | `Companies:Enabled = false` | Список компаний |
| CompanyFollowersScraper | `CompanyFollowers:Enabled = false` | Подписчики компаний |
| ResumeListPageScraper | `ResumeList:Enabled = false` | Список резюме |
| CategoryScraper | `Category:Enabled = false` | Категории |
| ExpertsScraper | `Experts:Enabled = false` | Эксперты |
| CompanyDetailScraper | `CompanyDetail:Enabled = false` | Детали компаний |
| UserProfileScraper | `UserProfile:Enabled = false` | Профили пользователей |
| UserFriendsScraper | `UserFriends:Enabled = false` | Друзья пользователей |
| CompanyRatingScraper | `CompanyRating:Enabled = false` | Рейтинги компаний |

## Настройки UserResumeDetailScraper

```xml
<add key="UserResumeDetail:Enabled" value="true" />
<add key="UserResumeDetail:TimeoutSeconds" value="60" />
<add key="UserResumeDetail:EnableRetry" value="true" />
<add key="UserResumeDetail:EnableTrafficMeasuring" value="true" />
<add key="UserResumeDetail:OutputMode" value="Both" />
```

## Как изменить конфигурацию

### Включить другой скрапер:
1. Откройте `JobBoardScraper/App.config`
2. Найдите нужный скрапер (например, `CompanyRating:Enabled`)
3. Измените `value="false"` на `value="true"`
4. Сохраните файл

### Отключить UserResumeDetailScraper:
```xml
<add key="UserResumeDetail:Enabled" value="false" />
```

### Включить несколько скраперов одновременно:
```xml
<add key="UserResumeDetail:Enabled" value="true" />
<add key="UserProfile:Enabled" value="true" />
<add key="CompanyDetail:Enabled" value="true" />
```

## Запуск

После изменения конфигурации просто запустите приложение:

```bash
dotnet run --project JobBoardScraper
```

Приложение автоматически запустит только включенные скраперы.

## Проверка конфигурации

При запуске приложение выведет информацию о включенных скраперах:

```
[Program] UserResumeDetailScraper: Enabled
[Program] CompanyRatingScraper: Disabled
[Program] ExpertsScraper: Disabled
...
```

## Дополнительная информация

- [USER_RESUME_DETAIL_SCRAPER.md](docs/USER_RESUME_DETAIL_SCRAPER.md) - Документация по UserResumeDetailScraper
- [App.config](JobBoardScraper/App.config) - Файл конфигурации
