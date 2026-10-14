using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/ssr-request.test.ts</c>. The fixture is built as upstream builds it: <c>base: '/subpath/'</c>,
/// <c>assetsInlineLimit: 0</c> and an integration that injects a script into every page. Upstream hands a
/// <c>Request</c> for <c>http://example.com/...</c> to <c>app.render()</c>; here the request travels over HTTP with
/// <c>Host: example.com</c>, which gives the page the same origin. The test adapter also serves <c>manifest.assets</c> itself,
/// where this host serves them from its static files layer, so the asset requests prove that layer honours the base too.
/// </summary>
[Trait("Upstream", "core/ssr-request.test.ts")]
public class SsrRequestTests(SiteHost sites)
{
    private const string Fixture = "core-ssr-request";

    private Task<ConformanceSite> SiteAsync() => sites.GetAsync(Fixture);

    [Fact(DisplayName = "Gets the request passed in")]
    public async Task Page_SeesTheOriginOfTheRequest()
    {
        var site = await SiteAsync();

        using var response = await site.SendAsync(HttpMethod.Get, "/subpath/request", null, ("Host", "example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http://example.com", (await response.ReadHtmlAsync()).QuerySelector("#origin")?.TextContent);
    }

    [Fact(DisplayName = "public file is copied over")]
    public async Task PublicFile_IsCopiedToTheClientOutput()
    {
        var site = await SiteAsync();

        Assert.True(site.PathExists("client/cars.json"), "The build did not copy public/cars.json to client/cars.json.");
    }

    [Fact(DisplayName = "CSS assets have their base prefix")]
    public async Task Stylesheet_HasTheBasePrefix_AndIsServed()
    {
        var site = await SiteAsync();
        using var page = await site.SendAsync(HttpMethod.Get, "/subpath/request", null, ("Host", "example.com"));
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var href = (await page.ReadHtmlAsync()).QuerySelector("link")?.GetAttribute("href");

        Assert.NotNull(href);
        Assert.StartsWith("/subpath/", href);
        using var css = await site.GetAsync(href);
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.Equal("text/css", css.Content.Headers.ContentType?.MediaType);
        Assert.Contains("background:#ff4500", await css.ReadTextAsync());
    }

    [Fact(DisplayName = "script assets have their base prefix")]
    public async Task Scripts_HaveTheBasePrefix_AndAreServed()
    {
        var site = await SiteAsync();
        using var page = await site.SendAsync(HttpMethod.Get, "/subpath/request", null, ("Host", "example.com"));
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var scripts = (await page.ReadHtmlAsync()).QuerySelectorAll("script");

        Assert.NotEmpty(scripts);
        foreach (var script in scripts)
        {
            var src = script.GetAttribute("src");
            Assert.NotNull(src);
            Assert.StartsWith("/subpath/", src);
            using var js = await site.GetAsync(src);
            Assert.Equal(HttpStatusCode.OK, js.StatusCode);
            Assert.Contains("javascript", js.Content.Headers.ContentType?.MediaType);
            Assert.NotEmpty(await js.ReadTextAsync());
        }
    }

    [Fact(DisplayName = "assets can be fetched")]
    public async Task PublicFile_IsServedBelowTheBase()
    {
        var site = await SiteAsync();

        using var response = await site.GetAsync("/subpath/cars.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.ReadTextAsync());
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
    }

    [Fact(DisplayName = "middleware gets the normalized path sent in the request")]
    public async Task Middleware_SeesThePathWithoutDuplicateSlashes()
    {
        var site = await SiteAsync();

        var response = await site.SendRawAsync("/this//is/my/////directory", "GET", null, ("Host", "example.com"));

        Assert.Equal(301, response.StatusCode);
        Assert.Equal("/", response.Header("Location"));
    }
}
