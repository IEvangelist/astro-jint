using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/ssr-prerender-get-static-paths.test.ts</c>. Upstream has two identical describes, one built
/// with <c>output: 'server'</c> and one with <c>output: 'static'</c> (a Vite plugin removes the <c>prerender</c> exports, so the
/// pages are prerendered by default); each Theory here runs against both builds. Both use <c>site: 'https://mysite.dev/'</c> and
/// <c>base: '/blog'</c>. Upstream reads the prerendered files from the build output; the port requests them from the host.
/// <c>is only called once during build</c> asserts <c>true</c> upstream: the check is that the build does not throw, because a
/// page throws when its <c>getStaticPaths()</c> runs twice. The port builds the fixture, which fails in the same case, and also
/// requests the three pages that this <c>getStaticPaths()</c> returns.
/// </summary>
[Trait("Upstream", "core/ssr-prerender-get-static-paths.test.ts")]
public class SsrPrerenderGetStaticPathsTests(SiteHost sites)
{
    private const string Fixture = "core-ssr-prerender-get-static-paths";

    private Task<ConformanceSite> SiteAsync(string output) => sites.GetAsync(Fixture, new { output });

    [Theory(DisplayName = "is only called once during build")]
    [InlineData("server")]
    [InlineData("static")]
    public async Task GetStaticPaths_RunsOnceDuringTheBuild(string output)
    {
        var site = await SiteAsync(output);

        foreach (var name in new[] { "a", "b", "c" })
        {
            using var response = await site.GetAsync($"/blog/{name}/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal($"Page {name}", (await response.ReadHtmlAsync()).QuerySelector("title")?.TextContent);
        }
    }

    [Theory(DisplayName = "Astro.url sets the current pathname")]
    [InlineData("server")]
    [InlineData("static")]
    public async Task PrerenderedPage_SeesTheBaseInItsUrl(string output)
    {
        var site = await SiteAsync(output);

        using var response = await site.GetAsync("/blog/food/tacos/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(site.PathExists("client/food/tacos/index.html"), "The build did not write client/food/tacos/index.html.");
        var html = await response.ReadHtmlAsync();
        Assert.Equal("10", html.QuerySelector("#props")?.TextContent);
        Assert.Equal("/blog/food/tacos/", html.QuerySelector("#url")?.TextContent);
    }
}
