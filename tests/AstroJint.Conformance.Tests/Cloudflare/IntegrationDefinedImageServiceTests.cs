using System.Text;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/integration-defined-image-service.test.ts</c>: an image service that an
/// integration sets from <c>astro:config:setup</c> (<c>updateConfig</c>) takes part in the build the same as one that the site
/// sets with <c>image.service</c>, although the adapter's own hook runs first. The integration is the factory
/// <c>cf-image-service</c> of <c>tests/conformance/support/integrations.mjs</c>; it sets <c>src/image-service.ts</c> of the
/// shared fixture <c>cf-pages</c>.
/// <para>
/// The test is one of the loop over the adapter's <c>imageService: 'compile'</c> and <c>'custom'</c>, which this adapter does
/// not have; the host has the one behavior, and the test is the single instance. The page is <c>/images/prerendered/</c>, the
/// upstream page is the index of a fixture of its own.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/integration-defined-image-service.test.ts")]
public class IntegrationDefinedImageServiceTests(SiteHost sites)
{
    [Fact(DisplayName = "imageService: 'compile' - integration-defined service transform() runs at build time and markup is respected")]
    public async Task IntegrationService_RunsAtBuildTimeAndItsMarkupIsKept()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.ImageServiceFromIntegration);
        var document = ConformanceSite.ParseHtml(await CfImages.ReadPrerenderedAsync(site, "images/prerendered"));
        var img = document.QuerySelector("img")!;

        // The integration-defined service should decorate the prerendered markup...
        Assert.Equal("custom", img.GetAttribute("data-image-service"));

        // ...and its transform() should run during build-time asset generation, exactly as it does when the same service is
        // set via `image.service` user config (covered by CompileImageServiceTests).
        var src = img.GetAttribute("src") ?? "";
        Assert.Matches(@"^/_astro/.+", src);
        var data = await CfImages.ReadClientFileAsync(site, src);
        Assert.Equal(CfImages.TransformMarker, Encoding.UTF8.GetString(data, 0, 20));
    }
}
