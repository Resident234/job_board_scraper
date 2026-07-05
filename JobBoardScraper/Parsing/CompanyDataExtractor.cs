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
}