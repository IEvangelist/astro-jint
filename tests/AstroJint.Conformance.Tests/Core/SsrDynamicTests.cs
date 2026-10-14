using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/ssr-dynamic.test.ts</c>. Upstream fetches pages through the test adapter's <c>app.render()</c>;
/// here they are requested over HTTP. <c>Public assets take priority</c> asserts that <c>app.match()</c> finds no route for
/// <c>/favicon.ico</c>; the port requests the file and checks that the host answers with the bytes of <c>public/favicon.ico</c>
/// instead of rendering the <c>/[id]</c> route.
/// </summary>
[Trait("Upstream", "core/ssr-dynamic.test.ts")]
public class SsrDynamicTests(SiteHost sites)
{
    private const string Fixture = "core-ssr-dynamic";

    private Task<ConformanceSite> SiteAsync() => sites.GetAsync(Fixture);

    private async Task<HttpResponseMessage> GetOkAsync(string path)
    {
        var site = await SiteAsync();
        var response = await site.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }

    [Fact(DisplayName = "Do not have to implement getStaticPaths")]
    public async Task DynamicPage_NeedsNoGetStaticPaths()
    {
        using var response = await GetOkAsync("/123");

        Assert.Equal("Item 123", (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
    }

    [Fact(DisplayName = "Includes page styles")]
    public async Task DynamicPage_LinksItsStylesheet()
    {
        using var response = await GetOkAsync("/123");

        var links = (await response.ReadHtmlAsync()).QuerySelectorAll("link");

        var link = Assert.Single(links);
        var site = await SiteAsync();
        using var css = await site.GetAsync(link.GetAttribute("href")!);
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.Contains("font-size:11px", await css.ReadTextAsync());
    }

    [Fact(DisplayName = "Dynamic API routes work")]
    public async Task DynamicEndpoint_ReceivesItsParams()
    {
        using var response = await GetOkAsync("/api/products/33");

        using var json = JsonDocument.Parse(await response.ReadTextAsync());
        Assert.Equal("33", json.RootElement.GetProperty("id").GetString());
    }

    [Fact(DisplayName = "Injected route work")]
    public async Task InjectedRoute_ReceivesItsParams()
    {
        using var response = await GetOkAsync("/path-alias/33");

        using var json = JsonDocument.Parse(await response.ReadTextAsync());
        Assert.Equal("33", json.RootElement.GetProperty("id").GetString());
    }

    [Fact(DisplayName = "Public assets take priority")]
    public async Task PublicFile_TakesPriorityOverTheDynamicRoute()
    {
        var site = await SiteAsync();
        var file = await File.ReadAllBytesAsync(Path.Combine(site.DistPath, "client", "favicon.ico"));

        using var response = await GetOkAsync("/favicon.ico");

        Assert.Equal("image/x-icon", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(file, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact(DisplayName = "injectRoute entrypoint should not fail build if containing the extension several times in the path")]
    public async Task InjectedRoute_WithTheExtensionTwiceInItsEntrypoint_IsBuilt()
    {
        using var response = await GetOkAsync("/test");

        Assert.Equal("Index", (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
    }
}
