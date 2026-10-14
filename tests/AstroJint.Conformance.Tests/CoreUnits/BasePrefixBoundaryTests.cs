namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/base-prefix-boundary.test.ts</c>: with <c>base: '/app'</c> only <c>/app</c> and
/// the paths below <c>/app/</c> belong to the base, so <c>/appX/admin</c> must not be sliced down to the protected
/// <c>/admin</c> route.
/// <para>
/// Differences from upstream: the pages and the middleware are the real ones of the <c>app-pages</c> fixture, built with
/// <c>base: '/app'</c>. <c>app.match does not resolve /appX/admin to the protected route</c> reads the result of the internal
/// <c>App.match()</c>, which the host calls for every request and hands to <c>render()</c> as <c>routeData</c>: a miss is
/// the 404 page, so the test asserts that. <c>middleware blocks /appX/admin when the adapter supplies routeData</c> is the
/// same request as the one without it, because the host always supplies it; both tests send it.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/base-prefix-boundary.test.ts")]
public class BasePrefixBoundaryTests(SiteHost sites)
{
    private const string Fixture = "app-pages";

    private static readonly object Base = new { @base = "/app" };

    [Fact(DisplayName = "middleware blocks /app/admin with normal request")]
    public async Task Middleware_BlocksTheProtectedRoute()
    {
        var response = await RequestAsync("/app/admin");

        Assert.Equal(403, response.StatusCode);
    }

    [Fact(DisplayName = "middleware blocks /appX/admin (single-character prefix extension)")]
    public async Task Middleware_DoesNotLetASingleCharacterPrefixExtensionThrough()
    {
        var response = await RequestAsync("/appX/admin");

        Assert.NotEqual(200, response.StatusCode);
    }

    [Fact(DisplayName = "middleware blocks /app2/admin (digit prefix extension)")]
    public async Task Middleware_DoesNotLetADigitPrefixExtensionThrough()
    {
        var response = await RequestAsync("/app2/admin");

        Assert.NotEqual(200, response.StatusCode);
    }

    [Fact(DisplayName = "middleware blocks /app-/admin (punctuation prefix extension)")]
    public async Task Middleware_DoesNotLetAPunctuationPrefixExtensionThrough()
    {
        var response = await RequestAsync("/app-/admin");

        Assert.NotEqual(200, response.StatusCode);
    }

    [Fact(DisplayName = "app.match does not resolve /appX/admin to the protected route")]
    public async Task Match_DoesNotResolveAPrefixExtensionToTheProtectedRoute()
    {
        var response = await RequestAsync("/appX/admin");

        Assert.Equal(404, response.StatusCode);
        Assert.DoesNotContain("Admin Panel", response.Text);
    }

    [Fact(DisplayName = "middleware blocks /appX/admin when the adapter supplies routeData")]
    public async Task Middleware_DoesNotLetAPrefixExtensionThroughWhenTheHostSuppliesRouteData()
    {
        var response = await RequestAsync("/appX/admin");

        Assert.NotEqual(200, response.StatusCode);
        Assert.DoesNotContain("Admin Panel", response.Text);
    }

    private async Task<RawResponse> RequestAsync(string target)
    {
        var site = await sites.GetAsync(Fixture, Base);

        return await site.SendRawAsync(target);
    }
}
