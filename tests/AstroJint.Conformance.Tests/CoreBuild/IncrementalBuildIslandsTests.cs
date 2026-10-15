using System.Net;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/incremental-build-islands.test.ts</c>. The fixture has two prerendered pages with a
/// <c>cacheKey</c>, one with a server island and one without, and builds with <c>experimental.incrementalBuild</c>. Every build
/// mints a new key for the server islands, and the page with the island bakes ciphertext that only that key opens into its
/// HTML. The test builds twice into one cache. It plants a sentinel in the cached copy of each page, which a restore keeps and
/// a render overwrites, and checks that the second build restored the page without an island and rendered the one with it.
/// <para>
/// Differences from upstream: the build is made with the adapter under test (the test adapter is a stand-in for it), and the
/// output file that the test reads is requested from the host (a static file is served as it is). The second build writes
/// into another output folder (the harness gives every config its own, and the folder is not part of the config that the cache
/// is keyed by), so Astro has to restore the page from its cached copy, as it does after it emptied the folder. The published
/// Astro (7.3.8) names a cached copy by its path from the output folder, <c>client/plain/a/index.html</c>, where upstream
/// looks for <c>plain/a/index.html</c>. Stricter than upstream: the island of the page that was rendered again is requested
/// from the host, which only answers when the key that the page was rendered with is the key of the server build.
/// </para>
/// </summary>
[Trait("Upstream", "core/incremental-build-islands.test.ts")]
public partial class IncrementalBuildIslandsTests(SiteHost sites)
{
    private const string Fixture = "cb-incremental-islands";

    [GeneratedRegex("""fetch\((["'])(/_server-islands/[^"']+)\1""")]
    private static partial Regex ServerIslandFetch();

    [Fact(DisplayName = "busts server-island pages when the key changes but keeps other pages cached")]
    public async Task AChangedKey_RendersThePageWithAnIslandAgainAndRestoresTheOthers()
    {
        IncrementalBuild.Reset(Fixture);

        // Warm the cache. Without ASTRO_KEY, each build mints a fresh key.
        await sites.GetAsync(Fixture, IncrementalBuild.Build(1));

        // Astro empties the output folder each build, so a skipped page is restored from its cached copy. Plant a sentinel in each:
        // a restore keeps it, a render overwrites it.
        await File.WriteAllTextAsync(IncrementalBuild.CachedCopy(Fixture, "client/plain/a/index.html"), "cached plain sentinel");
        await File.WriteAllTextAsync(IncrementalBuild.CachedCopy(Fixture, "client/island/a/index.html"), "cached island sentinel");

        // Rebuild with a fresh (rotating) key.
        var site = await sites.GetAsync(Fixture, IncrementalBuild.Build(2));

        // The plain page has no server island, so the new key does not affect it and it is restored from the cache.
        Assert.Equal("cached plain sentinel", await site.GetStringAsync("/plain/a/"));

        // The island page bakes key-bound ciphertext into its HTML, so the changed key forces a render instead of a restore.
        var islandHtml = await site.GetStringAsync("/island/a/");
        Assert.NotEqual("cached island sentinel", islandHtml);
        var page = await PageAssets.PageAsync(site, "/island/a/");
        Assert.Equal("Island A", page.QuerySelector("h1")?.TextContent);

        var island = ServerIslandFetch().Match(islandHtml);
        Assert.True(island.Success, "the HTML should include the server island URL");
        using var response = await site.GetAsync(island.Groups[2].Value);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("I'm an island", await response.ReadTextAsync());
    }
}
