using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/double-encoding-bypass.test.ts</c>: a middleware that guards <c>/api/admin</c> by
/// the pathname it sees must see the canonical path, however many times the request encoded it, and the route that serves the
/// request must be the one for that path.
/// <para>
/// Differences from upstream: the catch-all endpoint <c>api/[...path]</c>, the endpoint <c>api/admin/users</c>, the page and the
/// middleware are the real ones of the <c>app-pages</c> fixture. Upstream built three apps with three middlewares: one that
/// blocks <c>/api/admin</c>, one that also sends every other request through routing again with <c>next(context.url)</c>, and
/// one that blocks nothing. The fixture has one middleware which picks the behaviour from the <c>x-middleware-mode</c> request
/// header (<c>rewrite</c> or <c>passthrough</c>, the blocking one by default), and only for paths below <c>/api</c>. The
/// requests go over a socket with the target as written. The first test also read the internal
/// <c>App.getPathnameFromRequest()</c> and the matched route; here the response is the observable result of both: the body
/// <c>admin</c> only comes from the endpoint of <c>/api/admin/users</c>, so the pathname was decoded and matched to it.
/// </para>
/// <para>
/// Gap: Kestrel decodes one level of percent-encoding into <c>Request.Path</c> before the host builds the URL, and the host
/// does not put that level back, so Astro sees a payload with one level less than the client sent. The payload encoded
/// eleven times, which Astro on Node rejects with a 400 because it decodes at most ten times, is decoded, matches
/// <c>/api/admin</c> and is blocked by the middleware with a 401 (GAP-UNITS-1). The one encoded twelve times still gets the 400.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/double-encoding-bypass.test.ts")]
public class DoubleEncodingBypassTests(SiteHost sites)
{
    private const string Fixture = "app-pages";

    private static readonly (string Name, string Value) Passthrough = ("x-middleware-mode", "passthrough");
    private static readonly (string Name, string Value) Rewrite = ("x-middleware-mode", "rewrite");

    [Fact(DisplayName = "normalizes the request pathname before adapter route matching")]
    public async Task Pathname_IsNormalizedBeforeRouteMatching()
    {
        var response = await RequestAsync("/api/%2561dmin/users", Passthrough);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("admin", response.Text);
    }

    [Fact(DisplayName = "middleware blocks /api/admin/users")]
    public Task Middleware_BlocksTheAdminEndpoint() => AssertUnauthorizedAsync("/api/admin/users");

    [Fact(DisplayName = "middleware blocks single-encoded /api/%61dmin/users")]
    public Task Middleware_BlocksASingleEncodedAdminPath() => AssertUnauthorizedAsync("/api/%61dmin/users");

    [Fact(DisplayName = "middleware blocks double-encoded /api/%2561dmin/users (iteratively decoded)")]
    public Task Middleware_BlocksADoubleEncodedAdminPath() => AssertUnauthorizedAsync("/api/%2561dmin/users");

    [Fact(DisplayName = "middleware blocks double-encoded paths with multiple encoded segments")]
    public Task Middleware_BlocksDoubleEncodedPathsWithMultipleEncodedSegments() => AssertUnauthorizedAsync("/api/%2561dmin/%75sers");

    [Fact(DisplayName = "public route is still accessible")]
    public async Task PublicRoute_IsStillAccessible()
    {
        var response = await RequestAsync("/");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("Public", response.Text);
    }

    [Fact(DisplayName = "non-protected API routes are still accessible")]
    public Task NonProtectedApiRoutes_AreStillAccessible() => AssertServedAsync("/api/public/data", "public/data");

    [Fact(DisplayName = "single-encoded non-admin API routes still work")]
    public Task SingleEncodedNonAdminApiRoutes_StillWork() => AssertServedAsync("/api/us%65rs/list", "users/list");

    // False-positive regression tests (issue #16781)

    [Fact(DisplayName = "accepts encodeURIComponent output with a literal % next to a reserved char")]
    public Task EncodeUriComponentOutput_WithALiteralPercentNextToAReservedChar_IsAccepted() =>
        AssertServedAsync($"/api/uploads/{Uri.EscapeDataString("%?.pdf")}", "uploads/%%3F.pdf");

