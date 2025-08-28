namespace job_board_scraper;

public static class AppConfig
{
    public static readonly char[] Chars = "abcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();
    public const string BaseUrl = "http://career.habr.com/";
    public const int MinLength = 4;
    public const int MaxLength = 4;
    public const int MaxConcurrentRequests = 5;
    public const int MaxRetries = 200;
    public const string ConnectionString = "Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;";
}