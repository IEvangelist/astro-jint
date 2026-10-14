using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/ssr-scripts.test.ts</c>. <c>Are included in the manifest.assets so that an adapter can know to serve static</c>
/// reads <c>app.manifest.assets</c> of the test adapter, which the host has no counterpart of: the host serves every file of
/// <c>dist/client</c>. The port is adapted to the effect that the manifest entry is for, so it requests the page, takes the URLs of the
/// hydration scripts that the <c>astro-island</c> names and checks that the host serves them as static files.
/// </summary>
[Trait("Upstream", "core/ssr-scripts.test.ts")]
public class SsrScriptsTests(SiteHost sites)
{
    private const string Fixture = "core-ssr-scripts";

    [Fact(DisplayName = "Are included in the manifest.assets so that an adapter can know to serve static")]
    public async Task HydrationScripts_AreServedAsStaticFiles()
    {
        var site = await sites.GetAsync(Fixture);

        using var page = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var island = (await page.ReadHtmlAsync()).QuerySelector("astro-island");
        Assert.NotNull(island);
        var urls = new[] { "component-url", "renderer-url" }.Select(name => island.GetAttribute(name)).ToList();
        Assert.All(urls, Assert.NotNull);
        foreach (var url in urls)
        {
            Assert.StartsWith("/_astro/", url);
            using var script = await site.GetAsync(url!);
            Assert.Equal(HttpStatusCode.OK, script.StatusCode);
            Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
            Assert.NotEmpty(await script.ReadTextAsync());
        }
    }

    [Fact(DisplayName = "import.meta.env.SSR is true")]
    public async Task ImportMetaEnvSsr_IsTrueWhileRendering()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/ssr");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("true", (await response.ReadHtmlAsync()).QuerySelector("#ssr")?.TextContent);
    }
}