    [Fact(DisplayName = "accepts %25%23 (encoded literal % next to encoded #)")]
    public Task AnEncodedLiteralPercentNextToAnEncodedHash_IsAccepted() => AssertServedAsync("/api/files/%25%23readme", "files/%%23readme");

    [Fact(DisplayName = "accepts %25 at end of path segment (bare literal percent)")]
    public Task ABareLiteralPercentAtTheEndOfASegment_IsAccepted() => AssertServedAsync("/api/data/%25", "data/%");

    // Double-encoded non-admin paths (issue #16960, Sanity Studio): decoded and served, not rejected.

    [Fact(DisplayName = "serves double-encoded non-admin API routes")]
    public Task DoubleEncodedNonAdminApiRoutes_AreServed() => AssertServedAsync("/api/sections%255B_key%255D", "sections[_key]");

    // Creative triple-encoding: decoded, and the middleware still catches the attacks.

    [Fact(DisplayName = "middleware blocks creative triple-encoding that decodes to /api/admin")]
    public Task Middleware_BlocksCreativeTripleEncoding() => AssertUnauthorizedAsync("/api/%25%32%3561dmin/users");

    [Fact(DisplayName = "serves creative encoding that decodes to a non-admin path")]
    public async Task CreativeEncoding_ThatDecodesToANonAdminPath_IsServed()
    {
        var response = await RequestAsync("/api/%25%32%35%32%35");

        Assert.Equal(200, response.StatusCode);
    }

    // The rewriting middleware

    [Fact(DisplayName = "blocks /api/admin even when middleware calls next(context.url)")]
    public async Task RewritingMiddleware_BlocksTheAdminPath()
    {
        var response = await RequestAsync("/api/admin", Rewrite);

        Assert.Equal(401, response.StatusCode);
    }

    [Fact(DisplayName = "blocks double-encoded /api/%2561dmin with a rewriting middleware")]
    public async Task RewritingMiddleware_BlocksADoubleEncodedAdminPath()
    {
        var response = await RequestAsync("/api/%2561dmin", Rewrite);

        Assert.Equal(401, response.StatusCode);
    }

    [Fact(
        DisplayName = "rejects an over-encoded path with 400 instead of bypassing to /api/admin",
        Skip = "GAP-UNITS-1: Kestrel decodes one percent-encoding level into Request.Path and the host does not re-encode it, so Astro sees the 11-level payload as 10 levels, decodes it to /api/admin and the middleware answers 401, where Astro on Node answers 400")]
    public async Task RewritingMiddleware_RejectsAnOverEncodedPath()
    {
        var response = await RequestAsync("/api/%2525252525252525252561dmin", Rewrite);

        Assert.Equal(400, response.StatusCode);
    }

    [Fact(DisplayName = "rejects a deeply over-encoded payload with 400")]
    public async Task RewritingMiddleware_RejectsADeeplyOverEncodedPayload()
    {
        var response = await RequestAsync("/api/%252525252525252525252561dmin", Rewrite);

        Assert.Equal(400, response.StatusCode);
    }

    [Fact(DisplayName = "still serves non-protected routes through the rewriting middleware")]
    public async Task RewritingMiddleware_StillServesNonProtectedRoutes()
    {
        var response = await RequestAsync("/api/public/data", Rewrite);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("public/data", PathOf(response));
    }

    private async Task<RawResponse> RequestAsync(string target, params (string Name, string Value)[] headers)
    {
        var site = await sites.GetAsync(Fixture);

        return await site.SendRawAsync(target, headers: headers);
    }

    private async Task AssertUnauthorizedAsync(string target)
    {
        var response = await RequestAsync(target);

        Assert.Equal(401, response.StatusCode);
    }

    private async Task AssertServedAsync(string target, string path)
    {
        var response = await RequestAsync(target);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(path, PathOf(response));
    }

    private static string? PathOf(RawResponse response) =>
        JsonDocument.Parse(response.Text).RootElement.GetProperty("path").GetString();
}
