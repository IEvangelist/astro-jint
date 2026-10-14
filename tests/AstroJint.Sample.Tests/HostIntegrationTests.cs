using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AstroJint.Sample;

/// <summary>The sample host around the site: ASP.NET Core endpoints next to the Astro routes, configuration and forwarded headers.</summary>
public partial class HostIntegrationTests(SampleSite site)
{
    [GeneratedRegex(@"^astro;dur=\d+$")]
    private static partial Regex ServerTiming();

    [Fact]
    public async Task AnEndpointOfTheHostWinsOverTheSite()
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Null(response.Header("X-Request-Id"));
    }

    [Fact]
    public async Task HostEndpointsLiveNextToTheRoutesOfAstro()
    {
        using var client = site.CreateClient();

        using var host = await client.GetAsync("/api/host");
        using var json = JsonDocument.Parse(await host.Content.ReadAsStringAsync());
        using var astro = await client.GetAsync("/api/time");

        Assert.StartsWith(".NET", json.RootElement.GetProperty("framework").GetString());
        Assert.NotNull(astro.Header("X-Request-Id"));
    }

    [Theory]
    [InlineData("/time")]
    [InlineData("/products/1")]
    [InlineData("/api/time")]
    [InlineData("/me")]
    public async Task TheMiddlewareDecoratesEveryOnDemandResponse(string path)
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(response.Header("X-Request-Id")));
        Assert.Matches(ServerTiming(), response.Header("Server-Timing") ?? "");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/about")]
    [InlineData("/blog/hello-jint")]
    public async Task TheMiddlewareDoesNotRunForAFile(string path)
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Header("X-Request-Id"));
        Assert.Null(response.Header("Server-Timing"));
    }

    [Fact]
    public async Task TheDefaultsOfTheEnvironmentSchemaApply()
    {
        using var client = site.CreateClient();

        var page = await (await client.GetAsync("/diagnostics")).ReadHtmlAsync();

        Assert.Equal("Hello from Jint", page.Text("#greeting"));
        Assert.Equal("not configured", page.Text("#api-token"));
        Assert.Equal("127.0.0.1", page.Text("#client-address"));
        Assert.Equal("http://localhost/diagnostics", page.Text("#url"));
    }

    [Fact]
    public async Task TheSecretsOfTheEnvironmentSchemaComeFromTheConfigurationOfTheHost()
    {
        using var client = site.CreateClient(new Dictionary<string, string?> { ["Astro:Env:API_TOKEN"] = "s3cret-token" });

        using var response = await client.GetAsync("/diagnostics");
        var body = await response.Content.ReadAsStringAsync();
        var page = body.ParseHtml();

        Assert.Equal("configured (12 characters)", page.Text("#api-token"));
        Assert.DoesNotContain("s3cret-token", body);
    }

    [Fact]
    public async Task APublicVariableOfTheEnvironmentSchemaIsFixedWhenTheSiteIsBuilt()
    {
        // Astro inlines the public variables of the server in the bundle, so the host cannot change them later.
        using var client = site.CreateClient(new Dictionary<string, string?> { ["Astro:Env:GREETING"] = "Bonjour" });

        var page = await (await client.GetAsync("/diagnostics")).ReadHtmlAsync();

        Assert.Equal("Hello from Jint", page.Text("#greeting"));
    }

    [Fact]
    public async Task TheHeadersOfAProxyDecideWhoTheVisitorIsAndWhereTheyCameFrom()
    {
        using var client = site.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/diagnostics?from=proxy");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "sample.example");

        using var response = await client.SendAsync(request);
        var page = await response.ReadHtmlAsync();

        Assert.Equal("203.0.113.7", page.Text("#client-address"));
        Assert.Equal("https://sample.example/diagnostics?from=proxy", page.Text("#url"));
    }
}
