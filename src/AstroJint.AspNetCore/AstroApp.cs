using System.Diagnostics;
using System.Text.Json;
using Jint.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AstroJint.AspNetCore;

/// <summary>
/// A built Astro site that is hosted in this process: its manifest, the index of its static files and the pool of
/// JavaScript engines that render its on-demand pages.
/// </summary>
internal sealed partial class AstroApp : IAsyncDisposable
{
    private static readonly JsonSerializerOptions LocalsJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AstroOptions _options;
    private readonly ILogger<AstroApp> _logger;

    public AstroApp(IOptions<AstroOptions> options, IHostEnvironment environment, ILoggerFactory loggerFactory, IDistributedCache sessionCache)
    {
        _options = options.Value;
        var logger = _logger = loggerFactory.CreateLogger<AstroApp>();

        DistPath = Path.GetFullPath(_options.DistPath, environment.ContentRootPath);
        Manifest = AstroManifest.Load(DistPath);
        ClientRoot = Path.GetFullPath(Manifest.Client, DistPath);

        var entryPath = Path.GetFullPath(Manifest.Entry, DistPath);
        if (!File.Exists(entryPath))
        {
            throw new FileNotFoundException($"The Astro server bundle '{entryPath}' named by '{AstroManifest.FileName}' does not exist.", entryPath);
        }

        Files = AstroStaticFileResolver.FromDirectory(ClientRoot, Manifest);
        FileCache = AstroStaticFileCache.Load(Files, ClientRoot, _options.StaticFileCacheBytes);
        var consoleSink = new LoggerConsoleSink(loggerFactory.CreateLogger(LoggerConsoleSink.Category));
        var sessions = new AstroSessionStore(sessionCache, _options.SessionIdleTimeout, loggerFactory.CreateLogger<AstroSessionStore>());
        var factory = new AstroEngineFactory(_options, entryPath, ReadClientFile, ReadClientBytes, ReadClientHeaders, sessions, consoleSink);
        Pool = new EnginePool<AstroEngineSlot>(_options.PoolSize, factory.CreateAsync, logger);

        LogLoaded(logger, Manifest.AstroVersion ?? "unknown", DistPath, Files.Count, _options.PoolSize);
        LogFileCache(logger, FileCache.Count, Files.Count, FileCache.Bytes, _options.StaticFileCacheBytes);
    }

    public string DistPath { get; }

    public string ClientRoot { get; }

    public AstroManifest Manifest { get; }

    public AstroStaticFileResolver Files { get; }

    public AstroStaticFileCache FileCache { get; }

    public EnginePool<AstroEngineSlot> Pool { get; }

    /// <summary>Creates all engines now, so that on-demand requests do not wait for them.</summary>
    public async Task WarmUpAsync()
    {
        var started = Stopwatch.GetTimestamp();
        await Pool.WarmUpAsync().ConfigureAwait(false);
        var elapsed = Stopwatch.GetElapsedTime(started);
        LogWarmedUp(_logger, Pool.Reserved, Pool.Size, elapsed.TotalMilliseconds);
    }

