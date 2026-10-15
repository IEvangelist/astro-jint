using System.Buffers;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace AstroJint.AspNetCore;

internal sealed class AstroRequestBodyTooLargeException() : Exception("The request body is larger than AstroOptions.MaxRequestBodyBytes.");

/// <summary>Translates between ASP.NET Core's HTTP types and the <see cref="HttpRequestMessage"/> that Jint's fetch handler takes.</summary>
internal static class AstroHttpBridge
{
    // A body that is declared to be this long or shorter is read into one array of exactly that size. Larger ones are
    // collected as they arrive, so that a client cannot make the host reserve memory for data that it never sends.
    private const int ExactBodyReadLimit = 64 * 1024;

    /// <param name="source">The request to translate.</param>
    /// <param name="maxBodyBytes">The largest request body to buffer.</param>
    /// <param name="cancellationToken">Cancels reading the request body.</param>
    /// <param name="allowedDomains">
    /// <c>security.allowedDomains</c> of the site. With entries, only a <c>Host</c> that matches one of them shapes the
    /// URL, and any other is replaced by <c>localhost</c> with the port of the server, as the Node adapter does.
    /// Without, the host is the one ASP.NET Core resolved for the request (see <c>HostFiltering</c> and <c>UseForwardedHeaders</c>).
    /// </param>
    public static async ValueTask<HttpRequestMessage> CreateRequestAsync(
        HttpRequest source,
        long maxBodyBytes,
        CancellationToken cancellationToken,
        IReadOnlyList<AstroAllowedDomain>? allowedDomains = null)
    {
        var request = new HttpRequestMessage(GetMethod(source.Method), BuildUrl(source, allowedDomains));

        try
        {
            if (!HttpMethods.IsGet(source.Method) && !HttpMethods.IsHead(source.Method))
            {
                // The content headers live on the content, so even an empty body gets one: a form without fields posts
                // nothing but a Content-Type, and formData() fails without it.
                request.Content = await ReadBodyAsync(source, maxBodyBytes, cancellationToken).ConfigureAwait(false);
            }

            foreach (var header in source.Headers)
            {
                var name = header.Key;
                if (IsHopByHop(name))
                {
                    continue;
                }

                // Several cookie lines (HTTP/2 splits them) are one header for a server-side reader.
                var values = header.Value;
                var value = values.Count == 1
                    ? values[0]!
                    : name.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ? string.Join("; ", values.ToArray()) : values.ToString();

                if (!request.Headers.TryAddWithoutValidation(name, value))
                {
                    request.Content?.Headers.TryAddWithoutValidation(name, value);
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

    /// <summary>
    /// Makes sure that the body of the response is complete and no longer depends on the engine, which the caller has to
    /// do while the engine is still its own. The content that Jint hands over is already an array of bytes, so there is
    /// nothing to do for it.
    /// </summary>
    public static Task BufferResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = response.Content;
        return content is ByteArrayContent || content.Headers.ContentLength == 0
            ? Task.CompletedTask
            : content.LoadIntoBufferAsync(cancellationToken);
    }

    /// <summary>
    /// Writes the response that the script returned. The body goes from the message to the response of the request as it
    /// is, without a copy of it in between; the caller disposes the message afterwards.
    /// </summary>
    public static Task WriteResponseAsync(HttpContext context, HttpResponseMessage response)
    {
        var target = context.Response;
        var status = (int)response.StatusCode;
        target.StatusCode = status;

        // Response.statusText of the script. HTTP/2 has no reason phrase, and without one Kestrel writes the standard text.
        if (IsReasonPhrase(response.ReasonPhrase))
        {
            context.Features.GetRequiredFeature<IHttpResponseFeature>().ReasonPhrase = response.ReasonPhrase;
        }

        // The non-validated view yields values as they were set; the typed view re-serialises them (Content-Type
        // 'text/plain;charset=utf-8' would come out as 'text/plain; charset=utf-8').
        var content = response.Content;
        AppendHeaders(target.Headers, response.Headers.NonValidated);
        AppendHeaders(target.Headers, content.Headers.NonValidated);

        if (status is 204 or 205 or 304 or < 200)
        {
            return Task.CompletedTask;
        }

        var length = content.Headers.ContentLength ?? 0;
        target.ContentLength = length;
        return length > 0 && !HttpMethods.IsHead(context.Request.Method)
            ? content.CopyToAsync(target.Body, context.RequestAborted)
            : Task.CompletedTask;
    }

    private static void AppendHeaders(IHeaderDictionary target, HttpHeadersNonValidated headers)
    {
        foreach (var header in headers)
        {
            if (IsHopByHop(header.Key))
            {
                continue;
            }

            var values = header.Value;
            if (values.Count == 1)
            {
                target.Append(header.Key, values.ToString());
                continue;
            }

            // Set-Cookie and friends: one line per value.
            var lines = new string[values.Count];
            var index = 0;
            foreach (var line in values)
            {
                lines[index++] = line;
            }

            target.Append(header.Key, new StringValues(lines));
        }
    }

    // Connection-level headers belong to the hop they arrived on. Content-Length is recomputed from the buffered body.
    private static bool IsHopByHop(string name) => name.Length switch
    {
        2 => name.Equals("TE", StringComparison.OrdinalIgnoreCase),
        6 => name.Equals("Expect", StringComparison.OrdinalIgnoreCase),
        7 => name.Equals("Trailer", StringComparison.OrdinalIgnoreCase) || name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase),
        10 => name.Equals("Connection", StringComparison.OrdinalIgnoreCase) || name.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase),
        14 => name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase),
        16 => name.Equals("Proxy-Connection", StringComparison.OrdinalIgnoreCase),
        17 => name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    // The methods that the fetch handler sees all the time are not worth a new object each.
    private static HttpMethod GetMethod(string method) => method switch
    {
        "GET" => HttpMethod.Get,
        "POST" => HttpMethod.Post,
        "PUT" => HttpMethod.Put,
        "DELETE" => HttpMethod.Delete,
        "HEAD" => HttpMethod.Head,
        "OPTIONS" => HttpMethod.Options,
        "PATCH" => HttpMethod.Patch,
        _ => new HttpMethod(method),
    };

    // A script can build the text from visitor input, and it ends up in the status line: tabs, spaces and printable ASCII only.
    private static bool IsReasonPhrase(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is not ('\t' or (>= ' ' and <= '~')))
            {
                return false;
            }
        }

