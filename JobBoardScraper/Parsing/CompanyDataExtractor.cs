using AngleSharp.Dom;
using JobBoardScraper.Core;
using JobBoardScraper.Infrastructure.Utils;
using System.Text.RegularExpressions;

namespace JobBoardScraper.Parsing;

/// <summary>
/// Экстрактор данных компаний с страниц рейтинга
/// </summary>
public static class CompanyDataExtractor
{
    /// <summary>
    /// Извлекает общее количество компаний из HTML-документа
    /// </summary>
    public static int ExtractTotalCompaniesCount(IHtmlDocument doc)
    {
        var totalElement = doc.QuerySelector(AppConfig.CompaniesTotalSelector);
        if (totalElement == null)
            return 0;

        var text = totalElement.TextContent;
        var match = Regex.Match(text, AppConfig.CompaniesTotalRegex);
        if (!match.Success)
            return 0;

        var numberStr = StringUtils.RemoveAllWhitespace(match.Groups[1].Value);
        if (int.TryParse(numberStr, out var total))
            return total;

        return 0;
    }

    /// <summary>
    /// Парсит список компаний с HTML-документа
    /// </summary>
    public static List<CompanyRecord> ParseCompaniesFromPage(IDocument doc, CancellationToken ct)
    {
        var companies = new List<CompanyRecord>();
        var sections = doc.QuerySelectorAll(AppConfig.CompanyRatingSectionSelector);

        foreach (var section in sections)
        {
            try
            {
                if (ExtractCompanyData(section) is { } company)
                {
                    companies.Add(company);
                }
            }
            catch (Exception ex)
            {
                ScraperLogger.LogError(null, "Ошибка при парсинге компании", ex);
            }
        }

        return companies;
    }

    /// <summary>
    /// Извлекает данные компании из HTML-элемента
    /// </summary>
    public static CompanyRecord? ExtractCompanyData(IElement section)
    {
        // 1. Извлекаем код компании из ссылки
        var titleLink = section.QuerySelector(AppConfig.CompanyRatingTitleLinkSelector);
        if (titleLink == null) return null;

        var href = titleLink.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(href)) return null;

        // Извлекаем код из href (например, "/companies/tensor" или "https://career.habr.com/companies/tensor" -> "tensor")
        var path = UrlManager.GetAbsolutePath(href);
        var code = path.TrimStart('/').Replace(AppConfig.CompanyRatingCompanyPathPrefix, "").Trim('/');
        if (string.IsNullOrWhiteSpace(code)) return null;

        var url = string.Format(AppConfig.CompanyRatingCompanyUrlTemplate, code);

        // 2. Извлекаем название компании
        var titleElement = section.QuerySelector(AppConfig.CompanyRatingTitleTextSelector);
        var title = titleElement?.TextContent?.Trim();

