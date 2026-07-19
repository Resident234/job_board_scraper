# Testing and Deployment Guide

🧪 **Comprehensive testing procedures and deployment checklist**

## 🚀 Deployment Procedures

### Database Migration

```bash
# Apply database schema changes
psql -U postgres -d habr_career -f sql/alter_resumes_add_additional_fields.sql

# Verify table structure
\d habr_resumes
```

### Build and Run

```bash
# Build the project
dotnet build

# Run the scraper
dotnet run --project JobBoardScraper
```

## 🧪 Testing Procedures

### Automated Testing (xUnit)

Проект содержит набор автоматизированных тестов в проекте `JobBoardScraper.Tests`.

```bash
# Run all tests
dotnet test JobBoardScraper.Tests/JobBoardScraper.Tests.csproj
```

**Основные области тестирования:**
- `DatabaseClientTests` — проверка корректности работы с БД
- `SmartHttpClientTests` — проверка логики HTTP-запросов и ретраев
- `ProxyTests` — тестирование ротации и управления прокси
- `HtmlParserTests` и `UserDataExtractorTests` — валидация парсинга данных
- `AdaptiveConcurrencyControllerTests` — проверка адаптивного управления параллелизмом

### Functional Testing

1. **Test with sample profiles**
   - Run scraper on 10-20 test profiles
   - Verify data extraction for all fields

2. **Field-specific validation**
   - ✅ Age extraction
   - ✅ Registration date parsing
   - ✅ Citizenship detection
   - ✅ Remote work availability
   - ✅ Handling of profiles without additional data
   - ✅ Private profile handling

### Database Validation

```sql
-- Check data integrity
SELECT link, age, registration, citizenship, remote_work
FROM habr_resumes
WHERE age IS NOT NULL
LIMIT 10;

-- Field completion statistics
SELECT
    COUNT(*) as total,
    COUNT(age) as with_age,
    COUNT(registration) as with_registration,
    COUNT(citizenship) as with_citizenship,
    COUNT(remote_work) as with_remote_work
FROM habr_resumes
WHERE public = true;
```

### Log Verification

- ✅ Verify new data appears in logs
- ✅ Check for parsing errors
- ✅ Validate output formatting

## ⚙️ Performance Testing

- **Execution time** measurement
- **Memory usage** monitoring
- **Database load** analysis
- **Proxy performance** (latency, success rate)

## 📋 Deployment Checklist

### Pre-deployment

- [ ] Database backup completed
- [ ] Configuration (`App.config`) validated
- [ ] Dependencies verified
- [ ] Proxy list updated and tested

### Deployment

- [ ] Apply database migrations
- [ ] Build project successfully
- [ ] Configure logging
- [ la ] Set up monitoring

### Post-deployment

- [ ] Verify data extraction from real pages
- [ ] Check error logs for unexpected exceptions
- [ ] Monitor proxy health and rotation
- [ ] Validate data quality in DB

## 🔧 Backward Compatibility

### Ensured Compatibility

- ✅ Existing code continues to work
- ✅ Method overloads for compatibility
- ✅ NULL values supported in new fields
- ✅ No breaking changes introduced in the DB schema

## 📊 Quality Assurance

### Test Coverage

| Area | Status | Notes |
|------|--------|-------|
| Data Extraction | ✅ Complete | Automated tests for extractors |
| Database Storage | ✅ Complete | Schema validated |
| Error Handling | ✅ Complete | Retry strategy tested |
| Proxy Rotation | ✅ Complete | Unit tests for ProxyCoordinator |
| Performance | ⏳ Pending | Needs benchmarking |

## 🚀 Next Steps

### Immediate Actions

1. **Complete database migration**
2. **Run full test suite (`dotnet test`)**
3. **Deploy to production environment**
4. **Monitor initial performance and proxy stability**

## 🛠️ Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| Database migration fails | Check PostgreSQL permissions and DB name |
| Scraper crashes | Verify `App.config` and proxy list |
| Data not saved | Check database connection and table schema |
| Performance issues | Adjust `AdaptiveConcurrencyController` settings |

### Debugging Commands

```bash
# Check database connection
psql -U postgres -d habr_career -c "SELECT 1;"

# View recent logs
tail -f logs/JobBoardScraper_*.log

# Test proxy connectivity
curl -x http://proxy1.example.com:8080 https://www.google.com
```

## 📚 Additional Resources

### Related Documentation

- **[Configuration Guide](CONFIGURATION.md)** - Setup instructions
- **[Architecture](ARCHITECTURE.md)** - System design
- **[Quick Start](QUICKSTART.md)** - Getting started
- **[HTTP Retry Strategy](HTTP_ERROR_RETRY_STRATEGY.md)** - Detailed retry logic