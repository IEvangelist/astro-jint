using System.Net;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/isr-edge-middleware.test.ts</c>. Upstream builds two sites with <c>isr</c>
/// and <c>edgeMiddleware</c>: one with a middleware file, one without. With the middleware, ISR used to send every route to
/// the ISR function and skip the edge middleware, so the tests read the route table of <c>config.json</c> (pages,
/// endpoints, the 404 and the routes excluded from ISR go through <c>_middleware</c>, redirects come first, prerendered
/// pages are files), call the generated middleware to see where <c>next()</c> forwards (<c>/_isr</c> with the path of the
/// request as the cache key, <c>/_render</c> for what is not cached) and call the ISR function to see that the query
/// string survives. Without the middleware, they check the shape of the ISR routes and that a malformed path override is a
/// 404.
/// <para>
/// Differences from upstream: Astro's middleware runs in the app, after the redirects of the config and for on-demand
/// routes only, so what upstream sets up by routing is what the requests below observe: the middleware header
/// (<c>x-astro-middleware</c>, set by the middleware of <c>vercel-server</c> after <c>next()</c>) is there on pages,
/// endpoints, the 404 page and the routes that the ISR exclusions overlap, and not on redirects and static files; each path
/// is rendered with its own parameters (the cache key of upstream); the query string reaches the endpoint. The
/// path-override tests of the second site run against a static site, where <c>/_isr</c> is no route, and against the
/// catch-all route, where the real path is what renders. The tests of the edge function and of the route table (the
/// function config, the destinations <c>_middleware</c>/<c>_render</c>/<c>_isr</c>, the ISR config, what <c>next()</c> forwards,
/// the shape of the root and spread patterns, the missing function) are about files and requests that only a Vercel build
/// has.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/isr-edge-middleware.test.ts")]
public class IsrEdgeMiddlewareTests(SiteHost sites)
{
    private const string MiddlewareHeader = "x-astro-middleware";

    private static void AssertMiddlewareRan(HttpResponseMessage response) =>
        Assert.Equal("ran", Assert.Single(response.HeaderValues(MiddlewareHeader)));

