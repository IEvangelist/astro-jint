using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/locals.test.ts</c>: the <c>locals</c> that the caller gives to
/// <c>App.render()</c> reach pages, endpoints and the 404 and 500 pages, and the error pages also see what a middleware
/// mutated before it failed.
/// <para>
/// Differences from upstream: the caller of <c>App.render()</c> is the ASP.NET Core host, whose locals come from
/// <c>AstroOptions.LocalsFactory</c>. The tests run on a site whose factory turns the request header <c>x-foo</c> into
/// <c>{ foo }</c>, so each request chooses the value that upstream passed in the render options. The pages are the real
/// <c>locals/foo</c>, <c>locals/api</c> and <c>locals/go-to-error-page</c> of the <c>app-pages</c> fixture, and the 404 and 500
/// pages print <c>Astro.locals.foo</c>. The last test uses <c>/mutated/go-to-error-page</c>: the fixture middleware sets
/// <c>locals.foo</c> for the paths below <c>/mutated/</c> and throws when it runs for the 500 page, where upstream built a
/// separate app with such a middleware. The test that gives <c>routeData: undefined</c> to <c>render()</c> is the same
/// request, as the host always matches the route first.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/locals.test.ts")]
public class LocalsTests(SiteHost sites)
{
    private const string Fixture = "app-pages";
    private const string Variant = "foo-header";

    private Task<ConformanceSite> GetSiteAsync() =>
        sites.GetAsync(Fixture, variant: Variant, configure: options =>
            options.LocalsFactory = context => new { foo = context.Request.Headers["x-foo"].ToString() });

    [Fact(DisplayName = "Can access Astro.locals in page")]
    public async Task Page_ReadsTheLocals()
    {
        var site = await GetSiteAsync();

        var response = await site.SendRawAsync("/locals/foo", headers: ("x-foo", "bar"));

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("""<h1 id="foo">bar</h1>""", response.Text);
    }

    [Fact(DisplayName = "Can access Astro.locals in api context")]
    public async Task Endpoint_ReadsTheLocals()
    {
        var site = await GetSiteAsync();

        var response = await site.SendRawAsync("/locals/api", headers: ("x-foo", "bar"));

        Assert.Equal(200, response.StatusCode);
        using var json = JsonDocument.Parse(response.Text);
        Assert.Equal("bar", json.RootElement.GetProperty("foo").GetString());
    }

    [Fact(DisplayName = "404.astro can access locals provided to app.render()")]
    public async Task NotFoundPage_ReadsTheLocals()
    {
        var site = await GetSiteAsync();

        var response = await site.SendRawAsync("/slkfnasf", headers: ("x-foo", "par"));

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("""id="foo">par""", response.Text);
    }

    [Fact(DisplayName = "500.astro can access locals provided to app.render()")]
    public async Task ServerErrorPage_ReadsTheLocals()
    {
        var site = await GetSiteAsync();

        var response = await site.SendRawAsync("/locals/go-to-error-page", headers: ("x-foo", "par"));

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("""id="foo">par""", response.Text);
    }

    [Fact(DisplayName = "404.astro/500.astro can access mutated locals even if middleware is skipped")]
    public async Task ServerErrorPage_ReadsTheLocalsMutatedByAMiddlewareThatFailsForIt()
    {
        var site = await GetSiteAsync();

        var response = await site.SendRawAsync("/mutated/go-to-error-page");

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("""id="foo">mutated-property""", response.Text);
    }
}
