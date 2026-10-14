using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/middleware.test.ts</c>. Upstream mixes dev-server tests, tests that call
/// <c>app.render()</c> on a test-adapter build and tests of build output; here every request is an HTTP request to the host.
/// <list type="bullet">
/// <item>The five tests of 'Middleware in DEV mode — integration hooks' and 'Integration hooks with no user middleware' run
/// the integration middleware registered with <c>addMiddleware()</c> (order <c>pre</c>, <c>post</c> and an entrypoint given
/// as a URL) in the dev server. There is no dev server here, so they are adapted to the production build: the same
/// integrations, the same pages and the same JSON answers, served by the host.</item>
/// <item>'should build the project without errors' needs the build to succeed (the harness fails when it does not). The port
/// also checks that the server entry was written and that the first request is what imports the middleware: the module
/// throws as soon as it is imported, so that request answers 500.</item>
/// <item><c>app.render(request, { routeData: app.match(request) })</c> becomes a plain GET: the adapter matches the route
/// itself, so a URL without a route reaches the app with no <c>routeData</c> just as it does upstream.</item>
/// <item>'can set locals for prerendered pages to use' reads <c>client/prerendered/index.html</c> upstream; the port requests
/// <c>/prerendered/</c> and gets the same file from the host.</item>
/// <item>The double and triple encoded <c>/admin</c> requests keep their percent signs on the wire (<c>%2561dmin</c> is
/// <c>%61dmin</c> after one decode), exactly as <c>new Request('http://example.com/%2561dmin')</c> does upstream. The
/// redirect target <c>/</c> is asserted as well.</item>
/// <item>Not ported: the edge middleware entrypoint hook ('the integration should receive the path to the middleware'), the
/// Tailwind static build and the four Middleware HMR tests; see <c>docs/conformance/core.md</c>.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/middleware.test.ts")]
public class MiddlewareTests(SiteHost sites)
{
    private const string Fixture = "core-middleware";
    private const string NoUserMiddlewareFixture = "core-middleware-no-user";
    private const string FullSsrFixture = "core-middleware-full-ssr";

    private async Task AssertJsonAnswerAsync(string fixture, string path, string property)
    {
        var site = await sites.GetAsync(fixture);

        using var response = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.ReadTextAsync());
        Assert.Equal("works", json.RootElement.GetProperty(property).GetString());
    }

    [Fact(DisplayName = "Middleware in DEV mode \u2014 integration hooks: Integration middleware marked as \"pre\" runs")]
    public Task IntegrationMiddleware_MarkedPre_Runs() =>
        AssertJsonAnswerAsync(Fixture, "/integration-pre", "pre");

    [Fact(DisplayName = "Middleware in DEV mode \u2014 integration hooks: Integration middleware marked as \"post\" runs")]
    public Task IntegrationMiddleware_MarkedPost_Runs() =>
        AssertJsonAnswerAsync(Fixture, "/integration-post", "post");

    [Fact(DisplayName = "Integration hooks with no user middleware: Integration middleware marked as \"pre\" runs")]
    public Task WithoutUserMiddleware_IntegrationMiddleware_MarkedPre_Runs() =>
        AssertJsonAnswerAsync(NoUserMiddlewareFixture, "/pre", "pre");

    [Fact(DisplayName = "Integration hooks with no user middleware: Integration middleware marked as \"post\" runs")]
    public Task WithoutUserMiddleware_IntegrationMiddleware_MarkedPost_Runs() =>
        AssertJsonAnswerAsync(NoUserMiddlewareFixture, "/post", "post");

    [Fact(DisplayName = "Integration hooks with no user middleware: Integration middleware marked as \"url\" runs")]
    public Task WithoutUserMiddleware_IntegrationMiddleware_GivenAsUrl_Runs() =>
        AssertJsonAnswerAsync(NoUserMiddlewareFixture, "/url", "post");

    [Fact(DisplayName = "Middleware should not be executed or imported during: should build the project without errors")]
    public async Task MiddlewareThatThrowsWhenImported_DoesNotBreakTheBuild()
    {
        var site = await sites.GetAsync(FullSsrFixture);

        Assert.True(site.PathExists("server/entry.mjs"));

        // The module only runs when a request imports the middleware, and it throws then.
        using var response = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact(DisplayName = "Middleware API in PROD mode, SSR: can render a page that does not exist")]
    public async Task UnknownPage_IsRewrittenByTheIntegrationMiddleware()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Null(document.QuerySelector("p"));
        Assert.Equal("New content!!", document.QuerySelector("span")?.InnerHtml);
    }

    [Fact(DisplayName = "Middleware API in PROD mode, SSR: can set locals for prerendered pages to use")]
    public async Task PrerenderedPage_SeesTheLocalsTheMiddlewareSetAtBuildTime()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/prerendered/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<p>yes they can!</p>", await response.ReadTextAsync());
    }

    // %2561dmin is decoded iteratively (%2561 -> %61 -> a -> admin), so the middleware sees /admin and, with no
    // Authorization header, redirects.
    [Fact(DisplayName = "Middleware API in PROD mode, SSR: Path encoding in middleware: middleware protects double-encoded /admin path")]
    public async Task DoubleEncodedAdminPath_TriggersTheMiddlewareRedirect()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/%2561dmin");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", Assert.Single(response.HeaderValues("Location")));
    }

    // %252561dmin -> %2561dmin -> %61dmin -> admin
    [Fact(DisplayName = "Middleware API in PROD mode, SSR: Path encoding in middleware: middleware protects triple-encoded /admin path")]
    public async Task TripleEncodedAdminPath_TriggersTheMiddlewareRedirect()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/%252561dmin");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", Assert.Single(response.HeaderValues("Location")));
    }
}
