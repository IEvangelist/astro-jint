using System.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AstroJint.AspNetCore;

/// <summary>
/// Loads the Astro build when the host starts, so that a missing or outdated build fails at startup instead of on
/// the first request, and creates the JavaScript engines in the background.
/// </summary>
internal sealed partial class AstroWarmupService(AstroApp app, IOptions<AstroOptions> options, ILogger<AstroWarmupService> logger) : IHostedService
{
    // With workstation GC, throughput stops scaling at 4-8 engines (docs/spikes/03-engine-pool-performance.md).
    private const int WorkstationGcPoolLimit = 8;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (settings.PoolSize > WorkstationGcPoolLimit && !GCSettings.IsServerGC)
        {
            LogWorkstationGc(logger, settings.PoolSize);
        }

        if (settings.WarmUp)
        {
            _ = Task.Run(app.WarmUpAsync, CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "{PoolSize} JavaScript engines are configured while the process uses workstation garbage collection. Set <ServerGarbageCollection>true</ServerGarbageCollection> (or DOTNET_gcServer=1) for better throughput under load.")]
    private static partial void LogWorkstationGc(ILogger logger, int poolSize);
}
