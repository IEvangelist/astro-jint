using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/locals.test.ts</c>. The Node adapter takes locals from its middleware
/// (<c>handler(req, res, next, locals)</c>); the ASP.NET Core host takes them from <c>AstroOptions.LocalsFactory</c>.
/// </summary>
[Trait("Upstream", "node/locals.test.ts")]
public class LocalsTests(SiteHost sites)
{
    private const string Fixture = "locals";

    [Fact(DisplayName = "Can use locals added by node middleware")]
    public async Task Locals_FromTheHost_ReachPages()
    {
        var site = await sites.GetAsync(Fixture, variant: "foo-bar", configure: options => options.LocalsFactory = _ => new { foo = "bar" });

        var html = await site.GetHtmlAsync("/from-node-middleware");

        Assert.Equal("bar", html.QuerySelector("h1")!.TextContent);
    }

    [Fact(DisplayName = "Throws an error when provided non-objects as locals")]
    public async Task Locals_ThatAreNotAnObject_FailTheRequest()
    {
        var site = await sites.GetAsync(Fixture, variant: "string-locals", configure: options => options.LocalsFactory = _ => "locals");

        using var response = await site.GetAsync("/from-node-middleware");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact(DisplayName = "Can use locals added by astro middleware")]
    public async Task Locals_FromAstroMiddleware_ReachPages()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await site.GetHtmlAsync("/from-astro-middleware");

        Assert.Equal("baz", html.QuerySelector("h1")!.TextContent);
    }

    [Fact(DisplayName = "Can access locals in API")]
    public async Task Locals_FromTheHost_ReachEndpoints()
    {
        var site = await sites.GetAsync(Fixture, variant: "foo-bar", configure: options => options.LocalsFactory = _ => new { foo = "bar" });

        using var response = await site.SendAsync(HttpMethod.Post, "/api");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("bar", json.RootElement.GetProperty("foo").GetString());
    }
}
