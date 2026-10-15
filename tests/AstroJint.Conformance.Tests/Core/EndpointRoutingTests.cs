using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/endpoint-routing.test.ts</c> on the <c>endpoint-routing</c> fixture, whose sources are part of
/// <c>core-endpoints</c> (see <see cref="EndpointsFixture"/>). Upstream starts the dev server and fetches the two redirecting endpoints with
/// <c>redirect: 'manual'</c>. The port requests them from the host, with the fixture built as <c>output: 'server'</c>, so the two tests
/// are adapted: it is the endpoint runtime of production that answers, and the assertions are the upstream ones. The client of the
/// harness does not follow redirects, which is <c>redirect: 'manual'</c>.
/// </summary>
[Trait("Upstream", "core/endpoint-routing.test.ts")]
public class EndpointRoutingTests(SiteHost sites)
{
    [Fact(DisplayName = "endpoints: should return a redirect response with location header")]
    public async Task Endpoint_ResponseRedirect_IsA307WithTheLocationHeaderAndNoRerouteDirective()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        using var response = await site.GetAsync("/response-redirect");

        Assert.Equal("https://example.com/destination", Assert.Single(response.HeaderValues("location")));
        Assert.Empty(response.HeaderValues("x-astro-reroute"));
        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }

    [Fact(DisplayName = "endpoints: should return a response with location header")]
    public async Task Endpoint_ResponseWithLocationHeader_IsA307WithTheLocationHeader()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        using var response = await site.GetAsync("/response");

        Assert.Equal("https://example.com/destination", Assert.Single(response.HeaderValues("location")));
        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }
}
