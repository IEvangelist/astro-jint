using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/config-mode.test.ts</c> on the <c>astro-basic</c> project (<c>core-astro-basic</c>, as upstream "a random
/// fixture, doesn't matter"). Upstream builds the project three ways. <c>output: 'server'</c> with the test adapter and loads the app
/// (<c>app.render(new Request('http://example.com/'))</c> is <c>GET /</c> through the host). <c>output: 'server'</c> without an adapter, whose
/// build must throw "without an adapter": N/A, Astro raises that error before an adapter takes part and the port has no test for it. And
/// <c>output: 'static'</c> with no adapter, whose <c>index.html</c> is read from the output directory (the port builds with the adapter, which
/// serves the site, and <c>GET /</c> is the file; <c>client/index.html</c> exists). The <c>describe.skip</c> in the file has no tests.
/// </summary>
[Trait("Upstream", "core/config-mode.test.ts")]
public class ConfigModeTests(SiteHost sites)
{
    private const string Fixture = "core-astro-basic";

    [Fact(DisplayName = "AstroConfig - config.output: output: 'server': deploy config provided: Builds an SSR-able app")]
    public async Task ServerOutputWithAnAdapter_BuildsAnApp_ThatRendersHtml()
    {
        var site = await sites.GetAsync(Fixture, new { output = "server" });

        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", Assert.Single(response.HeaderValues("Content-Type")));
        Assert.True((await response.ReadTextAsync()).Length > 0);
    }

    [Fact(DisplayName = "AstroConfig - config.output: output: 'static': Output config omitted: Builds to static HTML")]
    public async Task StaticOutput_BuildsToStaticHtml()
    {
        var site = await sites.GetAsync(Fixture, new { output = "static" });

        Assert.True(site.PathExists("client/index.html"), "Could not find the file, which mean it did not build.");
        using var response = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadTextAsync()).Length > 0);
    }
}
