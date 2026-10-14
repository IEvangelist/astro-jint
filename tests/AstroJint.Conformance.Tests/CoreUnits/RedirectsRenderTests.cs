namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/redirects/render.test.ts</c>: the status and the <c>Location</c> of the redirect
/// routes, and how the params of the request are substituted into the destination.
/// <para>
/// Differences from upstream: the tests called <c>renderRedirect</c>, <c>computeRedirectStatus</c>, <c>resolveRedirectTarget</c>
/// and <c>redirectIsExternal</c> of <c>dist/core/redirects/render.js</c> with a mocked state; here the redirects are the
/// <c>redirects</c> entries of the <c>app-redirects</c> fixture and the values are read from the real response. Every
/// request is sent raw so that nothing follows the redirect.
/// </para>
/// <para>
/// <c>redirectIsExternal</c> returns a boolean that cannot be seen over HTTP. Its tests assert what it decides instead: an
/// external destination goes through <c>Response.redirect</c>, which turns it into an absolute URL (<c>http://example.com</c>
/// becomes <c>http://example.com/</c>), a relative one is sent as it is written (adapted). Of the explicit status of
/// a redirect object only the one of a destination that is a route of the project is honoured, which is why the tests of
/// the external and relative objects assert the <c>Location</c> and not the status.
/// </para>
/// <para>
/// <c>renderRedirect</c> applies <c>encodeURI</c> to what <c>resolveRedirectTarget</c> returns, so the backtick of the
/// replacement patterns test ends up as <c>%60</c> (adapted). <c>uses redirectRoute when available</c> sets
/// <c>trailingSlash: 'always'</c>: the destination is written <c>/target</c> in the configuration and the generator of the
/// route it leads to is the only one that adds the trailing slash (adapted).
/// </para>
/// <para>
/// Not applicable: <c>falls back to "/" when no redirect is defined</c> and <c>returns / when redirect is undefined</c>,
/// because a redirect route always comes from a <c>redirects</c> entry, whose destination the configuration schema requires.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/redirects/render.test.ts")]
public class RedirectsRenderTests(SiteHost sites)
{
    private const string Fixture = "app-redirects";

