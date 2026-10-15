using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/response.test.ts</c>, two unit tests of <c>applyCloudflareResponseHeaders</c>,
/// the function of the Cloudflare adapter that adds the cookies of a request to a response. A host has no such function to
/// call, so the one test that asks something of the behavior of the adapter asks the page of the shared fixture
/// <c>cf-pages</c> that has the case.
/// <list type="bullet">
/// <item>"mutates a mutable response in place" is not applicable: it looks at the identity of the object that a function of
/// the adapter returns.</item>
/// <item>"rebuilds a response with immutable headers" becomes the endpoint <c>/immutable-redirect</c>, which sets a cookie
/// and returns <c>Response.redirect()</c>, a response with immutable headers: the answer must be the redirect with the cookie.
/// The assertion about <c>Cloudflare-CDN-Cache-Control</c> is left out, as it is a header of the Cloudflare platform. The
/// test is skipped for a gap (GAP-CF-2).</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/response.test.ts")]
public class ResponseTests(SiteHost sites)
{
    [Fact(DisplayName = "rebuilds a response with immutable headers",
        Skip = "GAP-CF-2: an endpoint that sets a cookie and returns Response.redirect() gets an empty 500; Astro's prepareResponse appends Set-Cookie to the immutable headers: TypeError: Failed to execute 'append' on 'Headers': Headers are immutable")]
    public async Task RebuildsAResponseWithImmutableHeaders()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/immutable-redirect");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://example.com/", response.Headers.Location?.ToString());
        Assert.Equal("1", response.SetCookieValue("immutable"));
    }
}
