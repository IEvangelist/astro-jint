using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/routing/rewrite-validation.test.ts</c>: Astro refuses to rewrite from a route rendered on
/// demand to a prerendered route (<c>ForbiddenRewrite</c>, answered with 500 and the custom 500 page), whether the page rewrites
/// with <c>Astro.rewrite()</c> or chained middleware rewrite with <c>context.rewrite()</c> after <c>sequence()</c>; a rewrite
/// between two routes rendered on demand works.
/// <para>
/// Differences from upstream: each group is a variant of the <c>app-routing</c> fixture (see <c>AppRouting</c>) whose routes are
/// injected as real pages. The prerendered target is a real prerendered route (<c>prerender: true</c>), so the build renders it
/// to a file, as upstream's 'serverLike' manifest assumes; the <c>/500</c> page shows 'ForbiddenRewrite' like upstream's. In a
/// built app the answer does not come from the <c>ForbiddenRewrite</c> check, though: the on-demand bundle has no module for a
/// prerendered route (<c>virtual:astro:pages</c> holds the routes of its environment only), so <c>tryRewrite</c> stops earlier with
/// 'Unexpectedly unable to find a component instance for route /prerendered'. The request ends in 500 with the custom 500 page
/// all the same, which is what upstream's test asserts too (its page is static text, so it cannot tell the error either). The
/// middleware of the second group is <c>src/middleware-library/rewrite-validation-sequence.ts</c>, the same
/// <c>sequence(first, second)</c> as upstream's, added to the site as its middleware.
/// </para>
/// <para>
/// Not applicable: 'succeeds when manifest.serverLike is false (static-only build)'. The test sets <c>serverLike: false</c> on the
/// manifest of its mock app but still rewrites from a page rendered on demand to another one, which is the scenario of the second
/// test. A real manifest has <c>serverLike: false</c> only when every route is prerendered (<c>core/routing/prerender.js</c>
/// turns the build into a server build as soon as one route is rendered on demand), and the host never renders a prerendered
/// route, so no request can reach that state; the rewrite itself is covered by the second test.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/routing/rewrite-validation.test.ts")]
public class RewriteValidationTests(SiteHost sites)
{
    private static readonly object Pages = new
    {
        routes = new[]
        {
            DataRoute("/ssr", "rewrite.astro", "/prerendered"),
            DataRoute("/prerendered", "html.astro", "<h1>Target</h1>", prerender: true),
            DataRoute("/500", "html.astro", "<h1>ForbiddenRewrite</h1>"),
            DataRoute("/source", "rewrite.astro", "/target"),
            DataRoute("/target", "html.astro", "<h1>Target</h1>"),
        },
    };

    private static readonly object WithSequence = new
    {
        routes = new[]
        {
            DataRoute("/ssr", "html.astro", "<h1>SSR</h1>"),
            DataRoute("/prerendered", "html.astro", "<h1>Target</h1>", prerender: true),
            DataRoute("/500", "html.astro", "<h1>ForbiddenRewrite</h1>"),
            DataRoute("/source", "html.astro", "<h1>Source</h1>"),
            DataRoute("/target", "html.astro", "<h1>Target</h1>"),
        },
        middleware = new[] { "rewrite-validation-sequence" },
    };

    // SSR-to-prerendered rewrite validation - via Astro.rewrite() (#executeRewrite)

    [Fact(DisplayName = "returns 500 when SSR source rewrites to a prerendered target")]
    public async Task AstroRewrite_ReturnsA500WhenTheSourceRewritesToAPrerenderedTarget()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/ssr");

        Assert.Equal(500, response.StatusCode);
        // The App renders the custom 500 page when an AstroError is thrown.
        Assert.Equal("ForbiddenRewrite", Text(response, "h1"));
    }

    [Fact(DisplayName = "succeeds when SSR source rewrites to an SSR target")]
    public async Task AstroRewrite_SucceedsWhenTheSourceRewritesToAnSsrTarget()
    {
        var site = await sites.GetAsync(Fixture, Pages);

        var response = await site.SendRawAsync("/source");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Target", Text(response, "h1"));
    }

    // SSR-to-prerendered rewrite validation - via sequence()

    [Fact(DisplayName = "returns 500 when chained middleware via sequence() rewrites from SSR to prerendered")]
    public async Task Sequence_ReturnsA500WhenChainedMiddlewareRewritesToAPrerenderedRoute()
    {
        var site = await sites.GetAsync(Fixture, WithSequence);

        var response = await site.SendRawAsync("/ssr");

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("ForbiddenRewrite", Text(response, "h1"));
    }

    [Fact(DisplayName = "succeeds when chained middleware via sequence() rewrites from SSR to SSR")]
    public async Task Sequence_SucceedsWhenChainedMiddlewareRewritesToAnSsrRoute()
    {
        var site = await sites.GetAsync(Fixture, WithSequence);

        var response = await site.SendRawAsync("/source");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Target", Text(response, "h1"));
    }
}
