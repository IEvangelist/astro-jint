using System.Buffers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
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

    public static async Task<HttpRequestMessage> CreateRequestAsync(HttpRequest source, long maxBodyBytes, CancellationToken cancellationToken)
    {
        var host = source.Host.HasValue ? source.Host : new HostString("localhost");
        var url = UriHelper.BuildAbsolute(source.Scheme, host, source.PathBase, source.Path, source.QueryString);
        var request = new HttpRequestMessage(new HttpMethod(source.Method), url);

        try
        {
            if (!HttpMethods.IsGet(source.Method) && !HttpMethods.IsHead(source.Method))
            {
                var body = await ReadBodyAsync(source, maxBodyBytes, cancellationToken).ConfigureAwait(false);
                if (body.Length > 0)
                {
                    request.Content = new ByteArrayContent(body);
                }
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
        foreach (var header in response.Headers.Concat(response.Content.Headers))
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
