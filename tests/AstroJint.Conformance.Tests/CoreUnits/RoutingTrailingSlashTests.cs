using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/routing/trailing-slash.test.ts</c>: which requests an endpoint answers under
/// <c>trailingSlash: 'always'</c> (also with file extensions: <c>/dot.json</c> never takes a slash) and under 'never' with a base.
/// The tests keep the title of the describe they are in ('always', 'never' with base '/base', 'never' with base '/mybase') in
/// the order of the file.
/// <para>
/// Differences from upstream: the routes are the same endpoints, injected into real builds (variants <see cref="AppMatch.Always"/>,
/// <see cref="AppMatch.NeverWithBase"/> and <see cref="AppMatch.NeverWithMyBase"/>) and answered by <c>match-endpoint.ts</c>, which
/// reports <c>Astro.routePattern</c>, the params and the pathname it saw. <c>Router.match</c> says 'not a match' for a request
/// that the trailing slash policy rejects, and <c>App.render()</c> turns that into what the client sees: a 301 to the path with the
/// slash added (or removed) for a path that can be fixed, a 404 for <c>/dot.json/</c>. <c>match.pathname</c> is the pathname
/// without the base, which an endpoint cannot see (<c>url.pathname</c> has the base), so those assertions use the pathname with
/// the base. 'should match root path with query params' and 'should match sub path with query params' ask for the path with the
/// query that their titles and comments talk about (upstream's code asks without it, as the Router never sees the query).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/routing/trailing-slash.test.ts")]
public class RoutingTrailingSlashTests(SiteHost sites)
{
    // trailingSlash: 'always'

    [Fact(DisplayName = "should match the API route when request has a trailing slash")]
    public async Task Always_MatchesTheApiRoute_WithATrailingSlash()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Always);

        var match = await site.RequestMatchAsync("/api/");

        Assert.Equal(200, match.Status);
        Assert.Equal("/api", match.Pattern);
    }

    [Fact(DisplayName = "should NOT match the API route when request lacks a trailing slash")]
    public async Task Always_DoesNotMatchTheApiRoute_WithoutATrailingSlash()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Always);

        var response = await site.SendRawAsync("/api");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/api/", response.Header("Location"));
    }

    [Fact(DisplayName = "should match an injected route when request has a trailing slash")]
    public async Task Always_MatchesAnInjectedRoute_WithATrailingSlash()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Always);

        var match = await site.RequestMatchAsync("/injected/");

        Assert.Equal(200, match.Status);
        Assert.Equal("/injected", match.Pattern);
    }

    [Fact(DisplayName = "should NOT match an injected route when request lacks a trailing slash")]
    public async Task Always_DoesNotMatchAnInjectedRoute_WithoutATrailingSlash()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Always);

        var response = await site.SendRawAsync("/injected");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/injected/", response.Header("Location"));
    }

    [Fact(DisplayName = "should match an injected route when request has a file extension and no slash")]
    public async Task Always_MatchesAnInjectedRoute_WithAFileExtensionAndNoSlash()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Always);

        var match = await site.RequestMatchAsync("/injected.json");

        Assert.Equal(200, match.Status);
        Assert.Equal("/injected.json", match.Pattern);
    }

    [Fact(DisplayName = "should NOT match the API route when request has a trailing slash, with a file extension")]
    public async Task Always_DoesNotMatchTheApiRoute_WithATrailingSlashAndAFileExtension()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Always);

        var response = await site.SendRawAsync("/dot.json/");

        Assert.Equal(404, response.StatusCode);
    }

    [Fact(DisplayName = "should also match the API route when request lacks a trailing slash, with a file extension")]
    public async Task Always_MatchesTheApiRoute_WithoutATrailingSlashAndWithAFileExtension()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Always);

        var match = await site.RequestMatchAsync("/dot.json");

        Assert.Equal(200, match.Status);
        Assert.Equal("/dot.json", match.Pattern);
    }

    // trailingSlash: 'never' with base: '/base'

    [Fact(DisplayName = "should not have trailing slash on root path when base is set and trailingSlash is never")]
    public async Task NeverWithBase_RootPath_HasNoTrailingSlash()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.NeverWithBase);

        var match = await site.RequestMatchAsync("/base");

        Assert.Equal(200, match.Status);
        Assert.Equal("/", match.Pattern);
    }

    [Fact(DisplayName = "should not match root path with trailing slash when base is set and trailingSlash is never")]
    public async Task NeverWithBase_RootPathWithATrailingSlash_IsRedirected()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.NeverWithBase);

        var response = await site.SendRawAsync("/base/");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/base", response.Header("Location"));
    }

    [Fact(DisplayName = "should match root path with query params when base is set and trailingSlash is never")]
    public async Task NeverWithBase_RootPathWithAQuery_IsMatched()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.NeverWithBase);

        var match = await site.RequestMatchAsync("/base?foo=bar");

        Assert.Equal(200, match.Status);
        Assert.Equal("/", match.Pattern);
    }

    [Fact(DisplayName = "should match sub path with query params when base is set and trailingSlash is never")]
    public async Task NeverWithBase_SubPathWithAQuery_IsMatched()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.NeverWithBase);

        var match = await site.RequestMatchAsync("/base/injected?foo=bar");

        Assert.Equal(200, match.Status);
        Assert.Equal("/injected", match.Pattern);
    }

    [Fact(DisplayName = "should match pathname route under base")]
    public async Task NeverWithBase_PathnameRoute_IsMatched()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.NeverWithBase);

        var match = await site.RequestMatchAsync("/base/pathname");

        Assert.Equal(200, match.Status);
        Assert.Equal("/pathname", match.Pattern);
        Assert.Equal("/base/pathname", match.Pathname);
    }

    [Fact(DisplayName = "should match subpage route under base")]
    public async Task NeverWithBase_SubpageRoute_IsMatched()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.NeverWithBase);

        var match = await site.RequestMatchAsync("/base/subpage");

        Assert.Equal(200, match.Status);
        Assert.Equal("/subpage", match.Pattern);
        Assert.Equal("/base/subpage", match.Pathname);
    }

    // trailingSlash: 'never' with base: '/mybase' (issue #13736)

    [Fact(DisplayName = "should match root path without trailing slash when base is set and trailingSlash is never")]
    public async Task NeverWithMyBase_RootPath_IsMatchedWithoutATrailingSlash()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.NeverWithMyBase);

        var match = await site.RequestMatchAsync("/mybase");

        Assert.Equal(200, match.Status);
        Assert.Equal("/", match.Pattern);
        Assert.Equal("/mybase", match.Pathname);
    }
}
