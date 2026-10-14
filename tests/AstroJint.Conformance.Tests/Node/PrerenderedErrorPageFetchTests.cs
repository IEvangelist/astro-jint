using System.Net;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/prerendered-error-page-fetch.test.ts</c>. Upstream patches the global
/// <c>fetch</c> to prove the adapter reads <c>404.html</c> and <c>500.html</c> from disk. The engine of this host cannot
/// make outbound requests at all, so the pages rendering with the right content proves the same thing.
/// </summary>
[Trait("Upstream", "node/prerendered-error-page-fetch.test.ts")]
public class PrerenderedErrorPageFetchTests(SiteHost sites)
{
    private const string Fixture = "prerendered-error-page-fetch";

    [Fact(DisplayName = "requests prerendered 404 page")]
    public async Task Prerendered404Page_IsReadFromDisk()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Custom 404 Page", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "requests prerendered 500 page")]
    public async Task Prerendered500Page_IsReadFromDisk()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/error?error=true");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Custom 500 Page", await response.ReadTextAsync());
    }
}
