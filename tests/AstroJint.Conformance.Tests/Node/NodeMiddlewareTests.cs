using System.Net;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/node-middleware.test.ts</c>. The upstream file runs the same checks behind the
/// Node adapter's standalone server, behind Express and behind Fastify. Express and Fastify have no counterpart here; the
/// ASP.NET Core host is both the standalone server and a piece of middleware, so each check runs once. Upstream's
/// <c>next()</c> fall-through to a custom 404 has no counterpart either: Astro's fallback endpoint answers every request
/// that no other endpoint takes.
/// </summary>
[Trait("Upstream", "node/node-middleware.test.ts")]
public class NodeMiddlewareTests(SiteHost sites)
{
    private const string Fixture = "node-middleware";

    [Fact(DisplayName = "when mode is standalone")]
    public async Task UnknownPaths_RenderThe404Page()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/error-page");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Page does not exist", (await response.ReadHtmlAsync()).Body!.TextContent);
    }

    [Fact(DisplayName = "should render the endpoint")]
    public async Task Endpoints_AreRendered()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/ssr");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Here's a random number", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "should render the index.html page [static]")]
    public async Task PrerenderedPages_AreServed()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("1", (await response.ReadHtmlAsync()).Body!.TextContent);
    }

    [Fact(DisplayName = "should render the index.html page [static] when the URL has the hash")]
    public async Task PrerenderedPages_AreServed_WhenTheUrlHasAHash()
    {
        var site = await sites.GetAsync(Fixture);

        // The client drops the fragment, as fetch() does, so the request is for '/'.
        using var response = await site.GetAsync("/#");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("1", (await response.ReadHtmlAsync()).Body!.TextContent);
    }

    [Theory(DisplayName = "should render the dynamic pages")]
    [InlineData("foo")]
    [InlineData("bar")]
    public async Task DynamicPages_AreRendered(string value)
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync($"/dyn/{value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(value, (await response.ReadHtmlAsync()).Body!.TextContent);
    }

    [Fact(DisplayName = "should fall through a prerendered dynamic route to an SSR fallback")]
    public async Task PrerenderedRoutes_FallThroughToTheOnDemandRoute()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/articles/unknown");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/articles/known-article", Assert.Single(response.HeaderValues("Location")));
    }

    // Regression test for https://github.com/withastro/astro/issues/16039: assets that the on-demand build emits
    // (CSS, fonts, images) must be served as files and not be answered by a catch-all route.
    [Fact(DisplayName = "should serve SSR-emitted CSS assets directly, not the catch-all page")]
    public async Task EmittedAssets_AreServedAsFiles_NotByTheCatchAllRoute()
    {
        var site = await sites.GetAsync("ssr-assets-middleware");

        var index = await site.GetHtmlAsync("/");
        var href = index.QuerySelector("link[rel=stylesheet]")?.GetAttribute("href");
        Assert.NotNull(href);
        Assert.Matches(@"/_astro/.*\.css$", href);

        using var response = await site.GetAsync(href);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/css", response.Content.Headers.ContentType?.MediaType);
    }
}
