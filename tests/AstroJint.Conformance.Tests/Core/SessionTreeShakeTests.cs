using System.Net;
using System.Text.Json.Nodes;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/session-tree-shake.test.ts</c>, built from the <c>session-tree-shake</c> routes, which are
/// the <c>api</c> and <c>no-session</c> routes that <c>SessionFalseTests</c> already runs; both classes use the
/// <c>core-session-config</c> fixture.
/// <list type="bullet">
/// <item>"No driver wired" describes an adapter that does not wire a default driver. This adapter wires the session store
/// of the host unless <c>session: false</c> says otherwise, so its closest state is the JSON configuration
/// <c>session: false</c>: <c>context.session</c> is undefined and the bundler drops the session runtime. The three tests
/// are therefore adapted, and they share the build of <c>SessionFalseTests</c>.</item>
/// <item>"A driver is configured" describes <c>session.driver: 'fs'</c>, which cannot run in Jint. The driver that the
/// adapter wires is the configured driver here, so the tests run with the default configuration.</item>
/// <item><c>app.render</c> becomes an HTTP request, and the bundle check reads the scripts of the output directory.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/session-tree-shake.test.ts")]
public class SessionTreeShakeTests(SiteHost sites)
{
    private const string Fixture = "core-session-config";

    private Task<ConformanceSite> GetSiteWithoutDriverAsync() => sites.GetAsync(Fixture, new { session = false });

    private Task<ConformanceSite> GetSiteWithDriverAsync() => sites.GetAsync(Fixture);

    [Fact(DisplayName = "session tree-shaking when no driver is wired: leaves Astro.session undefined")]
    public async Task NoDriver_SessionIsUndefined()
    {
        var site = await GetSiteWithoutDriverAsync();

        using var response = await site.GetAsync("/api");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(JsonNode.Parse(await response.ReadTextAsync())!["hasSession"]!.GetValue<bool>(), "expected context.session to be undefined");
    }

    [Fact(DisplayName = "session tree-shaking when no driver is wired: does not affect routes that never touch the session")]
    public async Task NoDriver_RouteWithoutSession_IsUnaffected()
    {
        var site = await GetSiteWithoutDriverAsync();

        using var response = await site.GetAsync("/no-session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"ok":true}"""), JsonNode.Parse(await response.ReadTextAsync())));
    }

    [Fact(DisplayName = "session tree-shaking when no driver is wired: excludes the session runtime and unstorage from the SSR bundle")]
    public async Task NoDriver_Bundle_ExcludesTheSessionRuntime()
    {
        var site = await GetSiteWithoutDriverAsync();

        var (unstorage, sessionRuntime) = BuildOutput.FindSessionRuntime(site);

        Assert.False(unstorage, "unstorage should not appear in the SSR bundle");
        Assert.False(sessionRuntime, "AstroSession class should not appear in the SSR bundle");
    }

    [Fact(DisplayName = "session runtime is retained when a driver is configured: makes Astro.session available")]
    public async Task Driver_SessionIsAvailable()
    {
        var site = await GetSiteWithDriverAsync();

        using var response = await site.GetAsync("/api");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(JsonNode.Parse(await response.ReadTextAsync())!["hasSession"]!.GetValue<bool>(), "expected context.session to be defined");
    }

    [Fact(DisplayName = "session runtime is retained when a driver is configured: keeps the session runtime and unstorage in the SSR bundle")]
    public async Task Driver_Bundle_KeepsTheSessionRuntime()
    {
        var site = await GetSiteWithDriverAsync();

        var (unstorage, sessionRuntime) = BuildOutput.FindSessionRuntime(site);

        Assert.True(unstorage, "unstorage should appear in the SSR bundle");
        Assert.True(sessionRuntime, "AstroSession class should appear in the SSR bundle");
    }
}
