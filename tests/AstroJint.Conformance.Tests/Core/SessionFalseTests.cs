using System.Net;
using System.Text.Json.Nodes;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/session-false.test.ts</c>, built from the <c>session-false</c> fixture of Astro with the
/// JSON configuration <c>session: false</c>. The fixture is shared with <c>SessionTreeShakeTests</c> (both upstream
/// fixtures hold the same <c>api</c> and <c>no-session</c> routes), so the two classes cost one build.
/// <list type="bullet">
/// <item><c>app.render(new Request('http://example.com' + path))</c> becomes an HTTP request to the host.</item>
/// <item>The bundle check reads the scripts of the output directory (<c>fixture.glob</c> and <c>readFile</c> upstream),
/// the server bundle included. The adapter builds one module without externals, so it is where the session runtime
/// would be.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/session-false.test.ts")]
public class SessionFalseTests(SiteHost sites)
{
    private const string Fixture = "core-session-config";

    private Task<ConformanceSite> GetSiteAsync() => sites.GetAsync(Fixture, new { session = false });

    [Fact(DisplayName = "session: false: does not affect routes that never touch the session")]
    public async Task RouteWithoutSession_IsUnaffected()
    {
        var site = await GetSiteAsync();

        using var response = await site.GetAsync("/no-session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"ok":true}"""), JsonNode.Parse(await response.ReadTextAsync())));
    }

    [Fact(DisplayName = "session: false: leaves Astro.session undefined when a route reads it")]
    public async Task Session_IsUndefined()
    {
        var site = await GetSiteAsync();

        using var response = await site.GetAsync("/api");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(JsonNode.Parse(await response.ReadTextAsync())!["hasSession"]!.GetValue<bool>(), "expected context.session to be undefined");
    }

    [Fact(DisplayName = "session: false: does not hijack a user `./session/provider.js` import")]
    public async Task UserSessionProvider_IsNotHijacked()
    {
        var site = await GetSiteAsync();

        using var response = await site.GetAsync("/user-provider");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("user-provider-was-not-hijacked", JsonNode.Parse(await response.ReadTextAsync())!["value"]!.GetValue<string>());
    }

    [Fact(DisplayName = "session: false: excludes the session runtime and unstorage from the SSR bundle")]
    public async Task Bundle_ExcludesTheSessionRuntime()
    {
        var site = await GetSiteAsync();

        var (unstorage, sessionRuntime) = BuildOutput.FindSessionRuntime(site);

        Assert.False(unstorage, "unstorage should not appear in the SSR bundle");
        Assert.False(sessionRuntime, "AstroSession class should not appear in the SSR bundle");
    }
}
