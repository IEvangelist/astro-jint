using System.Net;
using AngleSharp.Dom;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>What the tests of this folder share for reading the assets that a page links.</summary>
internal static class PageAssets
{
    /// <summary>
    /// The path and query of a URL that a page links, for a request to the host. A URL with an origin (an
    /// <c>assetsPrefix</c> on a CDN, for example) is requested from the host as if the CDN served the host's files.
    /// </summary>
    public static string RequestTarget(string href) =>
        Uri.TryCreate(href, UriKind.Absolute, out var absolute) ? absolute.PathAndQuery : href;

    /// <summary>Requests <paramref name="href"/> and checks that the host served it with status 200; the caller disposes the response.</summary>
    public static async Task<HttpResponseMessage> GetServedAsync(ConformanceSite site, string href)
    {
        var response = await site.GetAsync(RequestTarget(href));
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{href} answered {(int)response.StatusCode}.");
        return response;
    }

    /// <summary>The stylesheets that the page links, in document order, each fetched from the host.</summary>
    public static async Task<IReadOnlyList<(string Href, string Css)>> LinkedStylesheetsAsync(ConformanceSite site, string path)
    {
        var html = await site.GetHtmlAsync(path);
        return await FetchStylesheetsAsync(site, html);
    }

    /// <summary>The CSS of the page in document order: the text of the linked stylesheets and of the inline <c>&lt;style&gt;</c> elements.</summary>
    public static async Task<IReadOnlyList<string>> StylesInOrderAsync(ConformanceSite site, string path)
    {
        var html = await site.GetHtmlAsync(path);
        var styles = new List<string>();
        foreach (var element in html.QuerySelectorAll("link[rel=stylesheet], style"))
        {
            if (element.LocalName == "style")
            {
                styles.Add(element.TextContent);
                continue;
            }

            using var response = await GetServedAsync(site, element.GetAttribute("href")!);
            styles.Add(await response.ReadTextAsync());
        }

        return styles;
    }

    public static async Task<IReadOnlyList<(string Href, string Css)>> FetchStylesheetsAsync(ConformanceSite site, IDocument html)
    {
        var stylesheets = new List<(string, string)>();
        foreach (var link in html.QuerySelectorAll("link[rel=stylesheet]"))
        {
            var href = link.GetAttribute("href")!;
            using var response = await GetServedAsync(site, href);
            Assert.StartsWith("text/css", response.Content.Headers.ContentType?.ToString());
            stylesheets.Add((href, await response.ReadTextAsync()));
        }

        return stylesheets;
    }
}
