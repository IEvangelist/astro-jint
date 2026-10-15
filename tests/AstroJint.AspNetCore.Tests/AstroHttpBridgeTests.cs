using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
    public async Task CreateRequest_ReadsABackslashThatTheClientSentAsASlash()
    {
        var context = CreateContext();
        context.Request.Path = "/\\example.com/press";
        context.Request.QueryString = new QueryString("?x=1");
        context.Features.GetRequiredFeature<IHttpRequestFeature>().RawTarget = "/\\example.com/press?x=1";

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal("//example.com/press", request.RequestUri!.AbsolutePath);
        Assert.Equal("?x=1", request.RequestUri.Query);
    }

    [Fact]
    public async Task CreateRequest_KeepsABackslashThatTheClientSentEncoded()
    {
        var context = CreateContext();
        context.Request.Path = "/a\\b";
        context.Features.GetRequiredFeature<IHttpRequestFeature>().RawTarget = "/a%5Cb";

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal("/a%5Cb", request.RequestUri!.AbsolutePath);
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
    public async Task CreateRequest_UsesLocalhostAndTheServerPortWhenThereIsNoHost()
    {
        var context = CreateContext();
        context.Request.Host = default;
        context.Request.Scheme = "http";
        context.Connection.LocalPort = 5000;

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal("http://localhost:5000/", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task CreateRequest_FallsBackToLocalhostWhenTheHostMakesNoUrl()
    {
        var context = CreateContext();
        context.Request.Host = new HostString("exam ple.test");
        context.Connection.LocalPort = 5000;

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal("https://localhost:5000/", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task CreateRequest_TrustsTheHostThatAspNetCoreResolvedWhenThereAreNoAllowedDomains()
    {
        var context = CreateContext();
        context.Request.Host = new HostString("anything.test", 9000);
        context.Request.Headers["X-Forwarded-Host"] = "ignored.test";

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None, []);

        Assert.Equal("https://anything.test:9000/", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task CreateRequest_UsesAHostThatMatchesAnAllowedDomain()
    {
        var context = CreateContext();
        context.Request.Host = new HostString("legitimate.example.com");
        context.Connection.LocalPort = 5000;

        using var request = await AstroHttpBridge.CreateRequestAsync(
            context.Request, 1024, CancellationToken.None, [new() { Hostname = "legitimate.example.com" }]);

        Assert.Equal("https://legitimate.example.com/", request.RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("attacker.example.net")]
    [InlineData("legitimate.example.com/admin")]
    [InlineData("legitimate.example.com:1:2")]
    public async Task CreateRequest_ReplacesAHostThatMatchesNoAllowedDomainWithLocalhostAndTheServerPort(string host)
    {
        var context = CreateContext();
        context.Request.Host = new HostString(host);
        context.Request.Path = "/page";
        context.Connection.LocalPort = 5000;

        using var request = await AstroHttpBridge.CreateRequestAsync(
            context.Request, 1024, CancellationToken.None, [new() { Hostname = "legitimate.example.com" }]);

        Assert.Equal("https://localhost:5000/page", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task CreateRequest_ReplacesAHostThatMatchesNoAllowedDomainWithLocalhostWhenTheServerPortIsUnknown()
    {
        var context = CreateContext();
        context.Request.Host = new HostString("attacker.example.net");

        using var request = await AstroHttpBridge.CreateRequestAsync(
            context.Request, 1024, CancellationToken.None, [new() { Hostname = "legitimate.example.com" }]);

        Assert.Equal("https://localhost/", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task CreateRequest_ChecksTheHostAgainstTheForwardedProtocolOfAProtocolSpecificAllowedDomain()
    {
        AstroAllowedDomain[] allowed = [new() { Hostname = "example.test", Protocol = "https" }];

        var behindProxy = CreateContext();
        behindProxy.Request.Scheme = "http";
        behindProxy.Request.Host = new HostString("example.test");
        behindProxy.Request.Headers["X-Forwarded-Proto"] = "https, http";
        using var trusted = await AstroHttpBridge.CreateRequestAsync(behindProxy.Request, 1024, CancellationToken.None, allowed);

        var direct = CreateContext();
        direct.Request.Scheme = "http";
        direct.Request.Host = new HostString("example.test");
        using var untrusted = await AstroHttpBridge.CreateRequestAsync(direct.Request, 1024, CancellationToken.None, allowed);

        Assert.Equal("http://example.test/", trusted.RequestUri!.AbsoluteUri);
        Assert.Equal("http://localhost/", untrusted.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task CreateRequest_PassesForwardedHeadersOnToAstro()
    {
        var context = CreateContext();
        context.Request.Headers["X-Forwarded-Host"] = "abc.xyz:444";
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        using var request = await AstroHttpBridge.CreateRequestAsync(
            context.Request, 1024, CancellationToken.None, [new() { Hostname = "abc.xyz", Port = "444" }]);

        Assert.Equal("abc.xyz:444", Assert.Single(request.Headers.GetValues("X-Forwarded-Host")));
        Assert.Equal("https", Assert.Single(request.Headers.GetValues("X-Forwarded-Proto")));
    }

    [Fact]
    public async Task CreateRequest_CopiesHeaders()
    {
        var context = CreateContext();
        context.Request.Headers.Accept = "text/html";
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

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task CreateRequest_WithAnEmptyBody_HasEmptyContentThatKeepsTheContentHeaders(string method)
    {
        var context = CreateContext(method, []);
        context.Request.ContentType = "application/x-www-form-urlencoded";

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.NotNull(request.Content);
        Assert.Empty(await request.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/x-www-form-urlencoded", request.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task CreateRequest_AcceptsABodyOfExactlyTheLimit()
    {
        var context = CreateContext("PUT", new byte[10]);

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 10, CancellationToken.None);

        Assert.Equal(10, (await request.Content!.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task CreateRequest_ReadsABodyThatArrivesInParts()
    {
        var context = CreateContext("POST", "a body in many parts"u8.ToArray());
        context.Request.Body = new TrickleStream("a body in many parts"u8.ToArray());

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal("a body in many parts", await request.Content!.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateRequest_KeepsWhatArrivedOfABodyThatEndsBeforeItsDeclaredLength()
    {
        var context = CreateContext("POST", "four"u8.ToArray());
        context.Request.ContentLength = 10;

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal("four", await request.Content!.ReadAsStringAsync());
        Assert.Equal(4, request.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task CreateRequest_ReadsABodyWithoutADeclaredLength()
    {
        var context = CreateContext("POST", "chunked body"u8.ToArray());
        context.Request.ContentLength = null;

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal("chunked body", await request.Content!.ReadAsStringAsync());
        Assert.Equal(12, request.Content.Headers.ContentLength);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateRequest_ReadsALargeBodyWhetherOrNotItsLengthIsDeclared(bool declared)
    {
        var body = new byte[150_000];
        for (var index = 0; index < body.Length; index++)
        {
            body[index] = (byte)(index % 251);
        }

        var context = CreateContext("POST", body);
        if (!declared)
        {
            context.Request.ContentLength = null;
        }

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1_000_000, CancellationToken.None);

        Assert.Equal(body, await request.Content!.ReadAsByteArrayAsync());
        Assert.Equal(body.Length, request.Content.Headers.ContentLength);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("PATCH")]
    [InlineData("PURGE")]
    public async Task CreateRequest_CarriesTheMethod(string method)
    {
        var context = CreateContext(method);

        using var request = await AstroHttpBridge.CreateRequestAsync(context.Request, 1024, CancellationToken.None);

        Assert.Equal(method, request.Method.Method);
    }

    [Fact]
    public async Task CreateRequest_SharesTheInstancesOfTheCommonMethods()
    {
        using var get = await AstroHttpBridge.CreateRequestAsync(CreateContext().Request, 1024, CancellationToken.None);
        using var post = await AstroHttpBridge.CreateRequestAsync(CreateContext("POST", []).Request, 1024, CancellationToken.None);

        Assert.Same(HttpMethod.Get, get.Method);
        Assert.Same(HttpMethod.Post, post.Method);
    }

    [Fact]
    public async Task CreateRequest_RejectsADeclaredBodyOverTheLimitWithoutReadingIt()
    {
        var context = CreateContext("POST", new byte[11]);

        await Assert.ThrowsAsync<AstroRequestBodyTooLargeException>(async () =>
            await AstroHttpBridge.CreateRequestAsync(context.Request, 10, CancellationToken.None));
        Assert.Equal(0, context.Request.Body.Position);
    }

    [Fact]
    public async Task CreateRequest_RejectsAnUndeclaredBodyOverTheLimit()
    {
        var context = CreateContext("POST", new byte[40_000]);
        context.Request.ContentLength = null;

        await Assert.ThrowsAsync<AstroRequestBodyTooLargeException>(async () =>
            await AstroHttpBridge.CreateRequestAsync(context.Request, 20_000, CancellationToken.None));
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode status, string body = "")
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)) };
        return response;
    }

    [Fact]
    public async Task BufferResponse_LeavesTheContentThatJintHandsOverAsItIs()
    {
        using var response = CreateResponse(HttpStatusCode.OK, "hello");
        var content = response.Content;

        await AstroHttpBridge.BufferResponseAsync(response, CancellationToken.None);

        Assert.Same(content, response.Content);
    }

    [Fact]
    public async Task BufferResponse_LoadsContentThatIsProducedWhileItIsRead()
    {
        var source = new LazyContent("late body");
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = source };

        await AstroHttpBridge.BufferResponseAsync(response, CancellationToken.None);

        Assert.Equal(1, source.Serializations);
        Assert.Equal("late body", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, source.Serializations);
    }

    [Fact]
    public async Task WriteResponse_WritesStatusHeadersLengthAndBody()
    {
        var context = CreateContext();
        using var response = CreateResponse(HttpStatusCode.Created, "hello");
        response.Content.Headers.TryAddWithoutValidation("Content-Type", "text/plain");
        response.Headers.TryAddWithoutValidation("Set-Cookie", "a=1; Path=/");
        response.Headers.TryAddWithoutValidation("Set-Cookie", "b=2; Path=/");
        response.Headers.TryAddWithoutValidation("X-Custom", "yes");

        await AstroHttpBridge.WriteResponseAsync(context, response);

        Assert.Equal(201, context.Response.StatusCode);
        Assert.Equal(5, context.Response.ContentLength);
        Assert.Equal("text/plain", context.Response.Headers.ContentType.ToString());
        Assert.Equal(["a=1; Path=/", "b=2; Path=/"], context.Response.Headers.SetCookie.ToArray());
        Assert.Equal("yes", context.Response.Headers["X-Custom"].ToString());
        Assert.Equal("hello", ReadBody(context));
    }

    [Fact]
    public async Task WriteResponse_KeepsHeaderValuesExactlyAsTheScriptSetThem()
    {
        var context = CreateContext();
        using var response = CreateResponse(HttpStatusCode.OK);
        response.Content.Headers.TryAddWithoutValidation("Content-Type", "text/plain;charset=utf-8");

        await AstroHttpBridge.WriteResponseAsync(context, response);

        Assert.Equal("text/plain;charset=utf-8", context.Response.Headers.ContentType.ToString());
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
    public async Task WriteResponse_DropsConnectionLevelHeaders(string name)
    {
        var context = CreateContext();
        using var response = CreateResponse(HttpStatusCode.OK, "abc");
        Assert.True(response.Headers.TryAddWithoutValidation(name, "value"));

        await AstroHttpBridge.WriteResponseAsync(context, response);

        Assert.False(context.Response.Headers.ContainsKey(name));
        Assert.Equal(3, context.Response.ContentLength);
        Assert.Equal("abc", ReadBody(context));
    }

    [Fact]
    public async Task WriteResponse_WritesTheReasonPhrase()
    {
        var context = CreateContext();
        using var response = CreateResponse(HttpStatusCode.OK);
        response.ReasonPhrase = "tasty \tfine";

        await AstroHttpBridge.WriteResponseAsync(context, response);

        Assert.Equal("tasty \tfine", context.Features.GetRequiredFeature<IHttpResponseFeature>().ReasonPhrase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("caf\u00e9")]
    public async Task WriteResponse_LeavesTheStandardPhraseWhenTheTextIsNotAReasonPhrase(string reasonPhrase)
    {
        var context = CreateContext();
        using var response = CreateResponse(HttpStatusCode.OK);
        response.ReasonPhrase = reasonPhrase;

        await AstroHttpBridge.WriteResponseAsync(context, response);

        Assert.Null(context.Features.GetRequiredFeature<IHttpResponseFeature>().ReasonPhrase);
    }

    [Fact]
    public async Task WriteResponse_ToAHeadRequest_SendsTheLengthButNoBody()
    {
        var context = CreateContext("HEAD");
        using var response = CreateResponse(HttpStatusCode.OK, "hello");

        await AstroHttpBridge.WriteResponseAsync(context, response);

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
        using var response = CreateResponse((HttpStatusCode)status, "ignored");
        response.Headers.TryAddWithoutValidation("ETag", "\"x\"");

        await AstroHttpBridge.WriteResponseAsync(context, response);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Null(context.Response.ContentLength);
        Assert.Equal("\"x\"", context.Response.Headers.ETag.ToString());
        Assert.Equal("", ReadBody(context));
    }

    [Fact]
    public async Task WriteResponse_WithAnEmptyBody_SendsAZeroLength()
    {
        var context = CreateContext();
        using var response = CreateResponse(HttpStatusCode.NotFound);

        await AstroHttpBridge.WriteResponseAsync(context, response);

        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal(0, context.Response.ContentLength);
    }

    [Fact]
    public async Task WriteResponse_WithoutContent_SendsAZeroLength()
    {
        var context = CreateContext();
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);

        await AstroHttpBridge.BufferResponseAsync(response, CancellationToken.None);
        await AstroHttpBridge.WriteResponseAsync(context, response);

        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal(0, context.Response.ContentLength);
        Assert.Equal("", ReadBody(context));
    }

    [Fact]
    public async Task WriteResponse_WritesContentThatWasBufferedFirst()
    {
        var context = CreateContext();
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new LazyContent("late body") };

        await AstroHttpBridge.BufferResponseAsync(response, CancellationToken.None);
        await AstroHttpBridge.WriteResponseAsync(context, response);

        Assert.Equal(9, context.Response.ContentLength);
        Assert.Equal("late body", ReadBody(context));
    }

    // Reads three bytes at the most per call, as a connection that delivers a body in parts does.
    private sealed class TrickleStream(byte[] data) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 3)], cancellationToken);
    }

    // Content whose length is not known up front and that is produced when it is read.
    private sealed class LazyContent(string text) : HttpContent
    {
        public int Serializations { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            Serializations++;
            return stream.WriteAsync(Encoding.UTF8.GetBytes(text)).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
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
