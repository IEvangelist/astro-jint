using System.Text.Json;
using Microsoft.Extensions.Logging;
using static AstroJint.Conformance.CoreUnits.AppLogging;
using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/logger.test.ts</c>: the destination of the logger comes from the manifest
/// (<c>logger: { entrypoint }</c>, resolved on the first request and swapped in place, so that a logger made earlier keeps
/// writing through it), the console logger is the default, and a destination does not need <c>flush()</c> or <c>close()</c>.
/// <para>
/// All six tests are adapted, because upstream reads <c>app.logger.options.destination</c>, <c>app.adapterLogger</c> and calls
/// <c>app.logger.flush()</c> on an <c>App</c> that it built by hand, none of which a request can reach. What a request can see is
/// what the destination writes: the <c>console</c> of the script feeds the log of the host under the category
/// <c>AstroJint.Console</c>, and the page <c>/logger/&lt;message&gt;</c> of the <c>app-render</c> fixture writes its last segment
/// through <c>Astro.logger</c>. The destination of the manifest is the one of the upstream build test: the config
/// <c>logger: { entrypoint: 'astro/logger/json' }</c> (an extra build of the fixture, which bundles <c>virtual:astro:logger</c>)
/// writes <c>{ "message", "label", "level" }</c> as JSON, and without it the console logger writes a time, then the text. The
/// <c>AstroLogger</c> calls of the first, fourth and fifth test (<c>forkIntegrationLogger()</c> and <c>setDestination()</c>,
/// <c>flush()</c>, <c>close()</c>) are made by the <c>logger</c> probe (<c>GET /probe/logger</c>) on the loggers that
/// <c>astro/logger/console</c> and <c>astro/logger/json</c> create, inside the host, and every request in the fourth and fifth test
/// is the proof that the real <c>flush()</c> at the end of a render did not throw.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/logger.test.ts")]
public class LoggerTests(SiteHost sites)
{
    // The destination that the build test of upstream configures and its other tests put in the manifest by hand.
    private static readonly object JsonLogger = new { logger = new { entrypoint = "astro/logger/json" } };

    [Fact(DisplayName = "adapterLogger stays stable while the logger destination is resolved in place")]
    public async Task AdapterLogger_StaysStableWhileTheDestinationIsResolvedInPlace()
    {
        var site = await sites.GetAsync(Fixture);

        var probe = await site.ProbeAsync("logger");

        var result = probe["adapter-logger-is-stable"];
        Assert.True(result.Bool("destinationSwapped"), "the custom destination should be swapped in on the same logger instance");
        Assert.True(result.Bool("loggerKeepsItsOptions"));
        Assert.True(result.Bool("adapterLoggerKeepsItsOptions"), "adapterLogger should not re-create, the logger identity is stable");
        var line = Assert.Single(result.GetProperty("events").EnumerateArray());
        Assert.Equal("through the swapped destination", line.Str("message"));
        Assert.Equal("adapter", line.Str("label"));
        Assert.Equal("info", line.Str("level"));
    }

    [Fact(DisplayName = "resolves a custom logger destination from the manifest on first request")]
    public async Task CustomDestination_IsResolvedOnTheFirstRequest()
    {
        // A site of its own, so that the request below is the first one of its engine.
        var site = await sites.GetAsync(Fixture, JsonLogger, variant: "first-request", configure: options => options.PoolSize = 1);
        const string message = "written to the destination of the manifest";

        var response = await site.SendRawAsync(LoggerPage(message));

        Assert.Equal(200, response.StatusCode);
        var (entry, payload) = site.JsonLine(message);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("info", payload.Str("level"));
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("label").ValueKind);
    }

    [Fact(DisplayName = "falls back to console logger when no custom logger is configured")]
    public async Task NoCustomLogger_FallsBackToTheConsoleLogger()
    {
        var site = await sites.GetAsync(Fixture);
        const string message = "written to the console logger";

        var response = await site.SendRawAsync("/");
        var logged = await site.SendRawAsync(LoggerPage(message));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(200, logged.StatusCode);
        var entry = site.ConsoleLine(message);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.DoesNotContain("\"level\"", entry.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "flush does not throw when destination has no flush method")]
    public async Task Flush_DoesNotThrowWhenTheDestinationHasNone()
    {
        var site = await sites.GetAsync(Fixture, JsonLogger);

        // Rendering calls flush() of the logger when it is done, so a 200 is proof that it did not throw.
        var response = await site.SendRawAsync("/");
        var probe = await site.ProbeAsync("logger");

        Assert.Equal(200, response.StatusCode);
        Assert.False(probe["flush-without-a-flush-method"].Bool("destinationHasFlush"));
    }

    [Fact(DisplayName = "close does not throw when destination has no close method")]
    public async Task Close_DoesNotThrowWhenTheDestinationHasNone()
    {
        var site = await sites.GetAsync(Fixture, JsonLogger);

        var first = await site.SendRawAsync("/");
        var probe = await site.ProbeAsync("logger");
        var second = await site.SendRawAsync("/");

        Assert.Equal(200, first.StatusCode);
        Assert.False(probe["close-without-a-close-method"].Bool("destinationHasClose"));
        Assert.Equal(200, second.StatusCode);
    }

    [Fact(DisplayName = "bundles the custom logger and resolves it at runtime")]
    public async Task Build_BundlesTheCustomLoggerAndResolvesItAtRuntime()
    {
        var site = await sites.GetAsync(Fixture, JsonLogger);

        var response = await site.SendRawAsync("/");
        var logged = await site.SendRawAsync("/logger/page");
        var probe = await site.ProbeAsync("logger");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(200, logged.StatusCode);
        var destination = probe["destination-of-the-manifest"];
        Assert.Equal("function", destination.Str("write"));
        Assert.Equal("undefined", destination.Str("flush"));
        Assert.Equal("undefined", destination.Str("close"));
        var (info, infoPayload) = site.JsonLine("page info");
        Assert.Equal(LogLevel.Information, info.Level);
        Assert.Equal("info", infoPayload.Str("level"));
        var (warn, warnPayload) = site.JsonLine("page warn");
        Assert.Equal(LogLevel.Information, warn.Level); // the JSON destination uses console.info for everything below an error
        Assert.Equal("warn", warnPayload.Str("level"));
        var (error, errorPayload) = site.JsonLine("page error");
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Equal("error", errorPayload.Str("level"));
    }
}
