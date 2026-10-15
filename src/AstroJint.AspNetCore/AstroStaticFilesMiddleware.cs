using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace AstroJint.AspNetCore;

/// <summary>Marks the endpoint that renders Astro's on-demand pages, so that other components can recognise it.</summary>
internal sealed class AstroEndpointMetadata
{
    public static AstroEndpointMetadata Instance { get; } = new();
}

/// <summary>
/// Serves the pages and assets that <c>astro build</c> produced ahead of time. Everything else, and every request
/// that another endpoint of the application claims, goes on to the next component.
/// </summary>
internal sealed class AstroStaticFilesMiddleware(RequestDelegate next, AstroApp app)
{
    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) || IsClaimedByAnotherEndpoint(context))
        {
            return next(context);
        }

        var match = app.Files.Resolve(request.PathBase.Add(request.Path).Value ?? "/");
        return match.Kind switch
        {
            StaticFileMatchKind.File => app.FileCache.TryGet(match.Path!, out var cached)
                ? ServeCachedAsync(context, match.Immutable, cached)
                : ServeAsync(context, match),
            StaticFileMatchKind.Redirect => RedirectAsync(context, match.Path!),
            StaticFileMatchKind.None => next(context),
            _ => throw new UnreachableException(),
        };
    }

    // The fallback endpoint of MapAstro() matches every path, so only endpoints without Astro's marker count.
    private static bool IsClaimedByAnotherEndpoint(HttpContext context) =>
        context.GetEndpoint() is { } endpoint && endpoint.Metadata.GetMetadata<AstroEndpointMetadata>() is null;

    // A file that is held in memory. The two answers that make up almost all traffic, the whole file and "not
    // modified", are written directly. Anything else, such as a range or a precondition that is not the plain
    // revalidation of a browser, is left to ASP.NET Core's file result, which implements those rules.
    private static Task ServeCachedAsync(HttpContext context, bool immutable, CachedStaticFile file)
    {
        var requestHeaders = context.Request.Headers;
        if (StringValues.IsNullOrEmpty(requestHeaders.Range)
            && StringValues.IsNullOrEmpty(requestHeaders.IfRange)
            && StringValues.IsNullOrEmpty(requestHeaders.IfMatch)
            && StringValues.IsNullOrEmpty(requestHeaders.IfUnmodifiedSince))
        {
            var ifNoneMatch = requestHeaders.IfNoneMatch;
            var ifModifiedSince = requestHeaders.IfModifiedSince;

            if (StringValues.IsNullOrEmpty(ifNoneMatch))
            {
                if (StringValues.IsNullOrEmpty(ifModifiedSince))
                {
                    return WriteCachedAsync(context, immutable, file);
                }
            }
            else if (ifNoneMatch.Count == 1
                && string.Equals(ifNoneMatch[0], file.ETag, StringComparison.Ordinal)
                && (StringValues.IsNullOrEmpty(ifModifiedSince)
                    || (ifModifiedSince.Count == 1 && string.Equals(ifModifiedSince[0], file.LastModified, StringComparison.Ordinal))))
            {
                // What a browser sends to revalidate a file that it has: the validators of the response it has.
                return WriteNotModified(context, immutable, file);
            }
        }

        return ServeCachedWithResultAsync(context, immutable, file);
    }

    private static Task WriteCachedAsync(HttpContext context, bool immutable, CachedStaticFile file)
    {
        var response = context.Response;
        var headers = response.Headers;
        SetValidators(headers, immutable, file);
        headers.AcceptRanges = "bytes";
        response.ContentType = file.ContentType;
        response.ContentLength = file.Content.Length;

        return HttpMethods.IsHead(context.Request.Method) ? Task.CompletedTask : WriteBodyAsync(context, file.Content);
    }

    private static Task WriteNotModified(HttpContext context, bool immutable, CachedStaticFile file)
    {
        var response = context.Response;
        response.StatusCode = StatusCodes.Status304NotModified;
        SetValidators(response.Headers, immutable, file);
        return Task.CompletedTask;
    }

    // The headers that every response with the file has, and the ones that Astro rendered for the page at build time.
    private static void SetValidators(IHeaderDictionary headers, bool immutable, CachedStaticFile file)
    {
        foreach (var header in file.Headers)
        {
            headers[header.Key] = header.Value;
        }

        headers.ETag = file.ETag;
        headers.LastModified = file.LastModified;
        headers.CacheControl = immutable ? StaticFileConventions.ImmutableCacheControl : StaticFileConventions.RevalidateCacheControl;
    }

    // Asynchronous only when the connection cannot take all of the file at once.
    private static Task WriteBodyAsync(HttpContext context, byte[] content)
    {
        var write = context.Response.Body.WriteAsync(content, context.RequestAborted);
        return write.IsCompletedSuccessfully ? Task.CompletedTask : AwaitBodyAsync(context, write);
    }

    private static async Task AwaitBodyAsync(HttpContext context, ValueTask write)
    {
        try
        {
            await write.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The client left. The response cannot be completed, so end the connection like the file result does.
            context.Abort();
        }
    }

    private static async Task ServeCachedWithResultAsync(HttpContext context, bool immutable, CachedStaticFile file)
    {
        foreach (var header in file.Headers)
        {
            context.Response.Headers[header.Key] = header.Value;
        }

        if (!TryBeginResponse(context, immutable))
        {
            return;
        }

        var result = Results.Bytes(
            file.Content,
            file.ContentType,
            lastModified: file.LastModifiedValue,
            entityTag: file.EntityTag,
            enableRangeProcessing: true);

        await result.ExecuteAsync(context).ConfigureAwait(false);
    }

    private async Task ServeAsync(HttpContext context, StaticFileMatch match)
    {
        var file = new FileInfo(Path.Combine(app.ClientRoot, match.Path!));
        if (!file.Exists)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Headers that Astro rendered for the page at build time, such as its Content-Security-Policy.
        if (app.Files.GetHeaders(match.Path!) is { } headers)
        {
            foreach (var (name, value) in headers)
            {
                context.Response.Headers[name] = value;
            }
        }

        if (!TryBeginResponse(context, match.Immutable))
        {
            return;
        }

        var lastModified = StaticFileConventions.GetLastModified(file.LastWriteTimeUtc);
        var entityTag = new EntityTagHeaderValue(StaticFileConventions.GetETag(lastModified, file.Length));

        var result = Results.File(
            file.FullName,
            StaticFileConventions.GetContentType(file.Name),
            lastModified: lastModified,
            entityTag: entityTag,
            enableRangeProcessing: true);

        await result.ExecuteAsync(context).ConfigureAwait(false);
    }

    // What a file result needs besides the file itself. Returns false when the request has been answered.
    private static bool TryBeginResponse(HttpContext context, bool immutable)
    {
        // ASP.NET Core ignores an If-Match it cannot parse and would serve the file; other file servers fail the precondition.
        var request = context.Request;
        if (!StringValues.IsNullOrEmpty(request.Headers.IfMatch) && request.GetTypedHeaders().IfMatch.Count == 0)
        {
            context.Response.StatusCode = StatusCodes.Status412PreconditionFailed;
            return false;
        }

        // Only responses that carry the file may be cached. A failed precondition (412) or range (416) must never be,
        // least of all forever, and the status is not known until the file result has run.
        context.Response.OnStarting(static state =>
        {
            var (response, cacheControl) = ((HttpResponse, string))state;
            if (response.StatusCode is StatusCodes.Status200OK or StatusCodes.Status206PartialContent or StatusCodes.Status304NotModified)
            {
                response.Headers.CacheControl = cacheControl;
            }

            return Task.CompletedTask;
        }, (context.Response, immutable ? StaticFileConventions.ImmutableCacheControl : StaticFileConventions.RevalidateCacheControl));

        return true;
    }

    private static Task RedirectAsync(HttpContext context, string path)
    {
        context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
        context.Response.Headers.Location = new PathString(path).ToUriComponent() + context.Request.QueryString.ToUriComponent();
        return Task.CompletedTask;
    }
}
