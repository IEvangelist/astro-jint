using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace AstroJint.Conformance.Netlify;

/// <summary>
/// Port of <c>packages/integrations/netlify/test/functions/session-false.test.ts</c>. Upstream runs the <c>astro:config:setup</c>
/// hook of the adapter on a hand-made config and inspects what it hands back; the hook-level version of this adapter is in
/// <c>packages/astro-adapter-jint/test/session-config.test.ts</c>, and these tests build real sites and look at what the
/// hook is meant to produce.
/// <para>
/// Differences. The announcement in the build log (<c>Enabling sessions</c>) is not observable here; what the adapter wired is
/// read from the output instead: whether <c>Astro.session</c> exists, whether the output bundles the driver that talks to the
/// host (it is the only code that mentions <c>sessionGet</c>), and whether the <c>IDistributedCache</c> of the host holds the
/// session. Netlify Blobs is replaced by that cache, so the third test checks the host storage is the default. The second
/// test uses the memory driver of <c>unstorage</c> in a site with one engine, as a memory store belongs to its engine.
/// </para>
/// </summary>
[Trait("Upstream", "netlify/functions/session-false.test.ts")]
public class SessionFalseTests(SiteHost sites)
{
    private const string CookieName = "astro-session";
    private const string StoredSessionPrefix = "astro-jint:session:";
    private const string HostDriverMarker = "sessionGet";

    [Fact(DisplayName = "wires no session driver when sessions are disabled")]
    public async Task Disabled_WiresNoSessionDriver()
    {
        var site = await sites.GetAsync("session-state", config: new { session = false });

        using var response = await site.GetAsync("/state");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.ReadTextAsync());
        Assert.Equal("undefined", json.RootElement.GetProperty("session").GetString());
        Assert.Empty(response.HeaderValues("Set-Cookie"));
        Assert.False(BundleMentions(site, HostDriverMarker), "the output should not have the driver of the host");
    }

    [Fact(DisplayName = "does not override a driver the user configured")]
    public async Task ConfiguredDriver_IsNotOverridden()
    {
        var site = await sites.GetAsync(
            "sessions",
            config: new { session = new { driver = new { entrypoint = "unstorage/drivers/memory" } } },
            configure: options => options.PoolSize = 1,
            variant: "netlify-configured-driver");

        using var first = await site.GetAsync("/update");
        var sessionId = first.SetCookieValue(CookieName);
        using var second = await site.SendAsync(HttpMethod.Get, "/update", headers: ("Cookie", $"{CookieName}={sessionId}"));
        using var secondValue = JsonDocument.Parse(await second.ReadTextAsync());

        Assert.NotNull(sessionId);
        Assert.Equal("expected", secondValue.RootElement.GetProperty("previousValue").GetString());
        Assert.Null(await StoredSessionAsync(site, sessionId));
        Assert.False(BundleMentions(site, HostDriverMarker), "the output should not have the driver of the host");
    }

    [Fact(DisplayName = "still wires Netlify Blobs by default")]
    public async Task Default_StoresSessionsInTheDistributedCacheOfTheHost()
    {
        var site = await sites.GetAsync("sessions");

        using var response = await site.GetAsync("/update");
        var sessionId = response.SetCookieValue(CookieName);

        Assert.NotNull(sessionId);
        Assert.NotNull(await StoredSessionAsync(site, sessionId));
        Assert.True(BundleMentions(site, HostDriverMarker), "the output should have the driver of the host");
    }

    private static Task<string?> StoredSessionAsync(ConformanceSite site, string sessionId) =>
        site.Services.GetRequiredService<IDistributedCache>().GetStringAsync(StoredSessionPrefix + sessionId);

    private static bool BundleMentions(ConformanceSite site, string text) =>
        Directory.EnumerateFiles(Path.Combine(site.DistPath, "server"), "*.mjs", SearchOption.AllDirectories)
            .Any(file => File.ReadAllText(file).Contains(text, StringComparison.Ordinal));
}
