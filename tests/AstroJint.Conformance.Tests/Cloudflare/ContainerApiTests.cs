using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/container-api.test.ts</c>: a page that renders a component with the
/// Container API is prerendered by the build.
/// <para>
/// One adaptation: the upstream fixture has <c>output: 'static'</c>; the shared fixture <c>cf-pages</c> has <c>output: 'server'</c>
/// and marks the page with <c>prerender = true</c>, which makes it the same prerendered page. The test also requests the page
/// from the host, which serves the prerendered file.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/container-api.test.ts")]
public class ContainerApiTests(SiteHost sites)
{
    private const string Fixture = "cf-pages";

    [Fact(DisplayName = "should build and prerender Container API page correctly")]
    public async Task ContainerApiPage_IsPrerendered()
    {
        var site = await sites.GetAsync(Fixture);

        Assert.True(site.PathExists("client/test.html"), "The build wrote no client/test.html.");
        var built = await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "test.html"));
        Assert.Contains("Hello World!", built);

        using var response = await site.GetAsync("/test.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Hello World!", await response.ReadTextAsync());
    }
}
