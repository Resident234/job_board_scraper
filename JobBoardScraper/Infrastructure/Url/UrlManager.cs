using JobBoardScraper.Core;
using System.Collections.Generic;

namespace JobBoardScraper.Infrastructure.Url;

/// <summary>
/// Централизованный помощник для работы с URL карьерного сайта (<c>career.habr.com</c>).
/// Инкапсулирует типовые операции, которые ранее дублировались в скраперах:
/// <list type="bullet">
///   <item>склейка базового URL с относительным путём;</item>
///   <item>извлечение последнего сегмента пути (<c>userCode</c>/<c>companyCode</c>);</item>
///   <item>формирование URL пагинации друзей (<c>/friends?page=N</c>);</item>
///   <item>получение абсолютного пути из абсолютного или относительного URL;</item>
///   <item>удаление базового префикса из полного URL.</item>
///   <item>формирование URL с учетом фильтров для списка компаний.</item>
/// </list>
/// </summary>
public static class UrlManager
{
    #region Свойства

    /// <summary>
    /// Базовый URL приложения (<see cref="AppConfig.BaseUrl"/>), без завершающего слэша.
    /// </summary>
    public static string BaseUrl
    {
        get
        {
            var baseUrl = AppConfig.BaseUrl ?? string.Empty;
            return baseUrl.TrimEnd('/');
        }
    }

    #endregion

    #region Build* методы

    /// <summary>
    /// Формирует ссылку на профиль пользователя.
    /// </summary>
    /// <param name="username">Имя пользователя.</param>
    /// <returns>Ссылка на профиль пользователя (например, http://career.habr.com/username).</returns>
    public static string BuildUserProfileUrl(string username)
    {
        return Combine(AppConfig.BaseUrl, username);
    }

    /// <summary>
    /// Формирует URL страницы подписчиков компании.
    /// </summary>
    /// <param name="companyCode">Код компании.</param>
    /// <param name="page">Номер страницы.</param>
    /// <returns>URL страницы подписчиков компании.</returns>
    public static string BuildCompanyFollowersUrl(string companyCode, int page)
    {
        var baseUrl = string.Format(AppConfig.CompanyFollowersUrlTemplate, companyCode);

        if (page == 1)
        {
            return baseUrl;
        }

        return AddQueryParameter(baseUrl, "page", page.ToString());
    }

    /// <summary>
    /// Формирует URL страницы друзей для пользователя.
    /// </summary>
    /// <param name="userLink">Ссылка на профиль пользователя (абсолютная или относительная).</param>
    /// <param name="page">Номер страницы (<c>null</c>/<c>1</c> — без параметра пагинации).</param>
    /// <returns>URL вида <c>.../username/friends</c> или <c>.../username/friends?page=N</c>.</returns>
    public static string BuildFriendsUrl(string? userLink, int? page = null)
    {
        var root = (userLink ?? string.Empty).TrimEnd('/');
        return page is null or < 2
            ? root + "/friends"
            : $"{root}/friends?page={page}";
    }

    /// <summary>
    /// Формирует URL страницы списка экспертов.
    /// </summary>
    /// <param name="page">Номер страницы (<c>1</c> — без параметра пагинации).</param>
    /// <returns>URL страницы экспертов.</returns>
    public static string BuildExpertsUrl(int page)
    {
        return WithPage(AppConfig.ExpertsListUrl, page);
    }

