using System.Text.Json;
using AngleSharp.Dom;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/astro-assets-prefix.test.ts</c>. Upstream builds the <c>astro-assets-prefix</c> fixture six
/// times (<c>build.assetsPrefix</c> as one URL, as a path and as a map for several CDNs, each with <c>output: 'static'</c> and
/// <c>output: 'server'</c>) and reads the HTML from disk or from <c>app.render()</c>. Here the <c>cb-assets</c> fixture is built
/// three times, once per setting, and every page exists twice: prerendered (<c>/custom-base/</c>, <c>/custom-base/markdown/</c>, ...)
/// for the "static" tests and on demand under <c>/custom-base/ssr/</c> for the "server" tests, so a Theory with the cases
/// <c>static</c> and <c>server</c> covers both upstream describes of a test.
/// <para>
/// Differences from upstream: the HTML comes from an HTTP request to the host, and every asset that a page links (stylesheets,
/// the images, the island scripts) is also requested from the host, which has to serve it under the base with the content type
/// of its extension and the immutable cache header, wherever <c>assetsPrefix</c> points the page (the host does not serve a CDN,
/// so a prefixed URL is requested as <c>{base}/_astro/...</c>). The upstream loops over elements (<c>each</c>) pass when the
/// selector matches nothing; here the stylesheets and the images have to exist. The markdown image test checks both images of the page.
/// The image service is a stub that returns the pictures unchanged, as in upstream.
/// </para>
/// </summary>
[Trait("Upstream", "core/astro-assets-prefix.test.ts")]
public class AstroAssetsPrefixTests(SiteHost sites)
{
    private const string Fixture = "cb-assets";
    private const string Base = "/custom-base";

    private sealed record Prefixes(object Config, string Css, string Js, string Fallback);

    private static readonly Prefixes Single = new(
        new { @base = Base, build = new { assetsPrefix = "http://localhost:4321" } },
        "^http://localhost:4321/_astro/.*",
        "^http://localhost:4321/_astro/.*",
        "^http://localhost:4321/_astro/.*");

    private static readonly Dictionary<string, string> CdnMap = new()
    {
        ["js"] = "https://js.example.com",
        ["css"] = "https://css.example.com",
        ["fallback"] = "https://example.com",
    };

    internal static readonly object MultiCdnConfig = new { @base = Base, build = new { assetsPrefix = CdnMap } };

    private static readonly Prefixes MultiCdn = new(
        MultiCdnConfig,
        @"^https://css\.example\.com/_astro/.*",
        @"^https://js\.example\.com/_astro/.*",
        @"^https://example\.com/_astro/.*");

    private static readonly object PathPrefixConfig = new { @base = Base, build = new { assetsPrefix = "/starting-slash" } };

    /// <summary>The path of a page: prerendered for the "static" tests, rendered on demand for the "server" tests.</summary>
    private static string PagePath(string mode, string page = "") => mode == "static" ? $"{Base}/{page}" : $"{Base}/ssr/{page}";

    private async Task<(ConformanceSite Site, IDocument Html)> LoadAsync(object config, string path)
    {
        var site = await sites.GetAsync(Fixture, config);

        return (site, await PageAssets.PageAsync(site, path));
    }

    private async Task AssertStylesheetsAsync(object config, string mode, string pattern)
    {
        var (site, html) = await LoadAsync(config, PagePath(mode));

        var stylesheets = html.QuerySelectorAll("link[rel=stylesheet]");
        Assert.NotEmpty(stylesheets);
        foreach (var link in stylesheets)
        {
            var href = link.GetAttribute("href")!;
            Assert.Matches(pattern, href);
            await PageAssets.AssertServedAsync(site, href, Base);
        }
    }

    private async Task AssertImageAsync(Prefixes prefixes, string mode)
    {
        var (site, html) = await LoadAsync(prefixes.Config, PagePath(mode));

        var src = html.QuerySelector("#image-asset")!.GetAttribute("src")!;

        Assert.Matches(prefixes.Fallback, src);
        await PageAssets.AssertServedAsync(site, src, Base);
    }

    private async Task AssertIslandAsync(Prefixes prefixes, string mode)
    {
        var (site, html) = await LoadAsync(prefixes.Config, PagePath(mode));

        var island = html.QuerySelector("astro-island")!;
        foreach (var attribute in new[] { "component-url", "renderer-url" })
        {
            var url = island.GetAttribute(attribute)!;
            Assert.Matches(prefixes.Js, url);
            await PageAssets.AssertServedAsync(site, url, Base);
        }
    }

    private async Task AssertImagesAsync(Prefixes prefixes, string path, int count)
    {
        var (site, html) = await LoadAsync(prefixes.Config, path);

        var images = html.QuerySelectorAll("img");
        Assert.Equal(count, images.Length);
        foreach (var image in images)
        {
            var src = image.GetAttribute("src")!;
            Assert.Matches(prefixes.Fallback, src);
            await PageAssets.AssertServedAsync(site, src, Base);
        }
    }

    private async Task AssertSsrImageIsNotPrefixedAsync(Prefixes prefixes)
    {
        var (_, html) = await LoadAsync(prefixes.Config, PagePath("server", "markdown/"));

        var src = html.QuerySelector("img")!.GetAttribute("src")!;

        Assert.DoesNotMatch(prefixes.Fallback, src);
        // The on-demand image endpoint of the app, not the URL of an asset.
        Assert.StartsWith($"{Base}/_image?", src);
    }

