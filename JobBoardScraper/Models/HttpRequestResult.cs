using System.Net;

namespace JobBoardScraper.Models;

/// <summary>
/// Результат HTTP запроса с метаданными
/// </summary>
public class HttpRequestResult
{
    public bool IsSuccess { get; set; }
    public HttpStatusCode StatusCode { get; set; }
    public string? Content { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public string Url { get; set; } = string.Empty;
    
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
    public bool IsServerError => (int)StatusCode >= 500;
    public bool IsClientError => (int)StatusCode >= 400 && (int)StatusCode < 500;
}