    /// <summary>
    /// Формирует URL для списка компаний с учетом фильтров.
    /// </summary>
    /// <param name="baseUrl">Базовый URL списка компаний.</param>
    /// <param name="page">Номер страницы.</param>
    /// <param name="sizeFilter">Фильтр по размеру компании (например, <c>sz=1</c>).</param>
    /// <param name="categoryId">Идентификатор категории.</param>
    /// <param name="additionalFilter">Дополнительный фильтр (например, <c>with_vacancies=1</c>).</param>
    /// <returns>Сформированный URL.</returns>
    public static string BuildUrlWithFilters(
        string baseUrl,
        int page,
        int? sizeFilter,
        string? categoryId,
        KeyValuePair<string, string>? additionalFilter)
    {
        var url = baseUrl;

        if (page > 1)
        {
            url = WithPage(url, page);
        }

        if (sizeFilter.HasValue)
        {
            url = AddQueryParameter(url, "sz", sizeFilter.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            url = AddQueryParameter(url, "category_root_id", categoryId);
        }

        if (additionalFilter.HasValue)
        {
            url = AddQueryParameter(url, additionalFilter.Value.Key, additionalFilter.Value.Value);
        }

        return url;
    }

    /// <summary>
    /// Формирует URL для списка компаний с учетом фильтров.
    /// </summary>
    /// <param name="page">Номер страницы.</param>
    /// <param name="sizeFilter">Фильтр по размеру компании (например, <c>sz=1</c>).</param>
    /// <param name="categoryId">Идентификатор категории.</param>
    /// <param name="additionalFilter">Дополнительный фильтр (например, <c>with_vacancies=1</c>).</param>
    /// <returns>Сформированный URL.</returns>
    public static string BuildCompaniesListUrl(
        int page,
        int? sizeFilter,
        string? categoryId,
        KeyValuePair<string, string>? additionalFilter)
    {
        return BuildUrlWithFilters(
            AppConfig.CompaniesListUrl,
            page,
            sizeFilter,
            categoryId,
            additionalFilter);
    }

    #endregion

    #region Get* методы

    /// <summary>
    /// Возвращает URL списка компаний.
    /// </summary>
    /// <returns>URL списка компаний.</returns>
    public static string GetCompaniesListUrl()
    {
        return AppConfig.CompaniesListUrl;
    }

    /// <summary>
    /// Возвращает абсолютный путь (<c>/resumes</c>, <c>/companies/tensor</c>) из URL.
    /// Для относительных URL возвращает входное значение.
    /// </summary>
    /// <param name="href">Абсолютный или относительный URL.</param>
    /// <returns>Путь без схемы, хоста и query/fragment.</returns>
    public static string GetAbsolutePath(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return string.Empty;

        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
            return href;

        return uri.AbsolutePath;
    }

    #endregion

    #region With* методы

    /// <summary>
    /// Дописывает query-параметр <c>order=<value></c> к <paramref name="url"/>, если <paramref name="order"/> непустой.
    /// Корректно обрабатывает уже существующий query: при его наличии используется разделитель <c>&</c>, иначе <c>?</c>.
    /// </summary>
    /// <param name="url">Исходный URL (абсолютный или относительный).</param>
    /// <param name="order">Значение сортировки. Если <c>null</c> или пустое — URL возвращается без изменений.</param>
    /// <returns>URL с параметром <c>order</c> или без изменений, если значение не задано.</returns>
    public static string WithOrder(string? url, string? order)
    {
        if (string.IsNullOrWhiteSpace(order))
            return url ?? string.Empty;

        var basePart = url ?? string.Empty;
        var separator = basePart.Contains('?') ? "&" : "?";
        return $"{basePart}{separator}order={order}";
    }

    /// <summary>
    /// Дописывает query-параметр <c>page=N</c> к <paramref name="url"/> для страниц старше первой.
    /// Для первой страницы URL возвращается без изменений.
    /// </summary>
    /// <param name="url">Исходный URL (абсолютный или относительный).</param>
    /// <param name="page">Номер страницы.</param>
    /// <returns>URL с параметром <c>page</c> или без изменений для первой страницы.</returns>
    public static string WithPage(string? url, int page)
    {
        var basePart = url ?? string.Empty;
        if (page < 2)
            return basePart;

        var separator = basePart.Contains('?') ? "&" : "?";
        return $"{basePart}{separator}page={page}";
    }

    #endregion

    #region Format* методы

    /// <summary>
    /// Подставляет аргумент в URL-шаблон (<c>string.Format</c> с одним placeholder'ом).
    /// Не выполняет никаких преобразований URL — возвращает результат форматирования как есть.
    /// Чтобы получить абсолютный URL, оберните результат в <see cref="ToAbsolute"/>.
    /// </summary>
    /// <param name="template">Шаблон URL с placeholder'ом <c>{0}</c> (например, <c>/resumes?work_state={0}</c>).</param>
    /// <param name="arg0">Аргумент для подстановки в placeholder.</param>
    /// <returns>Строка после <c>string.Format(template, arg0)</c>.</returns>
    public static string Format(string template, object? arg0)
    {
        return string.Format(template ?? string.Empty, arg0);
    }

    /// <summary>
    /// Формирует URL для обхода company_ids, учитывая параметр <c>current_company</c>.
    /// Если <paramref name="currentCompanyParam"/> пустой, используется шаблон без параметра.
    /// </summary>
    /// <param name="companyId">Идентификатор компании.</param>
    /// <param name="currentCompanyParam">Параметр <c>current_company=1</c> или пустая строка.</param>
    /// <returns>Формированный URL.</returns>
    public static string FormatCompanyIdsUrl(long companyId, string? currentCompanyParam = null) =>
        FormatCompanyIdsUrl(companyId.ToString(System.Globalization.CultureInfo.InvariantCulture), currentCompanyParam);

    /// <summary>
    /// Формирует URL для обхода company_ids, учитывая параметр <c>current_company</c>.
    /// Если <paramref name="currentCompanyParam"/> пустой, используется шаблон без параметра.
    /// </summary>
    /// <param name="companyId">Идентификатор компании.</param>
    /// <param name="currentCompanyParam">Параметр <c>current_company=1</c> или пустая строка.</param>
    /// <returns>Формированный URL.</returns>
    public static string FormatCompanyIdsUrl(string companyId, string? currentCompanyParam = null)
    {
        var template = string.IsNullOrWhiteSpace(currentCompanyParam)
            ? AppConfig.ResumeListCompanyIdsUrlTemplate
            : AppConfig.ResumeListCompanyIdsUrlTemplateWithCurrentCompany;
        var baseUrl = UrlManager.Format(template, companyId);
        return string.IsNullOrWhiteSpace(currentCompanyParam) ? baseUrl : baseUrl + currentCompanyParam;
    }

    #endregion

    #region Вспомогательные методы

    /// <summary>
    /// Склеивает <paramref name="baseUrl"/> и относительный <paramref name="relativePath"/>,
    /// корректно обрабатывая слэши на стыке. Если <paramref name="relativePath"/> уже абсолютный —
    /// возвращается он.
    /// </summary>
    /// <param name="baseUrl">Базовый URL (если <c>null</c> — используется <see cref="BaseUrl"/>).</param>
    /// <param name="relativePath">Относительный путь (например, <c>/resumes</c>).</param>
    /// <returns>Абсолютный URL в виде строки.</returns>
    public static string Combine(string? baseUrl, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return (baseUrl ?? BaseUrl).TrimEnd('/');

        if (Uri.TryCreate(relativePath, UriKind.Absolute, out _))
            return relativePath;

        var root = (baseUrl ?? BaseUrl).TrimEnd('/');
        var rel = relativePath.StartsWith('/') ? relativePath : "/" + relativePath;
        return root + rel;
    }

    /// <summary>
    /// Преобразует относительный URL в абсолютный относительно <see cref="BaseUrl"/>.
    /// Если вход уже абсолютный — возвращается как есть.
    /// </summary>
    /// <param name="href">Относительный или абсолютный URL.</param>
    /// <returns>Абсолютный URL.</returns>
    public static string ToAbsolute(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return BaseUrl;

        if (Uri.TryCreate(href, UriKind.Absolute, out _))
            return href;

        return Combine(BaseUrl, href);
    }

    /// <summary>
    /// Удаляет <see cref="BaseUrl"/> из начала <paramref name="fullUrl"/>.
    /// Если вход не начинается с базы — возвращается <paramref name="fullUrl"/> без изменений.
    /// </summary>
    /// <param name="fullUrl">Полный URL.</param>
    /// <returns>URL без базового префикса.</returns>
    public static string StripBase(string? fullUrl)
    {
        if (string.IsNullOrWhiteSpace(fullUrl))
            return string.Empty;

        var root = BaseUrl;
        if (root.Length == 0)
            return fullUrl;

        return fullUrl.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? fullUrl[root.Length..]
            : fullUrl;
    }

    /// <summary>
    /// Добавляет query-параметр к URL.
    /// </summary>
    /// <param name="url">Исходный URL (абсолютный или относительный).</param>
    /// <param name="paramName">Имя параметра.</param>
    /// <param name="paramValue">Значение параметра.</param>
    /// <returns>URL с добавленным параметром.</returns>
    public static string AddQueryParameter(string? url, string paramName, string paramValue)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(paramName))
            return url ?? string.Empty;

        var basePart = url ?? string.Empty;
        var separator = basePart.Contains('?') ? "&" : "?";
        return $"{basePart}{separator}{paramName}={paramValue}";
    }

    #endregion
}