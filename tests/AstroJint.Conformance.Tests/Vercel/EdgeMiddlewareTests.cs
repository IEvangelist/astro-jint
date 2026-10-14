using System.Net;
using System.Text;
using System.Text.Json;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/edge-middleware.test.ts</c>. Upstream builds a site with
/// <c>middlewareMode: 'edge'</c> and checks that the middleware becomes an edge function (its <c>.vc-config.json</c>, the
/// <c>_middleware</c> destination of <c>config.json</c>), that calling that function returns the cookie the middleware set
/// together with a body, and that it forwards the HTTP method and the body of the request to the function behind it. Two
/// more tests, skipped upstream, look for the middleware in the files of the render function.
/// <para>
/// Differences from upstream: Astro's middleware runs inside the app, so there is no edge function to build or call; the
/// tests ask for what the edge function is there to deliver. The middleware of <c>vercel-server</c> is the one of the
/// upstream fixture: it sets the cookie <c>foo=bar</c> before <c>next()</c>, and has a top-level await whose result it puts
/// in the header <c>x-message</c> afterwards (upstream only loads the module, so the await is what its test exercises). The
/// cookie test requests a page, where upstream calls the function for <c>/foo</c>, and the forwarding test posts to an
/// endpoint of the site, which echoes what it received, instead of replacing <c>fetch</c>.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/edge-middleware.test.ts")]
public class EdgeMiddlewareTests(SiteHost sites)
{
    [Fact(DisplayName = "edge sets Set-Cookie headers")]
    public async Task TheCookieOfTheMiddlewareReachesTheResponse()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/one");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("foo=bar", Assert.Single(response.HeaderValues("Set-Cookie")));
        Assert.Contains("<h1>One</h1>", await response.ReadTextAsync());
        Assert.Equal("hello world", Assert.Single(response.HeaderValues("x-message")));
    }

    [Fact(DisplayName = "edge middleware forwards HTTP method and body")]
    public async Task TheMethodAndTheBodyReachTheEndpoint()
    {
        var site = await sites.ServerAsync();

        using var response = await site.SendAsync(HttpMethod.Post, "/api/data", new StringContent("""{"data":"test"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ran", Assert.Single(response.HeaderValues("x-astro-middleware")));
        using var received = JsonDocument.Parse(await response.ReadTextAsync());
        Assert.Equal("POST", received.RootElement.GetProperty("method").GetString());
        Assert.Equal("""{"data":"test"}""", received.RootElement.GetProperty("body").GetString());
    }
}
