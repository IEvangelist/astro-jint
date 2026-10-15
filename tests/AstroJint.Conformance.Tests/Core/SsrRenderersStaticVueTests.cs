using System.Net;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/ssr-renderers-static-vue.test.ts</c> on the <c>ssr-renderers-static-vue</c> fixture
/// (<c>core-ssr-renderers-static-vue</c>: the Vue integration and <c>output: 'server'</c>; the sources are the upstream ones, a prerendered
/// page that renders a Vue component and an on-demand endpoint).
/// <list type="bullet">
/// <item><c>does not include SSR renderers when only endpoints are dynamic</c>: adapted. It asserts that <c>app.manifest.renderers</c> of the test
/// adapter is empty. The manifest is a part of the server bundle that the engine loads, and the renderers are in it as <c>var renderers = [...]</c>
/// in the region of the <c>virtual:astro:renderers</c> module of <c>server/entry.mjs</c>, so the port asserts that this list is empty and that no
/// file of the server build has the server renderer of Vue (<c>createSSRApp</c>). It also requests the endpoint, to see that the engine runs
/// with an empty list of renderers (not asserted upstream). The last test is the control: it makes the page on-demand, and both checks then see the
/// renderer.</item>
/// <item><c>prerendered page renders the Vue component</c>: ported. The page is read from <c>dist/client/index.html</c> as upstream does, and
/// requested over HTTP, where the host serves that file.</item>
/// <item>The last test is not an upstream test (see above).</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/ssr-renderers-static-vue.test.ts")]
public partial class SsrRenderersStaticVueTests(SiteHost sites)
{
    private const string Fixture = "core-ssr-renderers-static-vue";

    private static readonly object OnDemandPage = new { conformance = new { onDemandPage = true } };

    // The server entry writes the renderers of the app in the region of the virtual:astro:renderers module.
    [GeneratedRegex(@"virtual:astro:renderers\s+var renderers = \[\];")]
    private static partial Regex EmptyRenderers();

    private static bool HasVueServerRenderer(ConformanceSite site) =>
        BuildOutput.ReadServerScripts(site).Values.Any(script => script.Contains("createSSRApp", StringComparison.Ordinal));

    [Fact(DisplayName = "SSR renderers with static framework pages: does not include SSR renderers when only endpoints are dynamic")]
    public async Task ServerEntry_WhenOnlyEndpointsAreDynamic_HasNoRenderers()
    {
        var site = await sites.GetAsync(Fixture);

        var entry = BuildOutput.ReadServerScripts(site)["entry.mjs"];
        Assert.Matches(EmptyRenderers(), entry);
        Assert.False(HasVueServerRenderer(site), "no file of the server build should contain the server renderer of Vue");
        // Not asserted upstream: the app runs, and answers its endpoint, with that list of renderers.
        using var response = await site.GetAsync("/api/hello");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("""{"ok":true}""", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "SSR renderers with static framework pages: prerendered page renders the Vue component")]
    public async Task PrerenderedPage_RendersTheVueComponent()
    {
        var site = await sites.GetAsync(Fixture);

        var file = await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "index.html"));
        Assert.Contains("Hello from Vue", file);
        using var response = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(file, await response.ReadTextAsync());
    }

    [Fact(DisplayName = "Not upstream: the server entry has the Vue renderer when the page is on-demand")]
    public async Task ServerEntry_WhenThePageIsOnDemand_HasTheVueRenderer()
    {
        var site = await sites.GetAsync(Fixture, OnDemandPage);

        var entry = BuildOutput.ReadServerScripts(site)["entry.mjs"];
        Assert.DoesNotMatch(EmptyRenderers(), entry);
        Assert.Contains("@astrojs/vue/server.js", entry);
        Assert.True(HasVueServerRenderer(site), "the server build should contain the server renderer of Vue");
        Assert.False(site.PathExists("client/index.html"));
        using var response = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Hello from Vue", await response.ReadTextAsync());
    }
}
