using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Node;

/// <summary>Port of <c>packages/integrations/node/test/well-known-locations.test.ts</c>.</summary>
[Trait("Upstream", "node/well-known-locations.test.ts")]
public class WellKnownLocationsTests(SiteHost sites)
{
    private const string Fixture = "well-known-locations";

    [Fact(DisplayName = "can load a valid well-known URI")]
    public async Task WellKnownUris_AreServed()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/.well-known/apple-app-site-association");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("applinks", out _));
    }

    [Fact(DisplayName = "cannot load a dot folder that is not a well-known URI")]
    public async Task DotFolders_AreNotServed()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/.hidden/file.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/.hidden-file")]
    [InlineData("/.hidden/file.json")]
    public async Task DotFiles_AreNotServed(string hidden)
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync(hidden);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The first two targets are the upstream tests; the others are the same attack with the dot segments encoded.
    [Theory]
    [InlineData("/.well-known/../.hidden-file")]
    [InlineData("/.well-known/../.hidden/file.json")]
    [InlineData("/.well-known/%2e%2e/.hidden-file")]
    [InlineData("/.well-known/%2E%2E/.hidden/file.json")]
    [InlineData("/.well-known/..%2f.hidden-file")]
    [InlineData("/.well-known/..%5c.hidden-file")]
    [InlineData("/.well-known\\..\\.hidden-file")]
    public async Task Traversal_FromWellKnownDoesNotReachDotFiles(string target)
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync(target);

        Assert.NotEqual(200, response.StatusCode);
        Assert.DoesNotContain("should-not-serve", response.Text);
    }
}
