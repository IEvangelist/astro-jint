using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/runtime-logger.test.ts</c> on the <c>runtime-logger</c> fixture (<c>core-runtime-logger</c>): the logger
/// destination configured in <c>logger.entrypoint</c> (<c>src/logger.mjs</c>, which records every message it is given) receives what
/// <c>astro:content</c> logs, at module scope of the middleware, in a middleware handler and in a page. All six tests are adapted, for the same
/// two reasons:
/// <list type="bullet">
/// <item>Upstream reads the recorded messages from <c>globalThis</c> of the test process, which the built server shares with it. The host runs
/// the server in a Jint engine that has its own <c>globalThis</c>, so the fixture has one endpoint that upstream does not have,
/// <c>src/pages/messages.json.ts</c>, which returns what <c>logger.mjs</c> recorded in that engine as JSON (<c>?clear</c> empties the
/// record after the answer). The middleware of the fixture logs a <c>missing-in-handler</c> warning for every request, including the one for
/// <c>/messages.json</c>, before the endpoint runs; the endpoint leaves that last one out so that a snapshot holds the messages of the
/// earlier requests only.</item>
/// <item><c>loadFixture</c> sets the log level to <c>warn</c> upstream (<c>logLevel</c> is not a config key, it is an inline flag the harness
/// cannot set); the build here runs at the default <c>info</c>. Every assertion selects messages by their text, as upstream does, so extra
/// <c>info</c> messages would not matter.</item>
/// </list>
/// The state is per engine, so every site runs one engine (<c>PoolSize = 1</c>). Upstream observes the messages of the bootstrap (everything
/// up to and including the first request) once, in <c>before</c>; those four tests each start their own host (a variant of its own: new host,
/// new engine, same build) and make the first request to <c>/</c> themselves, which keeps them independent of the order the tests run in.
/// The two tests inside a request share one host: they clear the record, request <c>/</c> and read the record.
/// </summary>
[Trait("Upstream", "core/runtime-logger.test.ts")]
public class RuntimeLoggerTests(SiteHost sites)
{
    private const string Fixture = "core-runtime-logger";

    private async Task<ConformanceSite> StartAsync(string variant) =>
        await sites.GetAsync(Fixture, configure: options => options.PoolSize = 1, variant: variant);

    private static async Task<JsonArray> SnapshotAsync(ConformanceSite site, bool clear = false)
    {
        using var response = await site.GetAsync(clear ? "/messages.json?clear" : "/messages.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.ReadTextAsync())!.AsArray();
    }

    private static async Task RequestIndexAsync(ConformanceSite site)
    {
        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Everything logged up to and including the first request of a new engine.
    private async Task<JsonArray> BootstrapMessagesAsync(string variant)
    {
        var site = await StartAsync(variant);
        await RequestIndexAsync(site);
        return await SnapshotAsync(site);
    }

    // What the request for "/" logs, on an engine that has already served other requests.
    private async Task<JsonArray> RequestMessagesAsync()
    {
        var site = await StartAsync("in-request");
        await SnapshotAsync(site, clear: true);
        await RequestIndexAsync(site);
        return await SnapshotAsync(site);
    }

    private static List<JsonNode> Matching(JsonArray messages, string pattern) =>
        messages.Select(message => message!).Where(message => Regex.IsMatch((string)message["message"]!, pattern)).ToList();

    [Fact(DisplayName = "Runtime logger: uses the configured destination, not the console")]
    public async Task ConfiguredDestination_ReceivesTheMessages()
    {
        var bootstrap = await BootstrapMessagesAsync("bootstrap-destination");

        Assert.True(bootstrap.Count > 0, "the configured destination received nothing");
        // Every message carries the AstroLoggerMessage shape, so it went through
        // AstroLogger rather than being handed straight to `console`.
        foreach (var message in bootstrap)
        {
            Assert.Equal(JsonValueKind.String, message!["message"]!.GetValueKind());
            Assert.True(message["newLine"]!.GetValueKind() is JsonValueKind.True or JsonValueKind.False);
            Assert.Contains((string)message["level"]!, new[] { "debug", "info", "warn", "error" });
        }
    }

    [Fact(DisplayName = "Runtime logger: getEntry() outside of a request: routes the missing-entry warning to the configured destination")]
    public async Task GetEntryAtModuleScope_RoutesTheWarningToTheDestination()
    {
        var bootstrap = await BootstrapMessagesAsync("bootstrap-module-scope");

        var matching = Matching(bootstrap, "missing-at-module-scope");
        // The module-scope warning should be logged once
        var message = Assert.Single(matching);
        Assert.Equal("warn", (string)message["level"]!);
        Assert.Equal("content", (string)message["label"]!);
        Assert.Equal("Entry blog \u2192 missing-at-module-scope was not found.", (string)message["message"]!);
    }

    [Fact(DisplayName = "Runtime logger: getEntry() outside of a request: logs it before the middleware handler runs")]
    public async Task GetEntryAtModuleScope_IsLoggedBeforeTheMiddlewareHandlerRuns()
    {
        var bootstrap = await BootstrapMessagesAsync("bootstrap-order");

        var order = Matching(bootstrap, "missing-(?:at-module-scope|in-handler)").Select(message => (string)message["message"]!);
        Assert.Equal(
            [
                "Entry blog \u2192 missing-at-module-scope was not found.",
                "Entry blog \u2192 missing-in-handler was not found.",
            ],
            order);
    }

    [Fact(DisplayName = "Runtime logger: getEntry() outside of a request: does not log again on later requests")]
    public async Task GetEntryAtModuleScope_IsNotLoggedAgainOnLaterRequests()
    {
        var site = await StartAsync("bootstrap-later-requests");
        await RequestIndexAsync(site);
        await SnapshotAsync(site, clear: true);

        await RequestIndexAsync(site);

        var recorded = await SnapshotAsync(site);
        Assert.Empty(Matching(recorded, "missing-at-module-scope"));
        // Not asserted upstream: the destination was recording, the request did log its own warnings.
        Assert.NotEmpty(Matching(recorded, "missing-from-page"));
    }

    [Fact(DisplayName = "Runtime logger: getEntry() inside a request: routes a middleware handler warning to the configured destination")]
    public async Task GetEntryInAMiddlewareHandler_RoutesTheWarningToTheDestination()
    {
        var recorded = await RequestMessagesAsync();

        var message = Matching(recorded, "missing-in-handler").First();
        Assert.Equal("warn", (string)message["level"]!);
        Assert.Equal("content", (string)message["label"]!);
    }

    [Fact(DisplayName = "Runtime logger: getEntry() inside a request: routes a page warning to the configured destination")]
    public async Task GetEntryInAPage_RoutesTheWarningToTheDestination()
    {
        var recorded = await RequestMessagesAsync();

        var message = Matching(recorded, "missing-from-page").First();
        Assert.Equal("warn", (string)message["level"]!);
        Assert.Equal("content", (string)message["label"]!);
        Assert.Equal("Entry blog \u2192 missing-from-page was not found.", (string)message["message"]!);
    }
}