    [Theory(DisplayName = "Assets Prefix: all stylesheets should start with assetPrefix")]
    [InlineData("static")]
    [InlineData("server")]
    public Task Single_StylesheetsStartWithPrefix(string mode) => AssertStylesheetsAsync(Single.Config, mode, Single.Css);

    [Theory(DisplayName = "Assets Prefix: image src start with assetsPrefix")]
    [InlineData("static")]
    [InlineData("server")]
    public Task Single_ImageSrcStartsWithPrefix(string mode) => AssertImageAsync(Single, mode);

    [Theory(DisplayName = "Assets Prefix: react component astro-island should import from assetsPrefix")]
    [InlineData("static")]
    [InlineData("server")]
    public Task Single_IslandImportsFromPrefix(string mode) => AssertIslandAsync(Single, mode);

    [Fact(DisplayName = "Assets Prefix - Static: import.meta.env.ASSETS_PREFIX works")]
    public async Task Single_EnvAssetsPrefixWorks()
    {
        var (_, html) = await LoadAsync(Single.Config, PagePath("static"));

        Assert.Equal("http://localhost:4321", html.QuerySelector("#assets-prefix-env")!.TextContent);
    }

    [Fact(DisplayName = "Assets Prefix - Static: markdown image src start with assetsPrefix")]
    public Task Single_MarkdownImagesStartWithPrefix() => AssertImagesAsync(Single, PagePath("static", "markdown/"), 2);

    [Fact(DisplayName = "Assets Prefix - Static: content collections image src start with assetsPrefix")]
    public async Task Single_CollectionImagesStartWithPrefix()
    {
        var (site, html) = await LoadAsync(Single.Config, PagePath("static", "blog/"));

        var src = html.QuerySelector("img")!.GetAttribute("src")!;

        Assert.Matches(Single.Fallback, src);
        await PageAssets.AssertServedAsync(site, src, Base);
    }

    [Fact(DisplayName = "Assets Prefix - Static: MDX content collection CSS imports should start with assetsPrefix")]
    public async Task Single_MdxCollectionCssStartsWithPrefix()
    {
        var (site, html) = await LoadAsync(Single.Config, PagePath("static", "mdx-blog/"));

        var stylesheets = html.QuerySelectorAll("link[rel=stylesheet]");
        Assert.NotEmpty(stylesheets);
        foreach (var link in stylesheets)
        {
            var href = link.GetAttribute("href")!;
            Assert.Matches(Single.Css, href);
            await PageAssets.AssertServedAsync(site, href, Base);
        }
    }

    [Fact(DisplayName = "Assets Prefix, server: markdown optimized image src does not start with assetsPrefix in SSR")]
    public Task Single_MarkdownImageInSsrIsNotPrefixed() => AssertSsrImageIsNotPrefixedAsync(Single);

    [Theory(DisplayName = "Assets Prefix, with path prefix: all stylesheets should start with assetPrefix")]
    [InlineData("static")]
    [InlineData("server")]
    public Task PathPrefix_StylesheetsStartWithPrefix(string mode) => AssertStylesheetsAsync(PathPrefixConfig, mode, "^/starting-slash/.*");

    [Theory(DisplayName = "Assets Prefix Multiple CDN: all stylesheets should start with cssAssetPrefix")]
    [InlineData("static")]
    [InlineData("server")]
    public Task MultiCdn_StylesheetsStartWithCssPrefix(string mode) => AssertStylesheetsAsync(MultiCdn.Config, mode, MultiCdn.Css);

    [Theory(DisplayName = "Assets Prefix Multiple CDN: image src start with fallback")]
    [InlineData("static")]
    [InlineData("server")]
    public Task MultiCdn_ImageSrcStartsWithFallback(string mode) => AssertImageAsync(MultiCdn, mode);

    [Theory(DisplayName = "Assets Prefix Multiple CDN: react component astro-island should import from jsAssetsPrefix")]
    [InlineData("static")]
    [InlineData("server")]
    public Task MultiCdn_IslandImportsFromJsPrefix(string mode) => AssertIslandAsync(MultiCdn, mode);

    [Fact(DisplayName = "Assets Prefix Multiple CDN - Static: import.meta.env.ASSETS_PREFIX works")]
    public async Task MultiCdn_EnvAssetsPrefixWorks()
    {
        var (_, html) = await LoadAsync(MultiCdn.Config, PagePath("static"));

        var env = JsonSerializer.Deserialize<Dictionary<string, string>>(html.QuerySelector("#assets-prefix-env")!.TextContent);

        Assert.Equal(CdnMap, env);
    }

    [Fact(DisplayName = "Assets Prefix Multiple CDN - Static: markdown image src start with assetsPrefix")]
    public Task MultiCdn_MarkdownImagesStartWithFallback() => AssertImagesAsync(MultiCdn, PagePath("static", "markdown/"), 2);

    [Fact(DisplayName = "Assets Prefix Multiple CDN - Static: content collections image src start with assetsPrefix")]
    public async Task MultiCdn_CollectionImagesStartWithFallback()
    {
        var (site, html) = await LoadAsync(MultiCdn.Config, PagePath("static", "blog/"));

        var src = html.QuerySelector("img")!.GetAttribute("src")!;

        Assert.Matches(MultiCdn.Fallback, src);
        await PageAssets.AssertServedAsync(site, src, Base);
    }

    [Fact(DisplayName = "Assets Prefix Multiple CDN, server: markdown optimized image src does not start with assetsPrefix in SSR")]
    public Task MultiCdn_MarkdownImageInSsrIsNotPrefixed() => AssertSsrImageIsNotPrefixedAsync(MultiCdn);
}
