using System.Security.Cryptography;
using System.Text;

namespace JobBoardScraper.Infrastructure.Utils;

/// <summary>
/// Утилиты для работы с хешами
/// </summary>
public static class HashUtils
{
    /// <summary>
    /// Вычисляет SHA256 хеш для текста
    /// </summary>
    public static string ComputeHash(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}