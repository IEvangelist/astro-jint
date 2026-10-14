using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AstroJint.AspNetCore.Tests;

public class AstroHostTests
{
    private static int Handled(JsonElement json) => json.GetProperty("handled").GetInt32();

    [Fact]
    public async Task Echo_RoundTripsMethodUrlHeadersAndBody()
    {
        await using var site = await TestSite.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/echo?x=1&q=a%20b")
        {
            Content = new StringContent("héllo ✓", Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("X-Test", "yes");

        using var response = await site.Client.SendAsync(request);
        var echo = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var headers = echo.GetProperty("headers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("POST", echo.GetProperty("method").GetString());
        Assert.Equal("http://localhost/echo?x=1&q=a%20b", echo.GetProperty("url").GetString());
        Assert.Equal("yes", headers.GetProperty("x-test").GetString());
        Assert.StartsWith("text/plain", headers.GetProperty("content-type").GetString());
        Assert.Equal("héllo ✓", echo.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Echo_JoinsSeparateCookieLinesIntoOneHeader()
    {
        await using var site = await TestSite.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/echo");
        request.Headers.TryAddWithoutValidation("Cookie", "a=1");
        request.Headers.TryAddWithoutValidation("Cookie", "b=2");

        using var response = await site.Client.SendAsync(request);
        var echo = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("a=1; b=2", echo.GetProperty("headers").GetProperty("cookie").GetString());
    }

    [Fact]
    public async Task Response_KeepsEverySetCookieLine()
    {
        await using var site = await TestSite.StartAsync();
        using var response = await site.Client.GetAsync("/cookies");

        Assert.Equal(["a=1; Path=/; HttpOnly", "b=2; Path=/; Secure"], response.Headers.GetValues("Set-Cookie"));
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Response_PassesRedirectsThrough()
    {
        await using var site = await TestSite.StartAsync();
        using var response = await site.Client.GetAsync("/redirect");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/target", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData(204)]
    [InlineData(304)]
    public async Task Response_HasNoBodyOrContentLengthForBodylessStatuses(int status)
    {
        await using var site = await TestSite.StartAsync();
        using var response = await site.Client.GetAsync($"/status?code={status}");

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.False(response.Content.Headers.Contains("Content-Length"));
    }

    [Fact]
    public async Task Response_PassesErrorStatusAndBodyThrough()
    {
        await using var site = await TestSite.StartAsync();
        using var response = await site.Client.GetAsync("/status?code=418&body=teapot");

        Assert.Equal(418, (int)response.StatusCode);
        Assert.Equal("teapot", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Response_RoundTripsBinaryBodies()
    {
        await using var site = await TestSite.StartAsync();
        using var response = await site.Client.GetAsync("/binary");

        Assert.Equal([0, 1, 2, 253, 254, 255], await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Response_SetsContentLengthForLargeBodies()
    {
        await using var site = await TestSite.StartAsync();
        using var response = await site.Client.GetAsync("/big?size=300000");

        Assert.Equal(300_000, response.Content.Headers.ContentLength);
        Assert.Equal(300_000, (await response.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task Head_ReturnsHeadersWithoutABody()
    {
        await using var site = await TestSite.StartAsync();
        using var response = await site.Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/big?size=1000"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1000, response.Content.Headers.ContentLength);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UnknownPaths_AreAnswered_ByTheEngine()
    {
        await using var site = await TestSite.StartAsync();
        using var response = await site.Client.GetAsync("/no-such-page");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Console_WritesToTheLogUnderItsOwnCategory()
    {
        await using var site = await TestSite.StartAsync(
            configureServices: services => services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug)));

        using var response = await site.Client.GetAsync("/console");

        Assert.Equal("logged", await response.Content.ReadAsStringAsync());
        Assert.Equal(
            [
                (LogLevel.Debug, "debug line"),
                (LogLevel.Information, "log line"),
                (LogLevel.Information, "info line"),
                (LogLevel.Warning, "warn line"),
                (LogLevel.Error, "error line"),
                (LogLevel.Information, "cart has 3 items"),
                (LogLevel.Information, "group"),
                (LogLevel.Information, "  inside"),
            ],
            site.Logs.Entries
                .Where(entry => entry.Category == "AstroJint.Console")
                .Select(entry => (entry.Level, entry.Message)));
    }

    [Fact]
    public async Task ScriptErrors_Return500AndKeepTheEngine()
    {
        await using var site = await TestSite.StartAsync(configure: options => options.PoolSize = 1);
        Assert.Equal(1, Handled(await site.GetJsonAsync("/count")));

        using var thrown = await site.Client.GetAsync("/throw");
        using var rejected = await site.Client.GetAsync("/reject");

        Assert.Equal(HttpStatusCode.InternalServerError, thrown.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, rejected.StatusCode);
        Assert.Equal(4, Handled(await site.GetJsonAsync("/count")));
        Assert.Equal(2, site.Logs.Entries.Count(entry => entry.Level == LogLevel.Error && entry.Message.Contains("JavaScript error")));
    }

    [Fact]
    public async Task SlowScripts_Return504AndReplaceTheEngine()
    {
        await using var site = await TestSite.StartAsync(configure: options =>
        {
            options.PoolSize = 1;
            options.RequestTimeout = TimeSpan.FromMilliseconds(400);
        });

        Assert.Equal(1, Handled(await site.GetJsonAsync("/count")));

        using var response = await site.Client.GetAsync("/forever");

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal(1, Handled(await site.GetJsonAsync("/count")));
        Assert.Contains(site.Logs.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("did not finish rendering"));
    }

    [Fact]
    public async Task ScriptsThatHoardMemory_Return500AndReplaceTheEngine()
    {
        await using var site = await TestSite.StartAsync(configure: options =>
        {
            options.PoolSize = 1;
            options.MemoryLimitBytes = 16 * 1024 * 1024;
        });

        Assert.Equal(1, Handled(await site.GetJsonAsync("/count")));

        using var response = await site.Client.GetAsync("/alloc");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, Handled(await site.GetJsonAsync("/count")));
        Assert.Contains(site.Logs.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("memory limit"));
    }

    [Fact]
    public async Task ClientAbort_StopsTheScriptQuietlyAndReplacesTheEngine()
    {
        await using var site = await TestSite.StartAsync(configure: options =>
        {
            options.PoolSize = 1;
            options.RequestTimeout = Timeout.InfiniteTimeSpan;
        });

        Assert.Equal(1, Handled(await site.GetJsonAsync("/count")));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => site.Client.GetAsync("/forever", cancellation.Token));

        // The next request has to wait for the abandoned render to give its engine up.
        Assert.Equal(1, Handled(await site.GetJsonAsync("/count")));
        Assert.DoesNotContain(site.Logs.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task PoolExhausted_Returns503WithRetryAfter_AndRecovers()
    {
        await using var site = await TestSite.StartAsync(configure: options =>
        {
            options.PoolSize = 1;
            options.RequestTimeout = TimeSpan.FromMilliseconds(300);
        });

        var held = await site.Astro.Pool.RentAsync(CancellationToken.None);

        using var busy = await site.Client.GetAsync("/count");
        site.Astro.Pool.Return(held, discard: false);
        using var recovered = await site.Client.GetAsync("/count");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, busy.StatusCode);
        Assert.Equal("1", busy.Headers.GetValues("Retry-After").Single());
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task Requests_WaitForAFreeEngine()
    {
        // Date.now() in a busy loop allocates about 128 MiB per 500 ms, so the memory budget would end the spin early.
        await using var site = await TestSite.StartAsync(configure: options =>
        {
            options.PoolSize = 1;
            options.MemoryLimitBytes = null;
        });

        var first = site.Client.GetAsync("/spin?ms=600");
        await WaitUntil(() => site.Astro.Pool.Reserved == 1);
        var second = site.Client.GetAsync("/count");

        using var firstResponse = await first;
        using var secondResponse = await second;

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(2, Handled(JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync()).RootElement));
    }

    [Fact]
    public async Task ConcurrentRequests_NeverUseMoreEnginesThanThePoolSize()
    {
        await using var site = await TestSite.StartAsync(configure: options => options.PoolSize = 3);

        var responses = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => site.Client.GetAsync("/spin?ms=20")));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.InRange(site.Astro.Pool.Reserved, 1, 3);
        Assert.Equal(site.Astro.Pool.Reserved, site.Astro.Pool.Idle);
    }

    [Fact]
    public async Task RequestBodies_OverTheLimit_Return413()
    {
        await using var site = await TestSite.StartAsync(configure: options => options.MaxRequestBodyBytes = 16);

        using var declared = await site.Client.PostAsync("/echo", new StringContent(new string('x', 100)));
        using var chunked = await site.Client.PostAsync("/echo", new StreamContent(new MemoryStream(new byte[100])));
        using var small = await site.Client.PostAsync("/echo", new StringContent("tiny"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, declared.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, chunked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, small.StatusCode);
    }

    [Fact]
    public async Task RequestBodies_ThatEndEarly_ReturnTheStatusOfTheFailureWithoutAnError()
    {
        await using var site = await TestSite.StartAsync(configureApp: app => app.Use((context, next) =>
        {
            context.Request.Body = new FailingBody(new BadHttpRequestException("Unexpected end of request content.", StatusCodes.Status400BadRequest));
            return next(context);
        }));

        using var response = await site.Client.PostAsync("/echo", new StringContent("{\"partial\":"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(site.Logs.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task RequestBodies_OfAClientThatLeft_AreDroppedWithoutAnError()
    {
        await using var site = await TestSite.StartAsync(configureApp: app => app.Use((context, next) =>
        {
            context.Request.Body = new FailingBody(new IOException("The client reset the request stream."));
            context.RequestAborted = new CancellationToken(canceled: true);
            return next(context);
        }));

        using var response = await site.Client.PostAsync("/echo", new StringContent("{\"partial\":"));

        Assert.Equal(0, site.Astro.Pool.Reserved);
        Assert.DoesNotContain(site.Logs.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task WarmUp_FillsThePoolInTheBackground()
    {
        await using var site = await TestSite.StartAsync(configure: options =>
        {
            options.PoolSize = 2;
            options.WarmUp = true;
        });

        await WaitUntil(() => site.Astro.Pool.Idle == 2);

        Assert.Contains(site.Logs.Entries, entry => entry.Message.Contains("Warmed up 2 of 2"));
    }

    [Fact]
    public async Task MissingBuild_FailsWhenTheHostStarts()
    {
        using var empty = DistFolder.CreateDefault();
        File.Delete(Path.Combine(empty.DistPath, "astro-jint.json"));

        var error = await Assert.ThrowsAsync<FileNotFoundException>(() => TestSite.StartAsync(empty));

        Assert.Contains("astro-jint.json", error.Message);
    }

    [Fact]
    public async Task InvalidOptions_FailWhenTheHostStarts()
    {
        using var dist = DistFolder.CreateDefault();

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            TestSite.StartAsync(dist, configure: options => options.PoolSize = 0));

        Assert.Contains("PoolSize", error.Message);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was not met in time.");
            await Task.Delay(25);
        }
    }

    /// <summary>A request body that fails the way Kestrel's does when the client leaves in the middle of it.</summary>
    private sealed class FailingBody(Exception failure) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw failure;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw failure;

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
