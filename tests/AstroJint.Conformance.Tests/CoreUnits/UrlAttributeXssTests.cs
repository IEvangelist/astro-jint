using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/url-attribute-xss.test.ts</c>: <c>addAttribute(url, 'href')</c> escapes the double
/// quotes and the ampersands of a URL so that the value stays inside one attribute, and writes nothing for <c>null</c> and
/// <c>undefined</c>.
/// <para>
/// The three tests are adapted: upstream calls <c>addAttribute()</c> and matches the string that it returns, which has no route, so
/// the page <c>attributes/url-xss</c> of the <c>app-render</c> fixture gives the same values to the <c>href</c> of four links
/// (the compiler turns <c>href={value}</c> into that call), and the tests read the start tag of each link in the markup: it must
/// have the same escapes, be a single well-formed <c>href</c> attribute (upstream's pattern <c>^\s+href="[^"]*"$</c>, applied to the
/// start tag), and the parsed element must have no other attribute than <c>id</c> and <c>href</c>.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/url-attribute-xss.test.ts")]
public class UrlAttributeXssTests(SiteHost sites)
{
    [Fact(DisplayName = "escapes double quotes in HTTP URLs containing &")]
    public async Task Url_WithDoubleQuotesAndAmpersand_HasItsQuotesEscaped()
    {
        var (html, document) = await GetPageAsync();

        var startTag = StartTag(html, "malicious");

        // The " must be escaped so the value stays inside a single attribute
        Assert.Contains("&quot;", startTag);
        Assert.Matches(@"^<a id=""malicious""\s+href=""[^""]*"">$", startTag);
        var link = document.QuerySelector("#malicious");
        Assert.Equal(["id", "href"], link?.Attributes.Select(attribute => attribute.Name));
        Assert.Equal("https://evil.com/?a=1&b=2\"+onmouseover=\"alert(1)", link?.GetAttribute("href"));
    }

    [Fact(DisplayName = "escapes & in HTTP URLs")]
    public async Task Url_WithAmpersands_HasThemEscaped()
    {
        var (html, document) = await GetPageAsync();

        var startTag = StartTag(html, "ampersands");

        Assert.Contains("&amp;", startTag);
        Assert.Matches(@"^<a id=""ampersands""\s+href=""[^""]*"">$", startTag);
        var link = document.QuerySelector("#ampersands");
        Assert.Equal(["id", "href"], link?.Attributes.Select(attribute => attribute.Name));
        Assert.Equal("https://example.com/?a=1&b=2&c=3", link?.GetAttribute("href"));
    }

    [Fact(DisplayName = "handles null and undefined values")]
    public async Task Url_NullOrUndefined_WritesNoAttribute()
    {
        var (html, document) = await GetPageAsync();

        Assert.Equal("<a id=\"null\">", StartTag(html, "null"));
        Assert.Equal("<a id=\"undefined\">", StartTag(html, "undefined"));
        Assert.False(document.QuerySelector("#null")?.HasAttribute("href"));
        Assert.False(document.QuerySelector("#undefined")?.HasAttribute("href"));
    }

    private async Task<(string Html, AngleSharp.Dom.IDocument Document)> GetPageAsync()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/attributes/url-xss");

        Assert.Equal(200, response.StatusCode);
        return (response.Text, Html(response));
    }

    // The start tag of the link, as written in the markup.
    private static string StartTag(string html, string id)
    {
        var start = html.IndexOf($"<a id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The page has no link '{id}'.");
        return html[start..(html.IndexOf('>', start) + 1)];
    }
}
