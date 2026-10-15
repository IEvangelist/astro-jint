using System.Text.Json;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/request-signal.test.ts</c>. The fixture is <c>request-signal</c> of Astro (<c>core-request-signal</c>):
/// three endpoints that wait for <c>request.signal</c> to abort and record it in <c>globalThis.__requestAbortState</c>, which
/// <c>/status.json</c> reports (and resets with <c>?reset=1</c>). The fixture has one more endpoint, <c>/stream-log</c>, that is not in
/// upstream and writes to the console of the engine instead of to that state.
/// <list type="bullet">
/// <item>The two <c>Node request abort integration</c> tests end the request by emitting <c>close</c> on a mock socket, ten milliseconds after
/// they call the handler. Here the client cancels its request after it has had time to start the endpoint, which closes the connection.
/// The state lives in the module of the engine, so the tests use a site with a single engine (<c>PoolSize = 1</c>): it is busy with the
/// endless response and can only answer <c>/status.json</c> once the host has let go of it. <b>Both are gaps</b> (GAP-CORE-2): the host never
/// runs the <c>abort</c> listeners (and it retires the engine, so the state is lost).</item>
/// <item>The <c>Dev server request abort signal</c> test runs a dev server upstream. Its only assertion is that a request that is not
/// aborted reports <c>aborted === false</c> after another request has been aborted, which the production build answers the same way:
/// adapted.</item>
/// </list>
/// A test that is not in upstream (it does not count in the matrix) also stays skipped as GAP-CORE-2: it reads the console of the engine to show
/// that the <c>abort</c> listener of <c>/stream-log</c> never runs, which separates the missing event from the lost state.
/// </summary>
[Trait("Upstream", "core/request-signal.test.ts")]
public class RequestSignalTests(SiteHost sites)
{
    private const string Fixture = "core-request-signal";

    private const string Gap = "GAP-CORE-2: the host cancels the render when the client disconnects and retires the engine; the 'abort' listeners of request.signal never run";

    private Task<ConformanceSite> GetSingleEngineSiteAsync() =>
        sites.GetAsync(Fixture, variant: "one-engine", configure: options => options.PoolSize = 1);

    [Fact(DisplayName = "Node request abort integration: aborts request.signal when the underlying socket closes", Skip = Gap)]
    public async Task Stream_RequestSignalAbortsWhenTheClientDisconnects()
    {
        var site = await GetSingleEngineSiteAsync();

        var state = await AbortAndReadStateAsync(site, "/stream");

        Assert.True(state.GetProperty("aborted").GetBoolean());
        Assert.Equal(["started", "aborted"], Events(state));
    }

    [Fact(DisplayName = "Node request abort integration: aborts request.signal for SSE responses when the underlying socket closes", Skip = Gap)]
    public async Task ServerSentEvents_RequestSignalAbortsWhenTheClientDisconnects()
    {
        var site = await GetSingleEngineSiteAsync();

        var state = await AbortAndReadStateAsync(site, "/sse");

        Assert.True(state.GetProperty("aborted").GetBoolean());
        Assert.Equal(["started", "aborted"], Events(state));
    }

    [Fact(DisplayName = "not in upstream: the abort listener of request.signal runs in the engine when the client disconnects", Skip = Gap)]
    public async Task AbortListener_RunsInTheEngineWhenTheClientDisconnects()
    {
        var site = await GetSingleEngineSiteAsync();
        var logStart = site.Logs.Count;

        await DisconnectAfterTheStartAsync(site, "/stream-log");

        await Task.Delay(1000);
        var output = site.Logs.Skip(logStart).Select(entry => entry.Message).ToList();
        Assert.Contains("stream-log: started, aborted=false", output);
        Assert.Contains("stream-log: aborted", output);
    }

    // The endpoint of upstream waits for the abort for up to five seconds; the second request is not aborted and runs to its end.
    [Fact(DisplayName = "Dev server request abort signal: aborts request.signal when the client disconnects")]
    public async Task AbortedRequest_DoesNotMakeTheNextRequestReportAnAbort()
    {
        var site = await sites.GetAsync(Fixture);
        using var cancellation = new CancellationTokenSource();

        var aborted = site.Client.GetAsync("/dev-signal-test", HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
        await Task.Delay(200);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => aborted);
        await Task.Delay(500);

        using var response = await site.GetAsync("/dev-signal-test");
        using var payload = JsonDocument.Parse(await response.ReadTextAsync());

        Assert.False(payload.RootElement.GetProperty("aborted").GetBoolean(), "non-aborted request should report aborted=false");
    }

    private static string[] Events(JsonElement state) =>
        [.. state.GetProperty("events").EnumerateArray().Select(item => item.GetString()!)];

    private static async Task<JsonElement> AbortAndReadStateAsync(ConformanceSite site, string path)
    {
        using (await site.GetAsync("/status.json?reset=1"))
        {
        }

        await DisconnectAfterTheStartAsync(site, path);

        using var response = await site.GetAsync("/status.json");
        return JsonDocument.Parse(await response.ReadTextAsync()).RootElement.Clone();
    }

    // The engine answers nothing else while the endpoint waits, so the start of the endpoint can only be waited for.
    private static async Task DisconnectAfterTheStartAsync(ConformanceSite site, string path)
    {
        using var cancellation = new CancellationTokenSource();
        var pending = site.Client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
        await Task.Delay(500);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }
}
