using System.Net;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/build-format-file.test.ts</c>. With <c>build.format</c> set to
/// <c>file</c> or <c>preserve</c>, Astro writes <c>two.html</c> where the default <c>directory</c> format writes
/// <c>two/index.html</c>; the page must still be served at the clean URL <c>/two</c>.
/// </summary>
[Trait("Upstream", "node/build-format-file.test.ts")]
public class BuildFormatFileTests(SiteHost sites)
{
    private const string Fixture = "prerender";

    private static readonly object FileFormat = new { output = "server", build = new { format = "file" } };
    private static readonly object PreserveFormat = new { output = "server", build = new { format = "preserve" } };

    [Theory(DisplayName = "Can render SSR route")]
    [InlineData("file")]
    [InlineData("preserve")]
    public async Task SsrRoute_IsRendered(string format)
    {
        var site = await sites.GetAsync(Fixture, ConfigOf(format));

        using var response = await site.GetAsync("/one");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("One", (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
    }

    [Theory(DisplayName = "Can render prerendered route with clean URL")]
    [InlineData("file")]
    [InlineData("preserve")]
    public async Task PrerenderedRoute_IsServedAtTheCleanUrl(string format)
    {
        var site = await sites.GetAsync(Fixture, ConfigOf(format));

        using var response = await site.GetAsync("/two");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Two", (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
    }

    [Fact(DisplayName = "file: Can render prerendered route with explicit .html extension")]
    public async Task PrerenderedRoute_IsServedWithItsExtension()
    {
        var site = await sites.GetAsync(Fixture, FileFormat);

        using var response = await site.GetAsync("/two.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Two", (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
    }

    [Fact(DisplayName = "file: Outputs pages as .html files, not directories")]
    public async Task PrerenderedRoute_IsAFileNotADirectory()
    {
        var site = await sites.GetAsync(Fixture, FileFormat);

        Assert.True(site.PathExists("client/two.html"));
        Assert.False(site.PathExists("client/two/index.html"));
    }

    private static object ConfigOf(string format) => format == "file" ? FileFormat : PreserveFormat;
}
