using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/endpoint-runtime.test.ts</c> on the <c>endpoint-routing</c> fixture, whose sources are part of
/// <c>core-endpoints</c> (see <see cref="EndpointsFixture"/>). Upstream starts the dev server, which answers every route per request, and
/// fetches <c>/incorrect</c> and <c>/headers</c>. The port requests the same routes from the host, with the fixture built as
/// <c>output: 'server'</c>, so that every route is on demand here too. The three tests are adapted: it is the endpoint runtime of
/// production that answers, and the assertions are the upstream ones. <c>fixture.fetch(path, { method: 'HEAD' })</c> is
/// <c>SendAsync(HttpMethod.Head, path)</c>.
/// <para>Not upstream: a <c>GET</c> request of <c>/headers</c>, which has the body that the <c>HEAD</c> test expects to be removed.</para>
/// </summary>
[Trait("Upstream", "core/endpoint-runtime.test.ts")]
public class EndpointRuntimeTests(SiteHost sites)
{
    [Fact(DisplayName = "endpoints: should respond with 500 for incorrect implementation")]
    public async Task Endpoint_ThatReturnsNoResponse_Is500()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        using var response = await site.GetAsync("/incorrect");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact(DisplayName = "endpoints: should respond with same code as GET response")]
    public async Task Endpoint_ThatReturnsNoResponse_Is500ForHeadToo()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        using var response = await site.SendAsync(HttpMethod.Head, "/incorrect");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact(DisplayName = "endpoints: should remove body and pass headers for HEAD requests")]
    public async Task Head_RemovesTheBodyAndKeepsTheHeaders()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        using var response = await site.SendAsync(HttpMethod.Head, "/headers");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("value", Assert.Single(response.HeaderValues("test")));
        Assert.Equal("", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "Not upstream: endpoints: a GET request has the body and the headers that HEAD passes without the body")]
    public async Task Get_HasTheBodyAndTheHeaders()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        using var response = await site.GetAsync("/headers");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("value", Assert.Single(response.HeaderValues("test")));
        Assert.Equal("content", await response.ReadTextAsync());
    }
}
