using System.Net;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/trailing-slash.test.ts</c>. The upstream <c>describe</c> blocks are the
/// <see cref="Variant"/> values: <c>trailingSlash</c> set to <c>always</c>, <c>never</c> or <c>ignore</c>, each with and
/// without a <c>base</c>, plus the "Without automatic output" block, which leaves <c>output</c> unset. The one test titled
/// "Can render prerendered route and query params" upstream is merged into "Can render prerendered route with query params".
/// <para>
/// Differences from upstream, all of them about <c>base</c>: the upstream fixture config sets <c>base: '/some-base'</c>, and
/// <c>loadFixture()</c> merges the inline config over it, so upstream's "without base" blocks do have a base, and the Node
/// adapter serves static files for paths outside of it. Here the "without base" variants have no base, and the host does not
/// serve static files outside of the <c>base</c> (see docs/conformance.md). The "base route" test requests the home page at its
/// own URL (<c>/some-base/</c>, or <c>/some-base</c> with <c>never</c>) where upstream requests <c>/</c>.
/// </para>
/// </summary>
[Trait("Upstream", "node/trailing-slash.test.ts")]
public class TrailingSlashTests(SiteHost sites)
{
    private const string Fixture = "trailing-slash";
    private const string Base = "/some-base";

    private static readonly string NonAscii = "สวัสดี";

    public enum Variant
    {
        AlwaysWithBase,
        AlwaysWithoutBase,
        AlwaysWithoutAutomaticOutput,
        NeverWithBase,
        NeverWithoutBase,
        IgnoreWithBase,
        IgnoreWithoutBase,
    }

    private static object ConfigFor(Variant variant) => variant switch
    {
        Variant.AlwaysWithBase => new { @base = Base, output = "static", trailingSlash = "always" },
        Variant.AlwaysWithoutBase => new { output = "static", trailingSlash = "always" },
        Variant.AlwaysWithoutAutomaticOutput => new { trailingSlash = "always" },
        Variant.NeverWithBase => new { @base = Base, output = "static", trailingSlash = "never" },
        Variant.NeverWithoutBase => new { output = "static", trailingSlash = "never" },
        Variant.IgnoreWithBase => new { @base = Base, output = "static", trailingSlash = "ignore" },
        Variant.IgnoreWithoutBase => new { output = "static", trailingSlash = "ignore" },
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };

    private static string BaseOf(Variant variant) =>
        variant is Variant.AlwaysWithBase or Variant.NeverWithBase or Variant.IgnoreWithBase ? Base : "";

    private async Task<ConformanceSite> SiteAsync(Variant variant) => await sites.GetAsync(Fixture, ConfigFor(variant));

