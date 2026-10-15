using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/incremental-build-headers.test.ts</c>. The fixture has one prerendered page whose
/// <c>getStaticPaths()</c> gives it a <c>cacheKey</c>, and builds with <c>experimental.incrementalBuild</c>, a Content Security
/// Policy (<c>security.csp</c>) and an adapter that takes the headers of the prerendered pages (<c>staticHeaders</c>). The test
/// builds it twice into one cache and checks that the page that the second build skips still has its policy.
/// <para>
/// Differences from upstream: the test adapter hands the map of the route headers to a callback, and this adapter writes the
/// policy into the manifest <c>astro-jint.json</c>, from which the host sends it with the file, so the test reads the policy from
/// the response for the page instead of from the map. The second build writes into another output folder (the harness gives
/// every config its own, and the folder is not part of the config that the cache is keyed by), which means that the page is
/// restored from the cache and not found in the output, and the build log of the second build says so (<c>(restored)</c>).
/// Stricter than upstream: the policy of the second build is the one of the first, and what the restored page links is served.
/// </para>
/// </summary>
[Trait("Upstream", "core/incremental-build-headers.test.ts")]
public class IncrementalBuildHeadersTests(SiteHost sites)
{
    private const string Fixture = "cb-incremental-headers";

    private static async Task<string> PolicyOfAsync(ConformanceSite site)
    {
        using var response = await site.GetAsync("/page/a/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.Single(response.HeaderValues("Content-Security-Policy"));
    }

    [Fact(DisplayName = "replays a skipped route into the static headers map")]
    public async Task ASkippedRoute_KeepsItsContentSecurityPolicy()
    {
        IncrementalBuild.Reset(Fixture);

        // Sanity: the warm build collected a CSP header for the route.
        var warm = await sites.GetAsync(Fixture, IncrementalBuild.Build(1));
        var warmPolicy = await PolicyOfAsync(warm);
        Assert.Contains("script-src", warmPolicy);

        // The header was persisted so it can be replayed on skip.
        using var cache = JsonDocument.Parse(await File.ReadAllTextAsync(IncrementalBuild.CacheFile(Fixture)));
        var path = cache.RootElement.GetProperty("routes").GetProperty("src/pages/page/[slug].astro").GetProperty("paths").GetProperty("/page/a");
        Assert.True(path.GetProperty("headers").GetArrayLength() > 0, "the path should record its response headers");

        // Rebuild with no changes: the route is skipped rather than rendered, so it never sets a header. Without a replay it would
        // be missing from the headers that the adapter writes.
        var rebuilt = await sites.GetAsync(Fixture, IncrementalBuild.Build(2));
        Assert.Contains("/page/a/index.html (restored)", rebuilt.BuildLog);
        Assert.Equal(warmPolicy, await PolicyOfAsync(rebuilt));

        var page = await PageAssets.PageAsync(rebuilt, "/page/a/");
        Assert.Equal("Page A", page.QuerySelector("h1")?.TextContent);
        foreach (var script in page.QuerySelectorAll("script[src]"))
        {
            await PageAssets.AssertServedAsync(rebuilt, script.GetAttribute("src")!);
        }
    }
}
