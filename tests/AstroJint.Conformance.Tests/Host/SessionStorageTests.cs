using System.Net;
using System.Text;
using System.Text.Json;

namespace AstroJint.Conformance.Host;

/// <summary>
/// How sessions behave in the host. The Node adapter keeps them in files; this host gives them to the
/// <c>IDistributedCache</c> of the application, which every engine of the pool shares and which outlives an engine. Upstream
/// tests the cart and the cookies of its fixture only through the three cases of <c>sessions.test.ts</c>, so the other
/// pages of the same fixture are exercised here. The fixture adds <c>/hang</c> to the upstream pages.
/// </summary>
public class SessionStorageTests(SiteHost sites)
{
    private const string Fixture = "sessions";
    private const string StateFixture = "session-state";
    private const string CookieName = "astro-session";

    [Fact]
    public async Task Session_KeepsTheDataOfTheVisitorAcrossRequests()
    {
        var site = await sites.GetAsync(Fixture);

        using var set = await site.GetAsync("/api?set=hello");
        var sessionId = set.SetCookieValue(CookieName);

        Assert.Equal("hello", await ReadValueAsync(site, "/api", sessionId));
        var page = await GetHtmlAsync(site, "/", sessionId);
        Assert.Equal("hello", page.QuerySelector("p")!.TextContent);
    }

