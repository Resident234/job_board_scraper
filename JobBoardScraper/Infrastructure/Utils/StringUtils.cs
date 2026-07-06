using System.Text;

namespace JobBoardScraper.Infrastructure.Utils;

/// <summary>
/// Утилиты для работы со строками
/// </summary>
public static class StringUtils
{
    /// <summary>
    /// Удаляет все пробелы и неразрывные пробелы из строки
    /// </summary>
    public static string RemoveAllWhitespace(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return input.Replace(" ", "").Replace("\u00A0", "");
    }
}