using System.Net;

namespace AstroJint.Conformance.Host;

/// <summary>
/// What the ASP.NET Core host hands to the Astro bundle (client address, locals, <c>astro:env</c>) and the prerendered
/// error pages it reads from disk. Upstream has no tests for these: its adapters get this data from their platform.
/// </summary>
public class HostContextTests(SiteHost sites)
{
    private const string Fixture = "jint-host-context";

    private static readonly string[] AdminAndEditor = ["admin", "editor"];

    [Fact]
    public async Task ClientAddress_IsTheRemoteAddressOfTheConnection()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await site.GetHtmlAsync("/client-address");

        Assert.Equal("127.0.0.1", html.QuerySelector("#address")!.TextContent);
    }

    [Fact]
    public async Task Locals_DefaultToAnEmptyObject()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await site.GetHtmlAsync("/locals");

        Assert.Equal("{}", html.QuerySelector("#locals")!.TextContent);
    }

    [Fact]
    public async Task Locals_AreProducedByTheHostForEveryRequest()
    {
        var site = await sites.GetAsync(Fixture, variant: "locals", configure: options =>
            options.LocalsFactory = context => new { user = "ada", path = context.Request.Path.Value, roles = AdminAndEditor });

        var first = await site.GetHtmlAsync("/locals");
        var second = await site.GetHtmlAsync("/locals?again");

        const string Expected = """{"user":"ada","path":"/locals","roles":["admin","editor"]}""";
        Assert.Equal(Expected, first.QuerySelector("#locals")!.TextContent);
        Assert.Equal(Expected, second.QuerySelector("#locals")!.TextContent);
    }

    [Fact]
    public async Task Env_UsesTheSchemaDefaultWhenTheHostSetsNothing()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await site.GetHtmlAsync("/env");

        Assert.Equal("hello", html.QuerySelector("#greeting")!.TextContent);
        Assert.Equal("unset", html.QuerySelector("#token")!.TextContent);
        Assert.Equal("unset", html.QuerySelector("#secret")!.TextContent);
    }

    [Fact]
    public async Task Env_SecretsComeFromTheOptionsOfTheHostAtRuntime()
    {
        var site = await sites.GetAsync(Fixture, variant: "env", configure: options => options.Env["API_TOKEN"] = "s3cret");

        var html = await site.GetHtmlAsync("/env");

        Assert.Equal("s3cret", html.QuerySelector("#token")!.TextContent);
        Assert.Equal("s3cret", html.QuerySelector("#secret")!.TextContent);
    }

    [Fact]
    public async Task Env_PublicVariablesAreInlinedAtBuildTime()
    {
        var site = await sites.GetAsync(Fixture, variant: "env-public", configure: options => options.Env["GREETING"] = "hi there");

        var html = await site.GetHtmlAsync("/env");

        Assert.Equal("hello", html.QuerySelector("#greeting")!.TextContent);
    }

    [Fact]
    public async Task UnknownRoutes_RenderThePrerendered404Page()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/does-not-exist");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Custom 404", html.QuerySelector("h1")!.TextContent);
    }

    [Fact]
    public async Task PagesThatThrow_RenderThePrerendered500Page()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/throws");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Custom 500", html.QuerySelector("h1")!.TextContent);
    }

    [Fact]
    public async Task PrerenderedPages_AreServedFromDisk()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Home", html.QuerySelector("h1")!.TextContent);
        Assert.NotNull(response.Headers.ETag);
    }
}
