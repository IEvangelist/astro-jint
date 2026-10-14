using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/external-redirects.test.ts</c>. The site is the shared fixture
/// <c>cf-pages</c>, built with <c>output: 'static'</c> like the upstream fixture; its redirect <c>/redirect-external</c> goes
/// to <c>http://test.invalid/destination</c>, a host that does not resolve, so a build that fetches the destination fails.
/// <para>
/// Difference: upstream reads the rule from the <c>_redirects</c> file that the adapter writes for Cloudflare. A host has
/// no such file and answers the redirect when it is asked for, so the test asks for the 301 and its <c>Location</c>.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/external-redirects.test.ts")]
public class ExternalRedirectsTests(SiteHost sites)
{
    [Fact(DisplayName = "should not attempt to prerender external redirect destinations")]
    public async Task ExternalDestination_IsNotPrerendered()
    {
        // The build is part of the test: it throws when it fetches the external destination.
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.Static);

        using var response = await site.GetAsync("/redirect-external");
        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("http://test.invalid/destination", response.Headers.Location?.ToString());

        Assert.False(
            site.PathExists("client/redirect-external/index.html"),
            "Should not create prerendered file for external redirect destination");
    }
}
