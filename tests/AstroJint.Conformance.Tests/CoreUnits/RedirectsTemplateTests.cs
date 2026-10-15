using AngleSharp.Dom;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/redirects/template.test.ts</c>: the HTML that a redirect carries as its body.
/// <para>
/// Differences from upstream: the tests call <c>redirectTemplate</c> and <c>hasMetaRefreshTo</c> with arguments of their
/// own. Over HTTP the template is reached through the one caller of the request path, the trailing-slash handler, which
/// answers a request for <c>/source</c> under <c>trailingSlash: 'always'</c> with a 301 (308 for a method other than GET)
/// whose body is that template, so the four tests that this can show are adapted to it. Its arguments are fixed by that
/// caller: the status is 301 or 308, the relative and the absolute location are the same redirect target
/// (<c>/source/</c>, with the query string of the request), and <c>from</c> is the URL of the request. The config
/// redirects of the host (see <c>redirects/render.test.ts</c>) never carry a body, so they do not reach the template.
/// </para>
/// <para>
/// The other fourteen are not applicable. Four exercise arguments that the request path never passes
/// (<c>uses 2 second delay for 302 redirects</c>, <c>omits "from" text when not provided</c>,
/// <c>handles external URLs in relative location</c> and <c>handles URL object for absoluteLocation</c>): a 302 body only
/// comes from the static build and the dev server, and every caller in the host passes a <c>from</c> and a location that
/// is a path of the site. The ten of <c>redirects/hasMetaRefreshTo</c> test a function that the installed 7.3.8 package
/// does not have (it belongs to the <c>experimental.redirectPage</c> of a later Astro version, see
/// <c>redirects/redirect-page.test.ts</c>).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/redirects/template.test.ts")]
public class RedirectsTemplateTests(SiteHost sites)
{
    private const string Fixture = "app-redirects";

    private static readonly object Always = new { trailingSlash = "always" };

    private async Task<(ConformanceSite Site, RawResponse Response, IDocument Document)> RedirectAsync(string target, string method = "GET")
    {
        var site = await sites.GetAsync(Fixture, Always);
        var response = await site.SendRawAsync(target, method, method == "GET" ? null : []);

        return (site, response, ConformanceSite.ParseHtml(response.Text));
    }

    [Fact(DisplayName = "generates correct HTML structure")]
    public async Task TrailingSlashRedirect_BodyIsTheRedirectTemplate()
    {
        var (_, response, document) = await RedirectAsync("/source");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/source/", response.Header("Location"));

        // Check DOCTYPE
        Assert.StartsWith("<!doctype html>", response.Text);

        // Check title
        Assert.Equal("Redirecting to: /source/", document.QuerySelector("title")!.TextContent);

        // Check meta refresh tag
        var metaRefresh = Assert.Single(document.QuerySelectorAll("meta[http-equiv=\"refresh\"]"));
        Assert.Equal("0;url=/source/", metaRefresh.GetAttribute("content"));

        // Check robots meta tag
        var metaRobots = Assert.Single(document.QuerySelectorAll("meta[name=\"robots\"]"));
        Assert.Equal("noindex", metaRobots.GetAttribute("content"));

        // Check canonical link
        var canonical = Assert.Single(document.QuerySelectorAll("link[rel=\"canonical\"]"));
        Assert.Equal("/source/", canonical.GetAttribute("href"));

        // Check body content
        var link = Assert.Single(document.QuerySelectorAll("body a"));
        Assert.Equal("/source/", link.GetAttribute("href"));
        Assert.Contains("Redirecting", link.InnerHtml);
        Assert.Contains("/source/", link.InnerHtml);
    }

    [Fact(DisplayName = "uses 0 second delay for non-302 redirects")]
    public async Task GetAndPostRedirects_UseNoDelay()
    {
        // Test 301
        var (_, get, getDocument) = await RedirectAsync("/source");

        Assert.Equal(301, get.StatusCode);
        Assert.Equal("0;url=/source/", getDocument.QuerySelector("meta[http-equiv=\"refresh\"]")!.GetAttribute("content"));

        // Test 308
        var (_, post, postDocument) = await RedirectAsync("/source", "POST");

        Assert.Equal(308, post.StatusCode);
        Assert.Equal("0;url=/source/", postDocument.QuerySelector("meta[http-equiv=\"refresh\"]")!.GetAttribute("content"));
    }

    [Fact(DisplayName = "includes \"from\" information when provided")]
    public async Task Redirect_NamesTheRequestUrlAsTheOrigin()
    {
        var (site, _, document) = await RedirectAsync("/source");

        var bodyText = document.Body!.InnerHtml;
        Assert.Contains($"from <code>{new Uri(site.BaseAddress, "/source")}</code>", bodyText);
        Assert.Contains("to <code>/source/</code>", bodyText);
    }

    [Fact(DisplayName = "handles special characters in URLs")]
    public async Task RedirectWithQuery_EscapesTheUrl()
    {
        var (_, response, document) = await RedirectAsync("/source?foo=bar&baz=qux");

        Assert.Equal("/source/?foo=bar&baz=qux", response.Header("Location"));

        // Title should show the URL as-is
        Assert.Equal("Redirecting to: /source/?foo=bar&baz=qux", document.QuerySelector("title")!.TextContent);

        // Meta refresh should preserve the URL structure
        Assert.Equal("0;url=/source/?foo=bar&baz=qux", document.QuerySelector("meta[http-equiv=\"refresh\"]")!.GetAttribute("content"));

        // Link href should be properly escaped
        Assert.Contains("&amp;", response.Text);
        Assert.Equal("/source/?foo=bar&baz=qux", document.QuerySelector("body a")!.GetAttribute("href"));
    }
}
