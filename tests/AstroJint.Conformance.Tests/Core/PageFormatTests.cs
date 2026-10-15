using System.Net;
using AngleSharp.Dom;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/page-format.test.ts</c> on the <c>page-format</c> fixture (<c>core-page-format</c>: the six pages of the
/// upstream fixture, with no integration). Upstream builds the fixture five times with no adapter, with <c>build.format</c>
/// <c>directory</c>, <c>file</c> and <c>preserve</c>, and with <c>preserve</c>, <c>base: '/test'</c> and <c>trailingSlash: 'always'</c>
/// with and without i18n, and reads files of the output. The port builds it with the same five configs as config variants of the harness (the
/// adapter is in the config of the fixture and the output is static, which prerenders every page into <c>dist/client</c>), and the tests read
/// the same files from there, so all nine are ported. Each test requests the page from the host too (not asserted upstream): the host serves
/// the file that the build wrote at the URL of the page, which is what <c>Astro.url</c> and the relative links in the page say it is.
/// </summary>
[Trait("Upstream", "core/page-format.test.ts")]
public class PageFormatTests(SiteHost sites)
{
    private const string Fixture = "core-page-format";

    private static readonly object FileFormat = new { build = new { format = "file" } };

    private static readonly object PreserveFormat = new { build = new { format = "preserve" } };

    private static readonly object PreserveWithBase = new { @base = "/test", trailingSlash = "always", build = new { format = "preserve" } };

    private static readonly object PreserveWithBaseAndI18n = new
    {
        @base = "/test",
        trailingSlash = "always",
        build = new { format = "preserve" },
        i18n = new { locales = new[] { "en" }, defaultLocale = "en", routing = new { prefixDefaultLocale = true, redirectToDefaultLocale = true } },
    };

    /// <summary>Reads a file of the client output, as upstream does, and checks that the host serves it at the URL of the page.</summary>
    private static async Task<IDocument> ReadPageAsync(ConformanceSite site, string file, string url)
    {
        var html = BuildOutput.ReadClientText(site, file);

        using var response = await site.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(html, await response.ReadTextAsync());

        return ConformanceSite.ParseHtml(html);
    }

    [Fact(DisplayName = "build.format: directory: Build: relative urls created point to sibling folders")]
    public async Task Directory_NestedPage_HasRelativeUrlsToSiblingFolders()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await ReadPageAsync(site, "/nested/page/index.html", "/nested/page/");

        Assert.Equal("/nested/page/another/", html.QuerySelector("#another")?.GetAttribute("href"));
    }

    [Fact(DisplayName = "build.format: file: Build: Astro.url pathname includes /index.html for root page in build")]
    public async Task File_RootPage_AstroUrlIncludesIndexHtml()
    {
        var site = await sites.GetAsync(Fixture, FileFormat);

        var html = await ReadPageAsync(site, "/index.html", "/");

        Assert.Equal("/index.html", html.QuerySelector("#url")?.TextContent);
    }

    [Fact(DisplayName = "build.format: file: Build: Astro.url pathname includes .html for non-root pages in build")]
    public async Task File_NestedPage_AstroUrlIncludesHtml()
    {
        var site = await sites.GetAsync(Fixture, FileFormat);

        var html = await ReadPageAsync(site, "/nested/page.html", "/nested/page.html");

        Assert.Equal("/nested/page.html", html.QuerySelector("#url")?.TextContent);
    }

    [Fact(DisplayName = "build.format: file: Build: relative urls created point to sibling folders")]
    public async Task File_NestedPage_HasRelativeUrlsToSiblingFolders()
    {
        var site = await sites.GetAsync(Fixture, FileFormat);

        var html = await ReadPageAsync(site, "/nested/page.html", "/nested/page.html");

        Assert.Equal("/nested/another/", html.QuerySelector("#another")?.GetAttribute("href"));
    }

    [Fact(DisplayName = "build.format: preserve: Build: Astro.url pathname is .html for non-index pages")]
    public async Task Preserve_NonIndexPage_AstroUrlIsHtml()
    {
        var site = await sites.GetAsync(Fixture, PreserveFormat);

        var html = await ReadPageAsync(site, "/nested/page.html", "/nested/page.html");

        Assert.Equal("/nested/page.html", html.QuerySelector("#url")?.TextContent);
    }

    [Fact(DisplayName = "build.format: preserve: Build: Astro.url pathname has trailing slash for index pages")]
    public async Task Preserve_IndexPage_AstroUrlHasTrailingSlash()
    {
        var site = await sites.GetAsync(Fixture, PreserveFormat);

        var html = await ReadPageAsync(site, "/nested/index.html", "/nested/");

        Assert.Equal("/nested/", html.QuerySelector("h2")?.TextContent);
    }

    [Fact(DisplayName = "build.format: preserve - i18n: Build: Astro.url points to right file")]
    public async Task PreserveWithBase_IndexPage_AstroUrlPointsToTheRightFile()
    {
        var site = await sites.GetAsync(Fixture, PreserveWithBase);

        var html = await ReadPageAsync(site, "/nested/index.html", "/test/nested/");

        Assert.Equal("/test/nested/", html.QuerySelector("h2")?.TextContent);
    }

    [Fact(DisplayName = "build.format: preserve - i18n: Build: relative urls created point to sibling folders")]
    public async Task PreserveWithI18n_NestedPage_HasRelativeUrlsToSiblingFolders()
    {
        var site = await sites.GetAsync(Fixture, PreserveWithBaseAndI18n);

        var html = await ReadPageAsync(site, "/en/nested/page.html", "/test/en/nested/page.html");

        Assert.Equal("/test/en/nested/another/", html.QuerySelector("#another")?.GetAttribute("href"));
    }

    [Fact(DisplayName = "build.format: preserve - i18n: Build: index files are written as index.html")]
    public async Task PreserveWithI18n_IndexPage_IsWrittenAsIndexHtml()
    {
        var site = await sites.GetAsync(Fixture, PreserveWithBaseAndI18n);

        var html = await ReadPageAsync(site, "/en/nested/index.html", "/test/en/nested/");

        Assert.Equal("Testing", html.QuerySelector("h1")?.TextContent);
    }
}
