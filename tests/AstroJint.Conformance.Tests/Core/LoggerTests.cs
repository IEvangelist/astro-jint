using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/logger.test.ts</c> on the <c>ssr-assets</c> fixture (<c>core-ssr-assets</c>): a <c>logger.entrypoint</c> is
/// bundled into the server output at build time and the destination it creates is the one the runtime logs through. Both tests are adapted:
/// upstream looks at <c>app.logger.options.destination.__custom</c> / <c>.__relative</c> of the app that <c>testAdapter</c> gives back, which
/// nothing over HTTP can reach, and a destination with an empty <c>write()</c> does not show up anywhere else. The destinations here log a
/// marker with <c>console.log</c> when they are created (the engine creates the destination when it loads the server bundle), which the host
/// writes to its log under <c>AstroJint.Console</c>; the tests assert that marker next to the <c>200</c> of <c>/</c>.
/// <list type="bullet">
/// <item><c>custom entrypoint</c>: the module is written by the test to a directory that is not part of the project (a folder of the output of
/// the test project) and deleted as soon as the build is done, as upstream does with its temporary directory, so the bundle cannot import it
/// at runtime. Upstream passes a <c>file:</c> URL object; the config here is JSON, so the entrypoint is the absolute path of the module. The
/// module is the one of upstream plus the <c>console.log</c> marker. The engine is created by the first request (<c>WarmUp</c> is off), after
/// the module has been deleted.</item>
/// <item><c>relative entrypoint</c>: <c>./src/logger.mjs</c> of the fixture, resolved against the project root. Not upstream: the fixture's
/// <c>logger.mjs</c> has the same <c>console.log</c> marker added.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/logger.test.ts")]
public class LoggerTests(SiteHost sites)
{
    private const string Fixture = "core-ssr-assets";
    private const string CustomMarker = "logger-test: custom destination created";
    private const string RelativeMarker = "logger-test: relative destination created";

    // The module of upstream (`__custom` lets it tell the module apart from a built-in fallback) plus the marker.
    private const string CustomLoggerModule = $$"""
        export default function () {
        	console.log('{{CustomMarker}}');
        	return { __custom: true, write() {} };
        }
        """;

    private static async Task AssertDestinationWasCreatedByTheEngineAsync(ConformanceSite site, string marker)
    {
        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(site.Logs, entry => entry.Category == "AstroJint.Console" && entry.Message.Contains(marker));
    }

    [Fact(DisplayName = "Logger: custom entrypoint: bundles and uses the custom logger at runtime (SSR)")]
    public async Task CustomEntrypoint_OutsideTheProject_IsBundledAndUsedAtRuntime()
    {
        // Kept outside the project so the built output cannot resolve it after deletion.
        var directory = Path.Combine(AppContext.BaseDirectory, "logger-custom-entrypoint");
        var entrypoint = Path.Combine(directory, "logger.mjs");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(entrypoint, CustomLoggerModule);

        ConformanceSite site;
        try
        {
            site = await sites.GetAsync(
                Fixture,
                new { logger = new { entrypoint = entrypoint.Replace('\\', '/') } },
                configure: options => options.WarmUp = false,
                variant: "logger-custom-entrypoint");
        }
        finally
        {
            // Simulates a deployed bundle: only the build output remains.
            Directory.Delete(directory, recursive: true);
        }

        await AssertDestinationWasCreatedByTheEngineAsync(site, CustomMarker);
    }

    [Fact(DisplayName = "Logger: relative entrypoint: bundles and uses the custom logger at runtime (SSR)")]
    public async Task RelativeEntrypoint_IsBundledAndUsedAtRuntime()
    {
        var site = await sites.GetAsync(Fixture, new { logger = new { entrypoint = "./src/logger.mjs" } });

        await AssertDestinationWasCreatedByTheEngineAsync(site, RelativeMarker);
    }
}
