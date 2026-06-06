# C# Coding Standards and Formatting Guide

This document outlines the C# coding standards and formatting rules that should be followed throughout the Job Board Scraper project to maintain consistency and readability.

## Table of Contents

- [Indentation and Formatting](#indentation-and-formatting)
- [Naming Conventions](#naming-conventions)
- [Method and Parameter Formatting](#method-and-parameter-formatting)
- [Brace Style](#brace-style)
- [Line Length and Wrapping](#line-length-and-wrapping)
- [Comments and Documentation](#comments-and-documentation)
- [Example Code](#example-code)

## Indentation and Formatting

### Basic Indentation Rules

- **Class Members**: 4 spaces (1 indentation level)
- **Method Bodies**: 8 spaces (2 indentation levels)
- **Nested Blocks**: 12+ spaces (3+ indentation levels)
- **Use spaces, not tabs** for indentation

### Class Structure Example

```csharp
public sealed class DatabaseClient
{
    private readonly string _connectionString;  // 4 spaces
    private readonly DatabaseStatistics _statistics = new();  // 4 spaces

    public DatabaseStatistics Statistics => _statistics;  // 4 spaces

    public DatabaseClient(string connectionString, ConsoleLogger? logger = null)  // 4 spaces
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));  // 8 spaces
        _logger = logger;  // 8 spaces
        _statistics.InitializeAllTables();  // 8 spaces
    }

    public void Insert(  // 4 spaces
        NpgsqlConnection conn,  // 8 spaces
        string link,  // 8 spaces
        string title)  // 8 spaces
    {  // Method body starts here
        if (conn is null) throw new ArgumentNullException(nameof(conn));  // 8 spaces

        try  // 8 spaces
        {  // Nested block
            EnsureConnectionOpen(conn);  // 12 spaces

            using var cmd = new NpgsqlCommand("SELECT 1 FROM habr_resumes WHERE link = @link LIMIT 1", conn);  // 12 spaces

            // ... more code at 12 spaces indentation
        }  // End nested block
        catch (Exception ex)  // 8 spaces
        {  // Error handling block
            Log($"[DB] Error: {ex.Message}");  // 12 spaces
        }  // End error handling
    }  // End method
}  // End class
```

## Method and Parameter Formatting

### Method Signatures

- Method declaration: 4 spaces indentation
- Parameters: 8 spaces indentation, aligned consistently
- Each parameter on a new line for methods with many parameters
- Parameter names use camelCase
- Default values aligned with parameter names

### Correct Method Formatting

```csharp
// Good: Parameters properly aligned with 8 spaces
public void Insert(
    NpgsqlConnection conn,
    string link,
    string title,
    string? slogan = null,
    string? code = null,
    bool? expert = null,
    string? workExperience = null,
    InsertMode mode = InsertMode.SkipIfExists,
    int? levelId = null,
    string? infoTech = null,
    int? salary = null,
    string? lastVisit = null,
    bool? isPublic = null,
    string? jobSearchStatus = null,
    bool? isEmpty = null,
    bool? isDeleted = null)
{
    // Method implementation
}

// Bad: Inconsistent indentation (what we fixed)
public void Insert(
    NpgsqlConnection conn,
    string link,
    string title,
    string? slogan = null,
    string? code = null,
    bool? expert = null,
    string? workExperience = null,
     InsertMode mode = InsertMode.SkipIfExists,  // ❌ Wrong: 9 spaces instead of 8
     int? levelId = null,                        // ❌ Wrong: 9 spaces instead of 8
     string? infoTech = null,                     // ❌ Wrong: 9 spaces instead of 8
    int? salary = null,
    string? lastVisit = null,
    bool? isPublic = null,
    string? jobSearchStatus = null,
    bool? isEmpty = null,
    bool? isDeleted = null)
{
    // Method implementation
}
```

## Naming Conventions

### General Rules

- **PascalCase** for class names, method names, properties, and public members
- **camelCase** for local variables, method parameters, and private fields
- **_camelCase** for private instance fields (with underscore prefix)
- **UPPER_CASE** for constants
- **IInterface** for interface names (prefix with 'I')

### Examples

```csharp
// Class names
public sealed class DatabaseClient
public class UserProfileData
public interface IDataRepository

// Method names
public void InsertCompany()
public bool EnqueueResume()
private void TryDumpStatistics()

// Properties
public DatabaseStatistics Statistics { get; }
public string ConnectionString { get; set; }

// Private fields
private readonly string _connectionString;
private Task? _dbWriterTask;
private ConcurrentQueue<DbRecord>? _saveQueue;

// Constants
public const int MAX_RETRY_ATTEMPTS = 3;
private const string DEFAULT_CONNECTION_TIMEOUT = "30";

// Local variables and parameters
var userId = GetUserId(userLink);
string formattedMessage = FormatLogMessage(error);
```

## Brace Style

### Opening Braces

- **Same line** as the declaration for methods, classes, and control structures
- **New line** only for namespace declarations

### Correct Brace Placement

```csharp
// Classes and methods - opening brace on same line
public sealed class DatabaseClient {
    public void Insert(NpgsqlConnection conn) {
        // Method body
    }
}

// Control structures - opening brace on same line
if (condition) {
    // Code block
}

try {
    // Try block
}
catch (Exception ex) {
    // Catch block
}

foreach (var item in collection) {
    // Loop body
}

// Namespaces - opening brace on new line
namespace JobBoardScraper.Data
{
    // Namespace content
}
```

## Line Length and Wrapping

### Line Length Guidelines

- **Maximum line length**: 120 characters
- **Preferred line length**: 80-100 characters
- **Wrap long method calls** at logical points with proper indentation

### Line Wrapping Examples

```csharp
// Good: Wrapped at logical points with proper indentation
var result = databaseClient.InsertUserProfile(
    userLink,
    userCode,
    userName,
    isExpert,
    levelTitle,
    infoTech,
    salary);

// Good: SQL query wrapped with proper indentation
using var cmd = new NpgsqlCommand(@"
    INSERT INTO habr_resumes
        (link, title, slogan, code, expert, work_experience, level_id, info_tech, salary, last_visit, public, job_search_status, is_empty, is_deleted, created_at, updated_at)
    VALUES
        (@link, @title, @slogan, @code, @expert, @work_experience, @level_id, @info_tech, @salary, @last_visit, @public, @job_search_status, @is_empty, @is_deleted, NOW(), NOW())",
    conn);

// Good: Long logical expression wrapped
if (userProfile.HasValue &&
    !string.IsNullOrWhiteSpace(userProfile.Value.UserName) &&
    userProfile.Value.IsExpert.HasValue)
{
    // Handle expert user
}
```

## Comments and Documentation

### Comment Styles

- **Single-line comments**: `//` for brief explanations
- **Multi-line comments**: `/* */` for longer explanations (use sparingly)
- **XML documentation**: `///` for public API documentation

### Documentation Examples

```csharp
/// <summary>
/// Inserts or updates a resume record in the database.
/// </summary>
/// <param name="conn">Active database connection</param>
/// <param name="link">Unique link to the resume</param>
/// <param name="title">Resume title</param>
/// <param name="slogan">Optional slogan</param>
/// <param name="code">Optional user code</param>
/// <param name="expert">Whether user is an expert</param>
/// <param name="workExperience">Work experience description</param>
/// <param name="mode">Insert mode (SkipIfExists or UpdateIfExists)</param>
/// <exception cref="ArgumentNullException">Thrown when conn or link is null</exception>
/// <exception cref="NpgsqlException">Thrown when database errors occur</exception>
public void Insert(
    NpgsqlConnection conn,
    string link,
    string title,
    string? slogan = null,
    string? code = null,
    bool? expert = null,
    string? workExperience = null,
    InsertMode mode = InsertMode.SkipIfExists)
{
    // Validate input parameters
    if (conn is null) throw new ArgumentNullException(nameof(conn));
    if (string.IsNullOrWhiteSpace(link)) throw new ArgumentException("Link must not be empty.", nameof(link));

    try
    {
        EnsureConnectionOpen(conn);

        // Check if record already exists when in SkipIfExists mode
        if (mode == InsertMode.SkipIfExists && ResumesRecordExistsByLink(conn, link))
        {
            Log($"[DB] Resume {link}: ⏭ SKIP (уже существует)");
            _statistics.RecordSkipped("habr_resumes", link);
            return;
        }

        /* Multi-line comment example:
           This section handles the actual database insertion
           using parameterized queries to prevent SQL injection
           and proper transaction management */
        using var cmd = new NpgsqlCommand(
            "INSERT INTO habr_resumes (...) VALUES (...)",
            conn);

        // TODO: Add more detailed error handling for specific constraint violations
        cmd.ExecuteNonQuery();
        _statistics.RecordInsert("habr_resumes", link);
    }
    catch (PostgresException pgEx) when (pgEx.SqlState == "23505")
    {
        // Handle unique constraint violation
        Log($"[DB] Resume {link}: ⏭ SKIP (уникальное ограничение)");
        _statistics.RecordSkipped("habr_resumes", link);
    }
    catch (NpgsqlException dbEx)
    {
        // Handle other database errors
        Log($"[DB] Resume {link}: ❌ ERROR - {dbEx.Message}");
        _statistics.RecordError("habr_resumes", link);
    }
}
```

## Example Code

Here's a complete example showing proper formatting:

```csharp
/// <summary>
/// Manages database operations for the job board scraper.
/// Implements proper connection handling and query execution.
/// </summary>
public sealed class DatabaseClient : IDisposable
{
    private readonly string _connectionString;
    private readonly ConsoleLogger? _logger;
    private readonly DatabaseStatistics _statistics = new();

    public DatabaseStatistics Statistics => _statistics;

    /// <summary>
    /// Initializes a new instance of the DatabaseClient.
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="logger">Optional logger for debugging</param>
    /// <exception cref="ArgumentNullException">Thrown when connectionString is null</exception>
    public DatabaseClient(string connectionString, ConsoleLogger? logger = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
        _statistics.InitializeAllTables();
    }

    /// <summary>
    /// Inserts a new company record into the database.
    /// </summary>
    /// <param name="conn">Active database connection</param>
    /// <param name="companyCode">Unique company code</param>
    /// <param name="companyUrl">Company URL</param>
    /// <param name="companyTitle">Optional company title</param>
    /// <param name="companyId">Optional company ID</param>
    public void InsertCompany(
        NpgsqlConnection conn,
        string companyCode,
        string companyUrl,
        string? companyTitle = null,
        long? companyId = null)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (string.IsNullOrWhiteSpace(companyCode))
            throw new ArgumentException("Company code must not be empty.", nameof(companyCode));

        try
        {
            EnsureConnectionOpen(conn);

            using var cmd = new NpgsqlCommand(@""
                INSERT INTO habr_companies
                    (code, url, title, company_id, created_at, updated_at)
                VALUES
                    (@code, @url, @title, @company_id, NOW(), NOW())
                ON CONFLICT (code)
                DO UPDATE SET
                    url = EXCLUDED.url,
                    title = EXCLUDED.title,
                    company_id = COALESCE(EXCLUDED.company_id, habr_companies.company_id),
                    updated_at = NOW()
                RETURNING xmax"", conn);

            cmd.Parameters.AddWithValue("@code", companyCode);
            cmd.Parameters.AddWithValue("@url", companyUrl);
            cmd.Parameters.AddWithValue("@title", companyTitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@company_id", companyId ?? (object)DBNull.Value);

            var xmaxResult = cmd.ExecuteScalar();
            var xmax = Convert.ToUInt32(xmaxResult);
            var isInsert = xmax == 0;

            if (isInsert)
                _statistics.RecordInsert("habr_companies", companyCode);
            else
                _statistics.RecordUpdate("habr_companies", companyCode);

            Log($"[DB] Company {companyCode}: {(isInsert ? "✓ INSERT" : "✓ UPDATE")}");
        }
        catch (NpgsqlException dbEx)
        {
            Log($"[DB] Company {companyCode}: ❌ ERROR - {dbEx.Message}");
            _statistics.RecordError("habr_companies", companyCode);
        }
    }

    // TODO: Add more methods following the same formatting standards
    // TODO: Implement IDisposable pattern for proper resource cleanup
}
```

## Formatting Tools

To automatically apply these standards, you can use:

1. **Visual Studio Format Document**: `Ctrl+K, Ctrl+D`
2. **JetBrains ReSharper**: Configure to use these standards
3. **dotnet format**: Command-line tool for formatting
4. **EditorConfig**: Add `.editorconfig` file to enforce standards

### Recommended EditorConfig Settings

```ini
# .editorconfig file for C# projects
root = true

[*.cs]
indent_size = 4
indent_style = space
tab_width = 4
end_of_line = lf
charset = utf-8-bom
trim_trailing_whitespace = true
insert_final_newline = true

# C# specific settings
csharp_indent_case_contents = true
csharp_indent_switch_labels = true
csharp_indent_labels = one_less_than_current
csharp_new_line_before_open_brace = none
csharp_space_after_keywords_in_control_flow_statements = true
csharp_space_between_method_declaration_parameter_list_parentheses = false
csharp_space_between_method_call_parameter_list_parentheses = false
```

## Maintenance and Enforcement

1. **Code Reviews**: Ensure all pull requests follow these standards
2. **CI/CD Integration**: Add formatting checks to build pipelines
3. **Automated Formatting**: Run formatters before commits
4. **Team Training**: Educate team members on the standards

By following these coding standards, we ensure that the Job Board Scraper codebase remains clean, consistent, and maintainable across all components.