    [Theory(DisplayName = "points page routes at the middleware")]
    [InlineData("/cached/42")]
    [InlineData("/islands")]
    [InlineData("/one")]
    public async Task PagesRunTheMiddleware(string path)
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertMiddlewareRan(response);
    }

    [Fact(DisplayName = "points routes excluded from ISR at the middleware too")]
    public async Task AnExcludedRouteRunsTheMiddleware()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertMiddlewareRan(response);
        Assert.Contains("<h1>Live</h1>", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "routes every on-demand route kind through the middleware")]
    public async Task EveryKindOfOnDemandRouteRunsTheMiddleware()
    {
        var site = await sites.ServerAsync();

        using var endpoint = await site.GetAsync("/api/data");
        using var notFoundPage = await site.GetAsync("/404");
        using var unmatched = await site.GetAsync(VercelSites.UnmatchedPath);

        Assert.Equal(HttpStatusCode.OK, endpoint.StatusCode);
        AssertMiddlewareRan(endpoint);
        Assert.Equal(HttpStatusCode.NotFound, notFoundPage.StatusCode);
        AssertMiddlewareRan(notFoundPage);
        Assert.Equal(HttpStatusCode.NotFound, unmatched.StatusCode);
        AssertMiddlewareRan(unmatched);
    }

    [Fact(DisplayName = "leaves prerendered pages as static files")]
    public async Task APrerenderedPageIsAStaticFile()
    {
        var site = await sites.ServerAsync();

        var file = await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "static", "index.html"));
        using var response = await site.GetAsync("/static");

        Assert.Contains("<h1>Static</h1>", file);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(file.ReplaceLineEndings("\n"), (await response.ReadTextAsync()).ReplaceLineEndings("\n"));
        Assert.Single(response.HeaderValues("ETag"));
        Assert.Empty(response.HeaderValues(MiddlewareHeader));
    }

    [Fact(DisplayName = "keeps redirects ahead of the middleware")]
    public async Task ARedirectIsAnsweredBeforeTheMiddleware()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/old");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/", Assert.Single(response.HeaderValues("Location")));
        Assert.Empty(response.HeaderValues(MiddlewareHeader));
    }

    [Fact(DisplayName = "forwards dynamic routes with their real path")]
    public async Task EachPathOfADynamicRouteHasItsOwnPage()
    {
        var site = await sites.ServerAsync();

        var first = await site.GetStringAsync("/cached/42");
        var second = await site.GetStringAsync("/cached/43");

        Assert.Contains("<h1>42</h1>", first);
        Assert.Contains("<h1>43</h1>", second);
    }

    [Fact(DisplayName = "forwards routes excluded from ISR to the serverless function")]
    public async Task TheRouteWithoutParametersWinsOverTheDynamicOne()
    {
        var site = await sites.ServerAsync();

        var body = await site.GetStringAsync("/live");

        Assert.Contains("<h1>Live</h1>", body);
        Assert.DoesNotContain("testing live", body);
    }

    [Fact(DisplayName = "still caches the dynamic route the exclusion overlaps")]
    public async Task TheDynamicRouteStillRendersOtherPaths()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/anything-else");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>testing anything-else</h1>", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "renders paths no route matches instead of caching them")]
    public async Task APathWithoutARouteIsRenderedAsTheNotFoundPage()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync(VercelSites.UnmatchedPath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.ReadHtmlAsync();
        Assert.Equal("404", html.QuerySelector("h1")?.TextContent);
        Assert.Equal(VercelSites.UnmatchedPath, html.QuerySelector("#path")?.TextContent);
        Assert.DoesNotContain(response.HeaderValues("Cache-Control"), value => value.Contains("max-age") || value.Contains("public"));
    }

    [Fact(DisplayName = "keeps the query string out of the cache key")]
    public async Task TheQueryStringDoesNotChangeThePrerenderedPage()
    {
        var site = await sites.ServerAsync();

        var plain = await site.GetStringAsync("/");
        using var response = await site.GetAsync("/?foo=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(plain, await response.ReadTextAsync());
    }

    [Fact(DisplayName = "keeps the query string the middleware forwarded")]
    public async Task TheQueryStringReachesTheEndpointThroughTheMiddleware()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/api/data?foo=bar");

        AssertMiddlewareRan(response);
        Assert.Equal("""{"ok":true,"query":"?foo=bar"}""", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "keeps the query string the route rewrite passed through")]
    public async Task TheQueryStringStaysNextToThePathParameter()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/api/data?x_astro_path=%2Fapi%2Fdata&foo=bar");

        Assert.Equal("""{"ok":true,"query":"?x_astro_path=%2Fapi%2Fdata&foo=bar"}""", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "still returns the response to the middleware")]
    public async Task TheMiddlewareGetsTheResponseOfThePage()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/cached/42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertMiddlewareRan(response);
        Assert.Contains("<h1>42</h1>", await response.ReadTextAsync());
    }

    [Theory(DisplayName = "returns 404 for a trusted override that does not start with /")]
    [InlineData("$0")]
    [InlineData("$1")]
    [InlineData("relative")]
    [InlineData("")]
    public async Task ABadOverrideChangesNothing(string badPath)
    {
        var query = $"x_astro_path={badPath}&x_astro_path_token=token";
        var staticSite = await sites.StaticAsync();
        var server = await sites.ServerAsync();

        using var unrouted = await staticSite.GetAsync($"/_isr?{query}");
        using var catchAll = await server.GetAsync($"/catch/real?{query}");

        Assert.Equal(HttpStatusCode.NotFound, unrouted.StatusCode);
        Assert.Empty(unrouted.HeaderValues("Location"));
        Assert.Equal(HttpStatusCode.OK, catchAll.StatusCode);
        Assert.Empty(catchAll.HeaderValues("Location"));
        Assert.Contains("<h1>Slug: real</h1>", await catchAll.ReadTextAsync());
    }

    [Fact(DisplayName = "returns 404 for a trusted override with no path at all")]
    public async Task AnOverrideWithoutAPathIsNotARoute()
    {
        var site = await sites.StaticAsync();

        using var response = await site.GetAsync("/_isr?x_astro_path_token=token");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(response.HeaderValues("Location"));
    }

    [Fact(DisplayName = "still renders a valid trusted override on the catch-all fixture")]
    public async Task TheCatchAllRendersItsPath()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/catch/cached/42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>Slug: cached/42</h1>", await response.ReadTextAsync());
    }
}
