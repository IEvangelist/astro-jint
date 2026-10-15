using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/routing/dev-routing.test.ts</c>: a request is matched against a table with the root, two
/// paths with a non-ASCII or a space character in them, a static route and a dynamic one.
/// <para>
/// Differences from upstream: the routes are injected into a real build (variant <see cref="AppMatch.DevRouting"/>, which has one
/// more route than upstream's table, <c>/foo/bar</c>, for the router-match tests) and each request is answered by
/// <c>match-info.astro</c>. Upstream gives <c>Router.match</c> the decoded text; here the characters are percent-encoded as a
/// browser sends them, so the test also puts Kestrel's and Astro's decoding in between. 'does not normalize internal multiple
/// slashes' gets a 404 here because <c>App.render()</c> collapses duplicate slashes before it matches (<c>/another///here</c> is
/// <c>/another/here</c>, which no route has). 'redirects multiple leading slashes' gets the redirect to <c>/</c> that upstream
/// expects, but from the trailing slash handler (it collapses a run of slashes at the end of a path, here the whole path) and not
/// from <c>Router.match</c>.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/routing/dev-routing.test.ts")]
public class DevRoutingTests(SiteHost sites)
{
    [Fact(DisplayName = "matches root path")]
    public async Task RootPath_IsMatched()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.DevRouting);

        var match = await site.RequestMatchAsync("/");

        Assert.Equal(200, match.Status);
        Assert.Equal("/", match.Pattern);
    }

    [Fact(DisplayName = "matches non-ASCII and space paths")]
    public async Task NonAsciiAndSpacePaths_AreMatched()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.DevRouting);

        var nonAscii = await site.RequestMatchAsync("/%E3%83%86%E3%82%B9%E3%83%88");
        var space = await site.RequestMatchAsync("/te%20st");

        Assert.Equal(200, nonAscii.Status);
        Assert.Equal("/\u30C6\u30B9\u30C8", nonAscii.Pattern);
        Assert.Equal(200, space.Status);
        Assert.Equal("/te st", space.Pattern);
    }

    [Fact(DisplayName = "matches static and dynamic routes")]
    public async Task StaticAndDynamicRoutes_AreMatched()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.DevRouting);

        var staticMatch = await site.RequestMatchAsync("/another");
        var dynamicMatch = await site.RequestMatchAsync("/1");

        Assert.Equal(200, staticMatch.Status);
        Assert.Equal("/another", staticMatch.Pattern);
        Assert.Equal(200, dynamicMatch.Status);
        Assert.Equal("/[id]", dynamicMatch.Pattern);
        Assert.Equal("""{"id":"1"}""", dynamicMatch.Params);
    }

    [Fact(DisplayName = "does not normalize internal multiple slashes")]
    public async Task InternalMultipleSlashes_AreNotMatched()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.DevRouting);

        var response = await site.SendRawAsync("/another///here");

        Assert.Equal(404, response.StatusCode);
    }

    [Fact(DisplayName = "redirects multiple leading slashes")]
    public async Task MultipleLeadingSlashes_AreRedirected()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.DevRouting);

        var response = await site.SendRawAsync("/////");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/", response.Header("Location"));
    }
}
