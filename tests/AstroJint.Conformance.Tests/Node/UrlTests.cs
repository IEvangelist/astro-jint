namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/url.test.ts</c>. The page prints <c>Astro.url.href</c>. The upstream tests
/// hand fake requests to the Node handler; these send the same <c>Host</c> and <c>X-Forwarded-*</c> headers to the
/// ASP.NET Core host. The fixture lists <c>abc.xyz:444</c>, <c>legitimate.example.com</c> and <c>localhost:3000</c> as
/// allowed domains. A <c>Host</c> that is not allowed gives <c>localhost</c> and the port the server listens on, as it does
/// in the Node adapter, so the "default" URL of these tests is <c>http://localhost:{port}/</c>.
/// </summary>
[Trait("Upstream", "node/url.test.ts")]
public class UrlTests(SiteHost sites)
{
    private const string Fixture = "url";

    private static async Task<string> GetUrlAsync(ConformanceSite site, params (string Name, string Value)[] headers)
    {
        using var response = await site.SendAsync(HttpMethod.Get, "/", content: null, headers);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var html = await response.ReadHtmlAsync();
        return html.QuerySelector("body")!.TextContent.Trim();
    }

    [Fact(DisplayName = "return http when non-secure")]
    public async Task Url_IsHttp_WhenNotSecure()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await site.GetStringAsync("/");

        Assert.Contains("http:", html);
        Assert.DoesNotContain("https:", html);
    }

    [Fact(DisplayName = "return https when secure")]
    public async Task Url_IsHttps_WhenSecure()
    {
        var site = await sites.GetAsync(Fixture, https: true);

        var html = await site.GetStringAsync("/");

        Assert.DoesNotContain("http:", html);
        Assert.Contains("https:", html);
    }

    [Fact(DisplayName = "return http when the X-Forwarded-Proto header is set to http")]
    public async Task Url_IsHttp_WhenForwardedProtoIsHttp()
    {
        var site = await sites.GetAsync(Fixture);

        var url = await GetUrlAsync(site, ("X-Forwarded-Proto", "http"));

        Assert.StartsWith("http:", url);
    }

    [Fact(DisplayName = "return https when the X-Forwarded-Proto header is set to https")]
    public async Task Url_IsHttps_WhenForwardedProtoIsHttps()
    {
        var site = await sites.GetAsync(Fixture);

        var url = await GetUrlAsync(site, ("X-Forwarded-Proto", "https"));

        Assert.StartsWith("https:", url);
    }

    [Fact(DisplayName = "includes forwarded host and port in the url")]
    public async Task Url_IncludesForwardedHostAndPort()
    {
        var site = await sites.GetAsync(Fixture);

        var url = await GetUrlAsync(
            site,
            ("X-Forwarded-Proto", "https"),
            ("X-Forwarded-Host", "abc.xyz"),
            ("X-Forwarded-Port", "444"),
            ("Host", "localhost:3000"));

        Assert.Equal("https://abc.xyz:444/", url);
    }

    [Fact(DisplayName = "accepts port in forwarded host and forwarded port")]
    public async Task Url_AcceptsPortInForwardedHost()
    {
        var site = await sites.GetAsync(Fixture);

        var url = await GetUrlAsync(
            site,
            ("X-Forwarded-Proto", "https"),
            ("X-Forwarded-Host", "abc.xyz:444"),
            ("X-Forwarded-Port", "444"));

        Assert.Equal("https://abc.xyz:444/", url);
    }

    [Fact(DisplayName = "ignores X-Forwarded-Host when no allowedDomains configured")]
    public async Task Url_IgnoresForwardedHost_ThatIsNotAllowed_AndUsesHost()
    {
        var site = await sites.GetAsync(Fixture);

        var url = await GetUrlAsync(
            site,
            ("X-Forwarded-Proto", "https"),
            ("X-Forwarded-Host", "malicious.example.com"),
            ("Host", "legitimate.example.com"));

        Assert.Equal("https://legitimate.example.com/", url);
    }

    [Fact(DisplayName = "rejects Host when it does not match allowedDomains")]
    public async Task Url_RejectsHost_ThatIsNotAllowed()
    {
        var site = await sites.GetAsync(Fixture);

        var url = await GetUrlAsync(site, ("Host", "attacker.example.net"));

        Assert.Equal("localhost", new Uri(url).Host);
        Assert.Equal(site.BaseAddress.Port, new Uri(url).Port);
    }

    [Fact(DisplayName = "rejects port in forwarded host when port not in allowedDomains")]
    public async Task Url_RejectsForwardedPort_ThatIsNotAllowed()
    {
        var site = await sites.GetAsync(Fixture);

        var url = await GetUrlAsync(
            site,
            ("X-Forwarded-Proto", "https"),
            ("X-Forwarded-Host", "abc.xyz:8080"),
            ("Host", "localhost:3000"));

        Assert.Equal("https://localhost:3000/", url);
    }

    [Fact(DisplayName = "rejects empty X-Forwarded-Host with allowedDomains configured")]
    public async Task Url_RejectsEmptyForwardedHost()
    {
        var site = await sites.GetAsync(Fixture);

        var url = await GetUrlAsync(
            site,
            ("X-Forwarded-Proto", "https"),
            ("X-Forwarded-Host", ""),
            ("Host", "legitimate.example.com"));

        Assert.Equal("https://legitimate.example.com/", url);
    }

    [Fact(DisplayName = "rejects X-Forwarded-Host with path injection attempt")]
    public async Task Url_RejectsForwardedHost_WithAPath()
    {
        var site = await sites.GetAsync(Fixture);

        var url = await GetUrlAsync(
            site,
            ("X-Forwarded-Proto", "https"),
            ("X-Forwarded-Host", "example.com/admin"),
            ("Host", "localhost:3000"));

        Assert.Equal("https://localhost:3000/", url);
    }
}
