namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/redirects/open-redirect.test.ts</c>: a redirect never sends a protocol-relative
/// <c>Location</c> (<c>//evil.com</c>), neither because its destination is one nor because a catch-all param that starts
/// with a slash is substituted into the destination.
/// <para>
/// Differences from upstream: the tests called <c>redirectIsExternal</c> and <c>getRouteGenerator</c> directly; here the
/// redirects are the <c>redirects</c> entries of the <c>app-redirects</c> fixture and the requests are sent raw. The
/// <c>getRouteGenerator</c> tests are the destinations that are catch-all routes of the fixture (<c>/[...slug]</c> and
/// <c>/prefix/[...slug]</c>) requested with the double slash that makes the param start with one, and they assert what the
/// upstream tests assert (the <c>Location</c> does not start with <c>//</c>) plus the path that comes out.
/// </para>
/// <para>
/// <c>redirectIsExternal</c> returns a boolean that cannot be seen over HTTP, so its tests assert what it decides: an
/// external destination goes through <c>Response.redirect</c>, which gives an absolute URL (<c>http://evil.com</c> becomes
/// <c>http://evil.com/</c>) and throws for a protocol-relative one it cannot parse, hence the 500 with no <c>Location</c> at
/// all (the error page of the fixture) in the tests of <c>//evil.com</c>, <c>//evil.com/path</c> and the object form, the
/// same as under Node (adapted). A destination that is not external is sent as it is written.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/redirects/open-redirect.test.ts")]
public class OpenRedirectTests(SiteHost sites)
{
    private const string Fixture = "app-redirects";

    private static void AssertNotProtocolRelative(RawResponse response, string location)
    {
        Assert.Equal(301, response.StatusCode);
        var actual = response.Header("Location");
        Assert.NotNull(actual);
        Assert.False(
            actual.StartsWith("//", StringComparison.Ordinal),
            $"Expected result to not start with '//', got '{actual}'");
        Assert.Equal(location, actual);
    }

    private static void AssertRefused(RawResponse response)
    {
        Assert.Equal(500, response.StatusCode);
        Assert.Null(response.Header("Location"));
        Assert.Contains("Server error", response.Text);
    }

    [Fact(DisplayName = "detects http:// as external")]
    public async Task HttpDestination_IsExternal()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/evil-http");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("http://evil.com/", response.Header("Location"));
    }

    [Fact(DisplayName = "detects https:// as external")]
    public async Task HttpsDestination_IsExternal()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/evil-https");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("https://evil.com/", response.Header("Location"));
    }

    [Fact(DisplayName = "detects protocol-relative //evil.com as external")]
    public async Task ProtocolRelativeDestination_IsExternal()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/protocol-relative");

        AssertRefused(response);
    }

    [Fact(DisplayName = "detects protocol-relative //evil.com/path as external")]
    public async Task ProtocolRelativeDestinationWithPath_IsExternal()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/protocol-relative-path");

        AssertRefused(response);
    }

    [Fact(DisplayName = "does not flag normal paths as external")]
    public async Task NormalPath_IsNotExternal()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/relative-slash");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/path", response.Header("Location"));
    }

    [Fact(DisplayName = "does not flag root path as external")]
    public async Task RootPath_IsNotExternal()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/home");

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/", response.Header("Location"));
    }

    [Fact(DisplayName = "detects protocol-relative URL in object form")]
    public async Task ProtocolRelativeDestinationInObjectForm_IsExternal()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/protocol-relative-object");

        AssertRefused(response);
    }

    [Fact(DisplayName = "does not produce protocol-relative URL when catch-all param contains leading slash")]
    public async Task CatchAllParamWithLeadingSlash_DoesNotProduceAProtocolRelativeUrl()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/old//evil.com/");

        AssertNotProtocolRelative(response, "/evil.com");
    }

    [Fact(DisplayName = "does not produce protocol-relative URL with trailing slash config")]
    public async Task CatchAllParamWithLeadingSlash_WithTrailingSlashAlways_DoesNotProduceAProtocolRelativeUrl()
    {
        var site = await sites.GetAsync(Fixture, new { trailingSlash = "always" });

        var response = await site.SendRawAsync("/old//evil.com/");

        AssertNotProtocolRelative(response, "/evil.com/");
    }

    [Fact(DisplayName = "does not produce protocol-relative URL with subpath")]
    public async Task CatchAllParamWithLeadingSlashAndSubpath_DoesNotProduceAProtocolRelativeUrl()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/old//evil.com/phish");

        AssertNotProtocolRelative(response, "/evil.com/phish");
    }

    [Fact(DisplayName = "still produces correct paths for normal params")]
    public async Task NormalParams_ProduceTheExpectedPaths()
    {
        var site = await sites.GetAsync(Fixture);

        Assert.Equal("/about", (await site.SendRawAsync("/old/about")).Header("Location"));
        Assert.Equal("/docs/getting-started", (await site.SendRawAsync("/old/docs/getting-started")).Header("Location"));
    }

    [Fact(DisplayName = "non-root catch-all is not affected")]
    public async Task NonRootCatchAll_DoesNotProduceAProtocolRelativeUrl()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/old-prefix//evil.com");

        AssertNotProtocolRelative(response, "/prefix/evil.com");
    }
}
