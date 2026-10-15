using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/fetch/rewrite-handler.test.ts</c>: what a rewrite does to the request cycle
/// (<c>applyRewriteToState</c> and <c>executeRewrite</c>): the route, pathname, params and request of the target, the status,
/// the rewrite from a route rendered on demand to a prerendered one, and a target that matches no route.
/// <para>
/// Adapted: upstream calls the two functions with a <c>FetchState</c> it builds and inspects the state afterwards. Here the page
/// at <c>/source</c> calls <c>Astro.rewrite()</c> (which is <c>executeRewrite</c>, itself ending in <c>applyRewriteToState</c>)
/// and the target page reports what it sees (<c>route-info.astro</c> shows <c>Astro.routePattern</c>, <c>Astro.url.pathname</c>
/// and <c>Astro.params</c>), so each assertion about a field of the state is about the same thing as the page sees it. The
/// variants are those of <c>app-routing</c> (see <c>AppRouting</c>): the routes are real pages injected from <c>src/routes</c>.
/// </para>
/// <para>
/// Adapted: 'throws ForbiddenRewrite for SSR-to-prerender rewrite'. A built app has no module for a prerendered route in its
/// on-demand bundle (<c>virtual:astro:pages</c> holds the routes of the environment only), so <c>tryRewrite</c> stops before
/// <c>applyRewriteToState</c> could throw <c>ForbiddenRewrite</c>, with 'Unexpectedly unable to find a component instance for
/// route /static'. The request still ends in 500, which is what a rewrite from a page rendered on demand to a prerendered one has
/// to do, with every adapter; what the test shows is that result and not the name of the error.
/// </para>
/// <para>
/// Not applicable: 'sets isRewriting to true' (a flag of the state; its effects, that the Astro global is rebuilt for the new route
/// and that the request log marks the rewrite, are what the params, route pattern and URL checks of these tests and of
/// <c>RewriteAppTests</c> show), 'invalidates cached contexts after rewrite' (it looks at the <c>props</c>,
/// <c>actionApiContext</c> and <c>apiContext</c> fields, which hold no value a request can see; that the rewritten page gets
/// the context of its new route is what <c>RewriteAppTests</c> checks through <c>Astro.params</c>, <c>Astro.locals</c> and
/// <c>Astro.originPathname</c>) and 'allows SSR-to-prerender rewrite for i18n fallback routes' (the exception only changes the
/// validation in <c>applyRewriteToState</c>, which a test can reach with a made-up component instance. With a built app the
/// rewrite to a prerendered route, with or without fallback routes, ends in <c>tryRewrite</c> for the reason given above: 500
/// either way, so a request cannot tell the exception from the rule).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/fetch/rewrite-handler.test.ts")]
public class RewriteHandlerTests(SiteHost sites)
{
    private static readonly object Pages = new
    {
        routes = new[]
        {
            DataRoute("/source", "rewrite.astro", "/target"),
            Route("/target", "route-info.astro"),
            DataRoute("/", "rewrite.astro", "/blog/hello"),
            Route("/blog/[id]", "route-info.astro"),
            DataRoute("/to-nowhere", "rewrite.astro", "/nowhere"),
            DataRoute("/with-a-request", "rewrite.astro", new
            {
                request = "/target-headers",
                init = new { headers = new Dictionary<string, string> { ["x-added"] = "by the rewrite" } },
            }),
            DataRoute("/with-a-url", "rewrite.astro", new { url = "/target-headers" }),
            Route("/target-headers", "target-headers.astro"),
        },
    };

    private static readonly object ARouteThatIsA404 = new
    {
        routes = new[]
        {
            DataRoute("/404", "rewrite.astro", "/target"),
            Route("/target", "route-info.astro"),
        },
    };

    private static readonly object ToAPrerenderedPage = new
    {
        routes = new[]
        {
            DataRoute("/source", "rewrite.astro", "/static"),
            DataRoute("/static", "html.astro", "<h1>Static</h1>", prerender: true),
        },
    };

    // applyRewriteToState

    [Fact(DisplayName = "updates routeData to the rewrite target")]
    public async Task Rewrite_UpdatesTheRouteToTheTarget()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/source");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("/target", Text(response, "#pattern"));
    }

    [Fact(DisplayName = "updates pathname to the rewrite target")]
    public async Task Rewrite_UpdatesThePathnameToTheTarget()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/source");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("/target", Text(response, "#pathname"));
    }

    [Fact(DisplayName = "updates params from the new route")]
    public async Task Rewrite_UpdatesTheParamsFromTheNewRoute()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"id":"hello"}""", Text(response, "#params"));
    }

    [Fact(DisplayName = "resets status to 200")]
    public async Task Rewrite_ResetsTheStatusTo200()
    {
        var site = await sites.GetAsync(Fixture, ARouteThatIsA404);

        var response = await site.SendRawAsync("/404");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("/target", Text(response, "#pattern"));
    }

    [Fact(DisplayName = "uses the Request directly when payload is a Request")]
    public async Task Rewrite_WithARequest_UsesThatRequest()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var withARequest = await site.SendRawAsync("/with-a-request", headers: ("X-Original", "from the client"));
        var withAUrl = await site.SendRawAsync("/with-a-url", headers: ("X-Original", "from the client"));

        Assert.Equal(200, withARequest.StatusCode);
        Assert.Equal("", Text(withARequest, "#original"));
        Assert.Equal("by the rewrite", Text(withARequest, "#added"));
        // A URL is turned into a copy of the original request instead.
        Assert.Equal(200, withAUrl.StatusCode);
        Assert.Equal("from the client", Text(withAUrl, "#original"));
        Assert.Equal("", Text(withAUrl, "#added"));
    }

    [Fact(DisplayName = "throws ForbiddenRewrite for SSR-to-prerender rewrite")]
    public async Task Rewrite_FromAnOnDemandRouteToAPrerenderedOne_Fails()
    {
        var site = await sites.GetAsync(Fixture, ToAPrerenderedPage);

        var response = await site.SendRawAsync("/source");

        Assert.Equal(500, response.StatusCode);
    }

    // executeRewrite()

    [Fact(DisplayName = "resolves the rewrite target and renders the new page")]
    public async Task Rewrite_ResolvesTheTargetAndRendersTheNewPage()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/source");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Target", Text(response, "h1"));
    }

    [Fact(DisplayName = "updates state to reflect the rewritten route")]
    public async Task Rewrite_UpdatesTheStateToReflectTheRewrittenRoute()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/source");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("/target", Text(response, "#pattern"));
        Assert.Equal("/target", Text(response, "#pathname"));
    }

    [Fact(DisplayName = "resolves params for dynamic rewrite targets")]
    public async Task Rewrite_ResolvesParamsForDynamicTargets()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("/blog/[id]", Text(response, "#pattern"));
        Assert.Equal("""{"id":"hello"}""", Text(response, "#params"));
    }

    [Fact(DisplayName = "returns 404 when rewrite target does not match any route")]
    public async Task Rewrite_WhenTheTargetMatchesNoRoute_Returns404()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/to-nowhere");

        Assert.Equal(404, response.StatusCode);
    }
}