    /// <summary>Renders the request with Astro's on-demand runtime and writes the response.</summary>
    public async Task RenderAsync(HttpContext context)
    {
        HttpRequestMessage request;
        try
        {
            request = await AstroHttpBridge
                .CreateRequestAsync(context.Request, _options.MaxRequestBodyBytes, context.RequestAborted, Manifest.AllowedDomains)
                .ConfigureAwait(false);
        }
        catch (AstroRequestBodyTooLargeException)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        catch (BadHttpRequestException ex)
        {
            // The body cannot be read to its end, for example because the client left halfway through an upload.
            context.Response.StatusCode = ex.StatusCode;
            return;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException && context.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        using (request)
        {
            var response = await RenderCoreAsync(context, request).ConfigureAwait(false);
            if (response is not null)
            {
                await AstroHttpBridge.WriteResponseAsync(context, response).ConfigureAwait(false);
            }
        }
    }

    public ValueTask DisposeAsync() => Pool.DisposeAsync();

    // Returns null when the outcome (an error status, or a client that left) has already been dealt with.
    private async Task<AstroResponse?> RenderCoreAsync(HttpContext context, HttpRequestMessage request)
    {
        var aborted = context.RequestAborted;
        var localsJson = SerializeLocals(context);

        // The timeout covers waiting for an engine as well as rendering, so a saturated pool surfaces as a 503.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(aborted);
        if (_options.RequestTimeout != Timeout.InfiniteTimeSpan)
        {
            deadline.CancelAfter(_options.RequestTimeout);
        }

        var token = deadline.Token;

        AstroEngineSlot? slot = null;
        try
        {
            slot = await Pool.RentAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }

        if (slot is null || token.IsCancellationRequested)
        {
            if (slot is not null)
            {
                Pool.Return(slot, discard: false);
            }

            if (!aborted.IsCancellationRequested)
            {
                LogNoFreeEngine(_logger, context.Request.Method, context.Request.Path, _options.RequestTimeout);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.Headers.RetryAfter = "1";
            }

            return null;
        }

        // An engine whose JavaScript was interrupted, or whose memory budget ran out, is replaced rather than reused.
        var discard = false;
        try
        {
            slot.Context.ClientAddress = AstroHttpBridge.GetClientAddress(context);
            slot.Context.LocalsJson = localsJson;
            slot.Context.Cancellation = token;

            using var message = await slot.Engine.WebApi.InvokeFetchHandlerAsync(request, token).ConfigureAwait(false);
            return await AstroHttpBridge.ReadResponseAsync(message, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            discard = true;
            if (!aborted.IsCancellationRequested)
            {
                LogTimeout(_logger, context.Request.Method, context.Request.Path, _options.RequestTimeout);
                context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            }

            return null;
        }
        catch (MemoryLimitExceededException ex)
        {
            discard = true;
            LogMemoryLimit(_logger, ex, context.Request.Method, context.Request.Path, _options.MemoryLimitBytes);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return null;
        }
        catch (Exception ex) when (ex is JavaScriptException or PromiseRejectedException)
        {
            LogJavaScriptError(_logger, ex, context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return null;
        }
        catch
        {
            discard = true;
            throw;
        }
        finally
        {
            slot.Context.Clear();
            Pool.Return(slot, discard);
        }
    }

    private string? SerializeLocals(HttpContext context) =>
        _options.LocalsFactory?.Invoke(context) is { } locals
            ? JsonSerializer.Serialize(locals, LocalsJsonOptions)
            : null;

    // Used by the bundle to load the prerendered 404 and 500 pages. The path is the percent-encoded path of a URL.
    private string? ReadClientFile(string pathname) =>
        FindClientFile(pathname) is { } path ? File.ReadAllText(path) : null;

    // Used by the bundle's image endpoint to load the original of a local image. Addressed like ReadClientFile.
    private byte[]? ReadClientBytes(string pathname) =>
        FindClientFile(pathname) is { } path ? File.ReadAllBytes(path) : null;

    // Used by the bundle to give a prerendered 404 or 500 page the headers that were recorded for its file (see
    // staticHeaders), which the static files middleware would have sent had the file been requested itself.
    private string? ReadClientHeaders(string pathname) =>
        Files.TryGetFile(Uri.UnescapeDataString(pathname), out var relativePath) && Files.GetHeaders(relativePath) is { } headers
            ? JsonSerializer.Serialize(headers)
            : null;

    private string? FindClientFile(string pathname) =>
        Files.TryGetFile(Uri.UnescapeDataString(pathname), out var relativePath)
            ? Path.Combine(ClientRoot, relativePath)
            : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "Hosting Astro {AstroVersion} from '{DistPath}': {StaticFileCount} static files, {PoolSize} JavaScript engines.")]
    private static partial void LogLoaded(ILogger logger, string astroVersion, string distPath, int staticFileCount, int poolSize);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Kept {CachedCount} of {StaticFileCount} static files in memory: {CachedBytes:N0} of {BudgetBytes:N0} bytes.")]
    private static partial void LogFileCache(ILogger logger, int cachedCount, int staticFileCount, long cachedBytes, long budgetBytes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Warmed up {Ready} of {PoolSize} JavaScript engines in {ElapsedMs:N0} ms.")]
    private static partial void LogWarmedUp(ILogger logger, int ready, int poolSize, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Method} {Path} was not rendered: no JavaScript engine became free within {Timeout}. Consider a larger PoolSize.")]
    private static partial void LogNoFreeEngine(ILogger logger, string method, PathString path, TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Method} {Path} did not finish rendering within {Timeout}. The JavaScript engine was replaced.")]
    private static partial void LogTimeout(ILogger logger, string method, PathString path, TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Method} {Path} exceeded the JavaScript memory limit of {Limit} bytes. The JavaScript engine was replaced.")]
    private static partial void LogMemoryLimit(ILogger logger, Exception exception, string method, PathString path, long? limit);

    [LoggerMessage(Level = LogLevel.Error, Message = "Rendering {Method} {Path} failed with a JavaScript error.")]
    private static partial void LogJavaScriptError(ILogger logger, Exception exception, string method, PathString path);
}
