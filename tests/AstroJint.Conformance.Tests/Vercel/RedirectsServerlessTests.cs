using System.Net;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/redirects-serverless.test.ts</c>. Upstream builds a static site with
/// redirects and checks that the build does not write an <c>.html</c> file for a redirect (<c>static/other/index.html</c>),
/// because the redirects are routes of the Vercel config.
/// <para>
/// Differences from upstream: the same two things are checked on the output of this adapter, which also builds with
/// <c>build.redirects: false</c> and leaves the redirects to the runtime, and the test then asks the host for the redirect
/// that the missing file would have been a stand-in for.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/redirects-serverless.test.ts")]
public class RedirectsServerlessTests(SiteHost sites)
{
    [Fact(DisplayName = "does not create .html files")]
    public async Task ARedirectHasNoHtmlFile()
    {
        var site = await sites.StaticAsync();

        Assert.False(site.PathExists("client/other/index.html"), "this file should not exist");
        Assert.False(site.PathExists("client/other.html"), "this file should not exist");
        Assert.False(site.PathExists("client/one/index.html"), "this file should not exist");
    }

    [Fact(DisplayName = "does not create .html files: the redirect is answered by the host")]
    public async Task TheRedirectIsAnsweredWithoutAFile()
    {
        var site = await sites.StaticAsync();

        using var response = await site.GetAsync("/other");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/subpage", Assert.Single(response.HeaderValues("Location")));
    }
}
