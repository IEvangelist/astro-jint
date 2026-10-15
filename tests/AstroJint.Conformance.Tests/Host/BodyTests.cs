using System.Net;
using System.Security.Cryptography;

namespace AstroJint.Conformance.Host;

/// <summary>
/// How the host moves request and response bodies. The host reads a request body completely before the script runs and
/// buffers the response body the script produces; the Node adapter streams both. Upstream has no tests for this.
/// </summary>
public class BodyTests(SiteHost sites)
{
    private const string Fixture = "api-route";
    private const int Limit = 64 * 1024;

    [Fact]
    public async Task RequestBody_AtTheLimit_IsForwarded()
    {
        var site = await sites.GetAsync(Fixture, variant: "small-limit", configure: options => options.MaxRequestBodyBytes = Limit);
        var data = RandomNumberGenerator.GetBytes(Limit);

        using var response = await site.SendAsync(HttpMethod.Post, "/hash", new ByteArrayContent(data));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SHA256.HashData(data), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task RequestBody_AboveTheLimit_Gets413()
    {
        var site = await sites.GetAsync(Fixture, variant: "small-limit", configure: options => options.MaxRequestBodyBytes = Limit);

        using var response = await site.SendAsync(HttpMethod.Post, "/hash", new ByteArrayContent(new byte[Limit + 1]));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task RequestBody_WithoutALengthButAboveTheLimit_Gets413()
    {
        var site = await sites.GetAsync(Fixture, variant: "small-limit", configure: options => options.MaxRequestBodyBytes = Limit);

        using var response = await site.SendAsync(HttpMethod.Post, "/hash", new ChunkedContent(new byte[Limit + 1]));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task RequestBody_WithoutALengthAtTheLimit_IsForwarded()
    {
        var site = await sites.GetAsync(Fixture, variant: "small-limit", configure: options => options.MaxRequestBodyBytes = Limit);
        var data = RandomNumberGenerator.GetBytes(Limit);

        using var response = await site.SendAsync(HttpMethod.Post, "/hash", new ChunkedContent(data));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SHA256.HashData(data), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task EmptyFormBody_KeepsItsContentType()
    {
        var site = await sites.GetAsync(Fixture);
        using var content = new ByteArrayContent([]);
        content.Headers.ContentType = new("application/x-www-form-urlencoded");

        using var response = await site.SendAsync(HttpMethod.Post, "/empty-form", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"contentType":"application/x-www-form-urlencoded","fields":[]}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StreamedResponse_IsDeliveredInFull()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/streaming?delay=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("hello\nhello\nhello\n", await response.Content.ReadAsStringAsync());
    }

    /// <summary>A body without a known length, which <see cref="HttpClient"/> sends with <c>Transfer-Encoding: chunked</c>.</summary>
    private sealed class ChunkedContent(byte[] data) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(data).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
