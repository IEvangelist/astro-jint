namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/malformed-uri.test.ts</c>: a request path with an invalid percent-sequence, such
/// as <c>%C0%AF</c> that automated scanners send, must not crash route matching and become a 500.
/// <para>
/// Differences from upstream: the first and the last test call the internal <c>App.match()</c>. The first one is the same
/// request as the second here, which the host also answers through <c>App.match()</c>; it asserts that nothing crashed and
/// that the path matched no route, which shows as a 404 page. The last one asks for <c>/</c> and expects the page. The
/// request goes over a socket with the target as written, and Kestrel may refuse such a target with a 400 before Astro sees
/// it; the host must answer either way and must not fail with a 500.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/malformed-uri.test.ts")]
public class MalformedUriTests(SiteHost sites)
{
    private const string Fixture = "app-pages";

    [Fact(DisplayName = "match() does not throw on an invalid percent-sequence")]
    public async Task Match_DoesNotThrowOnAnInvalidPercentSequence()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/%C0%AF");

        Assert.True(response.StatusCode is 400 or 404, $"The request returned {response.StatusCode}");
        Assert.DoesNotContain("Public", response.Text);
    }

    [Fact(DisplayName = "render() returns a 404 for a malformed percent-sequence")]
    public async Task Render_ReturnsA404ForAMalformedPercentSequence()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/%C0%AF");

        Assert.Equal(404, response.StatusCode);
    }

    [Fact(DisplayName = "valid routes still match")]
    public async Task ValidRoutes_StillMatch()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("Public", response.Text);
    }
}
