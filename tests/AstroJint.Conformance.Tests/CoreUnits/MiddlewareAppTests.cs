using System.Globalization;
using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/middleware/middleware-app.test.ts</c>: a middleware that runs through the whole
/// request pipeline sets locals, runs in a sequence, answers instead of the page (also with a 500 or for a route that does
/// not exist), fails, redirects, sets cookies, reads and rewrites the response of pages and endpoints, runs for the 404
/// page, guards a path by its <c>Authorization</c> header, and keeps its cookies and headers on the error pages.
/// <para>
/// Differences from upstream: it builds a different <c>App</c> with inline routes and a middleware for each test, while the
/// <c>app-middleware</c> fixture has one real <c>src/middleware.ts</c>, whose handlers (<c>src/handlers/middleware-app.ts</c>)
/// answer the URLs of one test each. So the tests use their own paths instead of <c>/</c>, <c>/lorem</c>, <c>/second</c> and
/// <c>/api/endpoint</c>: <c>/locals/set</c>, <c>/locals/by-url[/lorem]</c>, <c>/sequence[/second]</c>, <c>/passthrough</c>,
/// <c>/undefined</c>, <c>/cookies/middleware</c>, <c>/cookies/component</c>, <c>/clone</c>, <c>/api/redacted</c> (the second
/// <c>/api/endpoint</c> test, whose middleware is not the one of the first), <c>/api/multi/…</c> (the multiple cookies test,
/// which upstream runs on <c>/api/guarded/…</c> with a middleware of its own) and <c>/headers</c>. The pages print
/// <c>Astro.locals.name</c> as upstream's <c>simplePage()</c>, and the locals that upstream passes to <c>render()</c> come from
/// the request header <c>x-name</c> (see <see cref="AppMiddleware"/>). The manifest middleware of 'should call a second
/// middleware in a sequence via manifest' is a real <c>sequence(first, second)</c>. The valid <c>Authorization</c> value of the
/// admin test is redacted in the upstream file; it is <c>Bearer valid-token</c> here.
/// </para>
/// <para>
/// Adapted: the two framing tests. Upstream counts the calls of the middleware (the first answers with a stale
/// <c>Content-Length: 999</c> or <c>Transfer-Encoding: chunked</c>, the second, which renders the error page, passes the
/// response through) and reads the headers of the response of <c>App.render()</c>. A pool of engines cannot count calls, so the
/// middleware tells the passes apart by the status of what <c>next()</c> returned (200 of the page, then 404 or 500 of the
/// error page), and the host never sends the framing headers of the JavaScript response (it sets the
/// <c>Content-Length</c> of the buffered body and drops <c>Transfer-Encoding</c>), so the tests assert what a client sees: the
/// length is not the stale one but that of the body, there is no <c>chunked</c> coding, and the custom header is kept.
/// </para>
/// <para>
/// Extra assertions, not in upstream: the 500 of a middleware that returns <c>undefined</c> and of one that throws are the
/// 500 page of the fixture, which prints <c>Astro.props.error.name</c> (the name of the error that failed the request).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/middleware/middleware-app.test.ts")]
public class MiddlewareAppTests(SiteHost sites)
{
    [Fact(DisplayName = "should render locals data set by middleware")]
    public async Task Locals_AreSetByTheMiddleware()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/locals/set");