        return true;
    }

    private static Uri BuildUrl(HttpRequest source, IReadOnlyList<AstroAllowedDomain>? allowedDomains)
    {
        var path = GetPath(source);

        HostString host;
        if (allowedDomains is { Count: > 0 })
        {
            var forwardedProtocol = AstroHostValidator.ValidateForwardedProtocol(
                AstroHostValidator.GetFirstForwardedValue(source.Headers["X-Forwarded-Proto"]), allowedDomains);
            var trusted = AstroHostValidator.ValidateHost(source.Host.Value, forwardedProtocol ?? source.Scheme, allowedDomains);
            host = trusted is null ? GetFallbackHost(source) : new HostString(trusted);
        }
        else
        {
            host = source.Host.HasValue ? source.Host : GetFallbackHost(source);
        }

        var url = UriHelper.BuildAbsolute(source.Scheme, host, source.PathBase, path, source.QueryString);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri
            : new Uri(UriHelper.BuildAbsolute(source.Scheme, GetFallbackHost(source), source.PathBase, path, source.QueryString));
    }

    // What a request without a usable Host is taken to be addressed to.
    private static HostString GetFallbackHost(HttpRequest source) =>
        new(source.HttpContext.Connection.LocalPort is > 0 and var port ? $"localhost:{port}" : "localhost");

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

    private static async ValueTask<ByteArrayContent> ReadBodyAsync(HttpRequest source, long maxBodyBytes, CancellationToken cancellationToken)
    {
        var declared = source.ContentLength;
        if (declared > maxBodyBytes)
        {
            throw new AstroRequestBodyTooLargeException();
        }

        if (declared is 0)
        {
            return new ByteArrayContent([]);
        }

        // The usual body of a form or an API call: one array of the size that the client declared, filled straight from the connection.
        if (declared is > 0 and <= ExactBodyReadLimit)
        {
            var exact = new byte[(int)declared];
            var filled = 0;
            while (filled < exact.Length)
            {
                var read = await source.Body.ReadAsync(exact.AsMemory(filled), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                filled += read;
            }

            return new ByteArrayContent(exact, 0, filled);
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

                await body.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        // The content takes the buffer of the stream as it is; the surplus capacity stays out of the body.
        return body.TryGetBuffer(out var buffer)
            ? new ByteArrayContent(buffer.Array!, buffer.Offset, buffer.Count)
            : new ByteArrayContent(body.ToArray());
    }
}
