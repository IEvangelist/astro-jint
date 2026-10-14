using System.Net;
using System.Text;
using System.Text.Json;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/sessions.test.ts</c>. The Node adapter keeps sessions in the file system
/// and this one hands them to the <c>IDistributedCache</c> of the host, so these tests also show that a session written
/// by one engine of the pool is read by the other: the pool takes its engines in turn, and every test here reads back
/// with the cookie of an earlier request.
/// </summary>
[Trait("Upstream", "node/sessions.test.ts")]
public class SessionsTests(SiteHost sites)
{
    private const string Fixture = "sessions";

    [Fact(DisplayName = "can regenerate session cookies upon request")]
    public async Task Session_CanBeRegenerated()
    {
        var site = await sites.GetAsync(Fixture);

        using var first = await site.GetAsync("/regenerate");
        var firstSessionId = first.SetCookieValue("astro-session");

        using var second = await site.SendAsync(HttpMethod.Get, "/regenerate", headers: ("Cookie", $"astro-session={firstSessionId}"));
        var secondSessionId = second.SetCookieValue("astro-session");

        Assert.NotNull(firstSessionId);
        Assert.NotNull(secondSessionId);
        Assert.NotEqual(firstSessionId, secondSessionId);
    }

    [Fact(DisplayName = "can save session data by value")]
    public async Task Session_SavesDataByValue()
    {
        var site = await sites.GetAsync(Fixture);

        using var first = await site.GetAsync("/update");
        using var firstValue = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Assert.Equal("none", firstValue.RootElement.GetProperty("previousValue").GetString());

        var sessionId = first.SetCookieValue("astro-session");
        using var second = await site.SendAsync(HttpMethod.Get, "/update", headers: ("Cookie", $"astro-session={sessionId}"));
        using var secondValue = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

        Assert.Equal("expected", secondValue.RootElement.GetProperty("previousValue").GetString());
    }

    [Fact(DisplayName = "can save and restore URLs in session data")]
    public async Task Session_SavesAndRestoresUrls()
    {
        var site = await sites.GetAsync(Fixture);

        using var first = await site.SendAsync(HttpMethod.Post, "/_actions/addUrl", Json("""{"favoriteUrl":"https://domain.invalid"}"""));
        var firstText = await first.Content.ReadAsStringAsync();

        Assert.True(first.IsSuccessStatusCode, firstText);
        Assert.Contains("Favorite URL set to https://domain.invalid/ from nothing", firstText);

        var sessionId = first.SetCookieValue("astro-session");
        using var second = await site.SendAsync(
            HttpMethod.Post,
            "/_actions/addUrl",
            Json("""{"favoriteUrl":"https://example.com"}"""),
            ("Cookie", $"astro-session={sessionId}"));

        Assert.Contains("Favorite URL set to https://example.com/ from https://domain.invalid/", await second.Content.ReadAsStringAsync());
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
