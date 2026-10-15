using System.Net;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/server-island-prerender-deps.test.ts</c>. The first test uses the
/// shared fixture <c>cf-pages</c>: its prerendered page <c>/island/</c> has the server island <c>Island.astro</c>, which
/// imports <c>devalue</c> as the upstream island does. The second one uses the shared fixture <c>cf-ui</c>, where the
/// page <c>/island/</c> has an island that renders a Solid component, as the upstream fixture
/// <c>server-island-prerender-framework</c> does. Differences:
/// <list type="bullet">
/// <item>The server output is <c>dist/server</c> instead of the Worker directory; the chunk is searched in
/// <c>server/chunks</c> as <c>Island_*.mjs</c>.</item>
/// <item>The island of the second test is fetched from the host, not from <c>astro preview</c>, and the host renders it
/// with the bundle that Jint runs.</item>
/// <item>There is one more test, of the host: the island of <c>cf-pages</c> that the prerendered page asks for is
/// rendered at run time, with the bundled dependency.</item>
/// <item>The second test is a gap, GAP-CF-9: the Solid component of the island does not render, like the Solid page of
/// <see cref="WithSolidJsTests"/>.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/server-island-prerender-deps.test.ts")]
public partial class ServerIslandPrerenderDepsTests(SiteHost sites)
{
    [Fact(DisplayName = "bundles third-party imports for prerender-only server islands")]
    public async Task ServerIslandChunk_BundlesItsDependencies()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        var chunks = Directory.GetFiles(Path.Combine(site.DistPath, "server", "chunks"), "Island_*.mjs");

        var chunk = Assert.Single(chunks);
        var code = await File.ReadAllTextAsync(chunk);
        Assert.False(
            code.Contains("from 'devalue'") || code.Contains("from \"devalue\""),
            $"Server island chunk should not keep bare devalue imports:\n{code}");
    }

    [Fact(DisplayName = "renders framework components in prerender-only server islands", Skip = CfUi.SolidGap)]
    public async Task ServerIsland_RendersFrameworkComponents()
    {
        var site = await sites.GetAsync(CfUi.Fixture);

        using var pageResponse = await site.GetAsync("/island/");
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        var pageHtml = await pageResponse.Content.ReadAsStringAsync();
        var match = IslandFetch().Match(pageHtml);
        Assert.True(match.Success, $"Expected prerendered HTML to include server island fetch URL, got:\n{pageHtml}");

        using var islandResponse = await site.GetAsync(match.Groups[2].Value);

        Assert.Equal(HttpStatusCode.OK, islandResponse.StatusCode);
        var islandHtml = await islandResponse.Content.ReadAsStringAsync();
        Assert.True(
            islandHtml.Contains("id=\"framework-content\""),
            $"Expected framework content in server island response, got:\n{islandHtml}");
    }

    [Fact(DisplayName = "renders the dependency of a prerender-only server island at run time (host)")]
    public async Task ServerIsland_RendersWithItsDependencies()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        var pageHtml = await site.GetStringAsync("/island/");
        var match = IslandFetch().Match(pageHtml);
        Assert.True(match.Success, $"Expected prerendered HTML to include a server island fetch URL, got:\n{pageHtml}");

        using var response = await site.GetAsync(match.Groups[2].Value);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("Encoded: {message:\"hello world\"}", document.QuerySelector("p")?.TextContent);
    }

    [GeneratedRegex("""fetch\((["'])(/_server-islands/[^"']+)\1""")]
    private static partial Regex IslandFetch();
}
