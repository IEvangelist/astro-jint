using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/routing/virtual-routes.test.ts</c>: a route that an integration injects, with no file in
/// <c>src/pages</c>, is matched.
/// <para>
/// Differences from upstream: the route is injected with <c>injectRoute()</c> from the config of the variant, as every route of
/// the <c>app-routing</c> fixture, so it really has <c>origin: 'external'</c>; that is not observable over HTTP, so the test asserts
/// that the request is answered by the route. It shares its variant (<see cref="AppMatch.Priority"/>) with the routing-priority tests.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/routing/virtual-routes.test.ts")]
public class VirtualRoutesTests(SiteHost sites)
{
    [Fact(DisplayName = "matches injected virtual routes")]
    public async Task InjectedVirtualRoute_IsMatched()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Priority);

        var match = await site.RequestMatchAsync("/virtual");

        Assert.Equal(200, match.Status);
        Assert.Equal("/virtual", match.Pattern);
    }
}
