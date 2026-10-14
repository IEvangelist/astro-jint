using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/default-image-service.test.ts</c>: what the image service that the adapter
/// sets up without being asked does at build time and at run time. The pages are <c>/images/prerendered/</c> (the upstream
/// <c>index.astro</c> of the static fixture and <c>prerendered.astro</c> of the server fixture) and <c>/images/on-demand</c> of
/// the shared fixture <c>cf-pages</c>.
/// <para>
/// The static half builds the site with <c>output: 'static'</c>. Its loop has three instances upstream; the second, with the
/// output left out (which means static), builds the same site and is not repeated, and the third, <c>imageService:
/// 'cloudflare-binding'</c>, is an option of the Cloudflare adapter that this adapter does not have. The server half runs for
/// <c>output: 'server'</c> and for the output left out, which is <c>CfPages.Static</c> here since the fixture names its output.
/// </para>
/// <para>
/// Not applicable, see the matrix: 'does not build worker' (the host writes <c>server/entry.mjs</c> for every site, static or
/// not) and 'provisions the Images binding' (the <c>wrangler.json</c> of Cloudflare).
/// </para>
/// <para>
/// GAP-CF-5 and GAP-CF-6: the adapter replaces Sharp with a pass-through image service, so the build copies the pictures
/// instead of converting them to WebP and AVIF, and <c>/_image</c> sends the source picture for every request.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/default-image-service.test.ts")]
public class DefaultImageServiceTests(SiteHost sites)
{
    private const string Prerendered = "images/prerendered";

    private Task<ConformanceSite> GetStaticSiteAsync() => sites.GetAsync(CfPages.Fixture, CfPages.Static);

    private Task<ConformanceSite> GetServerSiteAsync(string output) =>
        sites.GetAsync(CfPages.Fixture, output == "inferred" ? CfPages.Static : null);

    [Fact(DisplayName = "does not emit /_image URLs")]
    public async Task StaticOutput_DoesNotEmitImageEndpointUrls()
    {
        var site = await GetStaticSiteAsync();

        var html = await CfImages.ReadPrerenderedAsync(site, Prerendered);

        Assert.DoesNotContain("/_image?", html);
    }

    [Fact(DisplayName = "points every image at a hashed build asset")]
    public async Task StaticOutput_PointsEveryImageAtABuildAsset()
    {
        var site = await GetStaticSiteAsync();
        var document = ConformanceSite.ParseHtml(await CfImages.ReadPrerenderedAsync(site, Prerendered));

        var found = CfImages.Srcs(document, "img, source");

        Assert.True(found.Count >= 6, $"expected Image, Picture and getImage output, got {found.Count}");
        foreach (var src in found)
        {
            Assert.Matches(@"^/_astro/penguin\.[\w-]+\.\w+$", src);
        }
    }

    [Fact(DisplayName = "writes every referenced asset into the client directory")]
    public async Task StaticOutput_WritesEveryReferencedAsset()
    {
        var site = await GetStaticSiteAsync();
        var document = ConformanceSite.ParseHtml(await CfImages.ReadPrerenderedAsync(site, Prerendered));

        foreach (var src in CfImages.Srcs(document, "img, source").Distinct())
        {
            var data = await CfImages.ReadClientFileAsync(site, src);
            Assert.True(data.Length > 0, $"empty asset written for {src}");
        }
    }

    [Fact(DisplayName = "honors the format each component asked for", Skip = CfImages.BuildConversionGap)]
    public async Task StaticOutput_HonorsTheRequestedFormats()
    {
        var site = await GetStaticSiteAsync();
        var document = ConformanceSite.ParseHtml(await CfImages.ReadPrerenderedAsync(site, Prerendered));

        var image = document.QuerySelector("#image")!.GetAttribute("src")!;
        Assert.EndsWith(".webp", image);
        CfImages.AssertWebp(await CfImages.ReadClientFileAsync(site, image));

        var sources = CfImages.Srcs(document, "#picture").Concat(CfImages.Srcs(document, "source")).ToList();
        Assert.True(sources.Count >= 4, $"expected two widths per format, got {sources.Count}");
        Assert.Contains(sources, src => src.EndsWith(".avif", StringComparison.Ordinal));

        Assert.EndsWith(".avif", document.QuerySelector("#get-image")!.GetAttribute("src")!);
    }

    [Theory(DisplayName = "transforms images on prerendered routes into build assets", Skip = CfImages.BuildConversionGap)]
    [InlineData("server")]
    [InlineData("inferred")]
    public async Task ServerOutput_TransformsPrerenderedImagesIntoBuildAssets(string output)
    {
        var site = await GetServerSiteAsync(output);
        var html = await CfImages.ReadPrerenderedAsync(site, Prerendered);

        var src = ConformanceSite.ParseHtml(html).QuerySelector("#image")!.GetAttribute("src")!;

        Assert.Matches(@"^/_astro/penguin\.[\w-]+\.webp$", src);
        Assert.DoesNotContain("/_image?", html);
        Assert.True((await CfImages.ReadClientFileAsync(site, src)).Length > 0);
    }

    [Theory(DisplayName = "routes images on on-demand routes through /_image")]
    [InlineData("server")]
    [InlineData("inferred")]
    public async Task ServerOutput_RoutesOnDemandImagesThroughTheEndpoint(string output)
    {
        var site = await GetServerSiteAsync(output);

        var document = await site.GetHtmlAsync("/images/on-demand");

        Assert.Matches(@"^/_image\?", document.QuerySelector("#image")!.GetAttribute("src")!);
    }

    [Theory(DisplayName = "serves /_image requests through the Images binding", Skip = CfImages.RuntimeConversionGap)]
    [InlineData("server")]
    [InlineData("inferred")]
    public async Task ServerOutput_ServesImageRequestsAsWebp(string output)
    {
        var site = await GetServerSiteAsync(output);
        var document = await site.GetHtmlAsync("/images/on-demand");
        var src = document.QuerySelector("#image")!.GetAttribute("src")!;

        using var response = await site.GetAsync(src);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory(DisplayName = "keeps Sharp out of the worker bundle")]
    [InlineData("server")]
    [InlineData("inferred")]
    public async Task ServerOutput_KeepsSharpOutOfTheServerBundle(string output)
    {
        var site = await GetServerSiteAsync(output);

        CfImages.AssertSharpNotBundled(CfImages.ReadServerBundle(site));
    }
}
