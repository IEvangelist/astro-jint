using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/server-entry.test.ts</c>: <c>build.serverEntry</c> names the file of the server entry.
/// <para>
/// Upstream builds one fixture with test adapters and renders requests with the built app of the test adapter. Here the
/// adapter under test is <c>astro-adapter-jint</c>, which lets Astro pick the entry (<c>entrypointResolution: 'auto'</c>) and
/// hands it its own <c>serverEntrypoint</c>: the scenario of the "self entrypoint" test (<c>selfTestAdapter</c>) and of the
/// "<c>serverEntrypoint</c>" test (the fake adapter), so those two are ported. Rendering a request with the built app becomes a
/// request to the host, which loads the entry that the manifest of the build (<c>astro-jint.json</c>) names; the test also
/// checks that name. Not applicable (see the matrix): the legacy adapter API test (<c>testAdapter</c>, <c>exports</c> instead of
/// an entrypoint) and the three tests whose fake adapter gives Rolldown its own <c>input</c> as a string, an object and an
/// array, because the adapter under test does not set one.
/// </para>
/// </summary>
[Trait("Upstream", "core/server-entry.test.ts")]
public class ServerEntryTests(SiteHost sites)
{
    private const string Fixture = "cb-server-entry";

    private static readonly object CustomEntry = new { build = new { serverEntry = "custom.mjs" } };

    [Fact(DisplayName = "should load the custom entry when using self entrypoint")]
    public async Task LoadsTheCustomEntryWhenUsingSelfEntrypoint()
    {
        var site = await sites.GetAsync(Fixture, CustomEntry);

        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Server Entry", await response.Content.ReadAsStringAsync());
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(site.DistPath, "astro-jint.json")));
        Assert.Equal("server/custom.mjs", manifest.RootElement.GetProperty("entry").GetString());
    }

    [Fact(DisplayName = "should load the custom entry when using serverEntrypoint with entryType: self")]
    public async Task EmitsTheCustomEntryWhenUsingServerEntrypoint()
    {
        var site = await sites.GetAsync(Fixture, CustomEntry);

        Assert.True(site.PathExists("server/custom.mjs"), "the server entry should be named after build.serverEntry");
        // Stricter than upstream: the custom name replaces the default one.
        Assert.False(site.PathExists("server/entry.mjs"), "the default server entry should not be emitted next to the custom one");
    }
}
