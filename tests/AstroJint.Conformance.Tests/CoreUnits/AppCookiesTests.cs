using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/cookies/app-cookies.test.ts</c>: cookies of the request are readable in an endpoint,
/// the ones an endpoint sets end up in the <c>Set-Cookie</c> headers of the response, also when the endpoint returns early,
/// and the ones set in a source route and in the route a rewrite leads to are both kept (the target wins on a clash).
/// <para>
/// Differences from upstream: the endpoints are the real ones of the <c>app-cookies</c> fixture, and the rewrite from
/// middleware is the real <c>src/middleware.ts</c> of it. Upstream read the cookies to set back with
/// <c>app.setCookieHeaders(response)</c> and passed <c>addCookieHeader</c> to <c>render()</c>; the host always renders with
/// <c>addCookieHeader: true</c>, so the cookies are the <c>Set-Cookie</c> header lines of the response, and the test that
/// excludes them (<c>app.render can exclude the cookie from the Set-Cookie header</c>) has no equivalent (Not applicable).
/// <c>Cookie</c> values are sent as written by the upstream tests (the JSON of the preferences, percent-encoded).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/cookies/app-cookies.test.ts")]
public class AppCookiesTests(SiteHost sites)
{
    private const string Fixture = "app-cookies";

    private const string LightPrefs = "prefs=%7B%22mode%22%3A%22light%22%7D";

    [Fact(DisplayName = "reads an empty cookie value in an endpoint")]
    public async Task Endpoint_ReadsAnEmptyCookieValue()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/empty", headers: ("Cookie", "empty="));

        Assert.Equal(200, response.StatusCode);
        var json = JsonDocument.Parse(response.Text).RootElement;
        Assert.True(json.GetProperty("present").GetBoolean());
        Assert.Equal("", json.GetProperty("value").GetString());
        Assert.False(json.GetProperty("missing").GetBoolean());
    }

    [Fact(DisplayName = "is able to get cookies from the request")]
    public async Task Endpoint_ReadsCookiesOfTheRequest()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/get-json", headers: ("Cookie", LightPrefs));

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("<dd>light</dd>", response.Text);
    }

    [Fact(DisplayName = "can set the cookie value")]
    public async Task Endpoint_SetsACookie()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/set-value", "POST", []);

        Assert.Equal(200, response.StatusCode);
        var cookies = response.HeaderValues("Set-Cookie");
        var cookie = Assert.Single(cookies);
        Assert.Contains("Expires", cookie);
    }

    [Fact(DisplayName = "app.render can include the cookie in the Set-Cookie header")]
    public async Task Response_IncludesTheCookieInTheSetCookieHeader()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/set-value", "POST", []);

        Assert.Equal(200, response.StatusCode);
        var value = response.Header("Set-Cookie");
        Assert.NotNull(value);
        Assert.StartsWith("admin=true; Expires=", value);
    }

    [Fact(DisplayName = "early returning a Response still includes set headers")]
    public async Task EarlyReturn_StillIncludesTheCookies()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/early-return", headers: ("Cookie", LightPrefs));

        Assert.Equal(302, response.StatusCode);
        AssertDarkPrefs(response);
    }

    [Fact(DisplayName = "API route can get and set cookies")]
    public async Task ApiRoute_GetsAndSetsCookies()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/set-prefs", "POST", [], ("Cookie", LightPrefs));

        Assert.Equal(302, response.StatusCode);
        AssertDarkPrefs(response);
    }

    [Fact(DisplayName = "can set cookies in a rewritten page request")]
    public async Task Rewrite_KeepsTheCookiesOfTheTarget()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/from");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains(response.HeaderValues("Set-Cookie"), cookie => cookie.Contains("my_cookie=value"));
    }

    [Fact(DisplayName = "overwrites cookie values set in the source page with values from the target page")]
    public async Task Rewrite_LetsTheTargetOverwriteTheCookiesOfTheSource()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/from");

        Assert.Equal(200, response.StatusCode);
        var cookies = response.HeaderValues("Set-Cookie");
        Assert.Contains(cookies, cookie => cookie.Contains("another=set-in-target"));
        Assert.DoesNotContain(cookies, cookie => cookie.Contains("another=set-in-from"));
    }

    [Fact(DisplayName = "allows cookies to be set in the source page")]
    public async Task Rewrite_KeepsTheCookiesOfTheSource()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/from");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains(response.HeaderValues("Set-Cookie"), cookie => cookie.Contains("set-in-from=yes"));
    }

    [Fact(DisplayName = "can set cookies in a rewritten endpoint request")]
    public async Task Rewrite_ToAnEndpoint_KeepsItsCookies()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/from-endpoint");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains(response.HeaderValues("Set-Cookie"), cookie => cookie.Contains("test=value"));
    }

    [Fact(DisplayName = "can set cookies in a rewritten endpoint request from middleware")]
    public async Task MiddlewareRewrite_KeepsTheCookiesOfTheTarget()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/rewrite-me");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains(response.HeaderValues("Set-Cookie"), cookie => cookie.Contains("my_cookie=value"));
    }

    private static void AssertDarkPrefs(RawResponse response)
    {
        var cookie = Assert.Single(response.HeaderValues("Set-Cookie"));
        var data = JsonDocument.Parse(Uri.UnescapeDataString(cookie["prefs=".Length..])).RootElement;
        Assert.Equal(JsonValueKind.Object, data.ValueKind);
        Assert.Equal("dark", data.GetProperty("mode").GetString());
    }
}
