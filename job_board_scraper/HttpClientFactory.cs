using System;
using System.Net.Http;

namespace job_board_scraper;

/// <summary>
/// Фабрика для создания настроенных экземпляров HttpClient
/// </summary>
public static class HttpClientFactory
{
    /// <summary>
    /// Создает настроенный HttpClient для работы с API
    /// </summary>
    /// <param name="baseUrl">Базовый URL для запросов</param>
    /// <param name="timeoutSeconds">Таймаут в секундах</param>
    /// <returns>Настроенный экземпляр HttpClient</returns>
    public static HttpClient CreateClient(string baseUrl, int timeoutSeconds = 10)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };

        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HabrScraper/1.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

        return client;
    }

    /// <summary>
    /// Создает настроенный HttpClient с использованием URL из конфигурации
    /// </summary>
    /// <param name="timeoutSeconds">Таймаут в секундах</param>
    /// <returns>Настроенный экземпляр HttpClient</returns>
    public static HttpClient CreateDefaultClient(int timeoutSeconds = 10)
    {
        return CreateClient(AppConfig.BaseUrl, timeoutSeconds);
    }
}
