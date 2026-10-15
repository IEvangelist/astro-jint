using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/content-collections-chunked.test.ts</c>. The upstream test starts the dev
/// server of a fixture with <c>experimental.collectionStorage: 'chunked'</c> whose collection has one entry of 3 MiB, and asks
/// for an on-demand page that reads that entry. The Cloudflare adapter turns <c>'chunked'</c> into chunks of 1 MiB (workerd
/// limits what a module may hold), so the entry spans several chunks. The test is adapted, and builds the fixture
/// <c>cf-chunked</c> (the same collection and page) for the host.
/// <list type="bullet">
/// <item>The adapter of the host does not change the size of a chunk, and with <c>'chunked'</c> alone the 3 MiB entry would be one
/// chunk of Astro's default of 20 MiB. The build asks for the object form with <c>chunkSize</c> of 1 MiB.</item>
/// <item>The manifest of the data store is read where the build writes it, in the cache directory
/// (<c>node_modules/.astro/data-store</c>); the dev server of upstream writes it to <c>.astro</c>.</item>
/// <item>The entry is read from the server bundle, by the page of the host: it answers 200 "ok" only when the chunks give back
/// the 3 MiB of the entry.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/content-collections-chunked.test.ts")]
public class ContentCollectionsChunkedTests(SiteHost sites)
{
    private const string Fixture = "cf-chunked";

    private static readonly object ChunkedStorage = new
    {
        experimental = new { collectionStorage = new { type = "chunked", chunkSize = 1024 * 1024 } },
    };

    [Fact(DisplayName = "loads a multi-chunk entry in workerd development")]
    public async Task LoadsAMultiChunkEntry()
    {
        var site = await sites.GetAsync(Fixture, ChunkedStorage);

        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(site.DistPath))!;
        var manifestPath = Path.Combine(root, "node_modules", ".astro", "data-store", "manifest.json");
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var chunks = manifest.RootElement.GetProperty("generated").EnumerateArray().Select(chunk => chunk.GetString()).Distinct().ToList();
        Assert.True(chunks.Count > 1, $"Expected the entry in more than one chunk, got {chunks.Count}");

        using var response = await site.GetAsync("/");

        var body = await response.ReadTextAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, CfContent.Describe(site, response, body));
        Assert.Equal("ok", body);
    }
}
