using System.Net;
using AngleSharp.Dom;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/ssr-prerender.test.ts</c>. Upstream asks the test adapter's <c>app</c> for the SSR pages and
/// reads the prerendered ones from the build output, because the test adapter does not serve prerendered routes (that is the
/// job of the adapter's static layer, which it models as a 404). Here every page is requested over HTTP and the host serves the
/// prerendered ones from <c>dist/client</c>. The two <c>app.manifest</c> assertions that inspect the in-process manifest
/// are replaced by what they protect over HTTP, or are not applicable; the matrix explains each one.
/// </summary>
[Trait("Upstream", "core/ssr-prerender.test.ts")]
public class SsrPrerenderTests(SiteHost sites)
{
    private const string Fixture = "core-ssr-prerender";

    // The second upstream describe builds the same fixture with inline stylesheets, which only the CSS tests need.
    private static readonly object InlineStylesheetsAlways = new { build = new { inlineStylesheets = "always" } };

    private Task<ConformanceSite> SiteAsync(object? config = null) => sites.GetAsync(Fixture, config);

    private static string InlineStyles(IDocument html) =>
        string.Concat(html.QuerySelectorAll("style").Select(style => style.TextContent));

    [Fact(DisplayName = "Does not render static page")]
    public async Task StaticPage_IsServedFromItsFile_InsteadOfBeingRendered()
    {
        var site = await SiteAsync();

        // The page prints ?q= when it is rendered on request; the file that the build wrote has no query.
        using var response = await site.GetAsync("/static?q=on-demand");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        var html = await response.ReadHtmlAsync();
        Assert.Equal("Hello world!", html.QuerySelector("#greeting")?.TextContent);
        Assert.Equal("", html.QuerySelector("#searchparams")?.TextContent);
    }

    [Fact(DisplayName = "includes prerendered pages in the asset manifest")]
    public async Task PrerenderedPage_IsAFileOfTheClientOutput()
    {
        var site = await SiteAsync();

        Assert.True(site.PathExists("client/static/index.html"), "The build did not write client/static/index.html.");
        using var response = await site.GetAsync("/static/index.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "raw import works in SSR route")]
    public async Task RawImport_WorksInAnSsrRoute()
    {
        var site = await SiteAsync();

        using var response = await site.GetAsync("/not-prerendered");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("background: blue", (await response.ReadHtmlAsync()).QuerySelector("#raw-styles")?.TextContent);
    }

    [Fact(DisplayName = "raw import works in prerendered route")]
    public async Task RawImport_WorksInAPrerenderedRoute()
    {
        var site = await SiteAsync();

        using var response = await site.GetAsync("/static");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("background: blue", (await response.ReadHtmlAsync()).QuerySelector("#raw-styles")?.TextContent);
    }

    [Fact(DisplayName = "shared component CSS is included in SSR route")]
    public async Task SharedComponentCss_IsIncludedInAnSsrRoute()
    {
        var site = await SiteAsync();

        using var response = await site.GetAsync("/not-prerendered");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("color:red", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "shared component CSS is included in prerendered route")]
    public async Task SharedComponentCss_IsIncludedInAPrerenderedRoute()
    {
        var site = await SiteAsync();

        using var response = await site.GetAsync("/static");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("color:red", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "Params are passed to component")]
    public async Task Params_ArePassedToTheComponent()
    {
        var site = await SiteAsync();

        using var response = await site.GetAsync("/users/houston");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("houston", (await response.ReadHtmlAsync()).QuerySelector(".user")?.TextContent);
    }

    [Fact(DisplayName = "fix bug id #6020")]
    public async Task CatchAllRoute_AtTheRoot_DoesNotGive404()
    {
        var site = await SiteAsync();

        using var response = await site.GetAsync("/some");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("not give 404", (await response.ReadHtmlAsync()).QuerySelector("p")?.TextContent);
    }

    [Fact(DisplayName = "Renders markdown pages correctly")]
    public async Task MarkdownPage_IsRenderedWithItsLayout()
    {
        var site = await SiteAsync();

        using var response = await site.GetAsync("/post");

        Assert.Equal("Subheading", (await response.ReadHtmlAsync()).QuerySelector("#subheading")?.TextContent);
    }

    [Fact(DisplayName = "SSR routes still have inline styles in the SSR manifest")]
    public async Task SsrRoute_StillInlinesItsStyles()
    {
        var site = await SiteAsync(InlineStylesheetsAlways);

        using var response = await site.GetAsync("/not-prerendered");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ssr-only", InlineStyles(await response.ReadHtmlAsync()));
    }

    [Fact(DisplayName = "prerendered HTML on disk still contains inline <style> tags")]
    public async Task PrerenderedPage_StillContainsItsInlineStyles()
    {
        var site = await SiteAsync(InlineStylesheetsAlways);

        using var response = await site.GetAsync("/static");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("prerender-only", InlineStyles(await response.ReadHtmlAsync()));
    }

    [Fact(DisplayName = "SSR entry chunk does not contain the prerender-only CSS string")]
    public async Task ServerEntry_DoesNotCarryTheStylesOfPrerenderedRoutes()
    {
        var site = await SiteAsync(InlineStylesheetsAlways);

        var entry = await File.ReadAllTextAsync(Path.Combine(site.DistPath, "server", "entry.mjs"));

        Assert.DoesNotContain("prerender-only", entry);
    }
}
