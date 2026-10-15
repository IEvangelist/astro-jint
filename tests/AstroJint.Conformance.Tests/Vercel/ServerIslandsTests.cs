using System.Net;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/server-islands.test.ts</c>. Upstream builds a server site and a static
/// site with a server island and reads the Vercel output: the route of the islands in <c>config.json</c> (it must be there,
/// and for the static site point to <c>_render</c>), the render function <c>_render.func</c> with its
/// <c>.vc-config.json</c> (the test loads it and sends the island URL of the prerendered page to it), and the absence of the
/// server entry among the static files.
/// <para>
/// Differences from upstream: the files of the Vercel output are replaced by the output of this adapter and by the requests
/// that it serves. The route of the islands is looked up in the manifest of the build (<c>astro-jint.json</c>) as a route
/// rendered per request, and the island URL is requested from the host instead of from the render function, which is the
/// server entry <c>server/entry.mjs</c> that the host loads. That the route is not served from a file is seen as a response
/// without the validators of a static file. The sites are the ones of <c>vercel-server</c> and <c>vercel-static</c>, whose
/// page <c>/islands</c> has the island (<c>/</c> of the static site has none, because it is shared with other tests).
/// </para>
/// </summary>
[Trait("Upstream", "vercel/server-islands.test.ts")]
public partial class ServerIslandsTests(SiteHost sites)
{
    private const string ServerIslandsRoute = "/_server-islands/[name]";

    [GeneratedRegex("""fetch\((["'])(/_server-islands/[^"']+)\1""")]
    private static partial Regex ServerIslandFetch();

    [Fact(DisplayName = "server islands route is in the config")]
    public async Task ServerIslandsRoute_InAServerBuild_IsInTheManifestAndAnswers()
    {
        var site = await sites.ServerAsync();
        var islandUrl = IslandUrl(await site.GetStringAsync("/islands"));

        using var response = await site.GetAsync(islandUrl);

        Assert.True(site.HasRoute(ServerIslandsRoute, prerender: false), "Default server islands route included");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("I'm an island", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "creates _render.func for server islands in static builds")]
    public async Task AStaticBuild_HasTheServerEntryForTheIslands()
    {
        var site = await sites.StaticAsync();

        Assert.True(site.PathExists("server/entry.mjs"), "the server entry should exist");
        Assert.True(site.PathExists("astro-jint.json"), "the manifest should exist");
    }

    [Fact(DisplayName = "renders the server island")]
    public async Task TheServerIslandOfAPrerenderedPage_IsRendered()
    {
        var site = await sites.StaticAsync();
        var html = await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "islands", "index.html"));
        var islandUrl = IslandUrl(html);

        using var response = await site.GetAsync(islandUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("I'm an island", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "does not publish the server entry as a static file")]
    public async Task TheServerEntry_IsNotAStaticFile()
    {
        var site = await sites.StaticAsync();

        using var entry = await site.GetAsync("/entry.mjs");
        using var serverEntry = await site.GetAsync("/server/entry.mjs");

        Assert.False(site.PathExists("client/entry.mjs"));
        Assert.Equal(HttpStatusCode.NotFound, entry.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, serverEntry.StatusCode);
    }

    [Fact(DisplayName = "includes server islands route in config")]
    public async Task ServerIslandsRoute_InAStaticBuild_IsRenderedPerRequest()
    {
        var site = await sites.StaticAsync();
        var islandUrl = IslandUrl(await site.GetStringAsync("/islands"));

        using var response = await site.GetAsync(islandUrl);

        Assert.True(site.HasRoute(ServerIslandsRoute, prerender: false), "Server islands route should be in config");
        Assert.False(Directory.Exists(Path.Combine(site.DistPath, "client", "_server-islands")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", Assert.Single(response.HeaderValues("Content-Type")));
        Assert.Empty(response.HeaderValues("ETag"));
        Assert.Empty(response.HeaderValues("Last-Modified"));
    }

    private static string IslandUrl(string html)
    {
        var match = ServerIslandFetch().Match(html);

        Assert.True(match.Success, "the HTML should include the server island URL");
        return match.Groups[2].Value;
    }
}
