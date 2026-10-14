using System.Net;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/prerender.test.ts</c>. The upstream <c>describe</c> blocks are the
/// <see cref="Variant"/> values: "Prerendering" with and without a base, "Via integration", and "Hybrid rendering" with and
/// without a base. Its "Shared modules" block uses the configuration of "Hybrid rendering / Without base". Upstream's "Dev"
/// block starts Astro's development server, which this adapter does not host, so it has no counterpart here.
/// </summary>
[Trait("Upstream", "node/prerender.test.ts")]
public class PrerenderTests(SiteHost sites)
{
    private const string Fixture = "prerender";

    public enum Variant
    {
        ServerWithBase,
        ServerWithoutBase,
        ViaIntegration,
        HybridWithBase,
        HybridWithoutBase,
    }

    private static object ConfigFor(Variant variant) => variant switch
    {
        Variant.ServerWithBase => new { @base = "/some-base", output = "server", redirects = new Dictionary<string, string> { ["/old-two"] = "/two" } },
        Variant.ServerWithoutBase => new { output = "server" },
        Variant.ViaIntegration => new
        {
            output = "server",
            integrations = new[] { new { conformanceIntegration = "prerender-route", componentEndsWith = "two.astro" } },
        },
        Variant.HybridWithBase => new { @base = "/some-base", output = "static" },
        Variant.HybridWithoutBase => new { output = "static" },
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };

    private static string BaseOf(Variant variant) => variant is Variant.ServerWithBase or Variant.HybridWithBase ? "/some-base" : "";

    private async Task<ConformanceSite> SiteAsync(Variant variant) => await sites.GetAsync(Fixture, ConfigFor(variant));

    // The paths are written relative to the base, which the variant adds.
    private async Task AssertPageAsync(Variant variant, string path, string heading)
    {
        var site = await SiteAsync(variant);

        using var response = await site.GetAsync(BaseOf(variant) + path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(heading, (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
    }

    [Theory(DisplayName = "Can render SSR route")]
    [InlineData(Variant.ServerWithBase, "/one", "One")]
    [InlineData(Variant.ServerWithoutBase, "/one", "One")]
    [InlineData(Variant.ViaIntegration, "/one", "One")]
    [InlineData(Variant.HybridWithBase, "/two", "Two")]
    [InlineData(Variant.HybridWithoutBase, "/two", "Two")]
    [InlineData(Variant.HybridWithoutBase, "/third", "shared")]
    public Task SsrRoute_IsRendered(Variant variant, string path, string heading) => AssertPageAsync(variant, path, heading);

    [Theory(DisplayName = "Can render prerendered route")]
    [InlineData(Variant.ServerWithBase, "/two", "Two", "client/two/index.html")]
    [InlineData(Variant.ServerWithoutBase, "/two", "Two", "client/two/index.html")]
    [InlineData(Variant.ViaIntegration, "/two", "Two", "client/two/index.html")]
    [InlineData(Variant.HybridWithBase, "/one", "One", "client/one/index.html")]
    [InlineData(Variant.HybridWithoutBase, "/one", "One", "client/one/index.html")]
    public async Task PrerenderedRoute_IsAFileOfTheBuild(Variant variant, string path, string heading, string file)
    {
        await AssertPageAsync(variant, path, heading);

        Assert.True((await SiteAsync(variant)).PathExists(file), $"The build has no '{file}'.");
    }

    [Theory(DisplayName = "Can render prerendered route with redirect and query params")]
    [InlineData(Variant.ServerWithBase, "/two?foo=bar", "Two")]
    [InlineData(Variant.ServerWithoutBase, "/two?foo=bar", "Two")]
    [InlineData(Variant.HybridWithBase, "/one?foo=bar", "One")]
    [InlineData(Variant.HybridWithoutBase, "/one?foo=bar", "One")]
    public Task PrerenderedRoute_IgnoresTheQuery(Variant variant, string path, string heading) => AssertPageAsync(variant, path, heading);

    [Theory(DisplayName = "Can render prerendered route with query params")]
    [InlineData(Variant.ServerWithBase, "/two/?foo=bar", "Two")]
    [InlineData(Variant.ServerWithoutBase, "/two/?foo=bar", "Two")]
    [InlineData(Variant.HybridWithBase, "/one/?foo=bar", "One")]
    [InlineData(Variant.HybridWithoutBase, "/one/?foo=bar", "One")]
    public Task PrerenderedRoute_WithATrailingSlash_IgnoresTheQuery(Variant variant, string path, string heading) => AssertPageAsync(variant, path, heading);

    [Theory(DisplayName = "Can render prerendered route without trailing slash")]
    [InlineData(Variant.ServerWithBase, "/two", "Two")]
    [InlineData(Variant.HybridWithBase, "/one", "One")]
    public Task PrerenderedRoute_WithoutATrailingSlash_IsNotRedirected(Variant variant, string path, string heading) => AssertPageAsync(variant, path, heading);

    [Theory(DisplayName = "Can render prerendered dynamic route")]
    [InlineData(Variant.ServerWithBase)]
    [InlineData(Variant.ServerWithoutBase)]
    [InlineData(Variant.HybridWithBase)]
    [InlineData(Variant.HybridWithoutBase)]
    public Task PrerenderedDynamicRoute_IsRendered(Variant variant) => AssertPageAsync(variant, "/dogs/rover", "Good dog, rover!");

    [Theory(DisplayName = "Can render 404 matching a prerendered dynamic route pattern")]
    [InlineData(Variant.ServerWithBase)]
    [InlineData(Variant.ServerWithoutBase)]
    [InlineData(Variant.HybridWithBase)]
    [InlineData(Variant.HybridWithoutBase)]
    public async Task PathsThatMatchAPrerenderedDynamicRoute_ButWereNotBuilt_AreNotFound(Variant variant)
    {
        var site = await SiteAsync(variant);

        using var response = await site.GetAsync(BaseOf(variant) + "/dogs/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "Can render 404 matching a prerendered dynamic endpoint pattern")]
    public async Task PathsThatMatchAPrerenderedDynamicEndpoint_ButWereNotBuilt_AreNotFound()
    {
        var site = await SiteAsync(Variant.ServerWithBase);

        using var response = await site.GetAsync("/some-base/api/dogs/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "Can render redirects alongside prerendered dynamic endpoints")]
    public async Task Redirects_WorkAlongsidePrerenderedDynamicEndpoints()
    {
        var site = await SiteAsync(Variant.ServerWithBase);

        using var response = await site.GetAsync("/some-base/old-two");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
    }
}
