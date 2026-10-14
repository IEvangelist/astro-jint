using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/actions.test.ts</c>. Upstream calls <c>app.render()</c> on a test-adapter build and starts
/// dev servers for three tests; here every request is an HTTP request to the host.
/// <list type="bullet">
/// <item><c>new Request('http://example.com/...')</c> becomes a request to the host. The <c>Referer</c> headers of the form
/// posts carry the address of the site instead of <c>example.com</c>, so that the redirects back to the referer stay on it.</item>
/// <item><c>new FormData()</c> is an empty <c>multipart/form-data</c> body (just the closing boundary), which is what a
/// form without fields sends.</item>
/// <item><c>app.render(request, { addCookieHeader: true })</c> is what the adapter always does, so the <c>Set-Cookie</c> headers of
/// the redirect are on the wire. The test client does not follow redirects: <c>followExpectedRedirect</c> becomes a helper
/// that checks the status against Astro's <c>REDIRECT_STATUS_CODES</c> and sends the <c>Location</c> with the cookie as
/// <c>name=value</c>. Upstream passes the whole <c>Set-Cookie</c> header as the <c>Cookie</c> header, which the cookie
/// parser reads the same way.</item>
/// <item>'Does not process middleware cookie for prerendered routes' runs in the dev server upstream. It is adapted to the
/// production build: <c>/subscribe-prerendered</c> is a static file, which the host serves without asking the app, so the
/// cookie cannot change it. The cookie value is built from <c>serializeActionResult()</c> as upstream does.</item>
/// <item>'starts in dev and exposes action RPC routes' needs a dev server for a project that has no adapter. The port builds the
/// same project (static output, one <c>prerender = false</c> page, the <c>ping</c> action) with the adapter and posts to the
/// RPC route of the host. <c>devalue.parse()</c> is replaced by the <c>Devalue</c> reader of this folder.</item>
/// <item>'Works with adapter and all pages prerendered' uses the <c>actions</c> project with <c>output: 'static'</c> in the dev
/// server. That project cannot be built as static output, because <c>invalid.astro</c> calls an action while it is prerendered.
/// The port uses a small project (<c>core-actions-static</c>) with the <c>subscribe</c> action and, in the variant of this
/// test, no on-demand page: only the injected <c>/_actions</c> route renders on request.</item>
/// <item>'Ignores action name for GET requests' asserts <c>assert.ok($('#user'))</c> upstream, which is true for any cheerio
/// selection and names an element that the page does not have. The port asserts that the action did not run: the page answers
/// 200 and its error elements are empty.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/actions.test.ts")]
public class ActionsTests(SiteHost sites)
{
    private const string Fixture = "core-actions";
    private const string StaticFixture = "core-actions-static";

    // Astro's REDIRECT_STATUS_CODES (core/constants.ts).
    private static readonly int[] RedirectStatusCodes = [301, 302, 303, 307, 308, 300, 304];

    private static ByteArrayContent EmptyFormData()
    {
        const string boundary = "----conformance-formdata";
        var content = new ByteArrayContent(Encoding.ASCII.GetBytes($"--{boundary}--\r\n"));
        content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");
        return content;
    }

    private static ByteArrayContent Json(string json)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static Task<HttpResponseMessage> PostEmptyFormAsync(ConformanceSite site, string pathAndQuery, string referrerPath) =>
        site.SendAsync(HttpMethod.Post, pathAndQuery, EmptyFormData(), ("Referer", new Uri(site.BaseAddress, referrerPath).ToString()));

    private static async Task<HttpResponseMessage> FollowExpectedRedirectAsync(ConformanceSite site, string pathAndQuery, string referrerPath)
    {
        using var redirect = await PostEmptyFormAsync(site, pathAndQuery, referrerPath);

        Assert.Contains((int)redirect.StatusCode, RedirectStatusCodes);
        Assert.NotNull(redirect.Headers.Location);
        var location = new Uri(site.BaseAddress, redirect.Headers.Location);
        var payload = redirect.SetCookieValue("ACTION_PAYLOAD");
        Assert.False(string.IsNullOrEmpty(payload), "Expected the redirect to set the ACTION_PAYLOAD cookie.");
        return await site.SendAsync(HttpMethod.Get, location.PathAndQuery, null, ("Cookie", $"ACTION_PAYLOAD={payload}"));
    }

