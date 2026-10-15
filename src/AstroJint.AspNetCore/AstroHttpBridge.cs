using System.Buffers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace AstroJint.AspNetCore;

/// <summary>An on-demand response, fully buffered and detached from the engine that produced it.</summary>
internal sealed record AstroResponse(int StatusCode, IReadOnlyList<KeyValuePair<string, StringValues>> Headers, byte[] Body);

internal sealed class AstroRequestBodyTooLargeException() : Exception("The request body is larger than AstroOptions.MaxRequestBodyBytes.");

/// <summary>Translates between ASP.NET Core's HTTP types and the <see cref="HttpRequestMessage"/> that Jint's fetch handler takes.</summary>
internal static class AstroHttpBridge
{
    // Connection-level headers belong to the hop they arrived on. Content-Length is recomputed from the buffered body.
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Proxy-Connection", "TE", "Trailer", "Transfer-Encoding", "Upgrade", "Content-Length", "Expect",
    };

    /// <param name="source">The request to translate.</param>
    /// <param name="maxBodyBytes">The largest request body to buffer.</param>
    /// <param name="cancellationToken">Cancels reading the request body.</param>
    /// <param name="allowedDomains">
    /// <c>security.allowedDomains</c> of the site. With entries, only a <c>Host</c> that matches one of them shapes the
    /// URL, and any other is replaced by <c>localhost</c> with the port of the server, as the Node adapter does.
    /// Without, the host is the one ASP.NET Core resolved for the request (see <c>HostFiltering</c> and <c>UseForwardedHeaders</c>).
    /// </param>
    public static async Task<HttpRequestMessage> CreateRequestAsync(
        HttpRequest source,
        long maxBodyBytes,
        CancellationToken cancellationToken,
        IReadOnlyList<AstroAllowedDomain>? allowedDomains = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(source.Method), BuildUrl(source, allowedDomains));

        try
        {
            if (!HttpMethods.IsGet(source.Method) && !HttpMethods.IsHead(source.Method))
            {
                // The content headers live on the content, so even an empty body gets one: a form without fields posts
                // nothing but a Content-Type, and formData() fails without it.
                var body = await ReadBodyAsync(source, maxBodyBytes, cancellationToken).ConfigureAwait(false);
                request.Content = new ByteArrayContent(body);
            }

            foreach (var header in source.Headers)
            {
                if (HopByHopHeaders.Contains(header.Key))
                {
                    continue;
                }

                // Several cookie lines (HTTP/2 splits them) are one header for a server-side reader.
                var value = header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase)
                    ? string.Join("; ", header.Value.ToArray())
                    : header.Value.ToString();

                if (!request.Headers.TryAddWithoutValidation(header.Key, value))
                {
                    request.Content?.Headers.TryAddWithoutValidation(header.Key, value);
                }
            }

            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }

    public static async Task<AstroResponse> ReadResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // The body is read here, while the engine is still ours, because the content may be produced lazily.
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        var headers = new List<KeyValuePair<string, StringValues>>();
        // The non-validated view yields values as they were set; the typed view re-serialises them (Content-Type
        // 'text/plain;charset=utf-8' would come out as 'text/plain; charset=utf-8').
        foreach (var header in response.Headers.NonValidated.Concat(response.Content.Headers.NonValidated))
        {
            if (!HopByHopHeaders.Contains(header.Key))
            {
                headers.Add(new(header.Key, new StringValues(header.Value.ToArray())));
            }
        }

        return new AstroResponse((int)response.StatusCode, headers, body);
    }

    public static async Task WriteResponseAsync(HttpContext context, AstroResponse response)
    {
        var target = context.Response;
        target.StatusCode = response.StatusCode;

        foreach (var (name, values) in response.Headers)
        {
            target.Headers.Append(name, values);
        }

        if (response.StatusCode is 204 or 205 or 304 or < 200)
        {
            return;
        }

        target.ContentLength = response.Body.Length;
        if (response.Body.Length > 0 && !HttpMethods.IsHead(context.Request.Method))
        {
            await target.Body.WriteAsync(response.Body, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private static Uri BuildUrl(HttpRequest source, IReadOnlyList<AstroAllowedDomain>? allowedDomains)
    {
        var path = GetPath(source);
        var fallback = new HostString(source.HttpContext.Connection.LocalPort is > 0 and var port ? $"localhost:{port}" : "localhost");

        HostString host;
        if (allowedDomains is { Count: > 0 })
        {
            var forwardedProtocol = AstroHostValidator.ValidateForwardedProtocol(
                AstroHostValidator.GetFirstForwardedValue(source.Headers["X-Forwarded-Proto"]), allowedDomains);
            var trusted = AstroHostValidator.ValidateHost(source.Host.Value, forwardedProtocol ?? source.Scheme, allowedDomains);
            host = trusted is null ? fallback : new HostString(trusted);
        }
        else
        {
            host = source.Host.HasValue ? source.Host : fallback;
        }

        var url = UriHelper.BuildAbsolute(source.Scheme, host, source.PathBase, path, source.QueryString);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri
            : new Uri(UriHelper.BuildAbsolute(source.Scheme, fallback, source.PathBase, path, source.QueryString));
    }

    // A URL parser reads a backslash in the path of an http(s) URL as a slash, so Astro sees //example.com for the target
    // /\example.com, as it does behind the Node adapter. Kestrel keeps the backslash in Path, which would become %5C and
    // make a page of its own of it. Only a backslash that the client wrote as one is folded: %5C stays a character of a segment.
    private static PathString GetPath(HttpRequest source)
    {
        var path = source.Path;
        if (!path.HasValue || !path.Value.Contains('\\'))
        {
            return path;
        }

        var rawTarget = source.HttpContext.Features.Get<IHttpRequestFeature>()?.RawTarget ?? "";
        var query = rawTarget.IndexOf('?');
        var rawPath = query < 0 ? rawTarget : rawTarget[..query];
        return rawPath.Contains('\\') ? new PathString(path.Value.Replace('\\', '/')) : path;
    }

    /// <summary>The address Astro reports as <c>Astro.clientAddress</c>.</summary>
    public static string? GetClientAddress(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return null;
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    private static async Task<byte[]> ReadBodyAsync(HttpRequest source, long maxBodyBytes, CancellationToken cancellationToken)
    {
        if (source.ContentLength > maxBodyBytes)
        {
            throw new AstroRequestBodyTooLargeException();
        }

        using var body = new MemoryStream();
        var chunk = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            int read;
            while ((read = await source.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (body.Length + read > maxBodyBytes)
                {
                    throw new AstroRequestBodyTooLargeException();
                }

                body.Write(chunk, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        return body.ToArray();
    }
}
