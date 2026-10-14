namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/encoded-backslash-bypass.test.ts</c>: a middleware that guards
/// <c>/users/admin</c> by the pathname that it sees must also see the spellings of it that use an encoded backslash, which
/// the URL parser turns into a slash.
/// <para>
/// Differences from upstream: the dynamic route <c>users/[slug]</c>, the public page and the middleware are the real ones of
/// the <c>app-pages</c> fixture. The requests go over a socket with the target as written, because <c>HttpClient</c> would
/// normalise it.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/encoded-backslash-bypass.test.ts")]
public class EncodedBackslashBypassTests(SiteHost sites)
{
    private const string Fixture = "app-pages";

    [Fact(DisplayName = "middleware blocks /users/admin with normal request")]
    public Task Middleware_BlocksANormalRequest() => AssertStatusAsync("/users/admin", 403);

    [Fact(DisplayName = "middleware blocks /users/%5Cadmin (encoded backslash)")]
    public Task Middleware_BlocksAnEncodedBackslash() => AssertStatusAsync("/users/%5Cadmin", 403);

    [Fact(DisplayName = "middleware blocks /users/%5cadmin (lowercase hex encoded backslash)")]
    public Task Middleware_BlocksALowercaseHexEncodedBackslash() => AssertStatusAsync("/users/%5cadmin", 403);

    [Fact(DisplayName = "middleware blocks /users/%5C%5Cadmin (double encoded backslash)")]
    public Task Middleware_BlocksTwoEncodedBackslashes() => AssertStatusAsync("/users/%5C%5Cadmin", 403);

    [Fact(DisplayName = "public route is still accessible")]
    public Task PublicRoute_IsStillAccessible() => AssertStatusAsync("/", 200);

    [Fact(DisplayName = "non-protected dynamic route is still accessible")]
    public Task NonProtectedDynamicRoute_IsStillAccessible() => AssertStatusAsync("/users/john", 200);

    private async Task AssertStatusAsync(string target, int status)
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync(target);

        Assert.Equal(status, response.StatusCode);
    }
}
