using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace AstroJint.AspNetCore.Tests;

public class AstroHttpBridgeTests
{
    private static DefaultHttpContext CreateContext(string method = "GET", byte[]? body = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.test");
        context.Request.Path = "/";
        context.Response.Body = new MemoryStream();

        if (body is not null)
        {
            context.Request.Body = new MemoryStream(body);
            context.Request.ContentLength = body.Length;
        }

        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEnd();
    }

    [Fact]
    public async Task CreateRequest_ComposesTheUrlFromSchemeHostPathBaseAndQuery()
    {
        var context = CreateContext();
        context.Request.Host = new HostString("example.test", 8443);
        context.Request.PathBase = "/app";
        context.Request.Path = "/products/a b";
        context.Request.QueryString = new QueryString("?x=1&y=two%20words");

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://example.test:8443/app/products/a%20b?x=1&y=two%20words", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task CreateRequest_UsesLocalhostWhenThereIsNoHost()
    {
        var context = CreateContext();
        context.Request.Host = default;
        context.Request.Scheme = "http";

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal("http://localhost/", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task CreateRequest_CopiesHeaders()
    {
        var context = CreateContext();
        context.Request.Headers["Accept"] = "text/html";
        context.Request.Headers["X-Custom"] = new StringValues(["a", "b"]);

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal("text/html", Assert.Single(request.Headers.GetValues("Accept")));
        Assert.Equal("a,b", Assert.Single(request.Headers.GetValues("X-Custom")));
    }

    [Fact]
    public async Task CreateRequest_JoinsCookieLines()
    {
        var context = CreateContext();
        context.Request.Headers.Cookie = new StringValues(["a=1", "b=2"]);

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal("a=1; b=2", Assert.Single(request.Headers.GetValues("Cookie")));
    }

    [Theory]
    [InlineData("Connection")]
    [InlineData("Keep-Alive")]
    [InlineData("Proxy-Connection")]
    [InlineData("TE")]
    [InlineData("Trailer")]
    [InlineData("Transfer-Encoding")]
    [InlineData("Upgrade")]
    [InlineData("Expect")]
    [InlineData("Content-Length")]
    public async Task CreateRequest_SkipsConnectionLevelHeaders(string name)
    {
        var context = CreateContext("POST", "hello"u8.ToArray());
        context.Request.Headers[name] = "value";

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.False(request.Headers.NonValidated.Contains(name));
        Assert.False(request.Content!.Headers.NonValidated.Contains(name));
    }

    [Fact]
    public async Task CreateRequest_ReadsTheBodyAndKeepsContentHeaders()
    {
        var context = CreateContext("POST", "name=Ada&role=admin"u8.ToArray());
        context.Request.ContentType = "application/x-www-form-urlencoded";

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("application/x-www-form-urlencoded", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("name=Ada&role=admin", await request.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task CreateRequest_DoesNotReadTheBodyOfGetAndHead(string method)
    {
        var context = CreateContext(method, "ignored"u8.ToArray());

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Null(request.Content);
        Assert.Equal(0, context.Request.Body.Position);
    }

    [Fact]
    public async Task CreateRequest_WithAnEmptyBody_HasNoContent()
    {
        var context = CreateContext("POST", []);

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Null(request.Content);
    }

    [Fact]
    public async Task CreateRequest_AcceptsABodyOfExactlyTheLimit()
    {
        var context = CreateContext("PUT", new byte[10]);

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 10, CancellationToken.None);

        Assert.Equal(10, (await request.Content!.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task CreateRequest_RejectsADeclaredBodyOverTheLimitWithoutReadingIt()
    {
        var context = CreateContext("POST", new byte[11]);

        await Assert.ThrowsAsync<AstroRequestBodyTooLargeException>(() =>
            AstroHttpBridge.CreateRequestAsync(context.Request, 10, CancellationToken.None));
        Assert.Equal(0, context.Request.Body.Position);
    }

    [Fact]
    public async Task CreateRequest_RejectsAnUndeclaredBodyOverTheLimit()
    {
        var context = CreateContext("POST", new byte[40_000]);
        context.Request.ContentLength = null;

        await Assert.ThrowsAsync<AstroRequestBodyTooLargeException>(() =>
            AstroHttpBridge.CreateRequestAsync(context.Request, 20_000, CancellationToken.None));
    }

    [Fact]
    public async Task ReadResponse_CopiesStatusHeadersAndBody()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("<h1>Hi</h1>", Encoding.UTF8, "text/html"),
        };
        response.Headers.TryAddWithoutValidation("Set-Cookie", "a=1; Path=/");
        response.Headers.TryAddWithoutValidation("Set-Cookie", "b=2; Path=/");
        response.Headers.TryAddWithoutValidation("X-Custom", "yes");

        var result = await AstroHttpBridge.ReadResponseAsync(response, CancellationToken.None);

        Assert.Equal(201, result.StatusCode);
        Assert.Equal("<h1>Hi</h1>", Encoding.UTF8.GetString(result.Body));
        Assert.Equal(["a=1; Path=/", "b=2; Path=/"], result.Headers.Single(h => h.Key == "Set-Cookie").Value.ToArray());
        Assert.Equal("yes", result.Headers.Single(h => h.Key == "X-Custom").Value.ToString());
        Assert.Equal("text/html; charset=utf-8", result.Headers.Single(h => h.Key == "Content-Type").Value.ToString());
    }

    [Fact]
    public async Task ReadResponse_DropsConnectionLevelHeaders()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        response.Headers.TransferEncodingChunked = true;
        response.Headers.ConnectionClose = true;

        var result = await AstroHttpBridge.ReadResponseAsync(response, CancellationToken.None);

        Assert.DoesNotContain(result.Headers, h => h.Key is "Transfer-Encoding" or "Connection" or "Content-Length");
        Assert.Equal([1, 2, 3], result.Body);
    }

    [Fact]
    public async Task WriteResponse_WritesStatusHeadersLengthAndBody()
    {
        var context = CreateContext();
        var response = new AstroResponse(
            201,
            [
                new("Content-Type", "text/plain"),
                new("Set-Cookie", new StringValues(["a=1", "b=2"])),
            ],
            "hello"u8.ToArray());

        await AstroHttpBridge.WriteResponseAsync(context, response);

        Assert.Equal(201, context.Response.StatusCode);
        Assert.Equal(5, context.Response.ContentLength);
        Assert.Equal("text/plain", context.Response.Headers.ContentType.ToString());
        Assert.Equal(["a=1", "b=2"], context.Response.Headers.SetCookie.ToArray());
        Assert.Equal("hello", ReadBody(context));
    }

    [Fact]
    public async Task WriteResponse_ToAHeadRequest_SendsTheLengthButNoBody()
    {
        var context = CreateContext("HEAD");

        await AstroHttpBridge.WriteResponseAsync(context, new AstroResponse(200, [], "hello"u8.ToArray()));

        Assert.Equal(5, context.Response.ContentLength);
        Assert.Equal("", ReadBody(context));
    }

    [Theory]
    [InlineData(204)]
    [InlineData(205)]
    [InlineData(304)]
    [InlineData(101)]
    public async Task WriteResponse_SendsNoBodyOrLengthForBodylessStatuses(int status)
    {
        var context = CreateContext();

        await AstroHttpBridge.WriteResponseAsync(context, new AstroResponse(status, [new("ETag", "\"x\"")], "ignored"u8.ToArray()));

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Null(context.Response.ContentLength);
        Assert.Equal("\"x\"", context.Response.Headers.ETag.ToString());
        Assert.Equal("", ReadBody(context));
    }

    [Fact]
    public async Task WriteResponse_WithAnEmptyBody_SendsAZeroLength()
    {
        var context = CreateContext();

        await AstroHttpBridge.WriteResponseAsync(context, new AstroResponse(404, [], []));

        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal(0, context.Response.ContentLength);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("10.0.0.5", "10.0.0.5")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    [InlineData("::1", "::1")]
    public void GetClientAddress_ReturnsTheRemoteAddress(string? remote, string? expected)
    {
        var context = CreateContext();
        context.Connection.RemoteIpAddress = remote is null ? null : IPAddress.Parse(remote);

        Assert.Equal(expected, AstroHttpBridge.GetClientAddress(context));
    }
}
