using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/session-false.test.ts</c>. The upstream tests call the <c>astro:config:setup</c>
/// hook of the adapter with a fake config and read the <c>session</c> that it hands back; these tests build the real site and
/// ask the page <c>/session</c> of the shared fixture <c>cf-pages</c> (it counts the requests of a session) what the config
/// came to, and look in the server bundle for the driver that is wired in.
/// <list type="bullet">
/// <item>"wires no session driver when sessions are disabled": with <c>session: false</c> the page has no session (the count
/// stays 1 whatever cookie it gets and none is set) and the host's driver is not in the bundle. The log line "Enabling sessions"
/// of the build is not asserted: the harness keeps the output of a build only when it fails.</item>
/// <item>"does not override a driver the user configured": a driver of the fixture (<c>src/session-driver.mjs</c>, which logs
/// what it does) is the one that stores the session, and the host's driver is not in the bundle.</item>
/// <item>"still wires Cloudflare KV by default": the host's driver (its <c>IDistributedCache</c>, not a KV namespace) keeps
/// the count of a session over two requests.</item>
/// <item>Not applicable: the two tests of <c>cloudflareConfigCustomizer</c>, which provisions the KV namespace of the
/// session driver in <c>wrangler.json</c>.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/session-false.test.ts")]
public class SessionFalseTests(SiteHost sites)
{
    private static readonly object SessionsOff = new { session = false };

    private static readonly object DriverOfTheUser = new { session = new { driver = new { entrypoint = "./src/session-driver.mjs" } } };

    [Fact(DisplayName = "wires no session driver when sessions are disabled")]
    public async Task WiresNoDriverWhenSessionsAreDisabled()
    {
        var site = await sites.GetAsync(CfPages.Fixture, SessionsOff);

        using var first = await site.GetAsync("/session");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("count: 1", await first.ReadTextAsync());
        Assert.Null(first.SetCookieValue("astro-session"));

        using var second = await site.SendAsync(HttpMethod.Get, "/session", null, ("Cookie", "astro-session=left-over-session"));
        Assert.Contains("count: 1", await second.ReadTextAsync());
        Assert.Null(second.SetCookieValue("astro-session"));

        Assert.False(BundleContains(site, "sessionGet"), "the host's session driver is in the server bundle");
        Assert.False(BundleContains(site, "createStorage"), "the session runtime is in the server bundle");
    }

    [Fact(DisplayName = "does not override a driver the user configured")]
    public async Task DoesNotOverrideADriverTheUserConfigured()
    {
        var site = await sites.GetAsync(CfPages.Fixture, DriverOfTheUser);

        using var response = await site.GetAsync("/session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("count: 1", await response.ReadTextAsync());
        Assert.NotNull(response.SetCookieValue("astro-session"));
        Assert.Contains(site.Logs, entry => entry.Message.Contains("conformance-driver setItem"));
        Assert.True(BundleContains(site, "conformance-driver"), "the driver of the user is not in the server bundle");
        Assert.False(BundleContains(site, "sessionGet"), "the host's session driver is in the server bundle");
    }

    [Fact(DisplayName = "still wires Cloudflare KV by default")]
    public async Task StillWiresADriverByDefault()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var first = await site.GetAsync("/session");
        Assert.Contains("count: 1", await first.ReadTextAsync());
        var sessionId = first.SetCookieValue("astro-session");
        Assert.NotNull(sessionId);

        using var second = await site.SendAsync(HttpMethod.Get, "/session", null, ("Cookie", $"astro-session={sessionId}"));
        Assert.Contains("count: 2", await second.ReadTextAsync());

        Assert.True(BundleContains(site, "sessionGet"), "the host's session driver is not in the server bundle");
        Assert.DoesNotContain(site.Logs, entry => entry.Message.Contains("conformance-driver"));
    }

    private static bool BundleContains(ConformanceSite site, string text) =>
        Directory.EnumerateFiles(Path.Combine(site.DistPath, "server"), "*.mjs", SearchOption.AllDirectories)
            .Any(file => File.ReadAllText(file).Contains(text, StringComparison.Ordinal));
}
