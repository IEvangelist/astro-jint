using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/headers.test.ts</c>. Upstream tests two functions of the adapter that
/// parse and write the <c>_headers</c> file of Cloudflare's assets binding. The file lets the browser cache the hashed
/// assets for a year; the host has no such file, and the tests ask for the same thing from the URLs of the assets of the
/// shared fixture <c>cf-pages</c>:
/// <list type="bullet">
/// <item>"creates _headers from scratch with the default assets dir", "creates _headers from scratch with a custom assets
/// dir" and "respects a base prefix in the assets pattern" become a request for a hashed asset in <c>/_astro/</c>, in
/// <c>/_custom/</c> (<c>build.assets</c>) and in <c>/blog/_astro/</c> (base <c>/blog/</c>); each must be served with
/// <c>Cache-Control: public, max-age=31536000, immutable</c>.</item>
/// <item>Not applicable: the other 21 tests are about the syntax of the <c>_headers</c> file (comments, placeholders, host
/// patterns, the <c>!</c> detach prefix, CRLF), about not overwriting rules of the project, and about the order of the
/// rules in the file.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/headers.test.ts")]
public class HeadersTests(SiteHost sites)
{
    [Fact(DisplayName = "creates _headers from scratch with the default assets dir")]
    public async Task DefaultAssetsDir_IsCachedImmutably()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        await AssertAssetIsImmutableAsync(site, "/images/prerendered/", "/_astro/");
    }

    [Fact(DisplayName = "creates _headers from scratch with a custom assets dir")]
    public async Task CustomAssetsDir_IsCachedImmutably()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.CustomAssets);

        await AssertAssetIsImmutableAsync(site, "/images/prerendered/", "/_custom/");
    }

    [Fact(DisplayName = "respects a base prefix in the assets pattern")]
    public async Task BasePrefix_IsCachedImmutably()
    {
        var site = await sites.GetAsync(CfPages.Fixture, CfPages.Base);

        await AssertAssetIsImmutableAsync(site, "/blog/images/prerendered/", "/blog/_astro/");
    }

    private static async Task AssertAssetIsImmutableAsync(ConformanceSite site, string page, string prefix)
    {
        var src = (await site.GetHtmlAsync(page)).QuerySelector("img#image")?.GetAttribute("src");
        Assert.NotNull(src);
        Assert.StartsWith(prefix, src);

        using var asset = await site.GetAsync(src);
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", string.Join(", ", asset.HeaderValues("Cache-Control")));
    }
}
