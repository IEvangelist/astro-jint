using System.Net;
using AngleSharp.Dom;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/before-hydration.test.ts</c> on the <c>before-hydration</c> fixture (<c>core-before-hydration</c>: the
/// Preact integration and a page with one <c>client:idle</c> island; the sources are the upstream ones). The test builds the fixture four
/// ways and the port does the same with the config variants of the harness.
/// <list type="bullet">
/// <item>SSG, with the <c>@test/before-hydration</c> integration (<c>injectScript('before-hydration', ...)</c>, which the fixture config adds for
/// <c>{ conformance: { beforeHydration: true } }</c>) and without it. Upstream builds with no adapter and reads <c>/index.html</c> from the
/// output. The build here has the Jint adapter and <c>output: 'static'</c>, so the page is prerendered into <c>dist/client</c>, and the tests
/// request it over HTTP, where the host serves it as a static file: ported. The test of the chunk checks that the file which the
/// <c>before-hydration-url</c> names exists in the output, as upstream does, and requests it from the host.</item>
/// <item>SSR, with the integration and without it: <c>output: 'server'</c>, and <c>app.render(new Request('http://example.com/'))</c> is
/// <c>GET /</c>. The test with the integration requests the script of the <c>before-hydration-url</c> as well (not asserted upstream).</item>
/// </list>
/// The upstream fixture config has the Preact integration and the tests pass it again inline; here it is in the config once.
/// </summary>
[Trait("Upstream", "core/before-hydration.test.ts")]
public class BeforeHydrationTests(SiteHost sites)
{
    private const string Fixture = "core-before-hydration";

    private static readonly object StaticWithIntegration = new { output = "static", conformance = new { beforeHydration = true } };
    private static readonly object StaticWithoutIntegration = new { output = "static" };
    private static readonly object ServerWithIntegration = new { output = "server", conformance = new { beforeHydration = true } };
    private static readonly object ServerWithoutIntegration = new { output = "server" };

    private static async Task<IDocument> GetPageAsync(ConformanceSite site)
    {
        using var response = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadHtmlAsync();
    }

    private static async Task AssertScriptIsServedAsync(ConformanceSite site, string url)
    {
        using var script = await site.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await script.ReadTextAsync());
    }

    [Fact(DisplayName = "Astro Scripts before-hydration: SSG: Is used by an integration: Build: Is included in the astro-island")]
    public async Task Ssg_WithIntegration_IslandHasBeforeHydrationUrl()
    {
        var site = await sites.GetAsync(Fixture, StaticWithIntegration);

        var html = await GetPageAsync(site);

        Assert.Single(html.QuerySelectorAll("astro-island[before-hydration-url]"));
    }

    [Fact(DisplayName = "Astro Scripts before-hydration: SSG: Is used by an integration: Build: Emits the before-hydration chunk to the client output")]
    public async Task Ssg_WithIntegration_BeforeHydrationChunkIsInTheClientOutput()
    {
        var site = await sites.GetAsync(Fixture, StaticWithIntegration);

        var html = await GetPageAsync(site);
        var url = html.QuerySelector("astro-island")?.GetAttribute("before-hydration-url");

        Assert.False(string.IsNullOrEmpty(url), "before-hydration-url attribute should have a value");
        // The URL should point to a file that exists in the build output
        Assert.True(site.PathExists($"client{url}"), "before-hydration chunk should exist in the client build output");
        await AssertScriptIsServedAsync(site, url!);
    }

    [Fact(DisplayName = "Astro Scripts before-hydration: SSG: Is not used by an integration: Build: Does not include before-hydration-url on the astro-island")]
    public async Task Ssg_WithoutIntegration_IslandHasNoBeforeHydrationUrl()
    {
        var site = await sites.GetAsync(Fixture, StaticWithoutIntegration);

        var html = await GetPageAsync(site);

        Assert.Empty(html.QuerySelectorAll("astro-island[before-hydration-url]"));
        // Not asserted upstream: the page does have its island, so the count above is not that of a page that rendered nothing.
        Assert.Single(html.QuerySelectorAll("astro-island"));
    }

    [Fact(DisplayName = "Astro Scripts before-hydration: SSR: Is used by an integration: Prod: Is included in the astro-island")]
    public async Task Ssr_WithIntegration_IslandHasBeforeHydrationUrl()
    {
        var site = await sites.GetAsync(Fixture, ServerWithIntegration);

        var html = await GetPageAsync(site);

        var island = Assert.Single(html.QuerySelectorAll("astro-island[before-hydration-url]"));
        // Not asserted upstream: the host serves the script that the attribute names.
        await AssertScriptIsServedAsync(site, island.GetAttribute("before-hydration-url")!);
    }

    [Fact(DisplayName = "Astro Scripts before-hydration: SSR: Is not used by an integration: Build: Does not include before-hydration-url on the astro-island")]
    public async Task Ssr_WithoutIntegration_IslandHasNoBeforeHydrationUrl()
    {
        var site = await sites.GetAsync(Fixture, ServerWithoutIntegration);

        var html = await GetPageAsync(site);

        Assert.Empty(html.QuerySelectorAll("astro-island[before-hydration-url]"));
        // Not asserted upstream: the page does have its island, so the count above is not that of a page that rendered nothing.
        Assert.Single(html.QuerySelectorAll("astro-island"));
    }
}
