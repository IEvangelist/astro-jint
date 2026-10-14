using System.Net;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/isr.test.ts</c>. Upstream builds a site with <c>isr</c> enabled and checks
/// the files of the ISR function: its prerender config, the route table of <c>config.json</c> (the routes that are cached
/// are rewritten to <c>/_isr?x_astro_path=…&amp;x_astro_path_token=…</c>, the others go to <c>_render</c>) and, by calling the
/// function, that the path override is only honoured with the token of the build.
/// <para>
/// Differences from upstream: there is no ISR, no token and no route table. The host renders every on-demand route per
/// request, and <c>x_astro_path</c> and <c>x_astro_path_token</c> are query parameters like any other, so the tests ask
/// for what the route table and the override are there to make possible. "generates expected routes" requests each route
/// of the upstream table and checks that the manifest of the build lists it as on-demand and that the host renders it. The
/// two tests that are about the token request <c>/_isr</c> (which, in <c>vercel-server</c>, is answered by the <c>[id]</c>
/// page, where upstream has no route for it and answers 404) and see that the page named by the override is not the one
/// that comes back. The two that honour the override request the page itself. "returns 404 for a trusted override that does
/// not start with /" runs on a static site with <c>trailingSlash: 'always'</c>, the case in which upstream fears a redirect,
/// where <c>/_isr</c> is no route. The tests of the prerender config, of the allow-list of the query and of the
/// <c>$1</c>/<c>$0</c> references of the route table are about files that only a Vercel build has.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/isr.test.ts")]
public class IsrTests(SiteHost sites)
{
    [Theory(DisplayName = "generates expected routes")]
    [InlineData("/two", 200, "<h1>Two</h1>", "/two")]
    [InlineData("/excluded/x", 200, "<h1>Dynamic</h1>", "/excluded/[dynamic]")]
    [InlineData("/excluded/a/b", 200, "<h1>Rest</h1>", "/excluded/[...rest]")]
    [InlineData("/api/x", 200, """{"id":"x"}""", "/api/[id]")]
    [InlineData("/api", 200, "OK", "/api")]
    [InlineData("/_server-islands/x", 400, "", "/_server-islands/[name]")]
    [InlineData("/_image", 400, "Invalid request", "/_image")]
    [InlineData("/one", 200, "<h1>One</h1>", "/one")]
    [InlineData("/a/b", 200, "<h1>A: a B: b</h1>", "/[a]/[b]")]
    [InlineData("/404", 404, "<h1>404</h1>", "/404")]
    [InlineData("/no/such/page", 404, "<h1>404</h1>", null)]
    public async Task EveryRouteOfTheTableIsRenderedPerRequest(string path, int status, string fragment, string? route)
    {
        var site = await sites.ServerAsync();
        if (route is not null)
        {
            Assert.True(site.HasRoute(route, prerender: false));
        }

        using var response = await site.GetAsync(path);

        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Contains(fragment, await response.ReadTextAsync());
    }

    [Fact(DisplayName = "ignores x_astro_path without a valid path token")]
    public async Task TheOverrideWithoutATokenIsIgnored()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/_isr?x_astro_path=/one");

        var body = await response.ReadTextAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>testing _isr</h1>", body);
        Assert.DoesNotContain("<h1>One</h1>", body);
    }

    [Fact(DisplayName = "ignores x_astro_path when only the x-vercel-isr header is set")]
    public async Task TheOverrideWithOnlyTheIsrHeaderIsIgnored()
    {
        var site = await sites.ServerAsync();

        using var response = await site.SendAsync(HttpMethod.Get, "/_isr?x_astro_path=/one", null, ("x-vercel-isr", "1"));

        var body = await response.ReadTextAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>testing _isr</h1>", body);
        Assert.DoesNotContain("<h1>One</h1>", body);
    }

    [Fact(DisplayName = "honors x_astro_path when the valid path token is present")]
    public async Task ThePageOfAnIsrRouteIsRendered()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/one");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>One</h1>", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "honors x_astro_path for dynamic routes when the valid path token is present")]
    public async Task ThePageOfADynamicIsrRouteIsRendered()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/a/b");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>A: a B: b</h1>", await response.ReadTextAsync());
    }

    [Theory(DisplayName = "returns 404 for a trusted override that does not start with /")]
    [InlineData("$1")]
    [InlineData("$0")]
    [InlineData("relative")]
    public async Task ABadOverrideIsNeitherARouteNorARedirect(string badPath)
    {
        var site = await sites.StaticWithTrailingSlashesAsync();

        using var response = await site.GetAsync($"/_isr?x_astro_path={badPath}&x_astro_path_token=token");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(response.HeaderValues("Location"));
    }
}
