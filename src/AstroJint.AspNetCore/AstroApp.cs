using System.Diagnostics;
using System.Text.Json;
using Jint.Runtime;
using Microsoft.AspNetCore.Http;
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
    private readonly EnginePool<AstroEngineSlot> _pool;

    public AstroApp(IOptions<AstroOptions> options, IHostEnvironment environment, ILogger<AstroApp> logger)
    {
        _options = options.Value;
        _logger = logger;

        DistPath = Path.GetFullPath(_options.DistPath, environment.ContentRootPath);
        Manifest = AstroManifest.Load(DistPath);
        ClientRoot = Path.GetFullPath(Manifest.Client, DistPath);

        var entryPath = Path.GetFullPath(Manifest.Entry, DistPath);
        if (!File.Exists(entryPath))
        {
            throw new FileNotFoundException($"The Astro server bundle '{entryPath}' named by '{AstroManifest.FileName}' does not exist.", entryPath);
        }

        Files = AstroStaticFileResolver.FromDirectory(ClientRoot, Manifest);

        var factory = new AstroEngineFactory(_options, entryPath, ReadClientFile);
        _pool = new EnginePool<AstroEngineSlot>(_options.PoolSize, factory.CreateAsync, logger);

        LogLoaded(logger, Manifest.AstroVersion ?? "unknown", DistPath, Files.Count, _options.PoolSize);
    }

    public string DistPath { get; }

    public string ClientRoot { get; }

    public AstroManifest Manifest { get; }

    public AstroStaticFileResolver Files { get; }

    public EnginePool<AstroEngineSlot> Pool => _pool;

    /// <summary>Creates all engines now, so that on-demand requests do not wait for them.</summary>
    public async Task WarmUpAsync()
    {
        var started = Stopwatch.GetTimestamp();
        await _pool.WarmUpAsync().ConfigureAwait(false);
        LogWarmedUp(_logger, _pool.Reserved, _pool.Size, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    /// <summary>Renders the request with Astro's on-demand runtime and writes the response.</summary>
    public async Task RenderAsync(HttpContext context)
    {
        HttpRequestMessage request;
        try
        {
            request = await AstroHttpBridge
                .CreateRequestAsync(context.Request, _options.MaxRequestBodyBytes, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (AstroRequestBodyTooLargeException)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
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

    public ValueTask DisposeAsync() => _pool.DisposeAsync();

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
            slot = await _pool.RentAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }

        if (slot is null || token.IsCancellationRequested)
        {
            if (slot is not null)
            {
                _pool.Return(slot, discard: false);
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
            _pool.Return(slot, discard);
        }
    }

    private string? SerializeLocals(HttpContext context) =>
        _options.LocalsFactory?.Invoke(context) is { } locals
            ? JsonSerializer.Serialize(locals, LocalsJsonOptions)
            : null;

    // Used by the bundle to load the prerendered 404 and 500 pages. The path is the percent-encoded path of a URL.
    private string? ReadClientFile(string pathname) =>
        Files.TryGetFile(Uri.UnescapeDataString(pathname), out var relativePath)
            ? File.ReadAllText(Path.Combine(ClientRoot, relativePath))
            : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "Hosting Astro {AstroVersion} from '{DistPath}': {StaticFileCount} static files, {PoolSize} JavaScript engines.")]
    private static partial void LogLoaded(ILogger logger, string astroVersion, string distPath, int staticFileCount, int poolSize);

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
