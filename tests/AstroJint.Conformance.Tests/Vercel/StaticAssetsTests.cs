using System.Net;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/static-assets.test.ts</c>. Upstream builds the same page four times, with
/// static and with server output, each with the default assets folder and with <c>build.assets: '_foo'</c>, and reads the
/// route table of <c>.vercel/output/config.json</c>: it must have a rule for the files of the assets folder that sets
/// <c>cache-control: public, max-age=31536000, immutable</c>, continues to the next route, and is before the filesystem
/// handle. The titles of the describe blocks (<c>static adapter</c>, <c>serverless adapter</c>) prefix the titles of the
/// tests, which repeat.
/// <para>
/// Differences from upstream: the rule of the route table is replaced by what it does. The hashed stylesheet that the page
/// links is served from the assets folder with the immutable header, and the page itself is not. The four builds are the
/// shared sites of the fixtures: <c>vercel-static</c> (with the default folder, and with <c>_foo</c> and
/// <c>trailingSlash: 'always'</c>) and <c>vercel-server</c> (with the default folder, and with <c>_foo</c> and a
/// prerendered 404 page), because a fixture is one build per config.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/static-assets.test.ts")]
public class StaticAssetsTests(SiteHost sites)
{
    private const string ValidCacheControl = "public, max-age=31536000, immutable";

    [Fact(DisplayName = "static adapter: has cache control")]
    public async Task StaticBuild_CachesTheHashedAssets() =>
        await AssertHashedAssetsAreCachedAsync(await sites.StaticAsync(), "/styled", "_astro");

    [Fact(DisplayName = "static adapter: has cache control other assets")]
    public async Task StaticBuild_CachesTheHashedAssetsOfAnotherFolder() =>
        await AssertHashedAssetsAreCachedAsync(await sites.StaticWithTrailingSlashesAsync(), "/styled/", "_foo");

    [Fact(DisplayName = "serverless adapter: has cache control")]
    public async Task ServerBuild_CachesTheHashedAssets() =>
        await AssertHashedAssetsAreCachedAsync(await sites.ServerAsync(), "/styled", "_astro");

    [Fact(DisplayName = "serverless adapter: has cache control other assets")]
    public async Task ServerBuild_CachesTheHashedAssetsOfAnotherFolder() =>
        await AssertHashedAssetsAreCachedAsync(await sites.ServerWithPrerenderedErrorPageAsync(), "/styled", "_foo");

    private static async Task AssertHashedAssetsAreCachedAsync(ConformanceSite site, string page, string assets)
    {
        var stylesheet = await site.StylesheetOfAsync(page);

        using var asset = await site.GetAsync(stylesheet);
        using var html = await site.GetAsync(page);

        Assert.StartsWith($"/{assets}/", stylesheet);
        Assert.True(Directory.Exists(Path.Combine(site.DistPath, "client", assets)), $"the build should write the hashed files to {assets}");
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal(ValidCacheControl, Assert.Single(asset.HeaderValues("Cache-Control")));
        Assert.Equal(HttpStatusCode.OK, html.StatusCode);
        Assert.DoesNotContain("immutable", string.Join(",", html.HeaderValues("Cache-Control")));
    }
}
