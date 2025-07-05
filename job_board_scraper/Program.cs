using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Npgsql;

class Program
{
    static readonly char[] chars = "abcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();
    static readonly string baseUrl = "http://career.habr.com/";
    static readonly int minLength = 3;
    static readonly int maxLength = 3;

    static async Task Main(string[] args)
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        
        NpgsqlConnection conn = new NpgsqlConnection("Server=localhost:5432;User Id=postgres; " + 
                                                     "Password=admin;Database=jobs;");
        conn.Open();
        
        for (int len = minLength; len <= maxLength; len++)
        {
            var usernames = new List<string>(GenerateUsernames(len));
            int totalLinks = usernames.Count;
            for (int i = 0; i < totalLinks; i++)
            {
                var username = usernames[i];
                string link = baseUrl + username;
                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var response = await client.GetAsync(link);
                    var html = await response.Content.ReadAsStringAsync();
                    stopwatch.Stop();
                    
                    double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                    Console.WriteLine($"HTTP запрос {link}: {elapsedSeconds:F3} сек");
                    
                    if ((int)response.StatusCode == 404)
                        continue;
                    var title = ExtractTitle(html);
                    Console.WriteLine($"{link} | {title}");
                    
                    // Запись в таблицу habr
                    try
                    {
                        NpgsqlCommand insertCommand = new NpgsqlCommand("INSERT INTO habr_resumes (link, title) VALUES (@link, @title)", conn);
                        insertCommand.Parameters.AddWithValue("@link", link);
                        insertCommand.Parameters.AddWithValue("@title", title);
                        int rowsAffected = insertCommand.ExecuteNonQuery();
                        Console.WriteLine($"Записано в БД: {rowsAffected} строк");
                    }
                    catch (NpgsqlException dbEx)
                    {
                        Console.WriteLine($"Ошибка БД для {link}: {dbEx.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Неожиданная ошибка при записи в БД для {link}: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    // Можно раскомментировать для отладки:
                    // Console.WriteLine($"Error for {url}: {ex.Message}");
                }
                
                // Добавить вывод прогресс бара обработанных ссылок
                Console.WriteLine($"Обработано ссылок: {i + 1}/{totalLinks}");
                Console.Out.Flush();
            }
        }
        
        conn.Close();
    }

    static IEnumerable<string> GenerateUsernames(int length)
    {
        var arr = new char[length];
        return GenerateUsernamesRecursive(arr, 0);
    }

    static IEnumerable<string> GenerateUsernamesRecursive(char[] arr, int pos)
    {
        if (pos == arr.Length)
        {
            yield return new string(arr);
            yield break;
        }
        foreach (var c in chars)
        {
            arr[pos] = c;
            foreach (var s in GenerateUsernamesRecursive(arr, pos + 1))
                yield return s;
        }
    }

    static string ExtractTitle(string html)
    {
        var match = Regex.Match(html, "<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : "(no title)";
    }
}
