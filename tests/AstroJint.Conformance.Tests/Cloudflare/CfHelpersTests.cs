using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/cf-helpers.test.ts</c>. The upstream file calls the helpers of the adapter
/// (<c>dist/utils/cf-helpers.js</c>) with mock objects. Most of them have nothing to do here; the two that do are asked of the
/// host through the pages of the shared fixture <c>cf-pages</c>.
/// <list type="bullet">
/// <item><c>getClientAddress</c>: the four tests ask the endpoint <c>/api/address</c> of a site that believes the header
/// <c>CF-Connecting-IP</c> (see <see cref="CfPages.BehindCloudflareAsync"/>) for <c>clientAddress</c>. Where the helper returns
/// <c>undefined</c> (no header, a header that is not an IP address) the host has the address of the connection to fall back
/// on and answers that, <c>127.0.0.1</c>; it is never undefined, and a payload in the header is not in the response.</item>
/// <item>Not applicable: <c>createLocals</c> (the <c>cfContext</c> of Cloudflare and its <c>runtime</c> migration errors),
/// <c>matchStaticAsset</c> and <c>fallbackToAssets</c> (the ASSETS binding; the host serves the static files itself, which the
/// tests of the static output cover) and the test of <c>createErrorPageFetch</c> that strips <c>.html</c> from the URL.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/cf-helpers.test.ts")]
public class CfHelpersTests(SiteHost sites)
{
    private const string Loopback = "127.0.0.1";

    private static async Task<string?> ClientAddressAsync(ConformanceSite site, string? header)
    {
        using var response = header is null
            ? await site.GetAsync("/api/address")
            : await site.SendAsync(HttpMethod.Get, "/api/address", null, ("cf-connecting-ip", header));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadTextAsync();
        Assert.DoesNotContain("script", body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("clientAddress").GetString();
    }

    [Fact(DisplayName = "returns the IP from cf-connecting-ip header")]
    public async Task GetClientAddress_ReturnsTheIpFromTheHeader()
    {
        var site = await CfPages.BehindCloudflareAsync(sites);

        Assert.Equal("203.0.113.50", await ClientAddressAsync(site, "203.0.113.50"));
    }

    [Fact(DisplayName = "returns only the first IP from a multi-value header")]
    public async Task GetClientAddress_ReturnsOnlyTheFirstIp()
    {
        var site = await CfPages.BehindCloudflareAsync(sites);

        Assert.Equal("203.0.113.50", await ClientAddressAsync(site, "203.0.113.50, 70.41.3.18"));
    }

    [Fact(DisplayName = "returns undefined when the header is missing")]
    public async Task GetClientAddress_FallsBackToTheConnectionWhenTheHeaderIsMissing()
    {
        var site = await CfPages.BehindCloudflareAsync(sites);

        Assert.Equal(Loopback, await ClientAddressAsync(site, null));
    }

    [Fact(DisplayName = "returns undefined for invalid IP values")]
    public async Task GetClientAddress_FallsBackToTheConnectionForInvalidValues()
    {
        var site = await CfPages.BehindCloudflareAsync(sites);

        Assert.Equal(Loopback, await ClientAddressAsync(site, "<script>alert(1)</script>"));
    }
}
