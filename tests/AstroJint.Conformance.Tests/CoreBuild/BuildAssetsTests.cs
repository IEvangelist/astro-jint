using AngleSharp.Dom;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/build-assets.test.ts</c>: where the build puts its assets (<c>/_astro</c> or
/// <c>build.assets</c>) and whether the HTML links them there. Upstream builds the <c>build-assets</c> fixture four times (with
/// and without <c>build.assets: 'custom-assets'</c>, once without adapter and once with a test adapter that declares
/// <c>buildOutput: 'static'</c>), always with <c>inlineStylesheets: 'never'</c>, and reads <c>dist</c>. Here the page of the
/// <c>cb-assets</c> fixture (a stylesheet and one React island) is requested from the host, and the files of
/// <c>dist/client/{assets}</c> are listed from disk.
/// <para>
/// Differences from upstream: the adapter is always the Jint adapter, so "static" is the page that the build prerendered
/// (<c>/</c>) and "server" is its twin that the host renders on demand (<c>/ssr/</c>); the adapter cannot declare
/// <c>buildOutput: 'static'</c>, which is why the "server" cases are adapted and not ported. Every stylesheet and script that
/// the page links is also requested from the host, which has to answer 200 with the content type of the file and the
/// immutable cache header. The two directory tests are stricter: the files that the page links have to be in the listing
/// of the directory ("populates"), and directly in it ("flat"); upstream only checks that the listing is not empty, and that no
/// name in it contains a slash, which cannot happen because <c>readdir</c> is not recursive.
/// </para>
/// </summary>
[Trait("Upstream", "core/build-assets.test.ts")]
public class BuildAssetsTests(SiteHost sites)
{
    private const string Fixture = "cb-assets";

    private static readonly object CustomAssetsConfig = new { build = new { assets = "custom-assets" } };

    private static string PagePath(string mode) => mode == "static" ? "/" : "/ssr/";

    private static string[] Entries(ConformanceSite site, string assets) =>
        Directory.GetFileSystemEntries(Path.Combine(site.DistPath, "client", assets));

    /// <summary>The URLs of the stylesheets and island scripts that the page links.</summary>
    private static string[] LinkedAssets(IDocument html) =>
    [
        .. html.QuerySelectorAll("link[href$=\".css\"]").Select(link => link.GetAttribute("href")!),
        .. html.QuerySelectorAll("astro-island").SelectMany(island => new[] { island.GetAttribute("component-url")!, island.GetAttribute("renderer-url")! }),
    ];

    private async Task AssertPopulatedAsync(object? config, string mode, string assets)
    {
        var site = await sites.GetAsync(Fixture, config);
        var html = await PageAssets.PageAsync(site, PagePath(mode));

        var names = Entries(site, assets).Select(entry => Path.GetFileName(entry)).ToArray();

        Assert.NotEmpty(names);
        var linked = LinkedAssets(html);
        Assert.NotEmpty(linked);
        Assert.All(linked, url => Assert.Contains(url[$"/{assets}/".Length..], names));
    }

    private async Task AssertFlatAsync(object? config, string mode, string assets)
    {
        var site = await sites.GetAsync(Fixture, config);
        var html = await PageAssets.PageAsync(site, PagePath(mode));

        Assert.All(Entries(site, assets), entry => Assert.True(File.Exists(entry), $"{entry} is not a file."));
        Assert.All(LinkedAssets(html), url => Assert.DoesNotContain('/', url[$"/{assets}/".Length..]));
    }

    private async Task AssertCssAsync(object? config, string mode, string assets)
    {
        var site = await sites.GetAsync(Fixture, config);
        var html = await PageAssets.PageAsync(site, PagePath(mode));

        var href = html.QuerySelector("link[href$=\".css\"]")!.GetAttribute("href")!;

        Assert.Matches($"^/{assets}/", href);
        await PageAssets.AssertServedAsync(site, href, assetsDir: assets);
    }

    private async Task AssertJsAsync(object? config, string mode, string assets)
    {
        var site = await sites.GetAsync(Fixture, config);
        var html = await PageAssets.PageAsync(site, PagePath(mode));

        var island = Assert.Single(html.QuerySelectorAll("astro-island"));
        foreach (var attribute in new[] { "component-url", "renderer-url" })
        {
            var url = island.GetAttribute(attribute)!;

            Assert.Matches($"^/{assets}/", url);
            await PageAssets.AssertServedAsync(site, url, assetsDir: assets);
        }
    }

    [Theory(DisplayName = "Populates /_astro directory")]
    [InlineData("static")]
    [InlineData("server")]
    public Task Default_PopulatesAstroDirectory(string mode) => AssertPopulatedAsync(null, mode, "_astro");

    [Theory(DisplayName = "Defaults to flat /_astro output")]
    [InlineData("static")]
    [InlineData("server")]
    public Task Default_OutputIsFlat(string mode) => AssertFlatAsync(null, mode, "_astro");

    [Theory(DisplayName = "emits CSS assets to /_astro")]
    [InlineData("static")]
    [InlineData("server")]
    public Task Default_CssIsEmittedToAstroDirectory(string mode) => AssertCssAsync(null, mode, "_astro");

    [Theory(DisplayName = "emits JS assets to /_astro")]
    [InlineData("static")]
    [InlineData("server")]
    public Task Default_JsIsEmittedToAstroDirectory(string mode) => AssertJsAsync(null, mode, "_astro");

    [Theory(DisplayName = "Populates /custom-assets directory")]
    [InlineData("static")]
    [InlineData("server")]
    public Task Custom_PopulatesCustomAssetsDirectory(string mode) => AssertPopulatedAsync(CustomAssetsConfig, mode, "custom-assets");

    [Theory(DisplayName = "emits CSS assets to /custom-assets")]
    [InlineData("static")]
    [InlineData("server")]
    public Task Custom_CssIsEmittedToCustomAssets(string mode) => AssertCssAsync(CustomAssetsConfig, mode, "custom-assets");

    [Theory(DisplayName = "emits JS assets to /custom-assets")]
    [InlineData("static")]
    [InlineData("server")]
    public Task Custom_JsIsEmittedToCustomAssets(string mode) => AssertJsAsync(CustomAssetsConfig, mode, "custom-assets");
}
