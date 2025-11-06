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
psql -U postgres -d jobs -f sql/add_expert_columns.sql

# 4. Запустить приложение
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

### 🔧 