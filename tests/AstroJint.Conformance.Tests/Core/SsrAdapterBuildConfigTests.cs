using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/ssr-adapter-build-config.test.ts</c> (<c>Integration buildConfig hook</c>) on the <c>ssr-request</c>
/// fixture (<c>core-ssr-request</c>). Upstream builds with an inline adapter whose <c>astro:config:setup</c> hook moves <c>build.client</c> and
/// <c>build.server</c> to <c>.root/client/</c> and <c>.root/server/</c> (<c>updateConfig</c>), and that provides its own server entry, the virtual
/// module <c>@my-ssr</c>, then looks for <c>cars.json</c> and <c>entry.mjs</c> in those directories. Both tests are ADAPTED: the inline adapter and its
/// entry are Astro's adapter API, and this adapter has an entry of its own. What this adapter has to honour is the same two options set
/// by the user, so the port sets <c>build.client</c> and <c>build.server</c> in the config (they are relative to <c>outDir</c>) and checks the
/// same two files; the adapter records the directories in <c>astro-jint.json</c>, and the port also requests the site, which only works when
/// the host finds the client files (a public file, which upstream reads from the directory) and the server entry (an on-demand page) there.
/// </summary>
[Trait("Upstream", "core/ssr-adapter-build-config.test.ts")]
public class SsrAdapterBuildConfigTests(SiteHost sites)
{
    private const string Fixture = "core-ssr-request";

    private static readonly object RelocatedBuild = new { build = new { client = "./.root/client/", server = "./.root/server/" } };

    [Fact(DisplayName = "Integration buildConfig hook: Puts client files in the client folder")]
    public async Task RelocatedClientDirectory_HoldsTheClientFiles_AndTheHostServesThem()
    {
        var site = await sites.GetAsync(Fixture, RelocatedBuild);

        Assert.True(File.Exists(Path.Combine(site.DistPath, ".root", "client", "cars.json")), "public/cars.json is not in .root/client");
        Assert.False(Directory.Exists(Path.Combine(site.DistPath, "client")), "the default client directory should not exist");
        // Not asserted upstream: the host serves the file from where the build put it (the fixture's base is /subpath/).
        using var response = await site.GetAsync("/subpath/cars.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(await response.ReadTextAsync());
    }

    [Fact(DisplayName = "Integration buildConfig hook: Puts the server entry into the server folder")]
    public async Task RelocatedServerDirectory_HoldsTheServerEntry_AndTheHostRunsIt()
    {
        var site = await sites.GetAsync(Fixture, RelocatedBuild);

        Assert.True(File.Exists(Path.Combine(site.DistPath, ".root", "server", "entry.mjs")), "entry.mjs is not in .root/server");
        Assert.False(Directory.Exists(Path.Combine(site.DistPath, "server")), "the default server directory should not exist");
        // Not asserted upstream: the host runs the entry from where the build put it.
        using var response = await site.SendAsync(HttpMethod.Get, "/subpath/request", null, ("Host", "example.com"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http://example.com", (await response.ReadHtmlAsync()).QuerySelector("#origin")?.TextContent);
    }
}
