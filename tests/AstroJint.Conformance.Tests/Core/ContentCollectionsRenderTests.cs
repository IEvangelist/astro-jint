using System.Net;
using AngleSharp.Dom;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/content-collections-render.test.ts</c> on the <c>content</c> fixture (<c>core-content</c>: the MDX
/// integration and <c>build.inlineStylesheets: 'never'</c>, which the upstream test sets because it was written when that was the default).
/// <list type="bullet">
/// <item><c>Build - SSG</c> builds the fixture with no adapter and reads <c>/launch-week/index.html</c> and the like from the output. The
/// fixture builds the same way with the Jint adapter (pages are prerendered into <c>dist/client</c>) and the tests request the same files over
/// HTTP, where the host serves them as static files: ported.</item>
/// <item><c>Build - SSR</c> builds it with <c>output: 'server'</c> and calls <c>app.render</c>: ported. They were GAP-CORE-1 gaps while the
/// server build of the adapter was a single <c>entry.mjs</c> (a build that renders a collection entry kept the manifest placeholder in it, so
/// every request answered 500); the adapter splits that build into chunks since dad60e1, and they pass. The de-duplication test
/// requests <c>/with-layout-prop/</c>, a path the fixture has no route for (<c>LayoutProp.astro</c> is not used by any page), so upstream
/// checks the 404 response and does not assert its status; the port asserts the 404 as well (not asserted upstream; it keeps the test from
/// passing on a response that is not the 404 of the app, such as a 500 with no body).</item>
/// <item>The last test is not an upstream test: the de-duplication check on <c>/blog/promo/4-launch-week</c>, a page that exists and renders
/// an entry with a stylesheet, where upstream's check has something to see.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/content-collections-render.test.ts")]
public class ContentCollectionsRenderTests(SiteHost sites)
{
    private const string Fixture = "core-content";

    private static readonly object SsrConfig = new { output = "server" };

    private static async Task<IDocument> GetHtmlAsync(ConformanceSite site, string path)
    {
        using var response = await site.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadHtmlAsync();
    }

    private static async Task AssertIncludesCssForRenderedEntryAsync(ConformanceSite site, string path)
    {
        var html = await GetHtmlAsync(site, path);

        // Renders content
        Assert.Equal(3, html.QuerySelectorAll("ul li").Length);
        // Includes styles
        Assert.Single(html.QuerySelectorAll("link[rel=stylesheet]"));
    }

    private static async Task AssertNoCssForNonRenderedEntriesAsync(ConformanceSite site)
    {
        var html = await GetHtmlAsync(site, "/");

        Assert.Empty(html.QuerySelectorAll("link[rel=stylesheet]"));
    }

    private static async Task AssertMdxComponentsExportAppliedAsync(ConformanceSite site, string path)
    {
        var html = await GetHtmlAsync(site, path);

        var h2 = html.QuerySelectorAll("h2");
        Assert.Single(h2);
        Assert.Equal("true", h2[0].GetAttribute("data-components-export-applied"));
    }

    private static async Task AssertCssIsNotDuplicatedAsync(ConformanceSite site, string path, HttpStatusCode expectedStatus)
    {
        using var response = await site.GetAsync(path);
        // Not asserted upstream, which only checks the body: the fixture has no route for /with-layout-prop/, so there the status is the 404 of the app.
        Assert.Equal(expectedStatus, response.StatusCode);
        var html = await response.ReadHtmlAsync();

        var seen = new HashSet<string>();
        foreach (var link in html.QuerySelectorAll("link[rel=stylesheet]"))
        {
            Assert.True(seen.Add(link.GetAttribute("href")!), $"stylesheet {link.GetAttribute("href")} is linked twice");
        }
        foreach (var style in html.QuerySelectorAll("style"))
        {
            Assert.True(seen.Add(style.TextContent), $"style text is repeated: {style.TextContent}");
        }
    }

    [Fact(DisplayName = "Content Collections - render(): Build - SSG: Includes CSS for rendered entry")]
    public async Task Ssg_RenderedEntryIncludesItsCss()
    {
        var site = await sites.GetAsync(Fixture);

        await AssertIncludesCssForRenderedEntryAsync(site, "/launch-week/");
    }

    [Fact(DisplayName = "Content Collections - render(): Build - SSG: Excludes CSS for non-rendered entries")]
    public async Task Ssg_NonRenderedEntriesHaveNoCss()
    {
        var site = await sites.GetAsync(Fixture);

        await AssertNoCssForNonRenderedEntriesAsync(site);
    }

    [Fact(DisplayName = "Content Collections - render(): Build - SSG: Includes component scripts for rendered entry")]
    public async Task Ssg_RenderedEntryIncludesItsComponentScripts()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await GetHtmlAsync(site, "/launch-week-component-scripts/");

        // Includes script
        Assert.Single(html.QuerySelectorAll("script[type=\"module\"]"));
        // Includes inline script
        Assert.Single(html.QuerySelectorAll("script[data-is-inline]"));
    }

    [Fact(DisplayName = "Content Collections - render(): Build - SSG: Excludes component scripts for non-rendered entries")]
    public async Task Ssg_NonRenderedEntriesHaveNoScripts()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await GetHtmlAsync(site, "/");

        Assert.Empty(html.QuerySelectorAll("script"));
    }

    [Fact(DisplayName = "Content Collections - render(): Build - SSG: Applies MDX components export")]
    public async Task Ssg_AppliesTheMdxComponentsExport()
    {
        var site = await sites.GetAsync(Fixture);

        await AssertMdxComponentsExportAppliedAsync(site, "/launch-week-components-export/");
    }

    [Fact(DisplayName = "Content Collections - render(): Build - SSR: Includes CSS for rendered entry")]
    public async Task Ssr_RenderedEntryIncludesItsCss()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        await AssertIncludesCssForRenderedEntryAsync(site, "/launch-week");
    }

    [Fact(DisplayName = "Content Collections - render(): Build - SSR: Exclude CSS for non-rendered entries")]
    public async Task Ssr_NonRenderedEntriesHaveNoCss()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        await AssertNoCssForNonRenderedEntriesAsync(site);
    }

    [Fact(DisplayName = "Content Collections - render(): Build - SSR: De-duplicates CSS used both in layout and directly in target page")]
    public async Task Ssr_CssUsedInLayoutAndPageIsNotDuplicated()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        await AssertCssIsNotDuplicatedAsync(site, "/with-layout-prop/", HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Content Collections - render(): Build - SSR: Applies MDX components export")]
    public async Task Ssr_AppliesTheMdxComponentsExport()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        await AssertMdxComponentsExportAppliedAsync(site, "/launch-week-components-export");
    }

    [Fact(DisplayName = "Content Collections - render(): Build - SSR: getCollection should return new instances of the array to be mutated safely")]
    public async Task Ssr_GetCollectionReturnsNewInstances()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        var html = await GetHtmlAsync(site, "/get-collection-equality");

        Assert.Equal("false", html.QuerySelector("[data-are-equal]")!.TextContent);
    }

    // Not an upstream test: the de-duplication check on a page that exists, which links the stylesheet of the entry it renders.
    [Fact(DisplayName = "Content Collections - render(): Build - SSR: an existing page links the CSS of its rendered entry once")]
    public async Task Ssr_ExistingPage_LinksTheCssOfItsRenderedEntryOnce()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        await AssertIncludesCssForRenderedEntryAsync(site, "/blog/promo/4-launch-week");
        await AssertCssIsNotDuplicatedAsync(site, "/blog/promo/4-launch-week", HttpStatusCode.OK);
    }
}
