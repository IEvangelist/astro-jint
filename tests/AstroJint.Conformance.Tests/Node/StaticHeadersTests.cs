using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/static-headers.test.ts</c>. The fixture turns <c>security.csp</c> and the
/// adapter's <c>staticHeaders</c> on, and its prerendered index has a server island, which is why the policy has a <c>script-src</c>.
/// The upstream names are kept, typos included.
/// <para>
/// Differences from upstream: the Node adapter writes the headers into <c>dist/_headers.json</c> keyed by URL path, and this
/// adapter writes them into <c>astro-jint.json</c> keyed by file, which is what a host that serves the files needs. The
/// "listener cleanup" test looks for a Node <c>MaxListenersExceededWarning</c>, which has no counterpart here; the test below
/// keeps what it can show, that many requests over keep-alive connections keep carrying the headers.
/// </para>
/// </summary>
[Trait("Upstream", "node/static-headers.test.ts")]
public class StaticHeadersTests(SiteHost sites)
{
    private const string Fixture = "static-headers";

    private static readonly object WithBase = new { @base = "/docs" };

    private static async Task<string?> ContentSecurityPolicyOfAsync(ConformanceSite site, string file)
    {
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(site.DistPath, "astro-jint.json")));
        var entry = manifest.RootElement.GetProperty("staticHeaders").EnumerateArray()
            .SingleOrDefault(entry => entry.GetProperty("file").GetString() == file);

        return entry.ValueKind == JsonValueKind.Undefined
            ? null
            : entry.GetProperty("headers").GetProperty("Content-Security-Policy").GetString();
    }

    [Fact(DisplayName = "CSP headers are added when CSP is enabled")]
    public async Task BuildRecordsTheCspOfTheIndex()
    {
        var site = await sites.GetAsync(Fixture);

        var csp = await ContentSecurityPolicyOfAsync(site, "index.html");

        Assert.NotNull(csp);
        Assert.Contains("script-src", csp);
    }

    [Fact(DisplayName = "CSP headers are added to the request")]
    public async Task CspIsSentWithTheIndex()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("script-src", Assert.Single(response.HeaderValues("Content-Security-Policy")));
    }

    [Fact(DisplayName = "CSP headers are added to dynamic orute")]
    public async Task CspIsSentWithAPageOfADynamicRoute()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/one");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("script-src", Assert.Single(response.HeaderValues("Content-Security-Policy")));
    }

    [Fact(DisplayName = "survives a request with a malformed port in the Host header")]
    public async Task MalformedHostPort_DoesNotTakeTheHostDown()
    {
        var site = await sites.GetAsync(Fixture);

        // HttpClient rewrites the Host header, so a socket is the only way to send a host with a port that is out of range.
        var malformed = await site.SendRawAsync("/", "GET", null, ("Host", "example.com:65536"));
        using var response = await site.GetAsync("/");

        Assert.True(malformed.StatusCode is 200 or 400, $"The malformed Host got {malformed.StatusCode}.");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "does not leak socket close listeners on keep-alive connections")]
    public async Task ManyKeepAliveRequests_KeepCarryingTheHeaders()
    {
        var site = await sites.GetAsync(Fixture);

        for (var request = 0; request < 30; request++)
        {
            using var response = await site.GetAsync("/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("script-src", Assert.Single(response.HeaderValues("Content-Security-Policy")));
        }

        Assert.DoesNotContain(site.Logs, entry => entry.Level >= Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact(DisplayName = "CSP headers are added to the index route under the base path")]
    public async Task CspIsSentWithTheIndexBehindABase()
    {
        var site = await sites.GetAsync(Fixture, WithBase);

        using var response = await site.GetAsync("/docs/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("script-src", Assert.Single(response.HeaderValues("Content-Security-Policy")));
    }

    [Fact(DisplayName = "CSP headers are added to a dynamic route under the base path")]
    public async Task CspIsSentWithAPageOfADynamicRouteBehindABase()
    {
        var site = await sites.GetAsync(Fixture, WithBase);

        using var response = await site.GetAsync("/docs/one");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("script-src", Assert.Single(response.HeaderValues("Content-Security-Policy")));
    }
}
