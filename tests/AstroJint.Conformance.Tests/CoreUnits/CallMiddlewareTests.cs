using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Adapted port of <c>packages/astro/test/units/middleware/call-middleware.test.ts</c>: how the result of a middleware is
/// turned into the response of the request, according to whether it called <c>next()</c> and what it returned (a
/// <c>Response</c>, <c>undefined</c> or another value), and what a middleware can do to the context and to the response
/// of <c>next()</c> (locals, headers, a clone of the body, a JSON body), synchronous or not.
/// <para>
/// Differences from upstream: <c>callMiddleware(handler, mockContext, responseFunction)</c> is called directly, with a mock
/// context and a response function that returns a given body. Here the same handlers (<c>src/handlers/call-middleware.ts</c>)
/// are the middleware of a real request, one URL each below <c>/call/</c>, and the response function is the endpoint
/// <c>src/pages/call/[...path].ts</c>, which answers the body of the upstream test (<c>original</c>, <c>from page</c>, …), so
/// every test is Adapted: it goes through the whole pipeline and answers over HTTP. The three tests that expect
/// <c>callMiddleware</c> to reject with an error named <c>MiddlewareNotAResponse</c> or <c>MiddlewareNoDataOrNextCalled</c>
/// expect the 500 of the request instead, and read the name of the error that failed it in the page of the fixture's
/// <c>500.astro</c>, which prints <c>Astro.props.error.name</c>.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/middleware/call-middleware.test.ts")]
public class CallMiddlewareTests(SiteHost sites)
{
    [Fact(DisplayName = "returns the middleware return value when next() is called and a Response is returned")]
    public async Task NextCalled_TheReturnedResponseIsTheResult()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/replace");

        Assert.Equal("modified", response.Text);
    }

    [Fact(DisplayName = "returns the responseFunction result when next() is called but middleware returns undefined")]
    public async Task NextCalled_AnUndefinedResultIsTheResponseOfNext()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/next-undefined");

        Assert.Equal("from page", response.Text);
    }

    [Fact(DisplayName = "throws MiddlewareNotAResponse when next() is called but middleware returns a non-Response")]
    public async Task NextCalled_ANonResponseFailsTheRequest()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/next-non-response");

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("""<p id="error-name">MiddlewareNotAResponse</p>""", response.Text);
    }

    [Fact(DisplayName = "returns the Response when middleware short-circuits without calling next()")]
    public async Task NextNotCalled_TheResponseIsTheResult()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/short-circuit");

        Assert.Equal("short-circuit", response.Text);
        Assert.Equal(200, response.StatusCode);
    }

    [Fact(DisplayName = "returns a 500 Response when middleware short-circuits with an error status")]
    public async Task NextNotCalled_AnErrorStatusIsTheResult()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/short-circuit-500");

        Assert.Equal(500, response.StatusCode);
    }

    [Fact(DisplayName = "throws MiddlewareNoDataOrNextCalled when middleware returns undefined without calling next()")]
    public async Task NextNotCalled_UndefinedFailsTheRequest()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/no-data");

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("""<p id="error-name">MiddlewareNoDataOrNextCalled</p>""", response.Text);
    }

    [Fact(DisplayName = "throws MiddlewareNotAResponse when middleware returns a non-Response without calling next()")]
    public async Task NextNotCalled_ANonResponseFailsTheRequest()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/non-response");

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("""<p id="error-name">MiddlewareNotAResponse</p>""", response.Text);
    }

    [Fact(DisplayName = "locals mutations are visible in the response function")]
    public async Task Context_LocalsMutationsAreVisibleToTheEndpoint()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/locals");

        Assert.Equal("name=bar", response.Text);
    }

    [Fact(DisplayName = "middleware can set response headers after calling next()")]
    public async Task Context_TheMiddlewareSetsHeadersOfTheResponse()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/headers");

        Assert.Equal("value", response.Header("X-Custom"));
    }

    [Fact(DisplayName = "middleware can clone the response, modify body, and return a new Response")]
    public async Task Context_TheMiddlewareClonesAndModifiesTheBody()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/clone");

        Assert.Equal("<p>it works</p>", response.Text);
    }

    [Fact(DisplayName = "middleware can intercept a JSON response, modify it, and return a new Response")]
    public async Task Context_TheMiddlewareModifiesAJsonResponse()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/json");

        using var json = JsonDocument.Parse(response.Text);
        Assert.Equal("REDACTED", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(42, json.RootElement.GetProperty("value").GetInt32());
    }

    [Fact(DisplayName = "works with a synchronous middleware that calls next()")]
    public async Task Synchronous_ThatCallsNext()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/sync-next");

        Assert.Equal("sync OK", response.Text);
    }

    [Fact(DisplayName = "works with a synchronous middleware that returns a Response")]
    public async Task Synchronous_ThatReturnsAResponse()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/call/sync-response");

        Assert.Equal("sync short-circuit", response.Text);
    }
}
