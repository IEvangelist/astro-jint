using System.Buffers.Text;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AstroJint.LoadTest;

/// <summary>
/// A keep-alive HTTP/1.1 connection that sends a prepared request and reads the response up to its last byte. It is not a
/// client: it sizes the body by Content-Length and drops it, so that the cost of the generator stays far below the server's.
/// </summary>
internal sealed class HttpConnection(EndPoint endPoint) : IDisposable
{
    private readonly byte[] _buffer = new byte[64 * 1024];
    private Socket? _socket;

    /// <summary>Sends the request. The status is 0 when the exchange failed, and the next request opens a new connection.</summary>
    public async ValueTask<(int Status, int Bytes)> SendAsync(ReadOnlyMemory<byte> request)
    {
        try
        {
            var socket = _socket ?? await ConnectAsync().ConfigureAwait(false);
            await socket.SendAsync(request, SocketFlags.None).ConfigureAwait(false);
            var (status, bytes, keepAlive) = await ReadResponseAsync(socket).ConfigureAwait(false);
            if (!keepAlive)
            {
                Close();
            }

            return (status, bytes);
        }
        catch (Exception ex) when (ex is SocketException or IOException or InvalidDataException or ObjectDisposedException)
        {
            Close();
            return (0, 0);
        }
    }

    public void Dispose() => Close();

    private async ValueTask<Socket> ConnectAsync()
    {
        var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(endPoint).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        _socket = socket;
        return socket;
    }

    private void Close()
    {
        _socket?.Dispose();
        _socket = null;
    }

    private async ValueTask<(int Status, int Bytes, bool KeepAlive)> ReadResponseAsync(Socket socket)
    {
        var count = 0;
        int headerEnd;
        while ((headerEnd = _buffer.AsSpan(0, count).IndexOf("\r\n\r\n"u8)) < 0)
        {
            if (count == _buffer.Length)
            {
                throw new InvalidDataException("The headers of the response do not fit the buffer.");
            }

            count += await ReceiveAsync(socket, _buffer.AsMemory(count)).ConfigureAwait(false);
        }

        headerEnd += 4;
        var headers = _buffer.AsSpan(0, headerEnd);
        var status = ParseStatus(headers);

        long bodyLength = 0;
        if (status is not (204 or 304 or < 200))
        {
            if (!TryGetHeader(headers, "Content-Length"u8, out var length) || !Utf8Parser.TryParse(length, out bodyLength, out _))
            {
                throw new InvalidDataException("The response has no Content-Length. The generator does not read chunked bodies.");
            }
        }

        var keepAlive = !(TryGetHeader(headers, "Connection"u8, out var connection) && connection.SequenceEqual("close"u8));
        var total = headerEnd + bodyLength;
        var received = (long)count;
        while (received < total)
        {
            // The body is dropped as it arrives, and nothing beyond this response is read.
            var chunk = _buffer.AsMemory(0, (int)Math.Min(_buffer.Length, total - received));
            received += await ReceiveAsync(socket, chunk).ConfigureAwait(false);
        }

        return (status, (int)total, keepAlive);
    }

    private static async ValueTask<int> ReceiveAsync(Socket socket, Memory<byte> destination)
    {
        var read = await socket.ReceiveAsync(destination, SocketFlags.None).ConfigureAwait(false);
        return read == 0 ? throw new IOException("The server closed the connection.") : read;
    }

    private static int ParseStatus(ReadOnlySpan<byte> headers) =>
        headers.Length > 12 && Utf8Parser.TryParse(headers.Slice(9, 3), out int status, out _)
            ? status
            : throw new InvalidDataException("The response does not start with a status line.");

    private static bool TryGetHeader(ReadOnlySpan<byte> headers, ReadOnlySpan<byte> name, out ReadOnlySpan<byte> value)
    {
        var rest = headers;
        while (true)
        {
            var end = rest.IndexOf("\r\n"u8);
            if (end <= 0)
            {
                value = default;
                return false;
            }

            var line = rest[..end];
            rest = rest[(end + 2)..];
            var colon = line.IndexOf((byte)':');
            if (colon == name.Length && Ascii.EqualsIgnoreCase(line[..colon], name))
            {
                value = line[(colon + 1)..].Trim((byte)' ');
                return true;
            }
        }
    }
}
