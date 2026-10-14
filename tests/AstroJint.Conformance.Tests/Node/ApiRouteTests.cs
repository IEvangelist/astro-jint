using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/api-route.test.ts</c>. Two upstream tests rely on the streaming of the Node
/// adapter and are adapted: the request body of <c>/hash</c> is handed to the script whole, so it is scaled down to fit
/// <c>AstroOptions.MaxRequestBodyBytes</c> instead of 256 MiB in 256 KiB chunks; and a response that is still streaming
/// when the client leaves is cut short by cancelling the render, which frees the engine, rather than by calling the
/// <c>cancel()</c> of the stream (a script cannot observe it, as the engine is thrown away).
/// </summary>
[Trait("Upstream", "node/api-route.test.ts")]
public class ApiRouteTests(SiteHost sites)
{
    private const string Fixture = "api-route";

    [Fact(DisplayName = "Can get the request body")]
    public async Task Endpoint_CanReadTheRequestBody()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.SendAsync(HttpMethod.Post, "/recipes", new StringContent("""{"id":2}""", Encoding.UTF8));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetArrayLength());
        Assert.Equal("Broccoli Soup", json.RootElement[0].GetProperty("name").GetString());
    }

    [Fact(DisplayName = "Can get binary data")]
    public async Task Endpoint_CanReadAndWriteBinaryData()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.SendAsync(HttpMethod.Post, "/binary", new ByteArrayContent([1, 2, 3, 4, 5]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([5, 4, 3, 2, 1], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact(DisplayName = "Can post large binary data")]
    public async Task Endpoint_CanReadLargeBinaryData()
    {
        var site = await sites.GetAsync(Fixture);
        var data = new byte[8 * 1024 * 1024];
        Random.Shared.NextBytes(data);

        using var response = await site.SendAsync(HttpMethod.Post, "/hash", new ByteArrayContent(data));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SHA256.HashData(data), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact(DisplayName = "Can bail on streaming")]
    public async Task Streaming_WhenTheClientLeaves_FreesTheEngine()
    {
        // With a single engine, a render that kept running would block every later request until it ended.
        var site = await sites.GetAsync(Fixture, variant: "one-engine", configure: options =>
        {
            options.PoolSize = 1;
            options.RequestTimeout = Timeout.InfiniteTimeSpan;
        });

        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => site.Client.GetAsync("/streaming?delay=60000", cancellation.Token));
        }

        var stopwatch = Stopwatch.StartNew();
        using var response = await site.GetAsync("/redirect");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"The engine was held for {stopwatch.Elapsed} after the client had left.");
        Assert.DoesNotContain(site.Logs, entry => entry.Level >= Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact(DisplayName = "Can respond with SSR redirect")]
    public async Task Endpoint_CanRedirect()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/redirect");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(["/destination"], response.HeaderValues("Location"));
    }

    [Fact(DisplayName = "Can respond with Astro.redirect")]
    public async Task Page_CanRedirect()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/astro-redirect");

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal(["/destination"], response.HeaderValues("Location"));
    }

    [Fact(DisplayName = "Can respond with Response.redirect")]
    public async Task Endpoint_CanRedirectWithTheResponseApi()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/response-redirect");

        // The host the request was addressed to is not one of the allowed domains, so Astro.url has the fallback host.
        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal([$"http://localhost:{site.BaseAddress.Port}/destination"], response.HeaderValues("Location"));
    }
}
