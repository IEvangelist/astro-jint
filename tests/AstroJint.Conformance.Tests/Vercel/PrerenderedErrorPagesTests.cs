using System.Net;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/prerendered-error-pages.test.ts</c>. Upstream builds a site whose 404 page is
/// prerendered and checks that the route table of <c>.vercel/output/config.json</c> ends with a route that answers every
/// other URL with status 404 and <c>/404.html</c>.
/// <para>
/// Differences from upstream: the route table is replaced by the requests it is meant to serve. A URL that no route
/// matches has to answer 404 with the file of the prerendered page, byte for byte. The test needs a site whose 404 page is
/// prerendered, so <c>vercel-server</c> is built with a config variant that makes it one; the page prints its own URL,
/// which tells the built page from one rendered for the request.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/prerendered-error-pages.test.ts")]
public class PrerenderedErrorPagesTests(SiteHost sites)
{
    // The page prints the URL it was built for, which is not the URL of any request.
    private const string BuiltPagePath = "/404/";

    [Fact(DisplayName = "falls back to 404.html")]
    public async Task AnUnmatchedUrlAnswersWithThePrerenderedErrorPage()
    {
        var site = await sites.ServerWithPrerenderedErrorPageAsync();

        using var response = await site.GetAsync(VercelSites.UnmatchedPath);
        var body = await response.ReadTextAsync();

        Assert.True(site.PathExists("client/404.html"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal((await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "404.html"))).ReplaceLineEndings("\n"), body.ReplaceLineEndings("\n"));
        Assert.Equal(BuiltPagePath, ConformanceSite.ParseHtml(body).QuerySelector("#path")?.TextContent);
    }

    [Theory(DisplayName = "falls back to 404.html: for every kind of URL")]
    [InlineData("/no/such/page")]
    [InlineData("/no/such/page/")]
    [InlineData("/missing/dir/file.png")]
    [InlineData("/no/such/page?x=1")]
    public async Task EveryUnmatchedUrlAnswersWithThePrerenderedErrorPage(string path)
    {
        var site = await sites.ServerWithPrerenderedErrorPageAsync();

        using var response = await site.GetAsync(path);
        var page = ConformanceSite.ParseHtml(await response.ReadTextAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("404", page.QuerySelector("h1")?.TextContent);
        Assert.Equal(BuiltPagePath, page.QuerySelector("#path")?.TextContent);
    }

    [Fact(DisplayName = "falls back to 404.html: only for the URLs that no route matches")]
    public async Task TheRoutesThatMatchKeepTheirOwnStatus()
    {
        var site = await sites.ServerWithPrerenderedErrorPageAsync();

        using var page = await site.GetAsync("/abc");
        using var endpoint = await site.GetAsync("/api/xyz");
        using var index = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(HttpStatusCode.OK, endpoint.StatusCode);
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
    }
}
