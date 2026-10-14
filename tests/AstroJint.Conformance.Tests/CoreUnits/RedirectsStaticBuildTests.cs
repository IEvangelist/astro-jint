namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/redirects/static-build.test.ts</c>: redirects that a page or a middleware answers
/// with, and what the static build writes for the redirects of the config.
/// <para>
/// Differences from upstream: most tests call <c>renderPath</c> of the static build with a mocked prerenderer and read the
/// HTML it would write. That is a build step, and the adapter turns it off (<c>build.redirects: false</c>, so that a static
/// site's redirect does not become a meta-refresh page that answers 200); the host answers the config redirects at request
/// time with a body-less 3xx instead, which <c>redirects/render.test.ts</c> covers destination by destination, so those
/// thirteen tests are not applicable: the six <c>static redirects — meta refresh output</c> tests (HTML of the static
/// build), <c>returns null for a redirect route when build.redirects is false</c> (the adapter forces it, and every
/// other test here shows that the redirect is a 3xx and no page), the three <c>InvalidRedirectDestination</c> tests (they
/// check route-manifest validation that throws while the routes of a static build are created, not an HTTP response) and
/// the three <c>custom 3xx.astro output</c> tests (<c>experimental.redirectPage</c> is not in the installed 7.3.8 package,
/// see <c>redirects/redirect-page.test.ts</c>).
/// </para>
/// <para>
/// The five tests with an HTTP outcome run against the <c>app-redirects</c> fixture. The middleware redirect and the
/// <c>Astro.redirect()</c> page are ported. <c>uses relative URL in redirect HTML even when site is set</c> asserts the
/// <c>Location</c> of a config redirect instead of the meta refresh of its HTML (adapted), and <c>uses relative URL in
/// Location header even when site is set</c> sets <c>site</c> for real, which the upstream mock app does not (adapted).
/// </para>
/// <para>
/// Gap (GAP-UNITS-5): <c>Warns when used inside a component</c> is written in full and skipped. Under Node the page is
/// streamed, so a component that redirects after its parent has begun makes the body fail with <c>The response has already
/// been sent to the browser and cannot be altered.</c>; the host renders with <c>streaming: false</c>, where Astro's
/// <c>renderToString</c> ignores a <c>Response</c> that a component emits ("<c>renderToString</c> doesn't work with emitting
/// responses"), so the page is served as a 200 without that component and the redirect is lost without an error. The test
/// was run once without the skip to confirm that (200, not 500). The over-HTTP form of the upstream assertion is a 500 with
/// the error page and no <c>Location</c>.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/redirects/static-build.test.ts")]
public class RedirectsStaticBuildTests(SiteHost sites)
{
    private const string Fixture = "app-redirects";

    [Fact(DisplayName = "renders redirect HTML for a page that returns a redirect via middleware")]
    public async Task MiddlewareRedirect_IsAnswered()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/middleware-redirect/");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/test", response.Header("Location"));
    }

    [Fact(DisplayName = "uses relative URL in redirect HTML even when site is set")]
    public async Task ConfigRedirect_WithSite_StaysRelative()
    {
        var site = await sites.GetAsync(Fixture, new { site = "https://example.com" });

        var response = await site.SendRawAsync("/plain");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/destination", response.Header("Location"));
        Assert.DoesNotContain("https://example.com", response.Header("Location"));
    }

    [Fact(DisplayName = "renders redirect HTML for a page that calls Astro.redirect() even when build.redirects is false")]
    public async Task PageRedirect_IsAnswered()
    {
        // /secret calls Astro.redirect('/login') in frontmatter.
        // build.redirects=false suppresses config-level redirect routes but must NOT
        // suppress pages that explicitly return a redirect response via Astro.redirect().
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/secret/");

        Assert.Equal(302, response.StatusCode);
        Assert.Equal("/login", response.Header("Location"));
    }

    [Fact(DisplayName = "uses relative URL in Location header even when site is set")]
    public async Task PageRedirect_WithSite_StaysRelative()
    {
        // The site config should not cause redirect URLs to become absolute.
        var site = await sites.GetAsync(Fixture, new { site = "https://example.com" });

        var response = await site.SendRawAsync("/secret/");

        var location = response.Header("Location")!;
        Assert.DoesNotContain("https://example.com", location);
        Assert.Equal("/login", location);
    }

    [Fact(
        DisplayName = "Warns when used inside a component",
        Skip = "GAP-UNITS-5: the host renders with streaming:false, where Astro silently drops a Response written by a component (200 with the page minus that component) instead of failing with ResponseSentError")]
    public async Task ComponentRedirect_AfterTheParentStarted_IsAnError()
    {
        // A child component calls Astro.redirect() after the parent has already
        // started rendering HTML: the redirect cannot be applied any more.
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/late");

        Assert.Equal(500, response.StatusCode);
        Assert.Null(response.Header("Location"));
        Assert.DoesNotContain("Testing", response.Text);
        Assert.Contains("Server error", response.Text);
    }
}
