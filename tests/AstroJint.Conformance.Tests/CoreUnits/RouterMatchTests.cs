using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/routing/router-match.test.ts</c> (<c>Router.match</c>): which route answers a path, with
/// which params, under each <c>trailingSlash</c> policy, with a base and with <c>build.format: 'file'</c>.
/// <para>
/// Differences from upstream: <c>Router.match()</c> returns <c>match</c>, <c>none</c> or <c>redirect</c> for the table of one test.
/// Here the routes are injected into real builds (variants <see cref="AppMatch.Collision"/>, <see cref="AppMatch.Always"/>,
/// <see cref="AppMatch.NeverInTheFileFormat"/>, <see cref="AppMatch.AlwaysWithBase"/>, <see cref="AppMatch.FileFormatWithBase"/>,
/// <see cref="AppMatch.HtmlInRoutes"/> and <see cref="AppMatch.DevRouting"/>) and each request is answered by
/// <c>match-info.astro</c> or <c>match-endpoint.ts</c>, which report <c>Astro.routePattern</c> and the params. What the client
/// sees stands for the router's answer: the route (<c>match</c>), a 404 (<c>none</c>) or a 301 with a <c>Location</c>
/// (<c>redirect</c>). The tables are bigger than upstream's, so some tests have more competitors, and a few have a different base
/// or build format to share a variant: 'handles trailingSlash=never' runs in the file format, 'matches within base path and
/// rejects outside' and '[slug].html routes still work alongside buildFormat=file stripping' run with a base and in the file format,
/// and 'matches dynamic routes that contain .html in their params' runs with its endpoint at <c>/files/[path]</c>, so that
/// <c>/[slug].html</c> does not compete with it.
/// </para>
/// <para>
/// Where <c>App.render()</c> is not <c>Router.match()</c>: the app asks the router with <c>allowWithoutBase: true</c>, so a path
/// outside of the base is still matched ('matches within base path and rejects outside' asserts that <c>/about</c> is served with
/// the base <c>/blog</c>; only the router's default says 'outside-base'), and it collapses duplicate slashes before it matches, so
/// the router's redirect for <c>//foo/bar</c> is never reached: the app serves <c>/foo/bar</c> without redirecting. A run of slashes
/// that is the whole path (<c>////</c>) is redirected to <c>/</c> by the trailing slash handler, as upstream expects.
/// </para>
/// <para>
/// Not ported: the three tests of <c>Router.matchAll</c>, which return every route that matches a path, prerendered ones
/// included. <c>matchAll</c> is not called when a request is rendered: the host never serves a prerendered route, and the
/// priority tests above cover which route answers.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/routing/router-match.test.ts")]
public class RouterMatchTests(SiteHost sites)
{
    [Fact(DisplayName = "prefers static routes over dynamic routes")]
    public async Task StaticRoute_IsPreferredOverADynamicOne()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Collision);

        var match = await site.RequestMatchAsync("/about");

        Assert.Equal(200, match.Status);
        Assert.Equal("/about", match.Pattern);
        Assert.Equal("{}", match.Params);
    }

    [Fact(DisplayName = "matches dynamic routes and returns params")]
    public async Task DynamicRoute_IsMatched_WithItsParams()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Collision);

        var match = await site.RequestMatchAsync("/hello");

        Assert.Equal(200, match.Status);
        Assert.Equal("/[slug]", match.Pattern);
        Assert.Equal("""{"slug":"hello"}""", match.Params);
    }

    [Fact(DisplayName = "prefers single params over spread params")]
    public async Task SingleParam_IsPreferredOverASpreadParam()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Collision);

        var single = await site.RequestMatchAsync("/posts/one");
        var spread = await site.RequestMatchAsync("/posts/one/two");

        Assert.Equal(200, single.Status);
        Assert.Equal("/posts/[id]", single.Pattern);
        Assert.Equal("""{"id":"one"}""", single.Params);
        Assert.Equal(200, spread.Status);
        Assert.Equal("/posts/[...slug]", spread.Pattern);
        Assert.Equal("""{"slug":"one/two"}""", spread.Params);
    }

    [Fact(DisplayName = "prefers partially static dynamic segments over fully dynamic segments")]
    public async Task PartiallyStaticSegment_IsPreferredOverAFullyDynamicOne()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Collision);

        var match = await site.RequestMatchAsync("/game-1");

        Assert.Equal(200, match.Status);
        Assert.Equal("/game-[title]", match.Pattern);
        Assert.Equal("""{"title":"1"}""", match.Params);
    }

    [Fact(DisplayName = "handles trailingSlash=always")]
    public async Task TrailingSlashAlways_OnlyMatchesWithTheSlash()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Always);

        var withoutSlash = await site.SendRawAsync("/blog");
        var withSlash = await site.RequestMatchAsync("/blog/");

        Assert.Equal(301, withoutSlash.StatusCode);
        Assert.Equal("/blog/", withoutSlash.Header("Location"));
        Assert.Equal(200, withSlash.Status);
        Assert.Equal("/blog", withSlash.Pattern);
    }

    [Fact(DisplayName = "handles trailingSlash=never")]
    public async Task TrailingSlashNever_OnlyMatchesWithoutTheSlash()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.NeverInTheFileFormat);

        var withoutSlash = await site.RequestMatchAsync("/blog");
        var withSlash = await site.SendRawAsync("/blog/");

        Assert.Equal(200, withoutSlash.Status);
        Assert.Equal("/blog", withoutSlash.Pattern);
        Assert.Equal(301, withSlash.StatusCode);
        Assert.Equal("/blog", withSlash.Header("Location"));
    }

    [Fact(DisplayName = "handles trailingSlash=ignore")]
    public async Task TrailingSlashIgnore_MatchesWithAndWithoutTheSlash()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Collision);

        var withoutSlash = await site.RequestMatchAsync("/about");
        var withSlash = await site.RequestMatchAsync("/about/");

        Assert.Equal(200, withoutSlash.Status);
        Assert.Equal("/about", withoutSlash.Pattern);
        Assert.Equal(200, withSlash.Status);
        Assert.Equal("/about", withSlash.Pattern);
    }

    [Fact(DisplayName = "matches within base path and rejects outside")]
    public async Task BasePath_MatchesInside_AndTheAppAlsoServesAPathOutsideIt()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.FileFormatWithBase);

        var withoutSlash = await site.RequestMatchAsync("/blog");
        var withSlash = await site.RequestMatchAsync("/blog/");
        var outside = await site.RequestMatchAsync("/about");

        Assert.Equal(200, withoutSlash.Status);
        Assert.Equal("/", withoutSlash.Pattern);
        Assert.Equal(200, withSlash.Status);
        Assert.Equal("/", withSlash.Pattern);
        Assert.Equal(200, outside.Status);
        Assert.Equal("/about", outside.Pattern);
    }

    [Fact(DisplayName = "redirects base path when trailingSlash=always")]
    public async Task BasePath_IsRedirected_WhenTrailingSlashIsAlways()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.AlwaysWithBase);

        var response = await site.SendRawAsync("/blog");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/blog/", response.Header("Location"));
    }

    [Fact(DisplayName = "accepts .html paths when buildFormat=file")]
    public async Task HtmlPaths_AreAccepted_InTheFileFormat()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.FileFormatWithBase);

        var index = await site.RequestMatchAsync("/blog/index.html");
        var about = await site.RequestMatchAsync("/blog/about.html");

        Assert.Equal(200, index.Status);
        Assert.Equal("/", index.Pattern);
        Assert.Equal(200, about.Status);
        Assert.Equal("/about", about.Pattern);
    }

    [Fact(DisplayName = "matches dynamic routes that contain .html in their params")]
    public async Task DynamicRoute_KeepsHtmlInItsParams()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.HtmlInRoutes);

        var match = await site.RequestMatchAsync("/files/file.html");

        Assert.Equal(200, match.Status);
        Assert.Equal("/files/[path]", match.Pattern);
        Assert.Equal("""{"path":"file.html"}""", match.Params);
    }

    [Fact(DisplayName = "matches [slug].html routes and extracts slug without .html")]
    public async Task SlugHtmlRoute_ExtractsTheSlugWithoutHtml()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.HtmlInRoutes);

        var match = await site.RequestMatchAsync("/dummy.html");

        Assert.Equal(200, match.Status);
        Assert.Equal("/[slug].html", match.Pattern);
        Assert.Equal("""{"slug":"dummy"}""", match.Params);
    }

    [Fact(DisplayName = "[slug].html routes do not match non-.html paths")]
    public async Task SlugHtmlRoute_DoesNotMatchPathsWithoutHtml()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.HtmlInRoutes);

        var response = await site.SendRawAsync("/dummy");

        Assert.Equal(404, response.StatusCode);
    }

    [Fact(DisplayName = "[slug].html routes still work alongside buildFormat=file stripping")]
    public async Task SlugHtmlRoute_WorksAlongsideStrippingInTheFileFormat()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.FileFormatWithBase);

        var about = await site.RequestMatchAsync("/blog/about.html");
        var dynamic = await site.SendRawAsync("/blog/dummy.html");

        Assert.Equal(200, about.Status);
        Assert.Equal("/about", about.Pattern);
        Assert.Equal(404, dynamic.StatusCode);
    }

    [Fact(DisplayName = "redirects multiple leading slashes at root")]
    public async Task MultipleLeadingSlashes_AtTheRoot_AreRedirected()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.DevRouting);

        var response = await site.SendRawAsync("////");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/", response.Header("Location"));
    }

    [Fact(DisplayName = "redirects multiple leading slashes while preserving path")]
    public async Task MultipleLeadingSlashes_AreCollapsed_PreservingThePath()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.DevRouting);

        var match = await site.RequestMatchAsync("//foo/bar");

        Assert.Equal(200, match.Status);
        Assert.Equal("/foo/bar", match.Pattern);
    }
}