    [Fact(DisplayName = "Astro Actions: dev: Does not process middleware cookie for prerendered routes")]
    public async Task PrerenderedRoute_IgnoresTheActionPayloadCookie()
    {
        var site = await sites.GetAsync(Fixture);
        var payload = JsonSerializer.Serialize(new
        {
            actionName = "subscribe",
            // serializeActionResult({ data: { channel: 'bholmesdev', subscribeButtonState: 'smashed' }, error: undefined })
            actionResult = new
            {
                type = "data",
                status = 200,
                contentType = "application/json+devalue",
                body = """[{"channel":1,"subscribeButtonState":2},"bholmesdev","smashed"]""",
            },
        });

        using var response = await site.SendAsync(
            HttpMethod.Get, "/subscribe-prerendered", null, ("Cookie", $"_astroActionPayload={WebUtility.UrlEncode(payload)}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("No cookie found.", document.Body?.TextContent.Trim());
    }

    [Fact(DisplayName = "Astro Actions: build: Should fail when calling an action without using Astro.callAction")]
    public async Task Page_CallingAnActionDirectly_Answers500()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/invalid");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact(DisplayName = "Astro Actions: build: Response middleware fallback - POST")]
    public async Task FormPost_RunsTheActionAndRendersItsResult()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await PostEmptyFormAsync(site, "/user?_action=getUser", "/user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("Houston", document.QuerySelector("#user")?.TextContent);
    }

    [Fact(DisplayName = "Astro Actions: build: Response middleware fallback - cookie forwarding")]
    public async Task FormPost_WithCookieForwarding_RedirectsAndRendersTheForwardedResult()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await FollowExpectedRedirectAsync(site, "/user?_action=getUser&actionCookieForwarding=true", "/user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("Houston", document.QuerySelector("#user")?.TextContent);
    }

    [Fact(DisplayName = "Astro Actions: build: Respects custom errors - POST")]
    public async Task FormPost_WhenTheActionThrows_AnswersWithTheStatusOfTheError()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await PostEmptyFormAsync(site, "/user-or-throw?_action=getUserOrThrow", "/user-or-throw");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("Not logged in", document.QuerySelector("#error-message")?.TextContent);
        Assert.Equal("UNAUTHORIZED", document.QuerySelector("#error-code")?.TextContent);
    }

    [Fact(DisplayName = "Astro Actions: build: Respects custom errors - cookie forwarding")]
    public async Task FormPost_WhenTheActionThrows_WithCookieForwarding_AnswersWithTheStatusOfTheError()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await FollowExpectedRedirectAsync(
            site, "/user-or-throw?_action=getUserOrThrow&actionCookieForwarding=true", "/user-or-throw");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("Not logged in", document.QuerySelector("#error-message")?.TextContent);
        Assert.Equal("UNAUTHORIZED", document.QuerySelector("#error-code")?.TextContent);
    }

    [Fact(DisplayName = "Astro Actions: build: Ignores action name for GET requests")]
    public async Task Get_IgnoresTheActionNameOfTheQuery()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/user-or-throw?_action=getUserOrThrow");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("", document.QuerySelector("#error-message")?.TextContent);
        Assert.Equal("", document.QuerySelector("#error-code")?.TextContent);
    }

    [Fact(DisplayName = "Astro Actions: build: Is callable from the server with rewrite")]
    public async Task Rewrite_ToAPageThatCallsAnAction_RendersTheResultOfTheAction()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/rewrite");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("/subscribe", document.QuerySelector("[data-url]")?.TextContent);
        Assert.Equal("bholmesdev", document.QuerySelector("[data-channel]")?.TextContent);
    }

    [Fact(DisplayName = "Astro Actions in static mode with prerender = false routes: starts in dev and exposes action RPC routes")]
    public async Task StaticProjectWithAnOnDemandPage_ExposesTheActionRpcRoutes()
    {
        var site = await sites.GetAsync(StaticFixture);

        using var response = await site.SendAsync(HttpMethod.Post, "/_actions/ping", Json("{}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json+devalue", response.Content.Headers.ContentType?.MediaType);
        Assert.True(Devalue.Parse(await response.ReadTextAsync())!["ok"]!.GetValue<bool>());
        Assert.Contains("Live page", await site.GetStringAsync("/"));
    }

    [Fact(DisplayName = "Works with adapter and all pages prerendered")]
    public async Task AllPagesPrerendered_StillExposesTheActionRpcRoutes()
    {
        var site = await sites.GetAsync(StaticFixture, new { conformance = new { allPrerendered = true } });

        // Without a page that renders on request the home page is a static file.
        Assert.True(site.PathExists("client/index.html"));
        using var response = await site.SendAsync(HttpMethod.Post, "/_actions/subscribe", Json("""{"channel":"bholmesdev"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = Devalue.Parse(await response.ReadTextAsync())!;
        Assert.Equal("bholmesdev", data["channel"]!.GetValue<string>());
        Assert.Equal("smashed", data["subscribeButtonState"]!.GetValue<string>());
    }
}
