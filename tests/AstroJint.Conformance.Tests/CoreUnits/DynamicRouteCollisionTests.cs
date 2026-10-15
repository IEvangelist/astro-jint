using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/routing/dynamic-route-collision.test.ts</c>: a static route beats a dynamic one, a static
/// route beats a spread one, and a dynamic route beats a spread one, for a path that all of them could match.
/// <para>
/// Differences from upstream: upstream builds a <c>Router</c> for each test from the two routes of the case. Here all the routes
/// are in one variant (<see cref="AppMatch.Collision"/>), which still has, for each path, the two competitors of the case as well
/// as others (for <c>/test</c> both <c>/[slug]</c> and <c>/[...slug]</c> compete with <c>/test</c>); the expected winners are the same.
/// Each request is answered by <c>match-info.astro</c>, which reports <c>Astro.routePattern</c> and the params.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/routing/dynamic-route-collision.test.ts")]
public class DynamicRouteCollisionTests(SiteHost sites)
{
    [Fact(DisplayName = "prefers static routes over dynamic routes")]
    public async Task StaticRoute_IsPreferredOverADynamicOne()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Collision);

        var match = await site.RequestMatchAsync("/about");

        Assert.Equal(200, match.Status);
        Assert.Equal("/about", match.Pattern);
    }

    [Fact(DisplayName = "prefers static nested index over spread routes")]
    public async Task StaticRoute_IsPreferredOverASpreadOne()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Collision);

        var match = await site.RequestMatchAsync("/test");

        Assert.Equal(200, match.Status);
        Assert.Equal("/test", match.Pattern);
    }

    [Fact(DisplayName = "prefers dynamic routes over spread routes for single segments")]
    public async Task DynamicRoute_IsPreferredOverASpreadOneForASingleSegment()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Collision);

        var match = await site.RequestMatchAsync("/blog");

        Assert.Equal(200, match.Status);
        Assert.Equal("/[slug]", match.Pattern);
        Assert.Equal("""{"slug":"blog"}""", match.Params);
    }
}
