using System.Net;

namespace AstroJint.Conformance.Host;

/// <summary>
/// A page that renders on demand and renders an MDX entry of a content collection, which is compiled when it is rendered and
/// brings the styles of the components it uses. Astro adds the manifest of the site and those assets to the server bundle in
/// two passes over the same chunk, and the second pass overwrites the first when the bundle is one file, which left the
/// manifest unset and every request answering 500.
/// </summary>
public class ContentCollectionHostTests(SiteHost sites)
{
    private const string Fixture = "content-collection";

    [Fact]
    public async Task OnDemandPage_RendersAnEntryOfACollection()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/posts/hello");
        var document = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello from a collection", document.QuerySelector("#post h1")?.TextContent);
        Assert.Equal("rendered on demand", document.QuerySelector("#post strong")?.TextContent);
        Assert.Equal("A component inside the entry.", document.QuerySelector("#post aside.note")?.TextContent);
        Assert.Contains(document.QuerySelectorAll("style"), style => style.TextContent.Contains(".note"));
    }

    [Fact]
    public async Task OnDemandPage_AnswersNotFoundForAnEntryThatDoesNotExist()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/posts/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PrerenderedPage_ListsTheEntriesOfTheCollection()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/");
        var document = await response.ReadHtmlAsync();

        var link = Assert.Single(document.QuerySelectorAll("li a"));
        Assert.Equal("/posts/hello", link.GetAttribute("href"));
        Assert.Equal("Hello from a collection", link.TextContent);
    }
}
