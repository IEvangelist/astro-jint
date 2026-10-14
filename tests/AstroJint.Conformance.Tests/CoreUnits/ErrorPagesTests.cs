using System.Text.RegularExpressions;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/error-pages.test.ts</c>: which page <c>App.render()</c> answers with when an
/// endpoint fails, has no handler for the method, a route does not match, a matching route returns a 404 or a page throws.
/// <para>
/// Differences from upstream: the routes come from the <c>app-pages</c> fixture, which has a custom 404 page with a stylesheet
/// and a 500 page, both rendered per request, and from <c>app-errors</c>, whose 500 page is prerendered. Each test hits a
/// route of the fixture instead of the inline <c>[...slug]</c> or <c>/any</c> ones: <c>/errors/endpoint-500</c> answers every
/// method with <c>oops</c> and a 500, <c>/api/route</c> only answers POST. The tests that render with and without
/// <c>routeData</c> are one request each, which the host always makes with the route it matched, so they are two ports of the
/// same request, and so are the two PUT tests. The header check of the first test is case-insensitive (the upstream
/// <c>startsWith('X-Astro-')</c> never matches the lowercase names of <c>Headers.keys()</c>). The stylesheet of the 404 page
/// is a <c>/_astro/*.css</c> file of the build instead of the <c>/main.css</c> that the upstream manifest names.
/// </para>
/// <para>
/// The last two untrusted-host tests spied on the URL given to <c>prerenderedErrorPageFetch</c>. The host passes a function
/// of its own that reads the prerendered page from the build folder, so the URL is not observable, and these tests check what
/// the visitor gets: the status 500 and the prerendered page, with a Host header that the configuration does not trust and
/// with one that <c>security.allowedDomains</c> allows. <c>PrerenderedErrorPageFetchTests</c> in the node folder has the
/// tests of the host side.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/error-pages.test.ts")]
public partial class ErrorPagesTests(SiteHost sites)
{
    private const string Pages = "app-pages";
    private const string Errors = "app-errors";

    private const string NotFoundText = "Something went horribly wrong!";

    private static readonly object AlwaysTrailingSlash = new { trailingSlash = "always" };

    private static readonly object AllowedDomain = new
    {
        security = new { allowedDomains = new[] { new { hostname = "myapp.com" } } },
    };

    [GeneratedRegex(@"<link[^>]*href=""/_astro/[^""]+\.css""")]
    private static partial Regex StylesheetLink();

    [Fact(DisplayName = "preserves headers and body for 500 responses from routes")]
    public async Task EndpointWith500_KeepsItsHeadersAndBody()
    {
        var site = await sites.GetAsync(Pages);

        var response = await site.SendRawAsync("/errors/endpoint-500");

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("1234", response.Header("x-debug"));
        Assert.DoesNotContain(response.Headers, header => header.Key.StartsWith("x-astro-", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("oops", response.Text);
    }

    [Fact(DisplayName = "renders the 404 page when an API route lacks a handler for the request method")]
    public Task ApiRouteWithoutAHandler_RendersThe404Page() => AssertNotFoundPageAsync("/api/route", method: "PUT");

    [Fact(DisplayName = "renders the 404 page when a route does not match")]
    public Task UnmatchedRoute_RendersThe404Page() => AssertNotFoundPageAsync("/some/fake/route");

    [Fact(DisplayName = "renders the 404 page when a route does not match and routeData is provided")]
    public Task UnmatchedRoute_RendersThe404PageWhenTheHostSuppliesRouteData() => AssertNotFoundPageAsync("/some/fake/route");

    [Fact(DisplayName = "renders the 404 page with imports when a matching route returns 404")]
    public async Task MatchingRouteThatReturns404_RendersThe404PageWithItsStyles()
    {
        var site = await sites.GetAsync(Pages);

        var response = await site.SendRawAsync("/blog/fake/route");

        Assert.Equal(404, response.StatusCode);
        Assert.Contains(NotFoundText, response.Text);
        Assert.Matches(StylesheetLink(), response.Text);
    }

    [Fact(DisplayName = "renders the 500 page when a route throws an error")]
    public async Task ThrowingRoute_RendersThe500Page()
    {
        var site = await sites.GetAsync(Pages);

        var response = await site.SendRawAsync("/causes-error");

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("This is an error page", response.Text);
    }

    [Fact(DisplayName = "renders the 404 page when an API route lacks a handler in production")]
    public Task ApiRouteWithoutAHandler_RendersThe404PageWithoutRouteData() => AssertNotFoundPageAsync("/api/route", method: "PUT");

    [Fact(DisplayName = "renders the 404 page when a route does not match with trailingSlash always")]
    public Task UnmatchedRoute_RendersThe404PageWithTrailingSlashAlways() => AssertNotFoundPageAsync("/ajksalscla/", AlwaysTrailingSlash);

    [Fact(DisplayName = "does not use an untrusted Host header origin for prerendered error page fetch")]
    public async Task UntrustedHost_GetsThePrerenderedErrorPageFromTheBuild()
    {
        var site = await sites.GetAsync(Errors);

        var response = await site.SendRawAsync("/causes-error", headers: ("Host", "evil.attacker:9999"));

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("Prerendered 500", response.Text);
        Assert.DoesNotContain("evil.attacker", response.Text);
    }

    [Fact(DisplayName = "uses validated Host header origin for prerendered error page fetch when allowedDomains matches")]
    public async Task AllowedHost_GetsThePrerenderedErrorPageFromTheBuild()
    {
        var site = await sites.GetAsync(Errors, AllowedDomain);

        var response = await site.SendRawAsync("/causes-error", headers: ("Host", "myapp.com"));

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("Prerendered 500", response.Text);
    }

    [Fact(DisplayName = "renders the 404 page when a route does not match with trailingSlash always and routeData")]
    public Task UnmatchedRoute_RendersThe404PageWithTrailingSlashAlwaysWhenTheHostSuppliesRouteData() =>
        AssertNotFoundPageAsync("/ajksalscla/", AlwaysTrailingSlash);

    private async Task AssertNotFoundPageAsync(string target, object? config = null, string method = "GET")
    {
        var site = await sites.GetAsync(Pages, config);

        var response = await site.SendRawAsync(target, method, method == "GET" ? null : []);

        Assert.Equal(404, response.StatusCode);
        Assert.Contains(NotFoundText, response.Text);
    }
}
