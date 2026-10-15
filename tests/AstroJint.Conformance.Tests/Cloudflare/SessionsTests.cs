using System.Net;
using System.Text;
using System.Text.Json;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/sessions.test.ts</c>. The Cloudflare adapter keeps sessions in a KV
/// namespace, this host in the <c>IDistributedCache</c> of the application, so the tests here are the ones of the first
/// describe block, asked of the existing fixture <c>sessions</c>. That fixture is the Cloudflare one, except for the option
/// <c>site</c> and a <c>console.log</c> in the middleware, which no test reads.
/// <para>
/// The test of the dev server (<c>can write and read session data when middleware is present</c>) is adapted: the host has no
/// dev mode, so its two requests go to the host, which is the same fixture with the same middleware (it reads the session of
/// every request). The test of the custom KV binding name and the two tests of the <c>kv_namespaces</c> of the wrangler
/// config are about Cloudflare bindings and have no counterpart here.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/sessions.test.ts")]
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
        var firstBody = await first.Content.ReadAsStringAsync();

        Assert.True(first.IsSuccessStatusCode, firstBody);
        Assert.Equal("Favorite URL set to https://domain.invalid/ from nothing", MessageOf(firstBody));

        var sessionId = first.SetCookieValue("astro-session");
        using var second = await site.SendAsync(
            HttpMethod.Post,
            "/_actions/addUrl",
            Json("""{"favoriteUrl":"https://example.com"}"""),
            ("Cookie", $"astro-session={sessionId}"));

        Assert.Equal("Favorite URL set to https://example.com/ from https://domain.invalid/", MessageOf(await second.Content.ReadAsStringAsync()));
    }

    [Fact(DisplayName = "can write and read session data when middleware is present")]
    public async Task Session_CanBeWrittenAndReadWithMiddleware()
    {
        var site = await sites.GetAsync(Fixture);

        using var first = await site.GetAsync("/update");
        Assert.True(first.StatusCode == HttpStatusCode.OK, "Expected 200 OK for session write");
        using var firstValue = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Assert.Equal("none", firstValue.RootElement.GetProperty("previousValue").GetString());

        var sessionId = first.SetCookieValue("astro-session");
        using var second = await site.SendAsync(HttpMethod.Get, "/update", headers: ("Cookie", $"astro-session={sessionId}"));
        Assert.True(second.StatusCode == HttpStatusCode.OK, "Expected 200 OK for session read");
        using var secondValue = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

        Assert.Equal("expected", secondValue.RootElement.GetProperty("previousValue").GetString());
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    // The body of an action is the devalue form of its result, which for { message } is [{"message":1},"the message"].
    private static string? MessageOf(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return root[root[0].GetProperty("message").GetInt32()].GetString();
    }
}
