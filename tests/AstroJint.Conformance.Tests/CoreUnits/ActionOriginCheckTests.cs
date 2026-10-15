using System.Text;
using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/actions/action-origin-check.test.ts</c>: the origin check of a form action that is called
/// through the RPC route (<c>POST /_actions/deleteAccount</c>) holds when <c>actions()</c> runs before <c>middleware()</c>, the order
/// of the <c>advanced-routing</c> example, so the check of the origin-check middleware has not run yet.
/// <para>
/// Differences from upstream: upstream composes a Hono app (<c>hono.use(actions())</c>, <c>hono.use(middleware())</c>,
/// <c>hono.use(pages())</c>) around a test <c>App</c> and reads the flag <c>ran</c> of its action. Here the user fetch handler of the
/// <c>app-fetch</c> fixture does the same composition with the functions of <c>astro/fetch</c>: the pipeline
/// <c>actions-before-middleware</c> answers with the response of <c>actions()</c> when it has one and goes on with
/// <c>middleware()</c> and <c>pages()</c> otherwise, on the <c>Standard</c> variant of <see cref="FetchIndexTests"/> (the origin
/// check is on by default). The flag is a counter of the runs of the handler, which the endpoint <c>/action-runs</c> reports, so the
/// site has a single engine (the counter is module state) and each test compares the counter before and after. The requests send the
/// <c>Host</c> <c>localhost</c> and the <c>Origin</c> that upstream uses; the body is the same, <c>confirm=1</c>.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/actions/action-origin-check.test.ts")]
public class ActionOriginCheckTests(SiteHost sites)
{
    private Task<ConformanceSite> GetSiteAsync() =>
        sites.GetAsync(
            "app-fetch", FetchIndexTests.Standard, variant: "action-origin", configure: options => options.PoolSize = 1);

    private static Task<RawResponse> ActionRequestAsync(ConformanceSite site, string origin) =>
        site.SendRawAsync(
            "/_actions/deleteAccount",
            "POST",
            Encoding.UTF8.GetBytes("confirm=1"),
            ("Host", "localhost"),
            ("Origin", origin),
            ("Content-Type", "application/x-www-form-urlencoded"),
            ("x-pipeline", "actions-before-middleware"));

    private static async Task<int> RunsAsync(ConformanceSite site)
    {
        var response = await site.SendRawAsync("/action-runs");

        Assert.Equal(200, response.StatusCode);
        return JsonDocument.Parse(response.Text).RootElement.GetProperty("runs").GetInt32();
    }

    [Fact(DisplayName = "blocks a cross-origin action request before it runs")]
    public async Task CrossOriginActionRequest_IsBlockedBeforeTheActionRuns()
    {
        var site = await GetSiteAsync();
        var runsBefore = await RunsAsync(site);

        var response = await ActionRequestAsync(site, "http://evil.example");

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(runsBefore, await RunsAsync(site));
    }

    [Fact(DisplayName = "allows a same-origin action request")]
    public async Task SameOriginActionRequest_RunsTheAction()
    {
        var site = await GetSiteAsync();
        var runsBefore = await RunsAsync(site);

        var response = await ActionRequestAsync(site, "http://localhost");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(runsBefore + 1, await RunsAsync(site));
    }
}
