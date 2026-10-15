using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace AstroJint.Conformance;

/// <summary>A response read from a plain socket.</summary>
public sealed record RawResponse(int StatusCode, IReadOnlyList<KeyValuePair<string, string>> Headers, byte[] Body)
{
    public string Text => Encoding.UTF8.GetString(Body);

    /// <summary>The first value of the header, or <see langword="null"/>.</summary>
    public string? Header(string name)
    {
        var values = HeaderValues(name);
        return values.Count > 0 ? values[0] : null;
    }

    public IReadOnlyList<string> HeaderValues(string name) =>
        [.. Headers.Where(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(header => header.Value)];
}

/// <summary>
/// Sends requests over a plain socket. <see cref="HttpClient"/> rewrites some request targets before they reach the wire
/// (backslashes, invalid escapes, dot segments), and some tests send exactly those on purpose.
/// </summary>
internal static class RawHttp
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Sends <paramref name="request"/> as is and reads the response until the server closes the connection.</summary>
    public static async Task<RawResponse> SendAsync(Uri address, byte[] request)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        using var client = new TcpClient();
        await client.ConnectAsync(address.Host, address.Port, timeout.Token);

        var stream = client.GetStream();
        await stream.WriteAsync(request, timeout.Token);
        await stream.FlushAsync(timeout.Token);

        using var received = new MemoryStream();
        await stream.CopyToAsync(received, timeout.Token);
        return Parse(received.ToArray());
    }

    public static byte[] BuildRequest(Uri address, string method, string target, IEnumerable<(string Name, string Value)> headers, byte[]? body)
    {
        var sent = headers.ToList();
        var head = new StringBuilder()
            .Append(method).Append(' ').Append(target).Append(" HTTP/1.1\r\n");

        // A test may send a Host of its own, which then stands in for the address of the socket.
        if (!sent.Any(header => header.Name.Equals("Host", StringComparison.OrdinalIgnoreCase)))
        {
            head.Append("Host: ").Append(address.Authority).Append("\r\n");
        }

        head.Append("Connection: close\r\n");
        foreach (var (name, value) in sent)
        {
            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }

        if (body is not null)
        {
            head.Append("Content-Length: ").Append(body.Length).Append("\r\n");
        }

        head.Append("\r\n");
        return [.. Encoding.UTF8.GetBytes(head.ToString()), .. body ?? []];
    }

    private static RawResponse Parse(byte[] bytes)
    {
        var end = bytes.AsSpan().IndexOf("\r\n\r\n"u8);
        if (end < 0)
        {
            throw new InvalidOperationException($"The response has no header section: '{Encoding.UTF8.GetString(bytes)}'.");
        }

        var lines = Encoding.UTF8.GetString(bytes, 0, end).Split("\r\n");
        var statusCode = int.Parse(lines[0].Split(' ', 3)[1], CultureInfo.InvariantCulture);
        var headers = new List<KeyValuePair<string, string>>();
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            headers.Add(KeyValuePair.Create(line[..colon], line[(colon + 1)..].Trim()));
        }

        var body = bytes.AsSpan(end + 4).ToArray();
        var chunked = headers.Any(header =>
            header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) && header.Value.Contains("chunked", StringComparison.OrdinalIgnoreCase));

        return new RawResponse(statusCode, headers, chunked ? DecodeChunked(body) : body);
    }

    private static byte[] DecodeChunked(byte[] body)
    {
        using var decoded = new MemoryStream();
        var position = 0;
        while (position < body.Length)
        {
            var lineEnd = body.AsSpan(position).IndexOf("\r\n"u8);
            var size = Convert.ToInt32(Encoding.ASCII.GetString(body, position, lineEnd).Split(';')[0], 16);
            position += lineEnd + 2;
            if (size == 0)
            {
                break;
            }

            decoded.Write(body, position, size);
            position += size + 2;
        }

        return decoded.ToArray();
    }
}
