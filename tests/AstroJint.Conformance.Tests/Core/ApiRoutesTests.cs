using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/api-routes.test.ts</c> on the <c>api-routes</c> fixture; its <c>binary.dat.ts</c> is part of
/// <c>core-endpoints</c> (see <see cref="EndpointsFixture"/>). Upstream builds the fixture with no adapter and reads <c>/binary.dat</c>
/// from the output. The build here is the one of the fixture with the route prerendered (<see cref="EndpointsFixture.Prerendered"/>), the
/// file is in <c>dist/client</c>, and the test reads it from there, so it is ported; it requests the file from the host too (the
/// host serves a prerendered file as it is).
/// <para>Not upstream: the on-demand build of the fixture answers the same bytes, and <c>/all-bytes.bin</c>, an endpoint of the fixture
/// with every byte value, comes through the engine and the host unchanged (a text encoding of the body anywhere on the way would show).</para>
/// </summary>
[Trait("Upstream", "core/api-routes.test.ts")]
public class ApiRoutesTests(SiteHost sites)
{
    [Fact(DisplayName = "API routes: Binary data: can be returned from a response")]
    public async Task BinaryData_PrerenderedEndpoint_IsWrittenAsTheBytesOfTheResponse()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name, EndpointsFixture.Prerendered);

        var dat = BuildOutput.ReadClientBytes(site, "binary.dat");

        Assert.Equal([0xFF], dat);
        // Not asserted upstream: the host serves the file as it is.
        using var response = await site.GetAsync("/binary.dat");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([0xFF], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact(DisplayName = "Not upstream: API routes: Binary data: an on-demand endpoint returns the bytes of the response")]
    public async Task BinaryData_OnDemandEndpoint_IsTheBytesOfTheResponse()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        // The route is on demand in this build: it has the engine, not a file, to answer it.
        Assert.False(site.PathExists("client/binary.dat"));
        using var response = await site.GetAsync("/binary.dat");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([0xFF], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact(DisplayName = "Not upstream: API routes: Binary data: every byte value comes through an on-demand endpoint unchanged")]
    public async Task BinaryData_OnDemandEndpointWithEveryByteValue_ComesThroughUnchanged()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        using var response = await site.GetAsync("/all-bytes.bin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Enumerable.Range(0, 256).Select(value => (byte)value).ToArray(), await response.Content.ReadAsByteArrayAsync());
    }
}
