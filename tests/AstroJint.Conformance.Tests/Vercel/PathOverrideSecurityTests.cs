using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/path-override-security.test.ts</c>. Upstream imports the <c>_render</c>
/// function that the adapter built and calls it with a request that tries to steer it to another route, by the
/// <c>x_astro_path</c> query parameter and the <c>x-astro-path</c> header that the ISR rewrite of Vercel uses.
/// <para>
/// Differences from upstream: there is no function module to import, so the requests go over HTTP to the host. The host
/// gives no meaning to either name, which is what the tests ask of the function: the route that answers is the one of the
/// URL. The fixture is the one of <c>serverless-with-dynamic-routes</c>, merged into <c>vercel-server</c>. The extra
/// tests send the same names to a page route and with the locals and middleware-secret headers that the Vercel adapter
/// does recognise, to show that none of them reaches the app.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/path-override-security.test.ts")]
public class PathOverrideSecurityTests(SiteHost sites)
{
    private static string IdOf(string json)
    {
        using var body = JsonDocument.Parse(json);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    [Fact(DisplayName = "ignores untrusted x_astro_path query param on _render")]
    public async Task IgnoresTheXAstroPathQueryParameter()
    {
        var site = await sites.ServerAsync();

        using var response = await site.GetAsync("/api/public?x_astro_path=/api/private");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public", IdOf(await response.ReadTextAsync()));
    }

    [Fact(DisplayName = "ignores untrusted x-astro-path header on _render")]
    public async Task IgnoresTheXAstroPathHeader()
    {
        var site = await sites.ServerAsync();

        using var response = await site.SendAsync(HttpMethod.Get, "/api/public", null, ("x-astro-path", "/api/private"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public", IdOf(await response.ReadTextAsync()));
    }

    [Theory(DisplayName = "ignores the path overrides when the URL is a page")]
    [InlineData("/abc?x_astro_path=/one")]
    [InlineData("/abc?x_astro_path=/api/private")]
    [InlineData("/abc?x_astro_path=%2Fone")]
    public async Task IgnoresThePathOverridesOnAPage(string target)
    {
        var site = await sites.ServerAsync();

        var html = await (await site.GetAsync(target)).ReadHtmlAsync();

        Assert.Equal("testing abc", html.QuerySelector("h1")?.TextContent);
    }

    [Fact(DisplayName = "ignores the x-astro-path header on a page")]
    public async Task IgnoresTheXAstroPathHeaderOnAPage()
    {
        var site = await sites.ServerAsync();

        using var response = await site.SendAsync(HttpMethod.Get, "/abc", null, ("x-astro-path", "/one"), ("x-astro-locals", "{\"role\":\"admin\"}"), ("x-astro-middleware-secret", "secret"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("testing abc", (await response.ReadHtmlAsync()).QuerySelector("h1")?.TextContent);
    }
}
