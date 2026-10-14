using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace AstroJint.AspNetCore;

/// <summary>
/// Where <c>Astro.session</c> keeps its data. The data lives outside the JavaScript engines, in an
/// <see cref="IDistributedCache"/>, because every request may be rendered by another engine of the pool and a
/// scaled-out site by another process. A failing cache is logged here; JavaScript only learns that it failed.
/// </summary>
internal sealed partial class AstroSessionStore(IDistributedCache cache, TimeSpan idleTimeout, ILogger<AstroSessionStore> logger)
{
    private const string KeyPrefix = "astro-jint:session:";

    private readonly DistributedCacheEntryOptions _entryOptions = new()
    {
        SlidingExpiration = idleTimeout == Timeout.InfiniteTimeSpan ? null : idleTimeout,
    };

    public async Task<string?> GetAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var data = await cache.GetAsync(KeyPrefix + id, cancellationToken).ConfigureAwait(false);
            return data is null ? null : Encoding.UTF8.GetString(data);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, "read");
            throw;
        }
    }

    public async Task SetAsync(string id, string data, CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetAsync(KeyPrefix + id, Encoding.UTF8.GetBytes(data), _entryOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, "write");
            throw;
        }
    }

    public async Task RemoveAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            await cache.RemoveAsync(KeyPrefix + id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, "delete");
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The distributed cache failed to {Operation} session data.")]
    private static partial void LogFailed(ILogger logger, Exception exception, string operation);
}
