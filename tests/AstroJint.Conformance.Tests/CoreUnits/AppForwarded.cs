using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// What the tests of the URL that a request gets (<see cref="NodeTests"/>, <see cref="DevUrlConstructionTests"/>) share: the variants
/// of the <c>app-fetch</c> fixture that differ in <c>security.allowedDomains</c>, a site per variant, and the request that asks the
/// fetch handler of the fixture (the <c>state</c> pipeline) for the URL and the client address that <c>FetchState</c> ended up with.
/// </summary>
internal static class AppForwarded
{
    public const string FetchFixture = "app-fetch";

    public const string ForwardedForGap =
        "GAP-UNITS-6: the host always passes the address of the connection as the client address, so X-Forwarded-For is never read, " +
        "where Astro on Node reads it when the forwarded host is one of the allowed domains";

    public const string UntrustedHostGap =
        "GAP-UNITS-7: without allowed domains the host takes the Host header that ASP.NET Core resolved for the URL of the request, " +
        "where Astro on Node falls back to localhost and the port of the server";

    // The variants have the routes of FetchIndexTests.ForwardedVariant, so the one with example.com shares its build and its site.
    public static readonly object NotConfigured = new { routes = new[] { Route("/", "hello.astro") } };
    public static readonly object EmptyList = Allowing();
    public static readonly object Example = Allowing(new { hostname = "example.com" });
    public static readonly object ExampleAnySubdomain = Allowing(new { hostname = "**.example.com" });
    public static readonly object VictimOneLevel = Allowing(new { hostname = "*.victim.com" });
    public static readonly object ExampleOverHttps = Allowing(new { hostname = "example.com", protocol = "https" });
    public static readonly object ExampleOverHttp = Allowing(new { protocol = "http", hostname = "example.com" });
    public static readonly object ExamplePort3000 = Allowing(new { hostname = "example.com", port = "3000" });
    public static readonly object ExamplePort65535 = Allowing(new { hostname = "example.com", port = "65535" });
    public static readonly object ExampleAndPort8443 = Allowing(new { hostname = "example.com" }, new { hostname = "example.com", port = "8443" });
    public static readonly object MyAppOverHttps = Allowing(new { protocol = "https", hostname = "myapp.example.com" });
    public static readonly object AnySubdomainOverHttps = Allowing(new { protocol = "https", hostname = "**.example.com" });
    public static readonly object MreLocal = Allowing(new { hostname = "mre.local" });
    public static readonly object MreLocalOverHttps = Allowing(new { protocol = "https", hostname = "mre.local" });

    private static object Allowing(params object[] allowedDomains) => new
    {
        routes = new[] { Route("/", "hello.astro") },
        security = new { allowedDomains },
    };

    /// <summary>
    /// The site of a variant. <paramref name="https"/> is a connection that Kestrel terminates (<c>encrypted: true</c> of the mock
    /// socket upstream) and <paramref name="remoteAddress"/> the address of the peer (<c>remoteAddress</c> of the mock socket),
    /// which a middleware of the test host sets before the host reads it.
    /// </summary>
    public static Task<ConformanceSite> GetForwardedSiteAsync(this SiteHost sites, object config, bool https = false, string? remoteAddress = null) =>
        remoteAddress is null
            ? sites.GetAsync(FetchFixture, config, https: https)
            : sites.GetAsync(FetchFixture, config, variant: $"address-{remoteAddress}", https: https, configureApp: app =>
                app.Use(async (context, next) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
                    await next();
                }));

    /// <summary>
    /// Sends a request for <c>/</c> that the fetch handler answers with its report of the <c>FetchState</c>. The raw client has no
    /// TLS, so a site with https is asked through the <c>HttpClient</c> of the site.
    /// </summary>
    public static async Task<(int Status, string Body)> StateAsync(ConformanceSite site, params (string Name, string Value)[] headers)
    {
        (string Name, string Value)[] all = [("x-pipeline", "state"), .. headers];
        if (site.BaseAddress.Scheme != Uri.UriSchemeHttps)
        {
            var raw = await site.SendRawAsync("/", "GET", null, all);
            return (raw.StatusCode, raw.Text);
        }

        using var response = await site.SendAsync(HttpMethod.Get, "/", null, all);
        return ((int)response.StatusCode, await response.ReadTextAsync());
    }

    /// <summary><c>request.url</c> of the <c>FetchState</c> that the request got.</summary>
    public static async Task<string> UrlAsync(ConformanceSite site, params (string Name, string Value)[] headers) =>
        await ReadAsync(site, "requestUrl", headers);

    /// <summary><c>clientAddress</c> of the <c>FetchState</c> that the request got.</summary>
    public static async Task<string> ClientAddressAsync(ConformanceSite site, params (string Name, string Value)[] headers) =>
        await ReadAsync(site, "clientAddress", headers);

    /// <summary>What the host takes for the URL of a request whose <c>Host</c> it does not trust: <c>localhost</c> and the port it listens on.</summary>
    public static string LocalhostUrl(ConformanceSite site) => $"{site.BaseAddress.Scheme}://localhost:{site.BaseAddress.Port}/";

    /// <summary>The <c>Host</c> header of a client that asks for <c>localhost</c> on the port of the site.</summary>
    public static (string Name, string Value) LocalhostHost(ConformanceSite site) => ("Host", $"localhost:{site.BaseAddress.Port}");

    private static async Task<string> ReadAsync(ConformanceSite site, string property, (string Name, string Value)[] headers)
    {
        var (status, body) = await StateAsync(site, headers);

        Assert.Equal(200, status);
        return JsonDocument.Parse(body).RootElement.GetProperty(property).GetString()!;
    }
}
