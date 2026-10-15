using System.Text.Json;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/production-async-hooks.test.ts</c>. Upstream builds a fixture with
/// <c>experimental.incrementalBuild</c> and scans the production worker for <c>async_hooks</c>, because the adapter uses
/// <c>AsyncLocalStorage</c> only while it prerenders. The tests build the shared fixture <c>cf-content</c> (which has the same two
/// pages, <c>/prerendered</c> and <c>/ssr</c>, and a content collection) with the same setting.
/// <list type="bullet">
/// <item>"prerenders through workerd with the incremental cache active" is adapted: the build is the prerender of Astro, in
/// Node, not in a workerd worker; the test asks for the manifest of the incremental cache (in <c>node_modules/.astro</c> of the
/// fixture) and for the prerendered page in <c>client/prerendered/index.html</c>.</item>
/// <item>"emits a production worker with zero occurrences of async_hooks" is adapted: the files of the server bundle that the
/// host can load are the ones <c>server/entry.mjs</c> and <c>server/chunks</c> have; the test scans every JavaScript file in
/// <c>server</c>, the <c>.prerender</c> folder of the build included, if the build leaves one.</item>
/// <item>Not applicable: "does not append nodejs_als to the deployed wrangler config": there is no wrangler config.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/production-async-hooks.test.ts")]
public class ProductionAsyncHooksTests(SiteHost sites)
{
    private static readonly object IncrementalBuild = new { experimental = new { incrementalBuild = true } };

    [Fact(DisplayName = "prerenders through workerd with the incremental cache active")]
    public async Task PrerendersWithTheIncrementalCache()
    {
        var site = await sites.GetAsync(CfContent.Fixture, IncrementalBuild);

        var manifest = Path.Combine(CfContent.RootOf(site), "node_modules", ".astro", "incremental-build.json");
        Assert.True(File.Exists(manifest), "the incremental manifest should be written");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifest));
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);

        Assert.True(site.PathExists("client/prerendered/index.html"), "the prerendered page should be emitted");
    }

    [Fact(DisplayName = "emits a production worker with zero occurrences of async_hooks")]
    public async Task EmitsAServerBundleWithoutAsyncHooks()
    {
        var site = await sites.GetAsync(CfContent.Fixture, IncrementalBuild);

        var files = Directory.EnumerateFiles(Path.Combine(site.DistPath, "server"), "*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".js", StringComparison.Ordinal)
                || file.EndsWith(".mjs", StringComparison.Ordinal)
                || file.EndsWith(".cjs", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            Assert.False(
                File.ReadAllText(file).Contains("async_hooks", StringComparison.Ordinal),
                $"production server file {file} must not reference async_hooks");
        }
    }
}
