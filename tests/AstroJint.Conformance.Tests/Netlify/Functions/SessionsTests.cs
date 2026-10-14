using System.Text;
using System.Text.Json;

namespace AstroJint.Conformance.Netlify;

/// <summary>
/// Port of <c>packages/integrations/netlify/test/functions/sessions.test.ts</c> (<c>Astro.session</c>, <c>Production</c>),
/// on the <c>sessions</c> fixture that <c>Node/SessionsTests.cs</c> uses: upstream's copy only differs by a log statement in
/// the middleware and a <c>site</c>.
/// <para>
/// Differences. Upstream configures <c>sessionDrivers.netlifyBlobs</c> against a mock <c>BlobsServer</c> on port 8971 and calls
/// the generated function; here the sessions live in the <c>IDistributedCache</c> of the host (the default of this adapter,
/// and the only store an engine can reach) and the requests go over HTTP. A session written by one engine of the pool is
/// read by the other, as the requests of a test take the engines in turn. <c>devalue.parse</c> is replaced by a reader of
/// the one shape that the action answers with, so the message is still compared in full.
/// </para>
/// </summary>
[Trait("Upstream", "netlify/functions/sessions.test.ts")]
public class SessionsTests(SiteHost sites)
{
    private const string Fixture = "sessions";
    private const string CookieName = "astro-session";

    [Fact(DisplayName = "can regenerate session cookies upon request")]
    public async Task Session_CanBeRegenerated()
    {
        var site = await sites.GetAsync(Fixture);

        using var first = await site.GetAsync("/regenerate");
        var firstSessionId = first.SetCookieValue(CookieName);

        using var second = await site.SendAsync(HttpMethod.Get, "/regenerate", headers: ("Cookie", $"{CookieName}={firstSessionId}"));
        var secondSessionId = second.SetCookieValue(CookieName);

        Assert.NotNull(firstSessionId);
        Assert.NotNull(secondSessionId);
        Assert.NotEqual(firstSessionId, secondSessionId);
    }

    [Fact(DisplayName = "can save session data by value")]
    public async Task Session_SavesDataByValue()
    {
        var site = await sites.GetAsync(Fixture);

        using var first = await site.GetAsync("/update");
        using var firstValue = JsonDocument.Parse(await first.ReadTextAsync());
        Assert.Equal("none", firstValue.RootElement.GetProperty("previousValue").GetString());

        var sessionId = first.SetCookieValue(CookieName);
        using var second = await site.SendAsync(HttpMethod.Get, "/update", headers: ("Cookie", $"{CookieName}={sessionId}"));
        using var secondValue = JsonDocument.Parse(await second.ReadTextAsync());

        Assert.Equal("expected", secondValue.RootElement.GetProperty("previousValue").GetString());
    }

    [Fact(DisplayName = "can save and restore URLs in session data")]
    public async Task Session_SavesAndRestoresUrls()
    {
        var site = await sites.GetAsync(Fixture);

        using var first = await site.SendAsync(HttpMethod.Post, "/_actions/addUrl", Json("""{"favoriteUrl":"https://domain.invalid"}"""));
        var firstText = await first.ReadTextAsync();

        Assert.True(first.IsSuccessStatusCode, firstText);
        Assert.Equal("Favorite URL set to https://domain.invalid/ from nothing", ReadMessage(firstText));

        var sessionId = first.SetCookieValue(CookieName);
        using var second = await site.SendAsync(
            HttpMethod.Post,
            "/_actions/addUrl",
            Json("""{"favoriteUrl":"https://example.com"}"""),
            ("Cookie", $"{CookieName}={sessionId}"));

        Assert.Equal("Favorite URL set to https://example.com/ from https://domain.invalid/", ReadMessage(await second.ReadTextAsync()));
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>
    /// Reads <c>message</c> from an action result in the <c>devalue</c> format, a JSON array whose first value is the
    /// object and whose objects refer to the other values by index: <c>[{"message":1},"text"]</c>.
    /// </summary>
    private static string? ReadMessage(string devalue)
    {
        using var values = JsonDocument.Parse(devalue);
        var message = values.RootElement[0].GetProperty("message").GetInt32();
        return values.RootElement[message].GetString();
    }
}
