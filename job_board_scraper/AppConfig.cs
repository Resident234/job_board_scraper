namespace job_board_scraper;

public static class AppConfig
{
    public static readonly char[] Chars = "abcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();
    public static readonly string BaseUrl = "http://career.habr.com/";
    public static readonly int MinLength = 4;
    public static readonly int MaxLength = 4;
    public static readonly int MaxConcurrentRequests = 5;
    public static readonly int MaxRetries = 200;
    public static readonly string ConnectionString = "Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;";
}