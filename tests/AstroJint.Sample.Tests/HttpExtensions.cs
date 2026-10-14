using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace AstroJint.Sample;

public static class HttpExtensions
{
    private static readonly HtmlParser Parser = new();

    public static async Task<IHtmlDocument> ReadHtmlAsync(this HttpResponseMessage response) =>
        (await response.Content.ReadAsStringAsync()).ParseHtml();

    public static IHtmlDocument ParseHtml(this string html) => Parser.ParseDocument(html);

    /// <summary>The text of the one element that matches the selector.</summary>
    public static string Text(this IHtmlDocument document, string selector) =>
        (document.QuerySelector(selector) ?? throw new Xunit.Sdk.XunitException($"The page has no element '{selector}': {document.Body?.InnerHtml}")).TextContent.Trim();

    public static bool Has(this IHtmlDocument document, string selector) => document.QuerySelector(selector) is not null;

    /// <summary>The values of a header of the response, or an empty list when it is absent.</summary>
    public static IReadOnlyList<string> HeaderValues(this HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? [.. values]
            : [];

    /// <summary>The value of the header, or <see langword="null"/> when the response has none.</summary>
    public static string? Header(this HttpResponseMessage response, string name) => response.HeaderValues(name).SingleOrDefault();

    /// <summary>
    /// The directives of the <c>Cache-Control</c> header in alphabetical order: <see cref="HttpClient"/> writes the
    /// directives it knows in an order of its own, and the order of directives is not significant.
    /// </summary>
    public static string[] CacheControl(this HttpResponseMessage response) =>
        [.. response.HeaderValues("Cache-Control").SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).Order(StringComparer.Ordinal)];

    /// <summary>The value of the cookie that the response sets with the given name, or <see langword="null"/> when it sets none.</summary>
    public static string? SetCookieValue(this HttpResponseMessage response, string name)
    {
        foreach (var header in response.HeaderValues("Set-Cookie"))
        {
            var pair = header.Split(';', 2)[0];
            var separator = pair.IndexOf('=');
            if (separator > 0 && pair[..separator].Trim() == name)
            {
                return pair[(separator + 1)..].Trim();
            }
        }

        return null;
    }
}
