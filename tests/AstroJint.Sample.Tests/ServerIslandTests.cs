using System.Net;
using System.Text.RegularExpressions;

namespace AstroJint.Sample;

/// <summary>
/// A server island is a component that renders on demand inside a prerendered page. The home page of the sample is a
/// file, and the clock in it is fetched from the engine pool when a browser shows the page.
/// </summary>
public partial class ServerIslandTests(SampleSite site)
{
    [GeneratedRegex(@"fetch\(""(?<url>/_server-islands/[^""]+)""")]
    private static partial Regex IslandRequest();

    private async Task<string> FindIslandUrlAsync(HttpClient client)
    {
        var script = await client.GetStringAsync("/");
        var match = IslandRequest().Match(script);
        Assert.True(match.Success, "The home page does not fetch a server island.");
        return match.Groups["url"].Value;
    }

    [Fact]
    public async Task ThePageIsAFileWithAFallbackForTheIsland()
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync("/");
        var page = await response.ReadHtmlAsync();

        Assert.Equal(["max-age=0", "must-revalidate", "public"], response.CacheControl());
        Assert.Contains("Loading the server time", page.Body?.TextContent);
        Assert.False(page.Has("#server-clock"));
    }

    [Fact]
    public async Task TheIslandIsRenderedOnDemandForEveryRequest()
    {
        using var client = site.CreateClient();
        var url = await FindIslandUrlAsync(client);

        using var firstResponse = await client.GetAsync(url);
        var first = await firstResponse.ReadHtmlAsync();
        await Task.Delay(50);
        var second = await (await client.GetAsync(url)).ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal("text/html", firstResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("noindex", firstResponse.Header("X-Robots-Tag"));
        Assert.StartsWith("Server time: ", first.Text("#server-clock"));
        Assert.NotEqual(first.Text("#server-clock time"), second.Text("#server-clock time"));
        Assert.InRange(
            DateTimeOffset.Parse(first.Text("#server-clock time")),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task TheEncryptedPropsOfAnIslandCannotBeForged()
    {
        using var client = site.CreateClient();
        var url = await FindIslandUrlAsync(client);
        var forged = Regex.Replace(url, "e=[^&]+", "e=00000000000000000000000000000000");

        using var response = await client.GetAsync(forged);

        Assert.False(response.IsSuccessStatusCode, $"The island was rendered with forged props: {response.StatusCode}.");
        Assert.DoesNotContain("server-clock", await response.Content.ReadAsStringAsync());
    }
}
