namespace AstroJint.Conformance.Netlify;

/// <summary>
/// Port of <c>packages/integrations/netlify/test/static/image-missing-dimension.test.ts</c>. The fixture is the one of upstream:
/// a static site whose index renders a remote image without its dimensions, with the <c>sharp</c> image service.
/// <para>
/// Differences from upstream: the fixture is built by <c>astro build</c> in a child process, so the failure is the failed
/// build that the harness reports, whose output names the error. The adapter replaces the <c>sharp</c> service by one that does
/// not transform images, which keeps the validation of the options that the test is about.
/// </para>
/// </summary>
[Trait("Upstream", "netlify/static/image-missing-dimension.test.ts")]
public class ImageMissingDimensionTests(SiteHost sites)
{
    [Fact(DisplayName = "throw on missing dimension in static build")]
    public async Task ABuildOfAnImageWithoutDimensionsFails()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => sites.GetAsync("netlify-image-missing-dimension"));

        Assert.Contains("MissingImageDimension", failure.Message);
    }
}
