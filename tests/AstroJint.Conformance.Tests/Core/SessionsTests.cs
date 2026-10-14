using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/sessions.test.ts</c> (<c>Astro.session</c>, <c>Production</c>), built from the
/// <c>sessions</c> fixture of Astro.
/// <list type="bullet">
/// <item>The upstream <c>fs</c> driver cannot run in Jint. The adapter wires the session store of the host
/// (<c>IDistributedCache</c>) in its place and keeps the <c>ttl</c> of the configuration, so these tests also show that a
/// session written by one engine of the pool is read by the others.</item>
/// <item><c>app.render(new Request('http://example.com' + path))</c> becomes an HTTP request, <c>app.setCookieHeaders(response)</c>
/// the <c>Set-Cookie</c> headers of the response and <c>Origin: http://example.com</c> the origin of the site, because
/// the host builds the URL of a request from its <c>Host</c> header.</item>
/// <item>Three of the eight upstream tests (can regenerate session cookies, can save session data by value, can save
/// and restore URLs) run the same pages as <c>Node\SessionsTests</c> and are not repeated here.</item>
/// <item><c>devalue.parse</c> is replaced by the <see cref="Devalue"/> reader of this folder.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/sessions.test.ts")]
public class SessionsTests(SiteHost sites)
{
    private const string Fixture = "core-sessions";
    private const string CookieName = "astro-session";

    [Fact(DisplayName = "Astro.session: Production: defaults to secure cookies in production")]
    public async Task SessionCookie_DefaultsToSecureAndHttpOnly()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/regenerate");
        var cookie = FirstSetCookie(response);

        Assert.Contains("Secure", cookie);
        Assert.Contains("HttpOnly", cookie);
    }

    [Fact(DisplayName = "Astro.session: Production: generates a new session ID when cookie value has no server-side data")]
    public async Task Session_CookieWithoutServerSideData_GetsANewId()
    {
        var site = await sites.GetAsync(Fixture);
        const string unknownId = "nonexistent-session-id-12345";

        using var response = await site.SendAsync(HttpMethod.Post, "/login", Json("""{"username":"testuser"}"""), SessionCookie(unknownId));
        Assert.True(response.IsSuccessStatusCode, await response.ReadTextAsync());
        var sessionId = FirstSessionId(response);

        Assert.NotEqual(unknownId, sessionId);

        // The original ID must not give access to the data of the new session.
        using var second = await site.SendAsync(HttpMethod.Get, "/update", headers: SessionCookie(unknownId));
        Assert.Equal("none", await PreviousValueAsync(second));
    }

    [Fact(DisplayName = "Astro.session: Production: preserves session ID when cookie value has existing server-side data")]
    public async Task Session_CookieWithServerSideData_KeepsItsId()
    {
        var site = await sites.GetAsync(Fixture);

        using var first = await site.GetAsync("/update");
        var firstSessionId = FirstSessionId(first);

        using var second = await site.SendAsync(HttpMethod.Get, "/update", headers: SessionCookie(firstSessionId));

        Assert.Equal(firstSessionId, FirstSessionId(second));
    }

    [Fact(DisplayName = "Astro.session: Production: regenerate() creates a new ID and cleans up the old session")]
    public async Task Session_Regenerate_CreatesANewIdAndDropsTheOldSession()
    {
        var site = await sites.GetAsync(Fixture);

        using var first = await site.GetAsync("/update");
        var firstSessionId = FirstSessionId(first);

        using var second = await site.SendAsync(HttpMethod.Post, "/login-safe", Json("""{"username":"testuser"}"""), SessionCookie(firstSessionId));
        Assert.True(second.IsSuccessStatusCode, await second.ReadTextAsync());
        var secondSessionId = SessionIds(second)[^1];

        Assert.NotEqual(firstSessionId, secondSessionId);

        // The old ID must have no data after the regeneration.
        using var third = await site.SendAsync(HttpMethod.Get, "/update", headers: SessionCookie(firstSessionId));
        Assert.Equal("none", await PreviousValueAsync(third));
    }

    [Fact(DisplayName = "Astro.session: Production: can load a session by ID")]
    public async Task Session_CanBeLoadedById()
    {
        var site = await sites.GetAsync(Fixture);
        var origin = site.BaseAddress.GetLeftPart(UriPartial.Authority);

        using var first = await site.SendAsync(
            HttpMethod.Post,
            "/_actions/addToCart",
            new FormUrlEncodedContent([new("productId", "item1")]),
            ("Origin", origin));
        var firstCart = Devalue.Parse(await first.ReadTextAsync())!["cart"]!.AsArray();
        Assert.Contains(firstCart, item => item?.GetValue<string>() == "item1");

        var firstSessionId = FirstSessionId(first);

        // No cookie here: the action loads the session by the ID that its input carries.
        using var second = await site.SendAsync(HttpMethod.Post, "/_actions/loadCart", Json($$"""{"id":"{{firstSessionId}}"}"""));
        var secondCart = Devalue.Parse(await second.ReadTextAsync())!["cart"]!.AsArray();

        Assert.Equal(firstCart.ToJsonString(), secondCart.ToJsonString());
    }

    private static (string Name, string Value) SessionCookie(string id) => ("Cookie", $"{CookieName}={id}");

    private static ByteArrayContent Json(string json)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static string FirstSetCookie(HttpResponseMessage response)
    {
        var headers = response.HeaderValues("Set-Cookie");
        Assert.NotEmpty(headers);
        return headers[0];
    }

    private static string FirstSessionId(HttpResponseMessage response)
    {
        var cookie = FirstSetCookie(response);
        Assert.StartsWith($"{CookieName}=", cookie);
        return cookie.Split(';')[0][(CookieName.Length + 1)..];
    }

    private static string[] SessionIds(HttpResponseMessage response) =>
        [.. response.HeaderValues("Set-Cookie")
            .Where(header => header.StartsWith($"{CookieName}=", StringComparison.Ordinal))
            .Select(header => header.Split(';')[0][(CookieName.Length + 1)..])];

    private static async Task<string?> PreviousValueAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.ReadTextAsync())!["previousValue"]?.GetValue<string>();
}
