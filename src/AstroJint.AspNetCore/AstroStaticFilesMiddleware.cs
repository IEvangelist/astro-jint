using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
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
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";
    private const string RevalidateCacheControl = "public, max-age=0, must-revalidate";

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

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
            StaticFileMatchKind.File => ServeAsync(context, match),
            StaticFileMatchKind.Redirect => RedirectAsync(context, match.Path!),
            StaticFileMatchKind.None => next(context),
            _ => throw new UnreachableException(),
        };
    }

    // The fallback endpoint of MapAstro() matches every path, so only endpoints without Astro's marker count.
    private static bool IsClaimedByAnotherEndpoint(HttpContext context) =>
        context.GetEndpoint() is { } endpoint && endpoint.Metadata.GetMetadata<AstroEndpointMetadata>() is null;

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

        // ASP.NET Core ignores an If-Match it cannot parse and would serve the file; other file servers fail the precondition.
        var request = context.Request;
        if (!StringValues.IsNullOrEmpty(request.Headers.IfMatch) && request.GetTypedHeaders().IfMatch.Count == 0)
        {
            context.Response.StatusCode = StatusCodes.Status412PreconditionFailed;
            return;
        }

        var lastModified = file.LastWriteTimeUtc;
        var lastModifiedSeconds = new DateTimeOffset(lastModified.Ticks - (lastModified.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
        var entityTag = new EntityTagHeaderValue($"\"{lastModifiedSeconds.ToFileTime() ^ file.Length:x}\"");

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
        }, (context.Response, match.Immutable ? ImmutableCacheControl : RevalidateCacheControl));

        var result = Results.File(
            file.FullName,
            GetContentType(file.Name),
            lastModified: lastModifiedSeconds,
            entityTag: entityTag,
            enableRangeProcessing: true);

        await result.ExecuteAsync(context).ConfigureAwait(false);
    }

    private static Task RedirectAsync(HttpContext context, string path)
    {
        context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
        context.Response.Headers.Location = new PathString(path).ToUriComponent() + context.Request.QueryString.ToUriComponent();
        return Task.CompletedTask;
    }

    private static string GetContentType(string fileName)
    {
        if (!ContentTypes.TryGetContentType(fileName, out var contentType))
        {
            return "application/octet-stream";
        }

        var isText = contentType.StartsWith("text/", StringComparison.Ordinal)
            || contentType is "application/json" or "application/javascript" or "application/xml"
            || contentType.EndsWith("+json", StringComparison.Ordinal)
            || contentType.EndsWith("+xml", StringComparison.Ordinal);

        return isText ? contentType + "; charset=utf-8" : contentType;
    }
}
