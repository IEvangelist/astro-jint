using System.Net;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/serverless-with-dynamic-routes.test.ts</c>. Upstream builds a site with a
/// prerendered index and on-demand dynamic routes, and checks that the build output has <c>static/index.html</c> and the
/// function config of <c>_render</c>.
/// <para>
/// Differences from upstream: there is no Vercel build output to read, so the test looks at the files this adapter writes
/// (<c>client/index.html</c> and the server entry) and then requests the three kinds of route, which is what the Vercel
/// output is there to make possible: the static index, an on-demand page and an on-demand endpoint.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/serverless-with-dynamic-routes.test.ts")]
public class ServerlessWithDynamicRoutesTests(SiteHost sites)
{
    [Fact(DisplayName = "build successful")]
    public async Task TheBuildHasTheStaticIndexAndTheServerEntry()
    {
        var site = await sites.ServerAsync();

        Assert.True(site.PathExists("client/index.html"));
        Assert.True(site.PathExists("server/entry.mjs"));
    }

    [Fact(DisplayName = "build successful: the prerendered index is served as it was built")]
    public async Task ThePrerenderedIndexIsServed()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("testing", (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
        Assert.Equal(
            (await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "index.html"))).ReplaceLineEndings("\n"),
            (await site.GetStringAsync("/")).ReplaceLineEndings("\n"));
    }

    [Fact(DisplayName = "build successful: a page of a dynamic route is rendered on demand")]
    public async Task ADynamicPageIsRenderedPerRequest()
    {
        var site = await sites.ServerAsync();

        var first = await site.GetHtmlAsync("/abc");
        var second = await site.GetHtmlAsync("/def");

        Assert.Equal("testing abc", first.QuerySelector("h1")?.TextContent);
        Assert.Equal("testing def", second.QuerySelector("h1")?.TextContent);
        Assert.False(site.PathExists("client/abc/index.html"));
    }

    [Fact(DisplayName = "build successful: an endpoint of a dynamic route is rendered on demand")]
    public async Task ADynamicEndpointAnswersPerRequest()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/api/xyz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"id":"xyz"}""", await response.ReadTextAsync());
        Assert.Contains("application/json", Assert.Single(response.HeaderValues("Content-Type")));
    }
}
