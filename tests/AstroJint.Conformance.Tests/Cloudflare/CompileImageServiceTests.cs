using System.Net;
using System.Text;
using AngleSharp.Dom;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/compile-image-service.test.ts</c>: what <c>/_image</c> refuses and serves,
/// and what the build does with the image service of the site. The pages are <c>/images/prerendered/</c>,
/// <c>/images/paths/one/</c> and <c>/images/raw-src</c> of the shared fixture <c>cf-pages</c>, the image services are its
/// <c>src/image-service.ts</c> (no Sharp: its transform puts a marker in front of the bytes) and
/// <c>src/sharp-image-service.ts</c> (Astro's Sharp service with its own markup).
/// <para>
/// The adapter has no <c>imageService</c> option and no <c>prerenderEnvironment</c> option, so the loop over
/// <c>'compile'</c> and <c>'custom'</c> and the <c>prerenderEnvironment: 'node'</c> suite have no counterpart. What the host
/// does is what the <c>compile</c> instance asks of the Cloudflare adapter: the build runs the image service of the site in
/// Node, and the server runs it, as it is bundled, when the endpoint is asked. The bundle assertions that name Cloudflare
/// files (<c>image-passthrough-endpoint</c>, <c>astro/dist/assets/endpoint/generic.js</c>) are not made. The runtime half of
/// the <c>custom</c> instance, that the service of the site stays live, is the one extra test of the host.
/// </para>
/// <para>
/// Adapted: 'forbids //' (the host says "Forbidden" where the Cloudflare endpoint says "Blocked") and the two Sharp-free
/// tests, which are the same scenario with this host's output.
/// </para>
/// <para>
/// Not applicable, see the matrix: the two tests that start the Astro dev server.
/// </para>
/// <para>
/// GAP-CF-5: the build copies pictures, it does not convert them, so no WebP is generated for the default service.
/// GAP-CF-7: a Sharp-backed service of the site makes <c>astro build</c> fail with MissingSharp. The last assertion of that
/// upstream test, that the Sharp chain is in the bundle, is about what the Cloudflare bundler does and is not made.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/compile-image-service.test.ts")]
public class CompileImageServiceTests(SiteHost sites)
{
    private const string Prerendered = "images/prerendered";

    [Fact(DisplayName = "forbids http://")]
    public async Task Endpoint_ForbidsHttp()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/_image?href=http://placehold.co/600x400");

