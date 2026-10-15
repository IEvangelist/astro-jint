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
/// adapter does what the Cloudflare one does: it reads the cookies with <c>app.setCookieHeaders()</c> and adds them to a copy of
/// the response, which has headers it may change.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/response.test.ts")]
public class ResponseTests(SiteHost sites)
{
    [Fact(DisplayName = "rebuilds a response with immutable headers")]
    public async Task RebuildsAResponseWithImmutableHeaders()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/immutable-redirect");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://example.com/", response.Headers.Location?.ToString());
        Assert.Equal("1", response.SetCookieValue("immutable"));
    }
}
