using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/routing/resolved-pathname.test.ts</c>: a <c>.html</c> suffix on the request for a dynamic
/// route does not end up in its params when the build format is 'file', and the params of one request do not leak into another.
/// <para>
/// Differences from upstream: the two endpoints (<c>/api/[category]</c> and <c>/api/[category]/[id]</c>, with
/// <c>trailingSlash: 'never'</c>) are injected into a real build (variant <see cref="AppMatch.NeverInTheFileFormat"/>) and each
/// request is answered by <c>match-endpoint.ts</c>, which reports <c>Astro.routePattern</c>, the params and the names of their
/// keys. <c>Router.match()</c> strips <c>.html</c> before it matches and gets the params from the stripped path, which is what
/// upstream asserts. <c>App.render()</c> asks the same router for the route but then takes the params of the pathname that it
/// keeps, and it only strips <c>.html</c> from that for pages (<c>FetchState</c> keeps the suffix of an endpoint, because it is
/// 'user-provided'): the endpoints get <c>books.html</c>, <c>42.html</c> and <c>1.html</c> as the value of their last param,
/// and the tests assert that. The twin pages <c>/pages/[category]</c> and <c>/pages/[category]/[id]</c> show upstream's values
/// (<c>books</c>, <c>42</c>) for the same requests. The concurrency test sends the requests at the same time instead of calling the
/// stateless <c>Router.match</c> twice, which is what 'concurrent' means for a host that renders on several engines.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/routing/resolved-pathname.test.ts")]
public class ResolvedPathnameTests(SiteHost sites)
{
    private static readonly string[] ConcurrentTargets = ["/api/books/1.html", "/api/movies/99"];

    [Fact(DisplayName = "should resolve params correctly for .html requests to dynamic routes")]
    public async Task HtmlRequest_ToADynamicRoute_ResolvesItsParams()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.NeverInTheFileFormat);

        var endpoint = await site.RequestMatchAsync("/api/books.html");
        var page = await site.RequestMatchAsync("/pages/books.html");

        Assert.Equal(200, endpoint.Status);
        Assert.Equal("/api/[category]", endpoint.Pattern);
        Assert.Equal("""{"category":"books.html"}""", endpoint.Params);
        Assert.Equal("category", endpoint.Keys);
        Assert.Equal(200, page.Status);
        Assert.Equal("/pages/[category]", page.Pattern);
        Assert.Equal("""{"category":"books"}""", page.Params);
        Assert.Equal("category", page.Keys);
    }

    [Fact(DisplayName = "should resolve params correctly for .html requests to nested dynamic routes")]
    public async Task HtmlRequest_ToANestedDynamicRoute_ResolvesItsParams()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.NeverInTheFileFormat);

        var endpoint = await site.RequestMatchAsync("/api/books/42.html");
        var page = await site.RequestMatchAsync("/pages/books/42.html");

        Assert.Equal(200, endpoint.Status);
        Assert.Equal("/api/[category]/[id]", endpoint.Pattern);
        Assert.Equal("""{"category":"books","id":"42.html"}""", endpoint.Params);
        Assert.Equal(200, page.Status);
        Assert.Equal("/pages/[category]/[id]", page.Pattern);
        Assert.Equal("""{"category":"books","id":"42"}""", page.Params);
    }

    [Fact(DisplayName = "should not cross-contaminate resolved pathnames between concurrent requests")]
    public async Task ConcurrentRequests_DoNotShareParams()
    {
        var site = await sites.GetAsync(Fixture, AppMatch.NeverInTheFileFormat);

        var requests = Enumerable.Range(0, 8)
            .SelectMany(_ => ConcurrentTargets)
            .Select(site.RequestMatchAsync)
            .ToArray();
        var matches = await Task.WhenAll(requests);

        for (var index = 0; index < matches.Length; index += 2)
        {
            var books = matches[index];
            var movies = matches[index + 1];
            Assert.Equal("/api/[category]/[id]", books.Pattern);
            Assert.Equal("""{"category":"books","id":"1.html"}""", books.Params);
            Assert.Equal("/api/[category]/[id]", movies.Pattern);
            Assert.Equal("""{"category":"movies","id":"99"}""", movies.Params);
        }
    }
}
