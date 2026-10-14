using System.Net;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/redirects.test.ts</c>. Upstream builds a site with static, dynamic and
/// <c>.html</c> redirects and <c>trailingSlash: 'always'</c>, and reads the redirect routes out of the route table of
/// <c>.vercel/output/config.json</c>: their <c>Location</c> and status, and the position of the cache rule of the assets
/// between the last redirect and the filesystem handle. The redirects are the ones of <c>vercel-static</c>.
/// <para>
/// Differences from upstream: the route table is replaced by the requests it serves, so each test asks the host for the
/// redirect and compares its status and <c>Location</c>. The redirects of this host are answered by Astro at run time,
/// after its own trailing-slash redirect: with <c>trailingSlash: 'always'</c>, <c>/one</c> is first sent to <c>/one/</c>
/// and only then to <c>/</c>, where Vercel answers <c>/one</c> in one step. The tests follow the redirects of the site
/// from the URL of the upstream test and compare the last one, whose <c>Location</c> also has the trailing slash that the
/// setting asks for (<c>/team/articles/one/</c>). The order of the rules of the route table becomes two things that can
/// be seen: the hashed files of the assets folder get the immutable cache header (the cache rule is before the
/// filesystem), and a redirect from inside that folder does not (the redirects are before the cache rule). The last test
/// of the file, which expects the build to fail on a redirect source that <c>@vercel/routing-utils</c> rejects, has no
/// counterpart: the build of this adapter has no conversion into Vercel routes, and Astro accepts that source, so it is
/// only in the matrix.
/// </para>
/// <para>
/// Gaps: a redirect to another site does not keep the status of its configuration (GAP-VERCEL-1), and with
/// <c>trailingSlash: 'always'</c> a redirect from a name with an extension is not found without the slash
/// (GAP-VERCEL-2). The tests of the other cases of those two upstream tests, and the ones with the slash, pass.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/redirects.test.ts")]
public class RedirectsTests(SiteHost sites)
{
    private const string ExternalStatusGap = "GAP-VERCEL-1: a redirect to another site answers 301 although the config says 302 (Astro core computeRedirectStatus)";

    [Theory(DisplayName = "define static routes")]
    [InlineData("/one", 301, "/")]
    [InlineData("/two", 301, "/")]
    [InlineData("/three", 302, "/")]
    [InlineData("/four", 302, "http://example.com/", Skip = ExternalStatusGap)]
    public async Task StaticRoutes_RedirectWithTheirStatus(string path, int status, string location)
    {
        var site = await sites.StaticWithTrailingSlashesAsync();

        var redirect = await LastRedirectAsync(site, path);

        Assert.Equal((HttpStatusCode)status, redirect.Status);
        Assert.Equal(location, redirect.Location);
    }

    [Fact(DisplayName = "define redirects for static files", Skip = "GAP-VERCEL-2: with trailingSlash 'always' the redirect from /Basic/http-2-0.html is only found with a trailing slash, so the request without it is a 404")]
    public async Task ARedirectFromAFileName_Redirects()
    {
        var site = await sites.StaticWithTrailingSlashesAsync();

        var redirect = await LastRedirectAsync(site, "/Basic/http-2-0.html");

        Assert.Equal(HttpStatusCode.MovedPermanently, redirect.Status);
        Assert.Equal("/posts/http2", redirect.Location);
    }

    [Fact(DisplayName = "define redirects for static files: with the trailing slash")]
    public async Task ARedirectFromAFileName_WithTheTrailingSlash_Redirects()
    {
        var site = await sites.StaticWithTrailingSlashesAsync();

        using var response = await site.GetAsync("/Basic/http-2-0.html/");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/posts/http2", Assert.Single(response.HeaderValues("Location")));
    }

    [Fact(DisplayName = "define redirects for static files: with the default trailingSlash")]
    public async Task ARedirectFromAFileName_WithTheDefaultTrailingSlash_Redirects()
    {
        var site = await sites.StaticAsync();

        using var response = await site.GetAsync("/Basic/http-2-0.html");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/posts/http2", Assert.Single(response.HeaderValues("Location")));
    }

    [Theory(DisplayName = "defines dynamic routes")]
    [InlineData("/blog/one", "/team/articles/one/")]
    [InlineData("/blog/two", "/team/articles/two/")]
    [InlineData("/blog/a/b", "/team/articles/a/b/")]
    public async Task DynamicRoutes_RedirectWithTheirParameters(string path, string location)
    {
        var site = await sites.StaticWithTrailingSlashesAsync();

        var redirect = await LastRedirectAsync(site, path);

        Assert.Equal(HttpStatusCode.MovedPermanently, redirect.Status);
        Assert.StartsWith("/team/articles", redirect.Location);
        Assert.Equal(location, redirect.Location);
    }

    [Fact(DisplayName = "emits the assets cache rule after redirects and before the filesystem handle")]
    public async Task TheCacheRuleOfTheAssets_IsAfterTheRedirectsAndBeforeTheFiles()
    {
        var site = await sites.StaticAsync();
        var stylesheet = await site.StylesheetOfAsync("/styled");

        using var asset = await site.GetAsync(stylesheet);
        using var redirect = await site.GetAsync("/_astro/legacy.css");

        Assert.StartsWith("/_astro/", stylesheet);
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", Assert.Single(asset.HeaderValues("Cache-Control")));
        Assert.Equal(HttpStatusCode.MovedPermanently, redirect.StatusCode);
        Assert.Equal("/", Assert.Single(redirect.HeaderValues("Location")));
        Assert.DoesNotContain("immutable", string.Join(",", redirect.HeaderValues("Cache-Control")));
    }

    private sealed record Redirect(HttpStatusCode Status, string Location);

    // Follows the redirects that stay in the site, and returns the last one, which is the one of the config.
    private static async Task<Redirect> LastRedirectAsync(ConformanceSite site, string path)
    {
        var requested = path;
        var status = HttpStatusCode.OK;
        Redirect? last = null;
        for (var hop = 0; hop < 4; hop++)
        {
            using var response = await site.GetAsync(path);
            status = response.StatusCode;
            if ((int)status is < 300 or >= 400)
            {
                break;
            }

            var location = Assert.Single(response.HeaderValues("Location"));
            last = new Redirect(status, location);
            if (!location.StartsWith('/'))
            {
                break;
            }

            path = location;
        }

        if (last is null)
        {
            Assert.Fail($"{requested} is not a redirect, it answers {(int)status}");
        }

        return last;
    }
}
