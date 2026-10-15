using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/client-address-node.test.ts</c>. The fixture is <c>client-address-node</c> of Astro
/// (<c>core-client-address</c>): an on-demand page that prints <c>Astro.clientAddress</c>, with <c>security.allowedDomains</c> set to
/// <c>localhost</c>. Upstream calls the request handler of the Node adapter with an <c>x-forwarded-for</c> header; the adapter trusts the
/// first value of that header because the <c>Host</c> of the request is one of the allowed domains, and hands it to Astro as the client
/// address. This host takes the address of the connection and does not read <c>x-forwarded-for</c> itself: the application that hosts it
/// decides which proxies it trusts, with the forwarded headers middleware of ASP.NET Core, which replaces the address of the connection.
/// So both tests are adapted: they send the same request (with the <c>Host</c> of an allowed domain) to a site whose
/// <c>configureApp</c> adds that middleware, trusting every proxy and the whole chain, which makes the first value of the header win as it
/// does in Node. The third test is not in upstream: with no such middleware (the default), the header cannot change the address.
/// </summary>
[Trait("Upstream", "core/client-address-node.test.ts")]
public class ClientAddressNodeTests(SiteHost sites)
{
    private const string Fixture = "core-client-address";

    [Fact(DisplayName = "NodeClientAddress: single value: clientAddress is 1.1.1.1")]
    public async Task SingleValue_IsTheClientAddress()
    {
        var site = await GetForwardedSiteAsync();

        using var response = await RequestAsync(site, "1.1.1.1");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1.1.1.1", html.QuerySelector("#address")!.TextContent);
    }

    [Fact(DisplayName = "NodeClientAddress: multiple values: clientAddress is 1.1.1.1")]
    public async Task MultipleValues_TheFirstIsTheClientAddress()
    {
        var site = await GetForwardedSiteAsync();

        using var response = await RequestAsync(site, "1.1.1.1,8.8.8.8, 8.8.8.2");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1.1.1.1", html.QuerySelector("#address")!.TextContent);
    }

    [Fact(DisplayName = "not in upstream: an x-forwarded-for header does not change the client address unless the application trusts it")]
    public async Task ForwardedHeader_IsIgnoredWithoutTheForwardedHeadersMiddleware()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await RequestAsync(site, "1.1.1.1");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("127.0.0.1", html.QuerySelector("#address")!.TextContent);
    }

    private Task<ConformanceSite> GetForwardedSiteAsync() =>
        sites.GetAsync(Fixture, variant: "forwarded-for", configureApp: UseForwardedFor);

    private static void UseForwardedFor(WebApplication app)
    {
        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor, ForwardLimit = null };
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        app.UseForwardedHeaders(options);
    }

    private static Task<HttpResponseMessage> RequestAsync(ConformanceSite site, string forwardedFor) =>
        site.SendAsync(HttpMethod.Get, "/", null, ("Host", $"localhost:{site.BaseAddress.Port}"), ("x-forwarded-for", forwardedFor));
}
