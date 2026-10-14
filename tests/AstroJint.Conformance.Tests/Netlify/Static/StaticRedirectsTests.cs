using System.Net;

namespace AstroJint.Conformance.Netlify;

/// <summary>
/// Port of <c>packages/integrations/netlify/test/static/redirects.test.ts</c> (<c>SSG - Redirects</c>), on the
/// <c>netlify-redirects</c> fixture, which also serves <c>functions/redirects.test.ts</c>.
/// <para>
/// Differences. The host writes no <c>_redirects</c> file: it answers a redirect route at request time with the configured
/// status, so the snapshot of that file becomes the responses of the same sources (the file lists <c>/two</c> and
/// <c>/other</c> with and without a trailing slash because <c>trailingSlash</c> is <c>ignore</c>). Netlify's rule for
/// <c>/blog/*</c> points at <c>/team/articles/*/index.html</c>, a file of the deployment; the host redirects to the page's
/// URL, and the second test follows that redirect to prove it leads to the prerendered page.
/// </para>
/// </summary>
[Trait("Upstream", "netlify/static/redirects.test.ts")]
public class StaticRedirectsTests(SiteHost sites)
{
    private const string Fixture = "netlify-redirects";

    [Theory(DisplayName = "Creates a redirects file")]
    [InlineData("/two", 302, "/")]
    [InlineData("/two/", 302, "/")]
    [InlineData("/other", 301, "/")]
    [InlineData("/other/", 301, "/")]
    [InlineData("/blog/one", 301, "/team/articles/one")]
    public async Task RedirectRoutes_AnswerWithTheirConfiguredStatus(string path, int status, string location)
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync(path);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal([location], response.HeaderValues("Location"));
    }

    [Fact(DisplayName = "Creates a redirects file: the target of /blog/* is a prerendered page")]
    public async Task SpreadRedirect_LeadsToThePrerenderedPage()
    {
        var site = await sites.GetAsync(Fixture);

        using var redirect = await site.GetAsync("/blog/two");
        var location = Assert.Single(redirect.HeaderValues("Location"));
        using var target = await site.GetAsync(location);

        Assert.Equal(HttpStatusCode.OK, target.StatusCode);
        Assert.Equal("Two", (await target.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
        Assert.True(site.PathExists("client/team/articles/two/index.html"));
    }
}
