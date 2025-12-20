using System.Net;
using System.Text.Json;

namespace JobBoardScraper.Infrastructure.Proxy;

/// <summary>
/// Провайдер для получения списка прокси из различных источников
/// </summary>
public class ProxyProvider
{
    private readonly HttpClient _httpClient;
    private readonly List<string> _proxyList;
    private readonly object _lock = new object();

    public ProxyProvider()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _proxyList = new List<string>();
    }

    public List<string> GetProxies()
    {
        lock (_lock) { return new List<string>(_proxyList); }
    }

    public void AddProxy(string proxy)
    {
        lock (_lock)
        {
            if (!_proxyList.Contains(proxy)) _proxyList.Add(proxy);
        }
    }

    public void Clear()
    {
        lock (_lock) { _proxyList.Clear(); }
    }

    public async Task LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;
        var lines = await File.ReadAllLinesAsync(filePath);
        foreach (var line in lines)
        {
            var proxy = line.Trim();
            if (!string.IsNullOrWhiteSpace(proxy) && !proxy.StartsWith("#"))
                AddProxy(proxy);
        }
    }

    public async Task SaveToFileAsync(string filePath)
    {
        var proxies = GetProxies();
        await File.WriteAllLinesAsync(filePath, proxies);
    }

    public async Task<int> LoadFromProxyScrapeAsync(int timeout = 10000, string country = "all")
    {
        try
        {
            var url = $"https://api.proxyscrape.com/v2/?request=get&protocol=http&timeout={timeout}&country={country}&ssl=all&anonymity=all";
            var response = await _httpClient.GetStringAsync(url);
            var proxies = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var proxy in proxies)
            {
                var trimmed = proxy.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed)) AddProxy($"http://{trimmed}");
            }
            return proxies.Length;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProxyProvider] ProxyScrape error: {ex.Message}");
            return 0;
        }
    }

    public async Task<int> LoadFromGeoNodeAsync(int limit = 100)
    {
        try
        {
            var url = $"https://proxylist.geonode.com/api/proxy-list?limit={limit}&page=1&sort_by=lastChecked&sort_type=desc";
            var response = await _httpClient.GetStringAsync(url);
            var json = JsonDocument.Parse(response);
            var data = json.RootElement.GetProperty("data");
            int count = 0;
            foreach (var item in data.EnumerateArray())
            {
                var ip = item.GetProperty("ip").GetString();
                var port = item.GetProperty("port").GetString();
                var protocols = item.GetProperty("protocols").EnumerateArray();
                foreach (var protocol in protocols)
                {
                    var proto = protocol.GetString()?.ToLower();
                    if (proto == "http" || proto == "https")
                    {
                        AddProxy($"{proto}://{ip}:{port}");
                        count++;
                        break;
                    }
                }
            }
            return count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProxyProvider] GeoNode error: {ex.Message}");
            return 0;
        }
    }

    public async Task<bool> TestProxyAsync(string proxyUrl, string testUrl = "https://httpbin.org/ip")
    {
        try
        {
            var proxy = new WebProxy(new Uri(proxyUrl));
            var handler = new HttpClientHandler { Proxy = proxy, UseProxy = true };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync(testUrl);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<int> RemoveDeadProxiesAsync(string testUrl = "https://httpbin.org/ip", int maxConcurrent = 10)
    {
        var proxies = GetProxies();
        var deadProxies = new List<string>();
        var semaphore = new SemaphoreSlim(maxConcurrent);
        var tasks = proxies.Select(async proxy =>
        {
            await semaphore.WaitAsync();
            try
            {
                if (!await TestProxyAsync(proxy, testUrl))
                    lock (_lock) { deadProxies.Add(proxy); }
            }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(tasks);
        lock (_lock)
        {
            foreach (var proxy in deadProxies) _proxyList.Remove(proxy);
        }
        return deadProxies.Count;
    }

    public void Dispose() => _httpClient?.Dispose();
}