        Assert.Equal("Forbidden", await response.ReadTextAsync());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "forbids https://")]
    public async Task Endpoint_ForbidsHttps()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/_image?href=https://placehold.co/600x400");

        Assert.Equal("Forbidden", await response.ReadTextAsync());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "forbids //")]
    public async Task Endpoint_ForbidsProtocolRelativeUrls()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/_image?href=//placehold.co/600x400");

        Assert.Equal("Forbidden", await response.ReadTextAsync());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "allows local")]
    public async Task Endpoint_AllowsLocalImages()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/_image?href=/_astro/penguin.gLBdjEDe.jpg&f=jpg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "with no user image.service: generates real WEBP assets at build time", Skip = CfImages.BuildConversionGap)]
    public async Task DefaultService_GeneratesWebpAssetsAtBuildTime()
    {
        var site = await sites.GetAsync(CfPages.Fixture);
        var document = ConformanceSite.ParseHtml(await CfImages.ReadPrerenderedAsync(site, Prerendered));

        CfImages.AssertWebp(await ReadGeneratedImageAsync(site, document));

        CfImages.AssertSharpNotBundled(CfImages.ReadServerBundle(site));
    }

    [Fact(DisplayName = "with a Sharp-free user image.service: runs its transform() at build time and respects its markup, without bundling Sharp")]
    public async Task SharpFreeService_RunsAtBuildTimeWithoutBundlingSharp()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.CustomImageService);
        var document = ConformanceSite.ParseHtml(await CfImages.ReadPrerenderedAsync(site, Prerendered));

        Assert.Equal("custom", document.QuerySelector("img")!.GetAttribute("data-image-service"));

        // The user service's transform() ran during the build (prepends a marker).
        var data = await ReadGeneratedImageAsync(site, document);
        Assert.Equal(CfImages.TransformMarker, Encoding.UTF8.GetString(data, 0, 20));

        // The user service is bundled, but it is Sharp-free so Sharp stays out.
        var serverBundle = CfImages.ReadServerBundle(site);
        Assert.Contains("src/image-service.ts", serverBundle);
        CfImages.AssertSharpNotBundled(serverBundle);

        var pathsDocument = ConformanceSite.ParseHtml(await CfImages.ReadPrerenderedAsync(site, "images/paths/one"));
        var pathsSrc = pathsDocument.QuerySelector("img")!.GetAttribute("src")!;
        Assert.Matches(@"^/_astro/paths\..+\.webp$", pathsSrc);
        Assert.True(site.PathExists($"client{pathsSrc}"), "getImage() in getStaticPaths");
        Assert.Single(AssetsOf(site, "paths.*"));

        // The page reads `kept.src`, so the original must survive next to the optimized copy.
        Assert.Equal(2, AssetsOf(site, "kept.*").Length);
    }

    [Fact(DisplayName = "with a Sharp-backed user image.service: generates assets, respects its markup, and bundles the Sharp chain",
        Skip = CfImages.SharpServiceGap)]
    public async Task SharpBackedService_GeneratesAssets()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.SharpImageService);
        var document = ConformanceSite.ParseHtml(await CfImages.ReadPrerenderedAsync(site, Prerendered));

        Assert.Equal("custom-sharp", document.QuerySelector("img")!.GetAttribute("data-image-service"));
        CfImages.AssertWebp(await ReadGeneratedImageAsync(site, document));

        Assert.Contains("src/sharp-image-service.ts", CfImages.ReadServerBundle(site));
    }

    [Fact(DisplayName = "generates real WEBP assets at build time with prerenderEnvironment: node", Skip = CfImages.BuildConversionGap)]
    public async Task NodePrerender_GeneratesWebpAssetsAtBuildTime()
    {
        var site = await sites.GetAsync(CfPages.Fixture);
        var document = ConformanceSite.ParseHtml(await CfImages.ReadPrerenderedAsync(site, Prerendered));

        CfImages.AssertWebp(await ReadGeneratedImageAsync(site, document));
    }

    [Fact(DisplayName = "runs custom Sharp-free image service transform() with prerenderEnvironment: node")]
    public async Task NodePrerender_RunsTheTransformOfASharpFreeService()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.CustomImageService);
        var document = ConformanceSite.ParseHtml(await CfImages.ReadPrerenderedAsync(site, Prerendered));

        var data = await ReadGeneratedImageAsync(site, document);

        // The user service's transform() ran during the build (prepends a marker).
        Assert.Equal(CfImages.TransformMarker, Encoding.UTF8.GetString(data, 0, 20));
    }

    [Fact(DisplayName = "keeps the user image.service live at runtime (host)")]
    public async Task SharpFreeService_RunsWhenTheEndpointIsAsked()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.CustomImageService);
        var document = await site.GetHtmlAsync("/images/on-demand");
        var image = document.QuerySelector("#image")!;
        Assert.Equal("custom", image.GetAttribute("data-image-service"));

        using var response = await site.GetAsync(image.GetAttribute("src")!);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(CfImages.TransformMarker, Encoding.UTF8.GetString(body, 0, 20));
    }

    private static async Task<byte[]> ReadGeneratedImageAsync(ConformanceSite site, IDocument document)
    {
        var src = document.QuerySelector("img")?.GetAttribute("src") ?? "";
        Assert.Matches(@"^/_astro/.+\.webp$", src);
        return await CfImages.ReadClientFileAsync(site, src);
    }

    private static string[] AssetsOf(ConformanceSite site, string pattern) =>
        Directory.GetFiles(Path.Combine(site.DistPath, "client", "_astro"), pattern);
}