        Assert.Contains("<p>bar</p>", response.Text);
    }

    [Fact(DisplayName = "should change locals data based on URL")]
    public async Task Locals_ChangeWithTheUrl()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var index = await site.SendRawAsync("/locals/by-url");
        var lorem = await site.SendRawAsync("/locals/by-url/lorem");

        Assert.Contains("<p>bar</p>", index.Text);
        Assert.Contains("<p>ipsum</p>", lorem.Text);
    }

    [Fact(DisplayName = "should call a second middleware in a sequence via manifest")]
    public async Task Sequence_CallsTheSecondMiddleware()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var index = await site.SendRawAsync("/sequence");
        var second = await site.SendRawAsync("/sequence/second");

        Assert.Contains("<p>first</p>", index.Text);
        Assert.Contains("<p>second</p>", second.Text);
    }

    [Fact(DisplayName = "should successfully create a new response bypassing the page")]
    public async Task ShortCircuit_ANewResponseBypassesThePage()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/rewrite");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("<span>New content!!</span>", response.Text);
    }

    [Fact(DisplayName = "should return a new response that is a 500")]
    public async Task ShortCircuit_ANewResponseCanBeA500()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/broken-500");

        Assert.Equal(500, response.StatusCode);
    }

    [Fact(DisplayName = "should return 200 if middleware returns a 200 Response for a non-existent route")]
    public async Task ShortCircuit_A200ResponseAnswersARouteThatDoesNotExist()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/no-route-but-200");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("It's OK!", response.Text);
    }

    [Fact(DisplayName = "should render the page normally if middleware only calls next()")]
    public async Task PassThrough_RendersThePageNormally()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/passthrough", headers: ("x-name", "passthrough"));

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("<p>passthrough</p>", response.Text);
    }

    [Fact(DisplayName = "should throw when middleware returns undefined without calling next()")]
    public async Task Error_AMiddlewareThatReturnsUndefinedFailsTheRequest()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/undefined");

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("""<p id="error-name">MiddlewareNoDataOrNextCalled</p>""", response.Text);
    }

    [Fact(DisplayName = "should render 500.astro when middleware throws an error")]
    public async Task Error_AMiddlewareThatThrowsRendersThe500Page()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/throw");

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("<title>500</title>", response.Text);
        Assert.Contains("""<p id="error-name">Error</p>""", response.Text);
    }

    [Fact(DisplayName = "should successfully redirect to another page")]
    public async Task Redirect_RedirectsToAnotherPage()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/redirect");

        Assert.Equal(302, response.StatusCode);
        Assert.Equal("/", response.Header("Location"));
    }

    [Fact(DisplayName = "should allow middleware to set cookies")]
    public async Task Cookies_TheMiddlewareSetsCookies()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/cookies/middleware", headers: ("x-name", "test"));

        Assert.Contains(response.HeaderValues("Set-Cookie"), cookie => cookie.Contains("foo=bar"));
    }

    [Fact(DisplayName = "should forward cookies set in a component when middleware returns a new response")]
    public async Task Cookies_ThoseOfAComponentSurviveANewResponse()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/cookies/component");

        Assert.Contains(response.HeaderValues("Set-Cookie"), cookie => cookie.Contains("from-component=component-value"));
    }

    [Fact(DisplayName = "should be able to clone the response and modify it")]
    public async Task Response_CanBeClonedAndModified()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/clone");

        Assert.Contains("it works", response.Text);
        Assert.DoesNotContain("testing", response.Text);
    }

    [Fact(DisplayName = "should correctly work for API endpoints that return a Response object")]
    public async Task ApiEndpoint_ReturnsItsResponseThroughTheMiddleware()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/api/endpoint");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.Header("Content-Type"));
        using var json = JsonDocument.Parse(response.Text);
        Assert.Equal("test", json.RootElement.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "should correctly manipulate the response coming from API endpoints")]
    public async Task ApiEndpoint_TheMiddlewareManipulatesItsResponse()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/api/redacted");

        using var json = JsonDocument.Parse(response.Text);
        Assert.Equal("REDACTED", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(42, json.RootElement.GetProperty("value").GetInt32());
    }

    [Fact(DisplayName = "should correctly call middleware for 404 routes")]
    public async Task NotFound_TheMiddlewareRunsForThe404Page()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/unknown-page");

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("bar", response.Text);
    }

    [Fact(DisplayName = "should allow accessing /admin with valid auth header")]
    public async Task Auth_AdminIsAllowedWithAnAuthorizationHeader()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/admin", "GET", null,
            ("Authorization", "Bearer valid-token"), ("x-name", "admin-content"));

        Assert.Equal(200, response.StatusCode);
    }

    [Fact(DisplayName = "should redirect /admin without auth header")]
    public async Task Auth_AdminRedirectsWithoutAnAuthorizationHeader()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/admin");

        Assert.Equal(302, response.StatusCode);
        Assert.Equal("/", response.Header("Location"));
    }

    [Fact(DisplayName = "should handle requests with spaces in path correctly")]
    public async Task Path_WithSpacesIsHandled()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/path%20with%20spaces");

        Assert.Equal(200, response.StatusCode);
    }

    [Fact(DisplayName = "should preserve cookies set by middleware when returning Response(null, { status: 404 })")]
    public async Task ErrorPages_The404KeepsTheCookiesOfTheMiddleware()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/api/guarded/secret");

        Assert.Equal(404, response.StatusCode);
        Assert.Contains(response.HeaderValues("Set-Cookie"), cookie => cookie.Contains("session=abc123"));
    }

    [Fact(DisplayName = "should preserve cookies set by middleware when returning Response(null, { status: 500 })")]
    public async Task ErrorPages_The500KeepsTheCookiesOfTheMiddleware()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/api/error/test");

        Assert.Equal(500, response.StatusCode);
        Assert.Contains(response.HeaderValues("Set-Cookie"), cookie => cookie.Contains("csrf=token456"));
    }

    [Fact(DisplayName = "should preserve multiple cookies from sequenced middleware during error page rerouting")]
    public async Task ErrorPages_MultipleCookiesOfTheMiddlewareAreKept()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/api/multi/secret");

        Assert.Equal(404, response.StatusCode);
        var cookies = response.HeaderValues("Set-Cookie");
        Assert.Contains(cookies, cookie => cookie.Contains("session=abc123"));
        Assert.Contains(cookies, cookie => cookie.Contains("csrf=token456"));
        Assert.Contains(cookies, cookie => cookie.Contains("auth_attempt=failed"));
    }

    [Fact(DisplayName = "should not preserve Content-Length from middleware when rendering 404 error page")]
    public async Task FramingHeaders_TheContentLengthOfTheMiddlewareDoesNotReachThe404()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/api/framing-404/secret");

        Assert.Equal(404, response.StatusCode);
        Assert.NotEqual("999", response.Header("Content-Length"));
        Assert.Equal(response.Body.Length.ToString(CultureInfo.InvariantCulture), response.Header("Content-Length"));
        Assert.Equal("keep-me", response.Header("X-Custom"));
    }

    [Fact(DisplayName = "should not preserve Transfer-Encoding from middleware when rendering 500 error page")]
    public async Task FramingHeaders_TheTransferEncodingOfTheMiddlewareDoesNotReachThe500()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/api/framing-500/test");

        Assert.Equal(500, response.StatusCode);
        Assert.Null(response.Header("Transfer-Encoding"));
        Assert.Equal("middleware", response.Header("X-Error-Source"));
    }

    [Fact(DisplayName = "should correctly set custom headers in middleware")]
    public async Task Headers_TheMiddlewareSetsCustomHeaders()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/headers", headers: ("x-name", "test"));

        Assert.Equal("custom-value", response.Header("X-Custom-Header"));
    }
}