    [Fact]
    public async Task Session_IsNotSharedWithOtherVisitors()
    {
        var site = await sites.GetAsync(Fixture);
        using var set = await site.GetAsync("/api?set=secret");

        Assert.Null(await ReadValueAsync(site, "/api", sessionId: null));
        Assert.Null(await ReadValueAsync(site, "/api", sessionId: Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task Session_Destroy_ForgetsTheData()
    {
        var site = await sites.GetAsync(Fixture);
        using var set = await site.GetAsync("/api?set=bye");
        var sessionId = set.SetCookieValue(CookieName);
        Assert.Equal("bye", await ReadValueAsync(site, "/api", sessionId));

        using var destroy = await site.SendAsync(HttpMethod.Get, "/destroy", headers: CookieOf(sessionId));
        Assert.Equal(HttpStatusCode.OK, destroy.StatusCode);

        Assert.Null(await ReadValueAsync(site, "/api", sessionId));
    }

    [Fact]
    public async Task FormAction_KeepsItsResultInTheSessionThroughTheRedirect()
    {
        var site = await sites.GetAsync(Fixture);
        var page = await site.GetHtmlAsync("/cart");
        Assert.Equal("Add something to your cart!", page.QuerySelector("#message")!.TextContent);
        var action = page.QuerySelector("form")!.GetAttribute("action");

        // The same origin check that applies to a browser: the form is posted from the page that has the form.
        using var submit = await site.SendAsync(
            HttpMethod.Post,
            "/cart" + action,
            new FormUrlEncodedContent([new("productId", "shoe")]),
            ("Origin", site.BaseAddress.GetLeftPart(UriPartial.Authority)));
        var sessionId = submit.SetCookieValue(CookieName);

        // Astro's `originPathname` ends in a slash for the default `directory` build format.
        Assert.Equal(HttpStatusCode.Found, submit.StatusCode);
        Assert.Equal(["/cart/"], submit.HeaderValues("Location"));
        Assert.NotNull(sessionId);

        var result = await GetHtmlAsync(site, "/cart", sessionId);
        Assert.Equal("""["shoe"]""", result.QuerySelector("#cart")!.TextContent);
        Assert.StartsWith("Product added to cart at ", result.QuerySelector("#message")!.TextContent);

        // The result of the action is shown once, while the cart stays.
        var again = await GetHtmlAsync(site, "/cart", sessionId);
        Assert.Equal("""["shoe"]""", again.QuerySelector("#cart")!.TextContent);
        Assert.Equal("Add something to your cart!", again.QuerySelector("#message")!.TextContent);
    }

    [Fact]
    public async Task JsonAction_ChangesTheDataOfTheSession()
    {
        var site = await sites.GetAsync(Fixture);
        using var set = await site.GetAsync("/api?set=anything");
        var sessionId = set.SetCookieValue(CookieName);

        using var clear = await site.SendAsync(
            HttpMethod.Post,
            "/_actions/clearCart",
            new StringContent("{}", Encoding.UTF8, "application/json"),
            CookieOf(sessionId));
        var text = await clear.Content.ReadAsStringAsync();

        Assert.True(clear.IsSuccessStatusCode, text);
        Assert.Contains("Cart cleared at ", text);

        using var read = await site.SendAsync(HttpMethod.Get, "/api", headers: CookieOf(sessionId));
        using var json = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("cart").ValueKind);
        Assert.Equal(0, json.RootElement.GetProperty("cart").GetArrayLength());
    }

    [Fact]
    public async Task Session_OfConcurrentVisitors_StayApart()
    {
        var site = await sites.GetAsync(Fixture);
        var visitors = Enumerable.Range(0, 24).ToArray();

        var sessionIds = await Task.WhenAll(visitors.Select(async visitor =>
        {
            using var response = await site.GetAsync($"/api?set=visitor-{visitor}");
            return response.SetCookieValue(CookieName);
        }));
        var values = await Task.WhenAll(visitors.Select(visitor => ReadValueAsync(site, "/api", sessionIds[visitor])));

        Assert.Equal(visitors.Select(visitor => $"visitor-{visitor}"), values);
        Assert.Equal(visitors.Length, sessionIds.Distinct().Count());
    }

    [Fact]
    public async Task Session_OutlivesTheEngineThatCreatedIt()
    {
        // The only engine is replaced after a timeout, so only storage outside of the engine can keep the data.
        var site = await sites.GetAsync(Fixture, variant: "one-engine", configure: options =>
        {
            options.PoolSize = 1;
            options.WarmUp = true;
            options.RequestTimeout = TimeSpan.FromSeconds(3);
        });

        using var first = await site.GetAsync("/update");
        var sessionId = first.SetCookieValue(CookieName);

        using var timedOut = await site.GetAsync("/hang");
        Assert.Equal(HttpStatusCode.GatewayTimeout, timedOut.StatusCode);
        Assert.Contains(site.Logs, entry => entry.Message.Contains("The JavaScript engine was replaced"));

        using var second = await site.SendAsync(HttpMethod.Get, "/update", headers: CookieOf(sessionId));
        using var json = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal("expected", json.RootElement.GetProperty("previousValue").GetString());
    }

    [Fact]
    public async Task Session_IsAvailableToEndpointsByDefault()
    {
        var site = await sites.GetAsync(StateFixture);

        using var response = await site.GetAsync("/state");

        Assert.Equal("object", await ReadSessionTypeAsync(response));
    }

    [Fact]
    public async Task Session_WhenDisabled_IsUndefinedAndNoCookieIsSet()
    {
        var site = await sites.GetAsync(StateFixture, config: new { session = false });

        using var response = await site.GetAsync("/state");

        Assert.Equal("undefined", await ReadSessionTypeAsync(response));
        Assert.Empty(response.HeaderValues("Set-Cookie"));
    }

    private static async Task<string?> ReadSessionTypeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("session").GetString();
    }

    private static async Task<AngleSharp.Dom.IDocument> GetHtmlAsync(ConformanceSite site, string path, string? sessionId)
    {
        using var response = await site.SendAsync(HttpMethod.Get, path, headers: CookieOf(sessionId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadHtmlAsync();
    }

    private static async Task<string?> ReadValueAsync(ConformanceSite site, string path, string? sessionId)
    {
        using var response = await site.SendAsync(HttpMethod.Get, path, headers: CookieOf(sessionId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("value", out var value) ? value.GetString() : null;
    }

    private static (string Name, string Value)[] CookieOf(string? sessionId) => sessionId is null ? [] : [("Cookie", $"{CookieName}={sessionId}")];
}
