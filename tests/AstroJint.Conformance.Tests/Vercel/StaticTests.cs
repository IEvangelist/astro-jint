using System.Net;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/static.test.ts</c>. Upstream builds a static site and reads the Vercel
/// output: the catch-all route of <c>config.json</c> must send the requests that nothing matched to <c>/404.html</c> with
/// status 404, and the output must have no render function (<c>functions/_render.func</c>).
/// <para>
/// Differences from upstream: the route table is replaced by the requests it answers, so a URL that no route matches gets
/// the status 404 and the exact content of <c>404.html</c>. The static build of this adapter always has the server entry
/// (<c>server/entry.mjs</c>: the redirects, the server islands and the sessions are answered at run time), so the second test
/// checks what a static site must still do without the function: the prerendered pages are answered from the files of the
/// build, with the validators of a static file, and are not rendered per request.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/static.test.ts")]
public class StaticTests(SiteHost sites)
{
    [Theory(DisplayName = "falls back to 404.html")]
    [InlineData(VercelSites.UnmatchedPath)]
    [InlineData("/nothing")]
    [InlineData("/missing.png")]
    public async Task AnUnmatchedUrl_FallsBackToThe404Page(string path)
    {
        var site = await sites.StaticAsync();
        var page = await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "404.html"));

        using var response = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(page, await response.ReadTextAsync());
    }

    [Fact(DisplayName = "does not create a serverless function")]
    public async Task PrerenderedPages_AreServedFromFiles()
    {
        var site = await sites.StaticAsync();

        using var response = await site.GetAsync("/subpage");

        Assert.True(site.PathExists("client/subpage/index.html"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, must-revalidate, max-age=0", Assert.Single(response.HeaderValues("Cache-Control")));
        Assert.Single(response.HeaderValues("ETag"));
        Assert.Single(response.HeaderValues("Last-Modified"));
        Assert.Equal("Subpage", (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
    }
}