    private static void AssertRedirect(RawResponse response, int status, string location)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(location, response.Header("Location"));
    }

    private static void AssertLocation(RawResponse response, string location)
    {
        Assert.InRange(response.StatusCode, 300, 399);
        Assert.Equal(location, response.Header("Location"));
    }

    [Fact(DisplayName = "returns true for http:// URLs")]
    public async Task HttpDestination_IsExternal()
    {
        var site = await sites.GetAsync(Fixture);

        AssertLocation(await site.SendRawAsync("/external-http-root"), "http://example.com/");
        AssertLocation(await site.SendRawAsync("/external-http"), "http://example.com/path");
    }

    [Fact(DisplayName = "returns true for https:// URLs")]
    public async Task HttpsDestination_IsExternal()
    {
        var site = await sites.GetAsync(Fixture);

        AssertLocation(await site.SendRawAsync("/external"), "https://example.com/");
        AssertLocation(await site.SendRawAsync("/external-path"), "https://example.com/path");
    }

    [Fact(DisplayName = "returns false for relative URLs")]
    public async Task RelativeDestinations_AreNotExternal()
    {
        var site = await sites.GetAsync(Fixture);

        AssertLocation(await site.SendRawAsync("/relative-slash"), "/path");
        AssertLocation(await site.SendRawAsync("/relative-dot"), "./path");
        AssertLocation(await site.SendRawAsync("/relative-up"), "../path");
        AssertLocation(await site.SendRawAsync("/relative-bare"), "path");
    }

    [Fact(DisplayName = "handles redirect objects with external destinations")]
    public async Task ObjectWithExternalDestination_IsExternal()
    {
        var site = await sites.GetAsync(Fixture);

        AssertLocation(await site.SendRawAsync("/external-object-https"), "https://example.com/");
        AssertLocation(await site.SendRawAsync("/external-object-http"), "http://example.com/");
    }

    [Fact(DisplayName = "handles redirect objects with relative destinations")]
    public async Task ObjectWithRelativeDestination_IsNotExternal()
    {
        var site = await sites.GetAsync(Fixture);

        AssertLocation(await site.SendRawAsync("/object-relative-slash"), "/path");
        AssertLocation(await site.SendRawAsync("/object-relative-dot"), "./path");
    }

    [Fact(DisplayName = "returns 301 for GET requests")]
    public async Task Get_Redirects301()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/source");

        AssertRedirect(response, 301, "/target");
    }

    [Fact(DisplayName = "returns 308 for non-GET requests")]
    public async Task Post_Redirects308()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/source", "POST", []);

        AssertRedirect(response, 308, "/target");
    }

    [Fact(DisplayName = "handles redirect object with custom status")]
    public async Task ObjectWithStatus_UsesTheStatus()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/temp");

        Assert.Equal(302, response.StatusCode);
    }

    [Fact(DisplayName = "encodes URIs properly")]
    public async Task Destination_IsUriEncoded()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/spaces");

        Assert.Equal("/target%20with%20spaces", response.Header("Location"));
    }

    [Fact(DisplayName = "handles external redirects")]
    public async Task ExternalDestination_UsesResponseRedirect()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/external");

        // Response.redirect normalizes the URL, which adds the slash
        AssertRedirect(response, 301, "https://example.com/");
    }

    [Fact(DisplayName = "substitutes single dynamic parameter")]
    public async Task SingleParam_IsSubstituted()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/articles/my-post");

        Assert.Equal("/news/my-post", response.Header("Location"));
    }

    [Fact(DisplayName = "substitutes multiple dynamic parameters")]
    public async Task MultipleParams_AreSubstituted()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/multi/foo/bar");

        Assert.Equal("/moved/foo/bar", response.Header("Location"));
    }

    [Fact(DisplayName = "substitutes spread parameters")]
    public async Task SpreadParam_IsSubstituted()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/rest/a/b/c");

        Assert.Equal("/moved-rest/a/b/c", response.Header("Location"));
    }

    [Fact(DisplayName = "encodes special characters in parameters")]
    public async Task ParamWithSpecialCharacters_IsEncoded()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/city/Las%20Vegas%E2%80%99");

        Assert.Equal("/moved-city/Las%20Vegas%E2%80%99", response.Header("Location"));
    }

    [Fact(DisplayName = "uses redirectRoute when available")]
    public async Task DestinationRoute_GeneratesTheLocation()
    {
        var site = await sites.GetAsync(Fixture, new { trailingSlash = "always" });

        var response = await site.SendRawAsync("/source/");

        AssertRedirect(response, 301, "/target/");
    }

    [Fact(DisplayName = "returns 301 for GET without an explicit status")]
    public async Task Get_WithoutExplicitStatus_Is301()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/plain");

        AssertRedirect(response, 301, "/destination");
    }

    [Fact(DisplayName = "returns 308 for POST without an explicit status")]
    public async Task Post_WithoutExplicitStatus_Is308()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/plain", "POST", []);

        AssertRedirect(response, 308, "/destination");
    }

    [Fact(DisplayName = "returns the explicit status when redirectRoute is defined and redirect is an object")]
    public async Task ObjectToARoute_UsesTheExplicitStatus()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/temp");

        AssertRedirect(response, 302, "/target");
    }

    [Fact(DisplayName = "falls back to method-based status when redirect is a string even with redirectRoute")]
    public async Task StringToARoute_UsesTheMethodBasedStatus()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/source", "POST", []);

        Assert.Equal(308, response.StatusCode);
    }

    [Fact(DisplayName = "substitutes a simple param into the redirect string")]
    public async Task SimpleParam_IsSubstitutedIntoTheString()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/simple/hello");

        Assert.Equal("/hello/page", response.Header("Location"));
    }

    [Fact(DisplayName = "substitutes a spread param")]
    public async Task SpreadParam_IsSubstitutedIntoTheString()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/spread/a/b/c");

        Assert.Equal("/a/b/c", response.Header("Location"));
    }

    [Fact(DisplayName = "substitutes a param value containing replacement patterns literally")]
    public async Task ParamWithReplacementPatterns_IsSubstitutedLiterally()
    {
        var site = await sites.GetAsync(Fixture);

        Assert.Equal("/moved-dollar/a$&b", (await site.SendRawAsync("/dollar/a$&b")).Header("Location"));
        // renderRedirect encodes the target, which turns the backtick into %60
        Assert.Equal("/moved-dollar/x$%60y", (await site.SendRawAsync("/dollar/x$%60y")).Header("Location"));
        Assert.Equal("/moved-dollar/a$'b", (await site.SendRawAsync("/dollar/a$'b")).Header("Location"));
        Assert.Equal("/moved-dollar/x$&y/z", (await site.SendRawAsync("/dollar-rest/x$&y/z")).Header("Location"));
    }

    [Fact(DisplayName = "returns the string as-is when there are no params")]
    public async Task StringWithoutParams_IsReturnedAsIs()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/plain");

        Assert.Equal("/destination", response.Header("Location"));
    }

    [Fact(DisplayName = "returns the destination from an object redirect")]
    public async Task ObjectRedirect_UsesTheDestination()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/object-plain");

        Assert.Equal("/new-destination", response.Header("Location"));
    }

    [Fact(DisplayName = "returns an external URL unchanged")]
    public async Task ExternalUrl_IsReturnedUnchanged()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/external-page");

        Assert.Equal("https://example.com/page", response.Header("Location"));
    }
}
