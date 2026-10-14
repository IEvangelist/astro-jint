namespace AstroJint.Conformance.Node;

/// <summary>Port of <c>packages/integrations/node/test/redirects.test.ts</c>. Redirects are never followed by the test client.</summary>
[Trait("Upstream", "node/redirects.test.ts")]
public class RedirectsTests(SiteHost sites)
{
    private const string Fixture = "redirects";

    [Fact(DisplayName = "should redirect with default 301 status for simple redirects")]
    public Task SimpleRedirects_Default301() => AssertRedirect("/old-page", 301, "/new-page");

    [Fact(DisplayName = "should redirect with custom 301 status")]
    public Task Redirects_WithCustom301() => AssertRedirect("/old-page-301", 301, "/new-page-301");

    [Fact(DisplayName = "should redirect with custom 302 status")]
    public Task Redirects_WithCustom302() => AssertRedirect("/old-page-302", 302, "/new-page-302");

    [Fact(DisplayName = "should handle dynamic redirects with parameters")]
    public Task DynamicRedirects_KeepTheirParameters() => AssertRedirect("/dynamic/test-slug", 301, "/pages/test-slug");

    [Fact(DisplayName = "should handle spread redirects with parameters")]
    public Task SpreadRedirects_KeepTheirParameters() => AssertRedirect("/spread/some/nested/path", 301, "/content/some/nested/path");

    [Fact(DisplayName = "should redirect to external URL")]
    public Task Redirects_ToExternalUrls() => AssertRedirect("/external", 301, "https://example.com/");

    private async Task AssertRedirect(string path, int status, string location)
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync(path);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(location, Assert.Single(response.HeaderValues("Location")));
    }
}
