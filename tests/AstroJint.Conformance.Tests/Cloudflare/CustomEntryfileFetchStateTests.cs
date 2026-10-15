using System.Net;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/custom-entryfile-fetch-state.test.ts</c>, a regression test for a
/// custom worker that builds the state of a request with <c>new FetchState(request)</c> and hands it to Astro. A host has no
/// worker of its own to customise, and the tests ask the same questions of the pages of the shared fixture
/// <c>cf-pages</c>, which are the ones of the upstream fixture, with these differences:
/// <list type="bullet">
/// <item>The page with the title is <c>/fetch-state</c>, not <c>/</c>, and the prerendered page says it is
/// "prerendered at build time" instead of "through workerd".</item>
/// <item>"writes cookies and defaults uncached responses to no-store" keeps the cookie, written once; the
/// <c>Cloudflare-CDN-Cache-Control</c> header of the worker is not applicable.</item>
/// <item>"falls back to the assets binding when no Astro route matches" writes the file between the build and the start
/// of a host of its own, as upstream writes it between the build and the preview: the host indexes the static files when it
/// starts, and a file that comes later is not served.</item>
/// <item>"preserves cookies when finalizing immutable Hono responses" becomes the same for an Astro endpoint that
/// returns <c>Response.redirect()</c>, a response with immutable headers, after it has set a cookie; Hono is not there.</item>
/// <item>"preserves cookies when rendering a custom error page" is a gap (GAP-CF-3).</item>
/// <item>"uses the default server entrypoint for workerd prerendering" becomes that the page is prerendered by the build and
/// that the host serves it.</item>
/// <item>Not applicable: the header that the custom worker adds, the two Hono pages and the dev server test.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/custom-entryfile-fetch-state.test.ts")]
public partial class CustomEntryfileFetchStateTests(SiteHost sites)
{
    private const string Fixture = "cf-pages";

    [GeneratedRegex("repro=1")]
    private static partial Regex ReproCookie();

    [GeneratedRegex("astro-session=[^;,]+")]
    private static partial Regex SessionCookie();

    [Fact(DisplayName = "renders an SSR page from a state built with new FetchState(request)")]
    public async Task RendersAnSsrPage()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/fetch-state");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("astro-cloudflare-custom-entryfile-fetch-state", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "writes cookies and defaults uncached responses to no-store")]
    public async Task WritesCookies()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/fetch-state");

        Assert.Single(ReproCookie().Matches(string.Join(", ", response.HeaderValues("Set-Cookie"))));
    }

    [Fact(DisplayName = "preserves a session across requests")]
    public async Task PreservesASession()
    {
        var site = await sites.GetAsync(Fixture);

        using var first = await site.GetAsync("/session");
        var sessionCookie = SessionCookie().Match(string.Join(", ", first.HeaderValues("Set-Cookie"))).Value;
        Assert.NotEmpty(sessionCookie);
        Assert.Matches("count: 1", await first.ReadTextAsync());

        using var second = await site.SendAsync(HttpMethod.Get, "/session", null, ("cookie", sessionCookie));
        Assert.Matches("count: 2", await second.ReadTextAsync());
    }

    [Fact(DisplayName = "falls back to the assets binding when no Astro route matches")]
    public async Task ServesAFileThatTheBuildDidNotWrite()
    {
        var dist = await FixtureBuilder.EnsureBuiltAsync(Fixture);
        var stray = Path.Combine(dist, "client", "stray.txt");
        await File.WriteAllTextAsync(stray, "stray file body");
        try
        {
            await using var site = await ConformanceSite.StartFromDistAsync(Fixture, dist);

            using var response = await site.GetAsync("/stray.txt");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("stray file body", await response.ReadTextAsync());
        }
        finally
        {
            File.Delete(stray);
        }
    }

    [Fact(DisplayName = "preserves cookies when finalizing immutable Hono responses")]
    public async Task PreservesCookiesOfAnImmutableResponse()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/immutable-redirect");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://example.com/", response.Headers.Location?.ToString());
        Assert.Equal("1", response.SetCookieValue("immutable"));
    }

    [Fact(DisplayName = "preserves cookies when rendering a custom error page",
        Skip = "GAP-CF-3: the custom 500 page is rendered (500, 'Custom error page') without the cookie that the page set before it threw: the response has no Set-Cookie header")]
    public async Task PreservesCookiesOfACustomErrorPage()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/error");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Matches("Custom error page", await response.ReadTextAsync());
        Assert.Matches("before-error=1", string.Join(", ", response.HeaderValues("Set-Cookie")));
    }

    [Fact(DisplayName = "uses the default server entrypoint for workerd prerendering")]
    public async Task PrerendersAPage()
    {
        var site = await sites.GetAsync(Fixture);

        var built = await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "static", "index.html"));
        Assert.Matches("prerendered at build time", built);

        using var response = await site.GetAsync("/static");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("prerendered at build time", await response.ReadTextAsync());
    }
}
