using System.Net;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/assets.test.ts</c>. Files that Astro emits below <c>_astro</c> have a content
/// hash in their name, so they may be cached forever; the page that links to one may not. A request that cannot succeed
/// must never be answered with the far-future header, or a CDN would cache the failure.
/// </summary>
[Trait("Upstream", "node/assets.test.ts")]
public class AssetsTests(SiteHost sites)
{
    private const string Fixture = "image";
    private const string Immutable = "public, max-age=31536000, immutable";

    [Fact(DisplayName = "Assets within the _astro folder should be given immutable headers")]
    public async Task AstroAssets_AreImmutable()
    {
        var site = await sites.GetAsync(Fixture);

        using var page = await site.GetAsync("/text-file");
        Assert.Empty(page.HeaderValues("cache-control"));

        var href = (await page.ReadHtmlAsync()).QuerySelector("a")?.GetAttribute("href");
        Assert.NotNull(href);

        using var asset = await site.GetAsync(href);
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal([Immutable], asset.HeaderValues("cache-control"));
    }

    [Fact(DisplayName = "Malformed If-Match header should return 412 without immutable cache headers")]
    public async Task MalformedIfMatch_Returns412WithoutTheImmutableHeader()
    {
        var site = await sites.GetAsync(Fixture);

        using var page = await site.GetAsync("/text-file");
        var href = (await page.ReadHtmlAsync()).QuerySelector("a")?.GetAttribute("href");
        Assert.NotNull(href);

        using var asset = await site.SendAsync(HttpMethod.Get, href, null, ("If-Match", "xxx"));

        Assert.Equal(HttpStatusCode.PreconditionFailed, asset.StatusCode);
        Assert.DoesNotContain(Immutable, asset.HeaderValues("cache-control"));
    }
}