    // The paths of the tests are written relative to the base, which the variant adds.
    private async Task AssertPageAsync(Variant variant, string path, string heading)
    {
        var site = await SiteAsync(variant);

        using var response = await site.GetAsync(BaseOf(variant) + path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(heading, (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
    }

    private async Task AssertRedirectAsync(Variant variant, string path, string location)
    {
        var site = await SiteAsync(variant);

        using var response = await site.GetAsync(BaseOf(variant) + path);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(BaseOf(variant) + location, Assert.Single(response.HeaderValues("Location")));
    }

    // fetch() would resolve the target, so the request is written to the socket as it is.
    private async Task AssertRawRedirectAsync(Variant variant, string path, string location)
    {
        var site = await SiteAsync(variant);

        var response = await site.SendRawAsync(BaseOf(variant) + path);

        Assert.Equal(301, response.StatusCode);
        Assert.Equal(BaseOf(variant) + location, response.Header("Location"));
    }

    // The home page is the base itself: with a base, that is /some-base/ or, when trailing slashes are never used, /some-base.
    [Theory(DisplayName = "Can render prerendered base route")]
    [InlineData(Variant.AlwaysWithBase, "/")]
    [InlineData(Variant.AlwaysWithoutBase, "/")]
    [InlineData(Variant.NeverWithBase, "")]
    [InlineData(Variant.NeverWithoutBase, "/")]
    [InlineData(Variant.IgnoreWithBase, "/")]
    [InlineData(Variant.IgnoreWithoutBase, "/")]
    public Task BaseRoute_IsRendered(Variant variant, string path) => AssertPageAsync(variant, path, "Index");

    [Theory(DisplayName = "Can render prerendered route with redirect")]
    [InlineData(Variant.AlwaysWithBase, "/one", "/one/")]
    [InlineData(Variant.AlwaysWithoutBase, "/one", "/one/")]
    [InlineData(Variant.NeverWithBase, "/one/", "/one")]
    [InlineData(Variant.NeverWithoutBase, "/one/", "/one")]
    public Task Route_WithTheOtherTrailingSlash_IsRedirected(Variant variant, string path, string location) =>
        AssertRedirectAsync(variant, path, location);

    [Theory(DisplayName = "Can render prerendered route with redirect and query params")]
    [InlineData(Variant.AlwaysWithBase, "/one?foo=bar", "/one/?foo=bar")]
    [InlineData(Variant.AlwaysWithoutBase, "/one?foo=bar", "/one/?foo=bar")]
    [InlineData(Variant.NeverWithBase, "/one/?foo=bar", "/one?foo=bar")]
    [InlineData(Variant.NeverWithoutBase, "/one/?foo=bar", "/one?foo=bar")]
    public Task Route_WithTheOtherTrailingSlash_IsRedirected_AndKeepsTheQuery(Variant variant, string path, string location) =>
        AssertRedirectAsync(variant, path, location);

    [Theory(DisplayName = "Preserves query string containing a second ? in redirect")]
    [InlineData(Variant.AlwaysWithBase, "/one?redirect=/x?y=1&z=2", "/one/?redirect=/x?y=1&z=2")]
    [InlineData(Variant.AlwaysWithoutBase, "/one?redirect=/x?y=1&z=2", "/one/?redirect=/x?y=1&z=2")]
    [InlineData(Variant.NeverWithBase, "/one/?redirect=/x?y=1&z=2", "/one?redirect=/x?y=1&z=2")]
    [InlineData(Variant.NeverWithoutBase, "/one/?redirect=/x?y=1&z=2", "/one?redirect=/x?y=1&z=2")]
    public Task Redirect_KeepsAQueryWithASecondQuestionMark(Variant variant, string path, string location) =>
        AssertRawRedirectAsync(variant, path, location);

    [Theory(DisplayName = "Can render prerendered route with query params")]
    [InlineData(Variant.AlwaysWithBase, "/one/?foo=bar")]
    [InlineData(Variant.AlwaysWithoutBase, "/one/?foo=bar")]
    [InlineData(Variant.NeverWithBase, "/one?foo=bar")]
    [InlineData(Variant.NeverWithoutBase, "/one?foo=bar")]
    public Task Route_WithTheRightTrailingSlash_IsRenderedWithAQuery(Variant variant, string path) =>
        AssertPageAsync(variant, path, "One");

    [Theory(DisplayName = "Does not add trailing slash to subresource urls")]
    [InlineData(Variant.AlwaysWithBase)]
    [InlineData(Variant.AlwaysWithoutBase)]
    public async Task SubresourceUrls_DoNotGetATrailingSlash(Variant variant)
    {
        var site = await SiteAsync(variant);

        using var response = await site.GetAsync(BaseOf(variant) + "/one.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The fixture is checked out with the line endings of the platform.
        Assert.Equal("h1 { color: red; }\n", (await response.ReadTextAsync()).ReplaceLineEndings("\n"));
    }

    [Theory(DisplayName = "Does not redirect requests for static assets with unusual filenames")]
    [InlineData(Variant.AlwaysWithBase)]
    [InlineData(Variant.AlwaysWithoutBase)]
    public async Task StaticAssets_WithUnusualFilenames_AreNotRedirected(Variant variant)
    {
        var site = await SiteAsync(variant);

        using var response = await site.GetAsync(BaseOf(variant) + "/_astro/bitgeneva12.NY2V_gnX.woff2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory(DisplayName = "Should return 404 when trying to serve a page with an internal path added to the URL")]
    [InlineData("//astro.build/press")]
    [InlineData("/foo//astro.build/press")]
    [InlineData("//example.com/es//astro.build")]
    [InlineData("//example.com/es//astro.build/press")]
    public async Task InternalPathsAddedToTheUrl_AreNotFound(string target)
    {
        var site = await SiteAsync(Variant.AlwaysWithoutAutomaticOutput);

        Assert.Equal(404, await FollowRedirectsAsync(site, target));
    }

    // fetch() follows redirects, so the upstream test sees where /foo//astro.build/press/ ends up. A redirect that leaves the
    // origin, such as one to //astro.build/press/, is the open redirect this test is about.
    private static async Task<int> FollowRedirectsAsync(ConformanceSite site, string target)
    {
        for (var hops = 0; hops < 10; hops++)
        {
            var response = await site.SendRawAsync(target);
            if (response.StatusCode is not (301 or 302 or 303 or 307 or 308))
            {
                return response.StatusCode;
            }

            var next = new Uri(new Uri(site.BaseAddress, target), response.Header("Location"));
            Assert.Equal(site.BaseAddress.Authority, next.Authority);
            target = next.PathAndQuery;
        }

        throw new InvalidOperationException($"Too many redirects from {target}.");
    }

    [Fact(DisplayName = "Does not append a trailing slash to a backslash-prefixed path")]
    public async Task BackslashPrefixedPaths_AreNotRedirected()
    {
        var site = await SiteAsync(Variant.AlwaysWithoutAutomaticOutput);

        // Browsers read /\example.com/press as //example.com/press, so a redirect that repeats it would be an open redirect.
        var response = await site.SendRawAsync("/\\example.com/press");

        Assert.Equal(404, response.StatusCode);
        Assert.Null(response.Header("Location"));
    }

    [Fact(DisplayName = "Can render prerendered non-ASCII route without trailing slash")]
    public async Task NonAsciiRoute_WithoutATrailingSlash_IsRendered()
    {
        await AssertPageAsync(Variant.NeverWithoutBase, "/" + Uri.EscapeDataString(NonAscii), NonAscii);
    }

    [Fact(DisplayName = "Redirects prerendered non-ASCII route with trailing slash")]
    public async Task NonAsciiRoute_WithATrailingSlash_IsRedirected()
    {
        var encoded = "/" + Uri.EscapeDataString(NonAscii);

        await AssertRedirectAsync(Variant.NeverWithoutBase, encoded + "/", encoded);
    }

    [Theory(DisplayName = "Can render prerendered route with slash")]
    [InlineData(Variant.IgnoreWithBase)]
    [InlineData(Variant.IgnoreWithoutBase)]
    public Task Route_WithASlash_IsRendered(Variant variant) => AssertPageAsync(variant, "/one/", "One");

    [Theory(DisplayName = "Can render prerendered route without slash")]
    [InlineData(Variant.IgnoreWithBase)]
    [InlineData(Variant.IgnoreWithoutBase)]
    public Task Route_WithoutASlash_IsRendered(Variant variant) => AssertPageAsync(variant, "/one", "One");

    [Theory(DisplayName = "Can render prerendered route with slash and query params")]
    [InlineData(Variant.IgnoreWithBase)]
    [InlineData(Variant.IgnoreWithoutBase)]
    public Task Route_WithASlash_IsRenderedWithAQuery(Variant variant) => AssertPageAsync(variant, "/one/?foo=bar", "One");

    [Theory(DisplayName = "Can render prerendered route without slash and with query params")]
    [InlineData(Variant.IgnoreWithBase)]
    [InlineData(Variant.IgnoreWithoutBase)]
    public Task Route_WithoutASlash_IsRenderedWithAQuery(Variant variant) => AssertPageAsync(variant, "/one?foo=bar", "One");
}
