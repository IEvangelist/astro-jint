using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/client-address.test.ts</c>: <c>Astro.clientAddress</c> and the
/// <c>clientAddress</c> of an endpoint come from the <c>cf-connecting-ip</c> header, as the first of its values, trimmed, and
/// only when it is an IP address.
/// <para>
/// The differences are all one. The Cloudflare adapter reads a header of the platform itself; the host never does, because
/// the only address it knows is the one of the connection, and a header that every client can send is not to be believed
/// by default. A site behind Cloudflare tells ASP.NET Core to believe the header, with the stock forwarded-headers
/// middleware, and these tests set it up the same way (the header of Cloudflare as the forwarded-for header, any proxy
/// trusted, every entry processed so that the first one is the one that stays). The tests then ask the same questions of
/// the page <c>/client-address</c> and the endpoint <c>/api/address</c> of the shared fixture <c>cf-pages</c>, which
/// are not at <c>/</c> as upstream. An address is then what the middleware makes of the header, and so the three tests that
/// expect a status 500 for a header with a payload in it expect that the payload does not get through to the page: the
/// middleware does not take what is not an IP address, the host answers 200 with the address of the connection, and the
/// payload is nowhere in the response. The last test, marked (host), is not upstream: it proves the other side of that
/// decision, that a site which has not set the middleware up gets the address of the connection whatever the header says.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/client-address.test.ts")]
public class ClientAddressTests(SiteHost sites)
{
    private const string Fixture = "cf-pages";
    private const string Loopback = "127.0.0.1";

    private Task<ConformanceSite> StartBehindCloudflareAsync() => CfPages.BehindCloudflareAsync(sites);

    private static async Task<string?> ClientAddressOfEndpointAsync(ConformanceSite site, string header)
    {
        using var response = await site.SendAsync(HttpMethod.Get, "/api/address", null, ("cf-connecting-ip", header));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.ReadTextAsync());
        return document.RootElement.GetProperty("clientAddress").GetString();
    }

    private static async Task<string> ClientAddressOfPageAsync(ConformanceSite site, string header)
    {
        using var response = await site.SendAsync(HttpMethod.Get, "/client-address", null, ("cf-connecting-ip", header));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadHtmlAsync()).QuerySelector("#address")!.TextContent;
    }

    [Fact(DisplayName = "returns the client IP from cf-connecting-ip header")]
    public async Task ReturnsTheClientIp()
    {
        var site = await StartBehindCloudflareAsync();

        Assert.Equal("203.0.113.50", await ClientAddressOfEndpointAsync(site, "203.0.113.50"));
    }

    [Fact(DisplayName = "returns only the first IP when cf-connecting-ip contains multiple values")]
    public async Task ReturnsOnlyTheFirstIp()
    {
        var site = await StartBehindCloudflareAsync();

        Assert.Equal("203.0.113.50", await ClientAddressOfEndpointAsync(site, "203.0.113.50, 70.41.3.18, 150.172.238.178"));
    }

    [Fact(DisplayName = "trims whitespace around the IP address")]
    public async Task TrimsWhitespace()
    {
        var site = await StartBehindCloudflareAsync();

        Assert.Equal("203.0.113.50", await ClientAddressOfEndpointAsync(site, "  203.0.113.50  "));
    }

    [Fact(DisplayName = "handles IPv6 addresses")]
    public async Task HandlesIpv6()
    {
        var site = await StartBehindCloudflareAsync();

        Assert.Equal("2001:db8::1", await ClientAddressOfEndpointAsync(site, "2001:db8::1"));
    }

    [Fact(DisplayName = "renders the client address in an Astro page")]
    public async Task RendersTheAddressInAPage()
    {
        var site = await StartBehindCloudflareAsync();

        Assert.Equal("198.51.100.42", await ClientAddressOfPageAsync(site, "198.51.100.42"));
    }

    [Fact(DisplayName = "renders only the first IP in an Astro page when header has multiple values")]
    public async Task RendersOnlyTheFirstIpInAPage()
    {
        var site = await StartBehindCloudflareAsync();

        Assert.Equal("198.51.100.42", await ClientAddressOfPageAsync(site, "198.51.100.42, 10.0.0.1"));
    }

    [Fact(DisplayName = "rejects HTML injection in cf-connecting-ip")]
    public async Task RejectsHtmlInjection() => await AssertPayloadIsRejectedAsync("<script>alert(1)</script>");

    [Fact(DisplayName = "rejects SQL injection in cf-connecting-ip")]
    public async Task RejectsSqlInjection() => await AssertPayloadIsRejectedAsync("'; DROP TABLE users; --");

    [Fact(DisplayName = "rejects path traversal in cf-connecting-ip")]
    public async Task RejectsPathTraversal() => await AssertPayloadIsRejectedAsync("../../etc/passwd");

    private async Task AssertPayloadIsRejectedAsync(string payload)
    {
        var site = await StartBehindCloudflareAsync();

        using var endpoint = await site.SendAsync(HttpMethod.Get, "/api/address", null, ("cf-connecting-ip", payload));
        using var page = await site.SendAsync(HttpMethod.Get, "/client-address", null, ("cf-connecting-ip", payload));

        Assert.Equal(HttpStatusCode.OK, endpoint.StatusCode);
        var body = await endpoint.ReadTextAsync();
        Assert.DoesNotContain(payload, body);
        Assert.DoesNotContain("script", body);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(Loopback, document.RootElement.GetProperty("clientAddress").GetString());

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.ReadTextAsync();
        Assert.DoesNotContain(payload, html);
        Assert.DoesNotContain("DROP TABLE", html);
        Assert.Equal(Loopback, ConformanceSite.ParseHtml(html).QuerySelector("#address")!.TextContent);
    }

    [Fact(DisplayName = "ignores cf-connecting-ip when the application does not trust it (host)")]
    public async Task IgnoresTheHeaderByDefault()
    {
        var site = await sites.GetAsync(Fixture);

        Assert.Equal(Loopback, await ClientAddressOfEndpointAsync(site, "203.0.113.50"));
        Assert.Equal(Loopback, await ClientAddressOfPageAsync(site, "198.51.100.42"));
    }
}
