using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/aborted-request.test.ts</c>. A client announces a body of a million bytes,
/// sends the first few and goes away. The Node adapter must not report that as an unhandled rejection, and the server
/// must keep serving. The upstream test waits for the script to start reading the body, which this host does before
/// the script runs, so the test waits for the request to reach the host instead. The "output" it inspects is the log.
/// </summary>
[Trait("Upstream", "node/aborted-request.test.ts")]
public partial class AbortedRequestTests
{
    private const string Fixture = "aborted-request";

    public enum Disconnect
    {
        /// <summary>The client closes the socket, which ends the stream with a FIN.</summary>
        Close,

        /// <summary>The client aborts the connection, which resets it, as <c>socket.destroy()</c> does when data is unread.</summary>
        Reset,
    }

    [Theory(DisplayName = "does not report an interrupted request body as an unhandled rejection")]
    [InlineData(Disconnect.Close)]
    [InlineData(Disconnect.Reset)]
    public async Task InterruptedRequestBody_IsNotReportedAsAnError(Disconnect disconnect)
    {
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var site = await ConformanceSite.StartAsync(Fixture, configureApp: app => app.Use((context, next) =>
        {
            if (HttpMethods.IsPost(context.Request.Method))
            {
                arrived.TrySetResult();
            }

            return next(context);
        }));
        var logStart = site.Logs.Count;

        using (var client = new TcpClient())
        {
            await client.ConnectAsync(site.BaseAddress.Host, site.BaseAddress.Port);
            await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(
                $"POST / HTTP/1.1\r\nHost: {site.BaseAddress.Authority}\r\nContent-Type: application/json\r\nContent-Length: 1000000\r\nConnection: close\r\n\r\n{{\"partial\":"));
            await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Give the host the time to start waiting for the rest of the body.
            await Task.Delay(300);
            if (disconnect == Disconnect.Reset)
            {
                client.LingerState = new LingerOption(true, 0);
            }
        }

        await WaitForStableLogsAsync(site);

        var output = site.Logs.Skip(logStart).ToList();
        Assert.DoesNotContain(output, entry => entry.Category != "AstroJint.Console" && entry.Level >= LogLevel.Error);
        Assert.DoesNotContain(output, entry => Unreported().IsMatch($"{entry.Message} {entry.Exception}"));

        using var response = await site.GetAsync("/");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    private static async Task WaitForStableLogsAsync(ConformanceSite site)
    {
        var last = -1;
        var stable = 0;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var count = site.Logs.Count;
            if (count == last)
            {
                if (++stable >= 3)
                {
                    return;
                }
            }
            else
            {
                last = count;
                stable = 0;
            }

            await Task.Delay(200);
        }

        Assert.Fail("The log never stabilized.");
    }

    [GeneratedRegex("ECONNRESET|[Uu]nhandled")]
    private static partial Regex Unreported();
}
