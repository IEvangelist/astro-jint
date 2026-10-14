using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/static-headers.test.ts</c>. The fixture turns <c>security.csp</c> and the
/// adapter's <c>staticHeaders</c> on, and its prerendered index has a server island, which is why the policy has a
/// <c>script-src</c>. Upstream reads the route of <c>/</c> from <c>.vercel/output/config.json</c> and expects its
/// <c>content-security-policy</c> header to have the directive.
/// <para>
/// Differences from upstream: the route table has no counterpart. This adapter writes the headers of the prerendered files
/// into <c>astro-jint.json</c> (<c>staticHeaders</c>, keyed by file) and the host sends them with the file, so the test
/// reads the policy of <c>index.html</c> from the manifest and requests <c>/</c> from the host. The second test is an
/// addition: the policy is also sent with another prerendered page and with a page rendered per request.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/static-headers.test.ts")]
public class StaticHeadersTests(SiteHost sites)
{
    [Fact(DisplayName = "CSP headers are added when CSP is enabled")]
    public async Task TheCspOfTheIndex_IsRecordedAndSent()
    {
        var site = await sites.ServerAsync();

        var recorded = await ContentSecurityPolicyOfAsync(site, "index.html");
        using var response = await site.GetAsync("/");

        Assert.NotNull(recorded);
        Assert.Contains("script-src", recorded);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("script-src", Assert.Single(response.HeaderValues("Content-Security-Policy")));
    }

    [Theory(DisplayName = "CSP headers are added when CSP is enabled: other pages")]
    [InlineData("/static")]
    [InlineData("/islands")]
    public async Task TheCsp_IsSentWithOtherPages(string path)
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("script-src", Assert.Single(response.HeaderValues("Content-Security-Policy")));
    }

    private static async Task<string?> ContentSecurityPolicyOfAsync(ConformanceSite site, string file)
    {
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(site.DistPath, "astro-jint.json")));
        var entry = manifest.RootElement.GetProperty("staticHeaders").EnumerateArray()
            .SingleOrDefault(entry => entry.GetProperty("file").GetString() == file);

        return entry.ValueKind == JsonValueKind.Undefined
            ? null
            : entry.GetProperty("headers").GetProperty("Content-Security-Policy").GetString();
    }
}
