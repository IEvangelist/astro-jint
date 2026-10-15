using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/routing/route-sanitization.test.ts</c>: a spread route at the root of a site with
/// <c>trailingSlash: 'never'</c> matches a path and keeps the name of its param (<c>testSlashTrim</c>, as it is written in the file name).
/// <para>
/// Differences from upstream: the route is injected into a real build (variant <see cref="AppMatch.NeverInTheFileFormat"/>, which
/// is shared with the resolved-pathname tests and so has <c>build.format: 'file'</c>, where upstream used 'directory') and the
/// request is answered by <c>match-info.astro</c>, which reports <c>Astro.routePattern</c> and the params. Upstream wrote the route
/// as <c>/[...testslashtrim]</c> in lower case, which is only a label there; the pattern here is <c>/[...testSlashTrim]</c>.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/routing/route-sanitization.test.ts")]
public class RouteSanitizationTests(SiteHost sites)
{
    [Fact(DisplayName = "should correctly match a route param with a trailing slash in its value")]
    public async Task SpreadRoute_KeepsTheNameOfItsParam()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.NeverInTheFileFormat);

        var match = await site.RequestMatchAsync("/a-route-param-with-leading-trailing-slash");

        Assert.Equal(200, match.Status);
        Assert.Equal("/[...testSlashTrim]", match.Pattern);
        Assert.Equal("""{"testSlashTrim":"a-route-param-with-leading-trailing-slash"}""", match.Params);
    }
}
