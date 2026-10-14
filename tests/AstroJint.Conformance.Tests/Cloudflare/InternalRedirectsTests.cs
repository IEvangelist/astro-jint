using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/internal-redirects.test.ts</c>. The site is the shared fixture
/// <c>cf-pages</c>, built with <c>output: 'static'</c> like the upstream fixture; its redirect <c>/redirect-internal</c>
/// goes to the page <c>/page2</c>.
/// <para>
/// Difference: upstream reads the rule from the <c>_redirects</c> file that the adapter writes for Cloudflare. A host has
/// no such file and answers the redirect when it is asked for, so the test asks for the 301 and its <c>Location</c>.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/internal-redirects.test.ts")]
public class InternalRedirectsTests(SiteHost sites)
{
    [Fact(DisplayName = "should not create a prerendered file for internal redirects")]
    public async Task InternalRedirect_IsNotPrerendered()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.Static);

        using var response = await site.GetAsync("/redirect-internal");
        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/page2", response.Headers.Location?.ToString());

        Assert.False(
            site.PathExists("client/redirect-internal/index.html"),
            "Should not create prerendered file for internal redirect destination");
    }
}
