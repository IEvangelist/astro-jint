namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/double-slash-bypass.test.ts</c>: a middleware that guards <c>/admin</c> and
/// <c>/dashboard</c> by the path prefix of <c>context.url.pathname</c> must also see the doubled-slash spellings of them.
/// <para>
/// Differences from upstream: the pages and the middleware are the real ones of the <c>app-pages</c> fixture, and the requests
/// go over a socket with the target as written, because <c>HttpClient</c> would parse <c>//admin</c> as a network-path
/// reference. The upstream <c>createApp()</c> built a fresh app for every test, the host serves all of them from one.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/double-slash-bypass.test.ts")]
public class DoubleSlashBypassTests(SiteHost sites)
{
    private const string Fixture = "app-pages";

    [Fact(DisplayName = "middleware blocks /admin with normal request")]
    public Task Middleware_BlocksANormalRequest() => AssertBlockedAsync("/admin");

    [Fact(DisplayName = "middleware blocks //admin (double-slash bypass attempt)")]
    public Task Middleware_BlocksDoubleSlash() => AssertBlockedAsync("//admin");

    [Fact(DisplayName = "middleware blocks ///admin (triple-slash bypass attempt)")]
    public Task Middleware_BlocksTripleSlash() => AssertBlockedAsync("///admin");

    [Fact(DisplayName = "middleware blocks //dashboard (double-slash on another protected route)")]
    public Task Middleware_BlocksDoubleSlashOnAnotherProtectedRoute() => AssertBlockedAsync("//dashboard");

    [Fact(DisplayName = "middleware blocks //admin/ (double-slash with trailing slash)")]
    public Task Middleware_BlocksDoubleSlashWithTrailingSlash() => AssertBlockedAsync("//admin/");

    [Fact(DisplayName = "public route is still accessible")]
    public async Task PublicRoute_IsStillAccessible()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("Public", response.Text);
    }

    private async Task AssertBlockedAsync(string target)
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync(target);

        Assert.Equal(403, response.StatusCode);
    }
}
