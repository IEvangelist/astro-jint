namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/redirects/redirect-page.test.ts</c>: the redirects of the host never carry a body,
/// whatever the project has for a <c>3xx.astro</c>, and a response a middleware answers with is kept as it is.
/// <para>
/// Differences from upstream: the tests build an app whose environment opts into rendering <c>3xx.astro</c> as the body of
/// a redirect (<c>rendersRedirectPage</c>, the <c>experimental.redirectPage</c> of a later Astro version than the 7.3.8 that
/// is installed, and only the build environment opts in). That feature is not in the package, so the two tests that
/// need it (<c>renders the custom page as the redirect body with props</c> and <c>uses a 2 second delay for 302 redirects</c>)
/// are not applicable: a project has no way to turn it on here, and the redirects of the host are answered at request time.
/// </para>
/// <para>
/// The other four are the cases without that opt-in and stay bodiless: the project without a <c>3xx.astro</c> (the
/// <c>app-errors</c> fixture with a redirect added by configuration, ported), the <c>3xx.astro</c> of <c>app-redirects</c>
/// that throws as soon as it is rendered and so proves that it never is (the test of the environment that does not opt in
/// is adapted to it, the one of the page that throws is ported), and the middleware that answers a request with a redirect of its
/// own (adapted: upstream also saw the page rendered as the body of that redirect, the 7.3.8 response has no body, and the
/// middleware call count is not observable).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/redirects/redirect-page.test.ts")]
public class RedirectPageTests(SiteHost sites)
{
    [Fact(DisplayName = "leaves the response bodiless when the project has no 3xx.astro")]
    public async Task ProjectWithout3xxPage_RedirectHasNoBody()
    {
        var site = await sites.GetAsync("app-errors", new { redirects = new Dictionary<string, string> { ["/old"] = "/new" } });

        var response = await site.SendRawAsync("/old");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/new", response.Header("Location"));
        Assert.Empty(response.Body);
    }

    [Fact(DisplayName = "leaves the response bodiless when the environment does not opt in")]
    public async Task ProjectWith3xxPage_RedirectHasNoBody()
    {
        var site = await sites.GetAsync("app-redirects");

        var response = await site.SendRawAsync("/source");

        Assert.Equal(301, response.StatusCode);
        Assert.Empty(response.Body);
    }

    [Fact(DisplayName = "skips middleware during the internal render and preserves the redirect response")]
    public async Task MiddlewareRedirect_IsPreserved()
    {
        var site = await sites.GetAsync("app-redirects");

        var response = await site.SendRawAsync("/mw-redirect");

        Assert.Equal(307, response.StatusCode);
        Assert.Equal("/new", response.Header("Location"));
        Assert.Equal("text/plain", response.Header("Content-Type"));
        Assert.Equal("preserved", response.Header("X-Original-Response"));
        Assert.Empty(response.Body);
    }

    [Fact(DisplayName = "falls back to a bodiless redirect when 3xx.astro throws")]
    public async Task Throwing3xxPage_RedirectHasNoBody()
    {
        var site = await sites.GetAsync("app-redirects");

        var response = await site.SendRawAsync("/source");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/target", response.Header("Location"));
        Assert.Empty(response.Body);
    }
}
