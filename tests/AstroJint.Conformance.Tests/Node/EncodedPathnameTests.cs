using System.Net;

namespace AstroJint.Conformance.Node;

/// <summary>Port of <c>packages/integrations/node/test/encoded.test.ts</c>.</summary>
[Trait("Upstream", "node/encoded.test.ts")]
public class EncodedPathnameTests(SiteHost sites)
{
    private const string Fixture = "encoded";

    // A client always percent-encodes the target; the mock request upstream skips that step, so both forms are requested.
    [Theory(DisplayName = "Can get an Astro file")]
    [InlineData("/什么")]
    [InlineData("/%E4%BB%80%E4%B9%88")]
    public async Task AstroPagesWithEncodedNames_AreRendered(string path)
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("什么</h1>", await response.Content.ReadAsStringAsync());
    }

    [Theory(DisplayName = "Can get a Markdown file")]
    [InlineData("/blog/什么")]
    [InlineData("/blog/%E4%BB%80%E4%B9%88")]
    public async Task MarkdownPagesWithEncodedNames_AreRendered(string path)
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("什么</h1>", await response.Content.ReadAsStringAsync());
    }
}
