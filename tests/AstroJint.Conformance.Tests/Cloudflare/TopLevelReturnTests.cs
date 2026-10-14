using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/top-level-return.test.ts</c>: a page with a top-level <c>return</c> in its
/// frontmatter, a bare <c>return</c> and a regular expression with a quote in it builds and renders.
/// <para>
/// The upstream tests read the log of the Vite dependency scan of the dev server, which the build of this adapter does not
/// run, for two messages: that the scan chokes on the top-level return, and that the scan fails at all. What the messages stand for is
/// that the page does not break the toolchain, so the tests check that the build of the page succeeded and that the host renders
/// it; the build of the fixture fails when the page cannot be compiled. Both tests ask for the same page, as the upstream tests
/// share one build.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/top-level-return.test.ts")]
public class TopLevelReturnTests(SiteHost sites)
{
    private const string Fixture = "cf-pages";

    [Fact(DisplayName = "Top-level Return: should support top-level return statements during dependency scanning")]
    public async Task TopLevelReturn_IsSupported()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/top-level-return");

        // guard() is false, so the page does not take its redirect.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("<h1>Top-level Return Test</h1>", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "Top-level Return: should not break JS syntax and should complete dependency scanning successfully")]
    public async Task RegexWithQuote_DoesNotBreakTheBuild()
    {
        var site = await sites.GetAsync(Fixture);

        // The build of the fixture has run to get here, with the regex literal /"/g in the frontmatter of the page.
        Assert.True(site.PathExists("astro-jint.json"), "The build wrote no manifest.");
        using var response = await site.GetAsync("/top-level-return");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>Top-level Return Test</h1>", await response.ReadTextAsync());
    }
}
