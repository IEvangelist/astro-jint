using System.Net;
using System.Text;
using AstroJint.AspNetCore;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AstroJint.Benchmarks;

/// <summary>Translating a request into the message Jint takes, and Jint's response message back into an HTTP response.</summary>
public class BridgeBenchmarks
{
    private const long MaxBodyBytes = 10L * 1024 * 1024;

    private ServiceProvider _services = null!;
    private IHeaderDictionary _headers = null!;
    private DefaultHttpContext _get = null!;
    private DefaultHttpContext _post = null!;
    private MemoryStream _postBody = null!;
    private byte[] _responseBody = null!;

    [Params(2_000, 20_000, 200_000)]
    public int BodySize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _services = new ServiceCollection().BuildServiceProvider();
        _headers = FakeHttp.BrowserHeaders();
        _get = FakeHttp.Create(_services, "GET", "/products/1", _headers, query: "?currency=eur");

        var body = Encoding.UTF8.GetBytes("{\"message\":\"" + new string('x', 1000) + "\"}");
        _postBody = new MemoryStream(body);
        var postHeaders = FakeHttp.BrowserHeaders();
        postHeaders.ContentType = "application/json";
        postHeaders.ContentLength = body.Length;
        _post = FakeHttp.Create(_services, "POST", "/api/echo", postHeaders, _postBody);

        _responseBody = Encoding.UTF8.GetBytes(new string('x', BodySize));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _postBody.Dispose();
        _services.Dispose();
    }

    [Benchmark]
    public async Task<int> CreateGetRequest()
    {
        using var request = await AstroHttpBridge.CreateRequestAsync(_get.Request, MaxBodyBytes, CancellationToken.None).ConfigureAwait(false);
        return request.Headers.NonValidated.Count;
    }

    [Benchmark]
    public async Task<int> CreatePostRequest()
    {
        _postBody.Position = 0;
        using var request = await AstroHttpBridge.CreateRequestAsync(_post.Request, MaxBodyBytes, CancellationToken.None).ConfigureAwait(false);
        return request.Headers.NonValidated.Count;
    }

    /// <summary>What it costs to make the response message, so that it can be subtracted from the row below.</summary>
    [Benchmark]
    public int ResponseMessageOnly()
    {
        using var message = CreateResponseMessage();
        return message.Headers.NonValidated.Count;
    }

    /// <summary>From the message that Jint returns to the response of the request.</summary>
    [Benchmark]
    public async Task<int> ResponseToHttpContext()
    {
        using var message = CreateResponseMessage();
        await AstroHttpBridge.BufferResponseAsync(message, CancellationToken.None).ConfigureAwait(false);
        var context = FakeHttp.Create(_services, "GET", "/products/1", _headers);
        await AstroHttpBridge.WriteResponseAsync(context, message).ConfigureAwait(false);
        return (int)context.Response.ContentLength.GetValueOrDefault();
    }

    private HttpResponseMessage CreateResponseMessage()
    {
        var message = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_responseBody) };
        message.Content.Headers.TryAddWithoutValidation("Content-Type", "text/html;charset=utf-8");
        message.Headers.TryAddWithoutValidation("Cache-Control", "public, max-age=60");
        message.Headers.TryAddWithoutValidation("X-Request-Id", "0HN7K4P2Q8R5S:00000001");
        message.Headers.TryAddWithoutValidation("Server-Timing", "astro;dur=1");
        message.Headers.TryAddWithoutValidation("Set-Cookie", "a=1; Path=/; HttpOnly");
        message.Headers.TryAddWithoutValidation("Set-Cookie", "b=2; Path=/");
        return message;
    }
}
