using System.Net;
using Microsoft.AspNetCore.Builder;
using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/client-address.test.ts</c>: <c>Astro.clientAddress</c> is what the caller of
/// <c>App.render()</c> gives, and a page that reads it answers 500 when the caller gave none.
/// <para>
/// Differences from upstream: the caller is the ASP.NET Core host, whose client address is <c>Connection.RemoteIpAddress</c>, so
/// the tests set it from a middleware of the test host (<c>configureApp</c>, one site per address) where upstream passed
/// <c>{ clientAddress }</c> to <c>render()</c>. 'Returns 500 when clientAddress is not provided' sets it to <c>null</c>, which the
/// host maps to "no address"; the page <c>client-address.astro</c> of the <c>app-render</c> fixture then fails with
/// <c>ClientAddressNotAvailable</c> and the fixture's 500 page, which fails for any error but its own, leaves the bare 500.
/// 'Prerendered routes are not served at runtime (404)' is not ported: a prerendered page that reads
/// <c>Astro.clientAddress</c> fails the build (the comment of the upstream test says so), and the other half of the test, that
/// <c>App.render()</c> does not serve a prerendered route, has no counterpart over HTTP: the host serves the files of the build
/// before it asks the app, so the app is never asked for a prerendered route.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/client-address.test.ts")]
public class ClientAddressTests(SiteHost sites)
{
    private Task<ConformanceSite> GetSiteAsync(IPAddress? address) =>
        sites.GetAsync(Fixture, variant: $"address-{address?.ToString() ?? "none"}", configureApp: app =>
            app.Use(async (context, next) =>
            {
                context.Connection.RemoteIpAddress = address;
                await next();
            }));

    [Fact(DisplayName = "Can get the address (default)")]
    public async Task ClientAddress_IsTheAddressOfTheConnection()
    {
        var site = await GetSiteAsync(IPAddress.Parse("0.0.0.0"));

        var response = await site.SendRawAsync("/client-address");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("0.0.0.0", AppRouting.Text(response, "#address"));
    }

    [Fact(DisplayName = "app.render can provide the address")]
    public async Task ClientAddress_FollowsTheAddressThatTheHostProvides()
    {
        var site = await GetSiteAsync(IPAddress.Parse("1.1.1.1"));

        var response = await site.SendRawAsync("/client-address");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("1.1.1.1", AppRouting.Text(response, "#address"));
    }

    [Fact(DisplayName = "Returns 500 when clientAddress is not provided")]
    public async Task ClientAddress_WithoutAnAddress_Answers500()
    {
        var site = await GetSiteAsync(null);

        var response = await site.SendRawAsync("/client-address");

        Assert.Equal(500, response.StatusCode);
    }
}
