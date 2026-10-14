using System.Net;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/server-rendered-error-pages.test.ts</c>. Upstream builds a site whose 404 page
/// is rendered on demand and checks that the catch-all route of the route table goes to the <c>_render</c> function without
/// a status of its own, because the function decides the status (a rendered page can answer 200).
/// <para>
/// Differences from upstream: the route table is replaced by the requests it serves. A URL that no route matches is
/// answered by the 404 page rendered for that request, with status 404; a URL that a route matches keeps the status that
/// route gives, so the status is not forced. The <c>vercel-server</c> site has the 404 page rendered on demand.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/server-rendered-error-pages.test.ts")]
public class ServerRenderedErrorPagesTests(SiteHost sites)
{
    [Fact(DisplayName = "catch-all forwards to the function without forcing a status")]
    public async Task AnUnmatchedUrlIsAnsweredByTheRenderedErrorPage()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync(VercelSites.UnmatchedPath);
        var page = ConformanceSite.ParseHtml(await response.ReadTextAsync());

        Assert.False(site.PathExists("client/404.html"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("404", page.QuerySelector("h1")?.TextContent);
        Assert.Equal(VercelSites.UnmatchedPath, page.QuerySelector("#path")?.TextContent);
    }

    [Theory(DisplayName = "catch-all forwards to the function without forcing a status: the status is the one of the route")]
    [InlineData("/abc", 200)]
    [InlineData("/api/xyz", 200)]
    [InlineData("/live", 200)]
    [InlineData("/catch/a/b", 200)]
    [InlineData("/no/such/page", 404)]
    public async Task TheStatusIsTheOneTheRenderGives(string path, int status)
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync(path);

        Assert.Equal((HttpStatusCode)status, response.StatusCode);
    }

    [Fact(DisplayName = "catch-all forwards to the function without forcing a status: each request is rendered for its own URL")]
    public async Task TheErrorPageIsRenderedPerRequest()
    {
        var site = await sites.ServerAsync();

        var first = await site.GetHtmlAsync("/no/such/one");
        var second = await site.GetHtmlAsync("/no/such/two");

        Assert.Equal("/no/such/one", first.QuerySelector("#path")?.TextContent);
        Assert.Equal("/no/such/two", second.QuerySelector("#path")?.TextContent);
    }
}