        // 3. Извлекаем рейтинг
        decimal? rating = null;
        var ratingElement = section.QuerySelector(AppConfig.CompanyRatingRatingSelector);
        if (ratingElement != null)
        {
            var fullText = ratingElement.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(fullText))
            {
                var match = Regex.Match(fullText, @"(\d+[.,]\d+|\d+)");
                if (match.Success && decimal.TryParse(match.Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ratingValue))
                {
                    rating = ratingValue;
                }
            }
        }

        // 4. Извлекаем описание
        var descriptionElement = section.QuerySelector(AppConfig.CompanyRatingDescriptionSelector);
        var about = descriptionElement?.TextContent?.Trim();

        // 5. Извлекаем город
        string? city = null;
        var metaElement = section.QuerySelector(AppConfig.CompanyRatingMetaSelector);
        if (metaElement != null)
        {
            var cityLink = metaElement.QuerySelector(AppConfig.CompanyRatingCityLinkSelector);
            if (cityLink != null)
            {
                city = cityLink.TextContent?.Trim();
            }
        }

        // 6. Извлекаем награды
        var awards = new List<string>();
        var awardsContainer = section.QuerySelector(AppConfig.CompanyRatingAwardsSelector);
        if (awardsContainer != null)
        {
            var awardImages = awardsContainer.QuerySelectorAll(AppConfig.CompanyRatingAwardImageSelector);
            foreach (var img in awardImages)
            {
                var alt = img.GetAttribute("alt");
                if (!string.IsNullOrWhiteSpace(alt))
                {
                    awards.Add(alt.Trim());
                }
            }
        }

        // 7. Извлекаем среднюю оценку
        decimal? scores = null;
        var scoresElement = section.QuerySelector(AppConfig.CompanyRatingScoresSelector);
        if (scoresElement != null)
        {
            var scoresText = scoresElement.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(scoresText) && decimal.TryParse(scoresText.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var scoresValue))
            {
                scores = scoresValue;
            }
        }

        // 8. Извлекаем текст отзыва и вычисляем хеш
        List<CompanyReviewRecord>? reviewRecords = null;
        var reviewElement = section.QuerySelector(AppConfig.CompanyRatingReviewSelector);
        if (reviewElement != null)
        {
            var reviewText = reviewElement.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(reviewText))
            {
                reviewRecords = new List<CompanyReviewRecord>
                {
                    new CompanyReviewRecord(
                        CompanyCode: code,
                        ReviewHash: HashUtils.ComputeHash(reviewText),
                        ReviewText: reviewText
                    )
                };
            }
        }

        return new CompanyRecord(
            CompanyCode: code,
            CompanyUrl: url,
            CompanyTitle: title,
            Rating: rating,
            About: about,
            City: city,
            Awards: awards.Count > 0 ? awards : null,
            Scores: scores,
            ReviewRecords: reviewRecords
        );
    }

    private static readonly Regex _companyHrefRegex = new Regex(AppConfig.CompaniesHrefRegex, RegexOptions.Compiled);

    /// <summary>
    /// Проверяет наличие следующей страницы на основе HTML-документа.
    /// </summary>
    /// <param name="doc">HTML-документ.</param>
    /// <param name="currentPage">Текущая страница.</param>
    /// <returns>True, если есть следующая страница.</returns>
    public static bool HasNextPage(IDocument doc, int currentPage)
    {
        var nextPageSelector = string.Format(AppConfig.CompaniesNextPageSelector, currentPage + 1);
        var nextPageLink = doc.QuerySelector(nextPageSelector);
        return nextPageLink != null;
    }

    /// <summary>
    /// Извлекает компании из HTML-документа списка компаний.
    /// </summary>
    /// <param name="doc">HTML-документ.</param>
    /// <param name="page">Текущая страница.</param>
    /// <returns>Список найденных компаний.</returns>
    public static List<CompanyRecord> ExtractCompanies(
        IDocument doc,
        int page)
    {
        var companyItems = doc.QuerySelectorAll(AppConfig.CompaniesItemSelector);
        var companies = new List<CompanyRecord>();

        if (companyItems.Length == 0)
        {
            return companies;
        }

        var seenOnPage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in companyItems)
        {
            // Извлекаем company_id из атрибута
            var companyIdStr = item.GetAttribute(AppConfig.CompaniesIdAttribute);
            long? companyId = null;
            if (!string.IsNullOrWhiteSpace(companyIdStr) && long.TryParse(companyIdStr, out var id))
            {
                companyId = id;
            }

            // Ищем ссылку внутри элемента
            var link = item.QuerySelector(AppConfig.CompaniesLinkSelector);
            if (link == null)
                continue;

            var href = link.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href))
                continue;

            var match = _companyHrefRegex.Match(href);
            if (!match.Success)
                continue;

            var companyCode = match.Groups[1].Value;

            if (string.IsNullOrWhiteSpace(companyCode))
                continue;

            // Дедупликация на странице
            if (!seenOnPage.Add(companyCode))
                continue;

            var companyUrl = UrlManager.Combine(AppConfig.CompaniesBaseUrl, companyCode);
            companies.Add(new CompanyRecord(companyCode, companyUrl, companyId));
        }

        return companies;
    }
}