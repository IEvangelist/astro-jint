using System.Net;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.Host;

/// <summary>
/// What the host adds to the static headers of the Node tests: with <c>staticHeaders</c> on, Astro leaves the policy of a
/// prerendered page out of the markup and the host sends it, and the pages that render on demand still get theirs from Astro.
/// </summary>
public partial class StaticHeadersHostTests(SiteHost sites)
{
    private const string Fixture = "static-headers";

    [GeneratedRegex(@"fetch\(""(?<url>/_server-islands/[^""]+)""")]
    private static partial Regex ServerIslandRequest();

    [Fact]
    public async Task PrerenderedPages_LeaveThePolicyToTheHeader()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/");
        var document = await response.ReadHtmlAsync();

        Assert.Single(response.HeaderValues("Content-Security-Policy"));
        Assert.Null(document.QuerySelector("meta[http-equiv='content-security-policy' i]"));
    }

    [Fact]
    public async Task OnDemandPages_CarryTheirPolicyFromAstro()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/on-demand");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("script-src", Assert.Single(response.HeaderValues("Content-Security-Policy")));
        Assert.Equal("/on-demand", (await response.ReadHtmlAsync()).QuerySelector("#path")?.TextContent);
    }

    [Fact]
    public async Task PrerenderedErrorPage_CarriesItsPolicyInTheHeader()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/no/such/page");
        var document = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Not found", document.QuerySelector("h1")?.TextContent);
        Assert.Contains("script-src", Assert.Single(response.HeaderValues("Content-Security-Policy")));
        Assert.Null(document.QuerySelector("meta[http-equiv='content-security-policy' i]"));
    }

    [Fact]
    public async Task ServerIslands_AreRenderedOnDemand()
    {
        var site = await sites.GetAsync(Fixture);
        var page = await site.GetStringAsync("/");
        var url = System.Net.WebUtility.HtmlDecode(ServerIslandRequest().Match(page).Groups["url"].Value);
        Assert.NotEmpty(url);

        using var response = await site.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("I am a server island", await response.ReadTextAsync());
    }

    [Fact]
    public async Task MalformedHostPort_OnARenderedPage_IsServedOrRefusedButNeverFails()
    {
        var site = await sites.GetAsync(Fixture);

        var malformed = await site.SendRawAsync("/on-demand", "GET", null, ("Host", "example.com:65536"));
        using var followUp = await site.GetAsync("/on-demand");

        Assert.True(malformed.StatusCode is 200 or 400, $"The malformed Host got {malformed.StatusCode}.");
        Assert.Equal(HttpStatusCode.OK, followUp.StatusCode);
        Assert.Equal("/on-demand", (await followUp.ReadHtmlAsync()).QuerySelector("#path")?.TextContent);
        Assert.DoesNotContain(site.Logs, entry => entry.Level >= Microsoft.Extensions.Logging.LogLevel.Error);
    }
}
