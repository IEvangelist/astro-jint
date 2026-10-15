using static AstroJint.Conformance.CoreUnits.AppSessions;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/sessions/session-false.test.ts</c>, which checks the option <c>session: false</c> (no session
/// storage, <c>Astro.session</c> undefined) on the schema, the manifest helper, the provider and the Vite plugin that swaps the
/// provider. Seven tests are adapted, three are not applicable; none is ported as it is and none had to be skipped. The unit tests
/// call the pieces on their own; here the pieces act in two builds of the <c>app-sessions</c> fixture, one with <c>session: false</c>
/// and one with <c>session: { ttl: 60 }</c> (upstream's object), and what a test sees is the built site.
/// <para>
/// "accepts `session: false`" and "still accepts a session object": the build of the fixture succeeds with the option, which is
/// all the schema has to do, and the endpoint <c>/api/has-session</c> tells whether <c>Astro.session</c> is defined (not without
/// the option, yes with the object). "sessionConfigToManifest(false) returns undefined": the manifest that the server build embeds has
/// no <c>"sessionConfig"</c> key, and has one when the option is an object; the text of the server scripts is searched for the
/// key. "registers no session provider, leaving Astro.session undefined": <c>Astro.session</c> is undefined in the request, and the
/// getter that stands in for it logs once per request that no session storage is configured (the upstream test does not look at the
/// log). The "sessions" feature that the upstream test checks with <c>getUsedFeatures(manifest)</c> is a flag of the build that
/// nothing shows over HTTP. "redirects the canonical provider path": with the option the plugin swaps <c>core/session/provider.js</c>
/// for the disabled provider, so the session runtime is not in the server build (the scripts have neither <c>class AstroSession</c> nor
/// <c>createStorage</c>), while the build with an object has both. "leaves an unrelated <c>session/provider.js</c> alone": the page
/// <c>/user-provider</c> of the fixture imports <c>src/session/provider.js</c> and shows what it exports, in the build with the
/// option. "keeps the real provider when a driver is configured": the build with an object runs <c>Astro.session</c> on the storage of
/// the host (a seeded session is read through a request).
/// </para>
/// <para>
/// Not applicable. "rejects other falsy values" runs the zod schema on <c>0</c>, <c>''</c> and <c>null</c>: the schema is evaluated by
/// <c>astro build</c> on the configuration, so the rejection is a failed build and not something a request shows. The two tests
/// of the symlinked provider path ("reached through resolution" and "passed directly") call <c>resolveId</c> of the plugin with a
/// symlink into the Astro package, which is what Vite does under <c>vite.resolve.preserveSymlinks: true</c>; a build of the fixture
/// with that option fails before the plugin matters (Rolldown cannot resolve <c>@astrojs/internal-helpers/path</c> from the Astro
/// package that pnpm links), and the second test hands the plugin a path that no build passes.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/sessions/session-false.test.ts")]
public class SessionsSessionFalseTests(SiteHost sites)
{
    private const string Variant = "sessions-session-false";
    private const string NoStorageWarning = "was accessed but no session storage is configured";

    private static readonly object SessionIsFalse = new { session = false };
    private static readonly object SessionIsAnObject = new { session = new { ttl = 60 } };

    private Task<(ConformanceSite Site, SessionSpyCache Cache)> SiteAsync(object config) => sites.GetSessionSiteAsync(Variant, config);

    private static int CountNoStorageWarnings(ConformanceSite site) =>
        site.ConsoleEntries().Count(entry => entry.Message.Contains(NoStorageWarning, StringComparison.Ordinal));

    // #region schema

    [Fact(DisplayName = "accepts `session: false`")]
    public async Task SessionFalse_IsAcceptedAndLeavesAstroSessionUndefined()
    {
        // The build runs the schema, so having a site is the proof that the option was accepted.
        var (site, _) = await SiteAsync(SessionIsFalse);

        Assert.False(await site.HasSessionAsync());
    }

    [Fact(DisplayName = "still accepts a session object")]
    public async Task SessionObject_IsAcceptedAndDefinesAstroSession()
    {
        var (site, _) = await SiteAsync(SessionIsAnObject);

        Assert.True(await site.HasSessionAsync());
    }

    // #endregion

    // #region manifest helpers

    [Fact(DisplayName = "sessionConfigToManifest(false) returns undefined")]
    public async Task SessionFalse_LeavesTheSessionConfigOutOfTheManifest()
    {
        var (withoutSession, _) = await SiteAsync(SessionIsFalse);
        var (withSession, _) = await SiteAsync(SessionIsAnObject);

        Assert.False(withoutSession.ServerBundleContains("\"sessionConfig\":"));
        Assert.True(withSession.ServerBundleContains("\"sessionConfig\":"));
    }

    // #endregion

    // #region disabled provider

    [Fact(DisplayName = "registers no session provider, leaving Astro.session undefined")]
    public async Task SessionFalse_RegistersNoSessionProvider()
    {
        var (site, _) = await SiteAsync(SessionIsFalse);
        var warningsBefore = CountNoStorageWarnings(site);

        var hasSession = await site.HasSessionAsync();

        Assert.False(hasSession);
        Assert.Equal(warningsBefore + 1, CountNoStorageWarnings(site));
    }

    // #endregion

    // #region provider plugin

    [Fact(DisplayName = "redirects the canonical provider path")]
    public async Task SessionFalse_ReplacesTheProviderSoTheRuntimeIsNotBundled()
    {
        var (withoutSession, _) = await SiteAsync(SessionIsFalse);
        var (withSession, _) = await SiteAsync(SessionIsAnObject);

        Assert.False(withoutSession.ServerBundleContains("class AstroSession"));
        Assert.False(withoutSession.ServerBundleContains("createStorage"));
        Assert.True(withSession.ServerBundleContains("class AstroSession"));
        Assert.True(withSession.ServerBundleContains("createStorage"));
    }

    [Fact(DisplayName = "leaves an unrelated `session/provider.js` alone")]
    public async Task SessionFalse_LeavesTheProviderOfTheProjectAlone()
    {
        var (site, _) = await SiteAsync(SessionIsFalse);

        var page = await site.GetHtmlAsync("/user-provider");

        Assert.Equal("the session provider of the project", page.QuerySelector("#provider")?.TextContent);
    }

    [Fact(DisplayName = "keeps the real provider when a driver is configured")]
    public async Task SessionObject_KeepsTheRealProvider()
    {
        var (site, cache) = await SiteAsync(SessionIsAnObject);
        var id = NewId();
        cache.Seed(id, SessionSeeds.User);

        var run = await site.RunSessionAsync(CookieFor("astro-session", id), SessionOp.Get("user"));

        Assert.Equal("\"alice\"", run.Step(0).Json);
    }

    // #endregion
}
