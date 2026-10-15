using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Adapted port of <c>packages/astro/test/units/middleware/sequence.test.ts</c>: <c>sequence()</c> without handlers passes
/// the request through, runs handlers in order, shares the context between them, lets the last one rewrite the response of
/// the page, mixes synchronous and asynchronous handlers, ignores falsy ones, lets an earlier one short-circuit the chain,
/// accumulates cookies and stops at a handler that redirects.
/// <para>
/// Differences from upstream: <c>sequence()</c> is called and its result is run through <c>callMiddleware</c> with a mock
/// context and a response function, and the test then reads the context and the closures (the locals, the order the
/// handlers ran in, the cookies of the jar). Here the combined handler of each test (<c>src/handlers/sequence.ts</c>, built
/// with the <c>sequence</c> of <c>astro:middleware</c>) is the middleware of one URL below <c>/seq/</c>, and the response
/// function is the endpoint <c>src/pages/seq/[...path].ts</c>, which answers the body of the upstream test, so every
/// test is Adapted. What the upstream test reads afterwards, the response tells: the locals as the JSON of the
/// <c>x-locals</c> header, the order of the calls as the <c>x-order</c> header (<c>1,2,3</c>), and the cookies as the
/// <c>Set-Cookie</c> headers that the host sends for them.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/middleware/sequence.test.ts")]
public class MiddlewareSequenceTests(SiteHost sites)
{
    private static JsonElement Locals(RawResponse response)
    {
        var header = response.Header("x-locals");
        Assert.NotNull(header);
        using var json = JsonDocument.Parse(header);
        return json.RootElement.Clone();
    }

    [Fact(DisplayName = "returns a passthrough middleware when called with no handlers")]
    public async Task NoHandlers_PassesTheRequestThrough()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/seq/none");

        Assert.Equal("passthrough", response.Text);
    }

    [Fact(DisplayName = "works with a single handler")]
    public async Task SingleHandler_RunsAndSetsTheLocals()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/seq/single");

        Assert.Equal("single", response.Text);
        Assert.True(Locals(response).GetProperty("touched").GetBoolean());
    }

    [Fact(DisplayName = "executes handlers in order")]
    public async Task Handlers_RunInOrder()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/seq/order");

        Assert.Equal("1,2,3", response.Header("x-order"));
    }

    [Fact(DisplayName = "propagates context mutations across handlers")]
    public async Task Handlers_ShareTheContext()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/seq/propagate");

        Assert.Equal("a-ab", response.Text);
    }

    [Fact(DisplayName = "allows the last handler to modify the response from the page")]
    public async Task LastHandler_ModifiesTheResponseOfThePage()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/seq/upper");

        Assert.Equal("HELLO WORLD", response.Text);
    }

    [Fact(DisplayName = "supports mixed sync and async handlers")]
    public async Task Handlers_MixSyncAndAsync()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/seq/mixed");

        Assert.Equal("mixed", response.Text);
        Assert.True(Locals(response).GetProperty("async").GetBoolean());
    }

    [Fact(DisplayName = "filters out falsy handlers")]
    public async Task FalsyHandlers_AreIgnored()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/seq/falsy");

        Assert.Equal("1,2", response.Header("x-order"));
    }

    [Fact(DisplayName = "allows earlier handlers to short-circuit the chain")]
    public async Task EarlierHandler_ShortCircuitsTheChain()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/seq/short-circuit");

        Assert.Equal("short-circuit", response.Text);
        Assert.Equal("1", response.Header("x-order")); // the second handler was never called
    }

    [Fact(DisplayName = "accumulates cookies set by multiple handlers")]
    public async Task Handlers_AccumulateCookies()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/seq/cookies");

        var cookies = response.HeaderValues("Set-Cookie");
        Assert.Contains(cookies, cookie => cookie.StartsWith("cookie1=value1"));
        Assert.Contains(cookies, cookie => cookie.StartsWith("cookie2=value2"));
    }

    [Fact(DisplayName = "handles a chain where middle handler returns a redirect")]
    public async Task MiddleHandler_RedirectsAndTheChainStops()
    {
        var site = await sites.GetMiddlewareSiteAsync();

        var response = await site.SendRawAsync("/seq/redirect");

        Assert.Equal(302, response.StatusCode);
        Assert.Equal("/login", response.Header("Location"));
        Assert.True(Locals(response).GetProperty("beforeRedirect").GetBoolean());
    }
}
