using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/with-base.test.ts</c>. The site is the shared fixture <c>cf-pages</c>
/// built with base <c>/blog/</c> and the two redirects of the upstream fixture (the second one is the test case of
/// withastro/astro#13165, a redirect with a trailing slash under a base).
/// <para>
/// Upstream looks at the files that the adapter writes for Cloudflare's assets binding, which serves the folder as it is, so
/// the pages and the public files have to lie in a <c>blog</c> folder, and the platform files <c>_redirects</c>,
/// <c>_headers</c> and <c>.assetsignore</c> have to lie beside it. The files of the host are private: it removes the base
/// from the URL and serves <c>client/static/index.html</c> for <c>/blog/static/</c>. The tests ask for the URLs.
/// </para>
/// <list type="bullet">
/// <item>"correctly prints redirects at the client root": the redirects answer 301 to <c>/</c>, with and without the
/// trailing slash.</item>
/// <item>"places prerendered pages under the base prefix" and "places public files under the base prefix": the URLs under
/// the base are served, and the same paths without the base are not.</item>
/// <item>"injects cache headers for assets with base prefix": the hashed assets under <c>/blog/_astro/</c> are served with
/// <c>Cache-Control: public, max-age=31536000, immutable</c>, the value that the adapter puts into <c>_headers</c>.</item>
/// <item>Not applicable: the tests of <c>.assetsignore</c>, <c>_headers</c>, <c>_redirects</c>, of the rules that a project
/// puts into <c>public/_redirects</c> and <c>public/_headers</c>, of the order of the rules in <c>_headers</c> and of
/// <c>assets.directory</c> in <c>wrangler.json</c> are about files of Cloudflare.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/with-base.test.ts")]
public class WithBaseTests(SiteHost sites)
{
    [Fact(DisplayName = "correctly prints redirects at the client root")]
    public async Task Redirects_AreAnswered()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.Base);

        foreach (var path in new[] { "/blog/a/redirect", "/blog/a/redirect/" })
        {
            using var response = await site.GetAsync(path);

            Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.Equal("/", response.Headers.Location?.ToString());
        }
    }

    [Fact(DisplayName = "places prerendered pages under the base prefix")]
    public async Task PrerenderedPages_AreUnderTheBase()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.Base);

        using var page = await site.GetAsync("/blog/static/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("prerendered at build time", await page.ReadTextAsync());

        using var outside = await site.GetAsync("/static/");
        Assert.Equal(HttpStatusCode.NotFound, outside.StatusCode);
    }

    [Fact(DisplayName = "places public files under the base prefix")]
    public async Task PublicFiles_AreUnderTheBase()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.Base);

        using var file = await site.GetAsync("/blog/favicon.ico");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);

        using var outside = await site.GetAsync("/favicon.ico");
        Assert.Equal(HttpStatusCode.NotFound, outside.StatusCode);
    }

    [Fact(DisplayName = "injects cache headers for assets with base prefix")]
    public async Task Assets_AreCachedImmutably()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.Base);

        var src = (await site.GetHtmlAsync("/blog/images/prerendered/")).QuerySelector("img#image")?.GetAttribute("src");
        Assert.NotNull(src);
        Assert.StartsWith("/blog/_astro/", src);

        using var asset = await site.GetAsync(src);
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", string.Join(", ", asset.HeaderValues("Cache-Control")));
    }
}
