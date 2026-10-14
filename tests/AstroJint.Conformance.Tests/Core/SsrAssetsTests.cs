using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/ssr-assets.test.ts</c>. The only upstream test (misnamed <c>Do not have to implement getStaticPaths</c>)
/// reads <c>app.manifest.assets</c>, the list of static files the test adapter serves: exactly one, a stylesheet. The host has no
/// manifest of assets; it serves what is in <c>dist/client</c>. The port asserts on that folder (one file, a stylesheet) and
/// requests the stylesheet that the page links to.
/// </summary>
[Trait("Upstream", "core/ssr-assets.test.ts")]
public class SsrAssetsTests(SiteHost sites)
{
    private const string Fixture = "core-ssr-assets";

    [Fact(DisplayName = "Do not have to implement getStaticPaths")]
    public async Task ClientOutput_HasOnlyTheStylesheet()
    {
        var site = await sites.GetAsync(Fixture);

        var assets = Directory.EnumerateFiles(Path.Combine(site.DistPath, "client"), "*", SearchOption.AllDirectories).ToList();

        var asset = Assert.Single(assets);
        Assert.EndsWith(".css", asset);
        using var page = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var link = Assert.Single((await page.ReadHtmlAsync()).QuerySelectorAll("link[rel=stylesheet]"));
        Assert.EndsWith(Path.GetFileName(asset), link.GetAttribute("href"));
        using var css = await site.GetAsync(link.GetAttribute("href")!);
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.Equal("text/css", css.Content.Headers.ContentType?.MediaType);
        Assert.Contains("color:red", await css.ReadTextAsync());
    }
}
