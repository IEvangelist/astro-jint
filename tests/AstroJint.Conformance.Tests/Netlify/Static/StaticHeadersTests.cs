using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.Netlify;

/// <summary>
/// Port of <c>packages/integrations/netlify/test/static/static-headers.test.ts</c>. The fixture is the one of upstream: a
/// <c>output: 'static'</c> site that turns <c>security.csp</c> and the <c>staticHeaders</c> option of the adapter on, and whose
/// prerendered index has a server island, which is why its policy has a <c>script-src</c>.
/// <para>
/// Differences from upstream: the Netlify adapter writes the server of the islands to <c>.netlify/v1/functions/ssr/</c> and the
/// policy to <c>.netlify/v1/config.json</c>, keyed by URL. This adapter always builds an on-demand server into
/// <c>server/</c> and records the policy in <c>astro-jint.json</c>, keyed by file. The first test checks the bundle and, as
/// what a function directory is for, that the server island answers from it. The second test checks the policy the host sends
/// with the index against the one that the build recorded.
/// </para>
/// </summary>
[Trait("Upstream", "netlify/static/static-headers.test.ts")]
public partial class StaticHeadersTests(SiteHost sites)
{
    private const string Fixture = "netlify-static-headers";

    [GeneratedRegex(@"fetch\(""(?<url>/_server-islands/[^""]+)""")]
    private static partial Regex ServerIslandRequest();

    private static async Task<string?> ContentSecurityPolicyOfAsync(ConformanceSite site, string file)
    {
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(site.DistPath, "astro-jint.json")));
        var entry = manifest.RootElement.GetProperty("staticHeaders").EnumerateArray()
            .SingleOrDefault(entry => entry.GetProperty("file").GetString() == file);

        return entry.ValueKind == JsonValueKind.Undefined
            ? null
            : entry.GetProperty("headers").GetProperty("Content-Security-Policy").GetString();
    }

    [Fact(DisplayName = "SSR function is generated when server islands are used with output: static")]
    public async Task ServerIslandsOfAStaticSiteAreRenderedOnDemand()
    {
        var site = await sites.GetAsync(Fixture);
        var server = Path.Combine(site.DistPath, "server");
        Assert.True(Directory.Exists(server), "The server directory should exist.");
        Assert.NotEmpty(Directory.EnumerateFileSystemEntries(server));

        var page = await site.GetStringAsync("/");
        var url = WebUtility.HtmlDecode(ServerIslandRequest().Match(page).Groups["url"].Value);
        Assert.NotEmpty(url);
        using var response = await site.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("I am a Server Island", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "CSP headers are added when CSP is enabled")]
    public async Task TheIndexCarriesThePolicyOfTheBuild()
    {
        var site = await sites.GetAsync(Fixture);

        var recorded = await ContentSecurityPolicyOfAsync(site, "index.html");
        using var response = await site.GetAsync("/");

        Assert.NotNull(recorded);
        Assert.Contains("script-src", recorded);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(recorded, Assert.Single(response.HeaderValues("Content-Security-Policy")));
    }
}
