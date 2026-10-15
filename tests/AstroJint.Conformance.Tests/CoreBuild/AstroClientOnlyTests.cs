using AngleSharp.Dom;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/astro-client-only.test.ts</c>: pages with <c>client:only</c> islands (Svelte, React, a Svelte
/// component of a package in <c>node_modules</c>, CSS modules) get their stylesheets and an empty island that the browser fills.
/// Upstream builds the <c>astro-client-only</c> fixture three times (static output, <c>output: 'server'</c> with a test adapter, and
/// static output with <c>site</c> and <c>base: '/blog'</c>), always with <c>inlineStylesheets: 'never'</c>, and reads the
/// HTML and the stylesheets from <c>dist</c> or <c>app.render()</c>. Here <c>cb-client-only</c> is built with the same three
/// settings and every page and stylesheet is requested from the host.
/// <para>
/// Differences from upstream: the CSS module of the fixture is a <c>.module.css</c> file, not <c>.module.scss</c>, because
/// installing <c>sass</c> for one fixture changes the resolution of <c>astro</c> for every fixture in the lockfile; the module
/// behavior that the tests look at (the class <c>._red_</c> in one stylesheet) does not depend on the preprocessor. The package
/// component (<c>pkg/</c>) is installed with <c>file:</c> as upstream does. The hydrator tests check that every island of the
/// page is empty and has a renderer, where upstream looks at the first one. Under <c>base: '/blog'</c> the stylesheets are
/// requested under the base, as the browser does (upstream strips it to read from <c>dist</c>).
/// </para>
/// </summary>
[Trait("Upstream", "core/astro-client-only.test.ts")]
public class AstroClientOnlyTests(SiteHost sites)
{
    private const string Fixture = "cb-client-only";

    private static readonly object ServerConfig = new { output = "server" };

    private static readonly object SubpathConfig = new { site = "https://site.com", @base = "/blog" };

    private static async Task AssertClientOnlyIslandsAsync(ConformanceSite site, string path)
    {
        var html = await PageAssets.PageAsync(site, path);

        var islands = html.QuerySelectorAll("astro-island");
        Assert.NotEmpty(islands);
        Assert.All(islands, island =>
        {
            // The island is empty: the browser renders it.
            Assert.Equal("", island.InnerHtml);
            Assert.False(string.IsNullOrEmpty(island.GetAttribute("renderer-url")), "The island has no renderer-url.");
        });
    }

    private static async Task<string> LinkedCssAsync(ConformanceSite site, IDocument html)
    {
        var stylesheets = await PageAssets.FetchStylesheetsAsync(site, html);

        return string.Concat(stylesheets.Select(stylesheet => stylesheet.Css));
    }

    private static async Task AssertGlobalAndSvelteCssAsync(ConformanceSite site, string path)
    {
        var html = await PageAssets.PageAsync(site, path);

        var css = await LinkedCssAsync(site, html);

        // yellowgreen minified
        Assert.Contains("#9acd32", css);
        Assert.Contains("Courier New", css);
    }

    private static async Task AssertCssModuleOnceAsync(ConformanceSite site, string path)
    {
        var html = await PageAssets.PageAsync(site, path);

        var stylesheets = await PageAssets.FetchStylesheetsAsync(site, html);
        var inlineStyles = html.QuerySelectorAll("style").Select(style => style.TextContent);
        var sheetsWithModuleClass = stylesheets.Select(stylesheet => stylesheet.Css).Concat(inlineStyles).Where(css => css.Contains("._red_"));

        Assert.Single(sheetsWithModuleClass);
    }

    [Fact(DisplayName = "Client only components: Loads pages using client:only hydrator")]
    public async Task Static_LoadsPagesUsingClientOnlyHydrator() =>
        await AssertClientOnlyIslandsAsync(await sites.GetAsync(Fixture, null), "/");

    [Fact(DisplayName = "Client only components: Adds the CSS to the page")]
    public async Task Static_AddsTheCssToThePage() =>
        await AssertGlobalAndSvelteCssAsync(await sites.GetAsync(Fixture, null), "/");

    [Fact(DisplayName = "Client only components: Adds the CSS to the page - standalone svelte component")]
    public async Task Static_AddsTheCssOfAStandaloneSvelteComponent()
    {
        var site = await sites.GetAsync(Fixture, null);
        var html = await PageAssets.PageAsync(site, "/persistent-counter-standalone/");

        Assert.Single(html.QuerySelectorAll("head link[rel=stylesheet]"));
        Assert.Contains("tomato", await LinkedCssAsync(site, html));
    }

    [Fact(DisplayName = "Client only components: Includes CSS from components that use CSS modules")]
    public async Task Static_IncludesTheCssOfCssModules()
    {
        var site = await sites.GetAsync(Fixture, null);
        var html = await PageAssets.PageAsync(site, "/css-modules/");

        Assert.Single(html.QuerySelectorAll("link[rel=stylesheet]"));
        Assert.Contains("._red_", await LinkedCssAsync(site, html));
    }

    [Fact(DisplayName = "Client only components: Includes CSS modules once when a component is rendered statically and client:only")]
    public async Task Static_IncludesCssModulesOnce() =>
        await AssertCssModuleOnceAsync(await sites.GetAsync(Fixture, null), "/css-modules-static-and-client-only/");

    [Fact(DisplayName = "Client only components: Includes CSS from package components")]
    public async Task Static_IncludesTheCssOfPackageComponents()
    {
        var site = await sites.GetAsync(Fixture, null);
        var html = await PageAssets.PageAsync(site, "/pkg/");

        Assert.Single(html.QuerySelectorAll("link[rel=stylesheet]"));
        Assert.Contains("green", await LinkedCssAsync(site, html));
    }

    [Fact(DisplayName = "Client only components with server output: Includes CSS modules once when a component is rendered on demand and client:only")]
    public async Task Server_IncludesCssModulesOnce() =>
        await AssertCssModuleOnceAsync(await sites.GetAsync(Fixture, ServerConfig), "/css-modules-static-and-client-only");

    [Fact(DisplayName = "Client only components subpath: Loads pages using client:only hydrator")]
    public async Task Subpath_LoadsPagesUsingClientOnlyHydrator() =>
        await AssertClientOnlyIslandsAsync(await sites.GetAsync(Fixture, SubpathConfig), "/blog/");

    [Fact(DisplayName = "Client only components subpath: Adds the CSS to the page")]
    public async Task Subpath_AddsTheCssToThePage() =>
        await AssertGlobalAndSvelteCssAsync(await sites.GetAsync(Fixture, SubpathConfig), "/blog/");

    [Fact(DisplayName = "Client only components subpath: Adds the CSS to the page for TSX components")]
    public async Task Subpath_AddsTheCssForTsxComponents()
    {
        var site = await sites.GetAsync(Fixture, SubpathConfig);
        var html = await PageAssets.PageAsync(site, "/blog/tsx-no-extension/");

        var stylesheets = html.QuerySelectorAll("link[rel=stylesheet]");
        Assert.All(stylesheets, link => Assert.StartsWith("/blog/", link.GetAttribute("href")));

        Assert.Contains("purple", await LinkedCssAsync(site, html));
    }
}
