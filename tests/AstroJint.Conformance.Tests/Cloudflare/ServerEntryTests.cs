using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/server-entry.test.ts</c>. The upstream fixture builds with
/// <c>build.serverEntry</c> set to <c>custom.mjs</c>, the "legacy entrypoint" of a worker; the test builds the pages of the
/// shared fixture <c>cf-content</c> with the same setting.
/// <list type="bullet">
/// <item>"should load the custom entry when using legacy entrypoint" is adapted: the entry is <c>server/custom.mjs</c> in the
/// output, as upstream asks, and the host, which starts from the entry that its manifest (<c>astro-jint.json</c>) names,
/// serves an on-demand page.</item>
/// <item>Not applicable: "prepends the process shim banner to the server entry". The <c>globalThis.process ??= {}</c> banner
/// is the Cloudflare adapter's; the host runs the entry as it is.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/server-entry.test.ts")]
public class ServerEntryTests(SiteHost sites)
{
    private static readonly object CustomEntry = new { build = new { serverEntry = "custom.mjs" } };

    [Fact(DisplayName = "should load the custom entry when using legacy entrypoint")]
    public async Task LoadsTheCustomEntry()
    {
        var site = await sites.GetAsync(CfContent.Fixture, CustomEntry);

        Assert.True(site.PathExists("server/custom.mjs"), "the build did not write server/custom.mjs");

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(site.DistPath, "astro-jint.json")));
        Assert.Equal("server/custom.mjs", manifest.RootElement.GetProperty("entry").GetString());

        using var response = await site.GetAsync("/ssr");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>SSR</h1>", await response.ReadTextAsync());
    }
}
