using System.Net;
using AngleSharp.Dom;
using static AstroJint.Conformance.Core.ServerIslandRequests;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/csp-server-islands.test.ts</c>. The fixtures are the ones of <c>server-islands.test.ts</c>
/// (<c>core-server-islands</c> and <c>core-server-islands-hybrid</c>) built with the JSON config <c>security: { csp: true }</c>.
/// <list type="bullet">
/// <item>Unlike <c>server-islands.test.ts</c>, this test leaves <c>security.checkOrigin</c> on, so the island request must carry an
/// <c>Origin</c> that matches the request URL. It is the origin of the site, because the host builds the URL of a request from
/// its <c>Host</c> header (upstream: <c>http://example.com</c>).</item>
/// <item><c>app.render(new Request(...))</c> is an HTTP request; the encrypted props and export are made as in
/// <see cref="ServerIslandRequests"/>.</item>
/// <item>The hybrid tests read <c>client/index.html</c> of the build output, as upstream reads it with <c>fixture.readFile</c>.</item>
/// <item><c>Hybrid: build (no adapter): Errors during the build</c> builds the fixture without an adapter and expects Astro's
/// <c>NoAdapterInstalledServerIslands</c> error. A project without an adapter has no host to ask, so the test is not applicable.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/csp-server-islands.test.ts")]
public class CspServerIslandsTests(SiteHost sites)
{
    private const string Fixture = "core-server-islands";
    private const string HybridFixture = "core-server-islands-hybrid";

    private static readonly object CspConfig = new { security = new { csp = true } };

    [Fact(DisplayName = "Server islands: SSR: omits the islands HTML")]
    public async Task Page_OmitsTheIslandHtml_AndHasTheIslandScript()
    {
        var site = await sites.GetAsync(Fixture, CspConfig);

        using var response = await site.GetAsync("/");
        var document = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(document.QuerySelectorAll("h2#island"));
        Assert.Single(document.QuerySelectorAll("script[data-island-id]"));

        // Not asserted upstream: an on-demand page carries the policy as a response header, and it lists the hash of every inline script.
        var policy = Assert.Single(response.HeaderValues("Content-Security-Policy"));
        Assert.Contains("script-src", policy);
        foreach (var script in document.QuerySelectorAll("script:not([src])"))
        {
            Assert.Contains($"'{CspDigest.Sha256(script.TextContent)}'", policy);
        }
    }

    [Fact(DisplayName = "Server islands: SSR: island is not indexed")]
    public async Task Island_Response_IsNotIndexed()
    {
        var site = await sites.GetAsync(Fixture, CspConfig);

        using var response = await PostIslandAsync(site, IslandBody());

        Assert.Equal("noindex", Assert.Single(response.HeaderValues("X-Robots-Tag")));
    }

    [Fact(DisplayName = "Server islands: Hybrid: build: Omits the island HTML from the static HTML")]
    public async Task HybridBuild_StaticHtml_OmitsTheIslandHtml()
    {
        var document = await ReadHybridIndexAsync();

        Assert.Empty(document.QuerySelectorAll("h2#island"));
        Assert.Equal(2, document.QuerySelectorAll("script[data-island-id]").Length);
    }

    [Fact(DisplayName = "Server islands: Hybrid: build: includes the server island runtime script once")]
    public async Task HybridBuild_StaticHtml_IncludesTheRuntimeScriptOnce()
    {
        var document = await ReadHybridIndexAsync();

        var runtimeScripts = document.QuerySelectorAll("script")
            .Where(script => script.InnerHtml.Trim().StartsWith("async function replaceServerIsland", StringComparison.Ordinal));

        Assert.Single(runtimeScripts);
    }

    private async Task<IDocument> ReadHybridIndexAsync()
    {
        var site = await sites.GetAsync(HybridFixture, CspConfig);
        return ConformanceSite.ParseHtml(await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "index.html")));
    }
}
