using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AstroJint.AspNetCore.Tests;

/// <summary>
/// Session data lives in the host's <see cref="IDistributedCache"/>, because any engine of the pool, and any
/// server of a scaled-out site, may render the next request of a visitor. The bundle reaches the cache through
/// <c>__astroJint.sessionGet</c>, <c>sessionSet</c> and <c>sessionRemove</c>, which return promises.
/// </summary>
public class AstroSessionTests
{
    private const string StoreCategory = "AstroJint.AspNetCore.AstroSessionStore";

    private static int Handled(JsonElement result) => result.GetProperty("handled").GetInt32();

    private static string Escape(string value) => Uri.EscapeDataString(value);

    [Fact]
    public async Task Session_RoundTripsWhatTheBundleSent()
    {
        await using var site = await TestSite.StartAsync();
        const string data = "[{\"cart\":1},[\"h\u00e9llo \u2603 \\\"quoted\\\"\\n\",{\"emoji\":\"\ud83d\ude00\"}]]";

        Assert.Equal(JsonValueKind.Null, (await site.GetJsonAsync("/session-get?id=a")).GetProperty("value").ValueKind);

        await site.GetJsonAsync($"/session-set?id=a&value={Escape(data)}");
        var stored = await site.GetJsonAsync("/session-get?id=a");

        Assert.Equal(data, stored.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Session_RemoveForgetsTheData()
    {
        await using var site = await TestSite.StartAsync();

        await site.GetJsonAsync("/session-set?id=gone&value=1");
        await site.GetJsonAsync("/session-set?id=kept&value=2");
        await site.GetJsonAsync("/session-remove?id=gone");

        Assert.Equal(JsonValueKind.Null, (await site.GetJsonAsync("/session-get?id=gone")).GetProperty("value").ValueKind);
        Assert.Equal("2", (await site.GetJsonAsync("/session-get?id=kept")).GetProperty("value").GetString());
    }

    [Fact]
    public async Task Session_RemovingWhatIsNotThereIsFine()
    {
        await using var site = await TestSite.StartAsync();

        var result = await site.GetJsonAsync("/session-remove?id=never-existed");

        Assert.True(result.TryGetProperty("engine", out _));
    }

    [Fact]
    public async Task Session_IsSharedByEveryEngine()
    {
        await using var site = await TestSite.StartAsync();
        var written = await site.GetJsonAsync("/session-set?id=shared&value=hello");

        // While one engine holds on to its read, the other request has to be served by the other engine.
        var reads = await Task.WhenAll(
            site.GetJsonAsync("/session-get?id=shared&hold=400"),
            site.GetJsonAsync("/session-get?id=shared&hold=400"));

        Assert.All(reads, read => Assert.Equal("hello", read.GetProperty("value").GetString()));
        Assert.NotEqual(reads[0].GetProperty("engine").GetString(), reads[1].GetProperty("engine").GetString());
        Assert.Contains(reads, read => read.GetProperty("engine").GetString() != written.GetProperty("engine").GetString());
    }

    [Fact]
    public async Task Session_ManyOperationsCanBePendingAtOnce()
    {
        await using var site = await TestSite.StartAsync();

        var result = await site.GetJsonAsync("/session-parallel?count=25");

        Assert.Equal(
            Enumerable.Range(0, 25).Select(index => $"value-{index}"),
            result.GetProperty("values").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task Session_AnOperationThatIsNotAwaitedStillReachesTheCache()
    {
        await using var site = await TestSite.StartAsync();

        await site.GetJsonAsync("/session-detached?id=late&value=arrived");

        Assert.Equal("arrived", (await site.GetJsonAsync("/session-get?id=late")).GetProperty("value").GetString());
    }

    [Fact]
    public async Task Session_UsesTheCacheTheApplicationRegistered()
    {
        var cache = new RecordingCache();
        await using var site = await TestSite.StartAsync(configureServices: services => services.AddSingleton<IDistributedCache>(cache));

        await site.GetJsonAsync("/session-set?id=abc&value=stored");

        // The synchronous read is not recorded, so it leaves the sequence of calls alone.
        Assert.Equal("stored", System.Text.Encoding.UTF8.GetString(cache.Get("astro-jint:session:abc")!));

        await site.GetJsonAsync("/session-get?id=abc");
        await site.GetJsonAsync("/session-remove?id=abc");

        Assert.Equal(
            new[] { ("set", "astro-jint:session:abc"), ("get", "astro-jint:session:abc"), ("remove", "astro-jint:session:abc") },
            cache.Calls.Select(call => (call.Operation, call.Key)).ToArray());
        Assert.Null(cache.Get("astro-jint:session:abc"));
    }

    [Fact]
    public void AddAstro_KeepsACacheThatWasRegisteredBefore()
    {
        var cache = new RecordingCache();
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache>(cache);

        services.AddAstro(options => options.DistPath = "dist");

        using var provider = services.BuildServiceProvider();
        Assert.Same(cache, provider.GetRequiredService<IDistributedCache>());
    }

    [Fact]
    public void AddAstro_ProvidesAMemoryCacheWhenThereIsNone()
    {
        var services = new ServiceCollection();

        services.AddAstro(options => options.DistPath = "dist");

        using var provider = services.BuildServiceProvider();
        Assert.IsType<MemoryDistributedCache>(provider.GetRequiredService<IDistributedCache>());
    }

    [Fact]
    public async Task Session_ExpiresAfterTheIdleTimeoutByDefault()
    {
        var cache = new RecordingCache();
        await using var site = await TestSite.StartAsync(configureServices: services => services.AddSingleton<IDistributedCache>(cache));

        await site.GetJsonAsync("/session-set?id=abc&value=1");

        var set = Assert.Single(cache.Calls, call => call.Operation == "set");
        Assert.Equal(TimeSpan.FromMinutes(20), set.Options!.SlidingExpiration);
        Assert.Null(set.Options.AbsoluteExpiration);
        Assert.Null(set.Options.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task Session_IdleTimeoutIsConfigurable()
    {
        var cache = new RecordingCache();
        await using var site = await TestSite.StartAsync(
            configure: options => options.SessionIdleTimeout = TimeSpan.FromMinutes(5),
            configureServices: services => services.AddSingleton<IDistributedCache>(cache));

        await site.GetJsonAsync("/session-set?id=abc&value=1");

        Assert.Equal(TimeSpan.FromMinutes(5), Assert.Single(cache.Calls, call => call.Operation == "set").Options!.SlidingExpiration);
    }

    [Fact]
    public async Task Session_CanBeKeptUntilItIsDeleted()
    {
        var cache = new RecordingCache();
        await using var site = await TestSite.StartAsync(
            configure: options => options.SessionIdleTimeout = Timeout.InfiniteTimeSpan,
            configureServices: services => services.AddSingleton<IDistributedCache>(cache));

        await site.GetJsonAsync("/session-set?id=abc&value=1");

        Assert.Null(Assert.Single(cache.Calls, call => call.Operation == "set").Options!.SlidingExpiration);
    }

    [Fact]
    public async Task Session_IsGoneOnceItHasBeenIdleForTooLong()
    {
        await using var site = await TestSite.StartAsync(configure: options => options.SessionIdleTimeout = TimeSpan.FromMilliseconds(300));

        await site.GetJsonAsync("/session-set?id=brief&value=1");
        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        Assert.Equal(JsonValueKind.Null, (await site.GetJsonAsync("/session-get?id=brief")).GetProperty("value").ValueKind);
    }

    [Fact]
    public async Task Session_ACacheFailureRejectsThePromise_AndIsLogged()
    {
        await using var site = await TestSite.StartAsync(
            configure: options => options.PoolSize = 1,
            configureServices: services => services.AddSingleton<IDistributedCache>(new FailingCache()));

        Assert.Equal(1, Handled(await site.GetJsonAsync("/count")));
        var result = await site.GetJsonAsync("/session-catch?id=x");

        Assert.False(result.GetProperty("ok").GetBoolean());
        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains("failed", result.GetProperty("error").GetString());
        Assert.DoesNotContain("cache is down", result.GetProperty("error").GetString());

        var logged = Assert.Single(site.Logs.Entries, entry => entry.Category == StoreCategory);
        Assert.Equal(LogLevel.Error, logged.Level);
        Assert.Equal("cache is down", logged.Exception?.Message);

        // The rejection was handled by the bundle, so the engine carries on.
        Assert.Equal(3, Handled(await site.GetJsonAsync("/count")));
    }

    [Fact]
    public async Task Session_ACacheFailureThatIsNotHandledIsA500()
    {
        await using var site = await TestSite.StartAsync(
            configure: options => options.PoolSize = 1,
            configureServices: services => services.AddSingleton<IDistributedCache>(new FailingCache()));

        using var response = await site.Client.GetAsync("/session-get?id=x");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(site.Logs.Entries, entry => entry.Category == StoreCategory && entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Session_MissingArgumentsRejectThePromise()
    {
        await using var site = await TestSite.StartAsync();

        var result = await site.GetJsonAsync("/session-bad-arguments");

        Assert.False(result.GetProperty("ok").GetBoolean());
        Assert.DoesNotContain(site.Logs.Entries, entry => entry.Category == StoreCategory);
    }

    [Fact]
    public async Task Session_OperationsAreCancelledWhenTheRequestTimesOut()
    {
        var cache = new GatedCache { HonorCancellation = true };
        await using var site = await TestSite.StartAsync(
            configure: options =>
            {
                options.PoolSize = 1;
                options.RequestTimeout = TimeSpan.FromMilliseconds(600);
            },
            configureServices: services => services.AddSingleton<IDistributedCache>(cache));

        Assert.Equal(1, Handled(await site.GetJsonAsync("/count")));
        using var response = await site.Client.GetAsync("/session-get?id=hang");

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        await cache.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, Handled(await site.GetJsonAsync("/count")));
        Assert.DoesNotContain(site.Logs.Entries, entry => entry.Category == StoreCategory);
    }

    [Fact]
    public async Task Session_ACacheThatOutlivesItsEngineDoesNoHarm()
    {
        var cache = new GatedCache();
        await using var site = await TestSite.StartAsync(
            configure: options =>
            {
                options.PoolSize = 1;
                options.RequestTimeout = TimeSpan.FromMilliseconds(600);
            },
            configureServices: services => services.AddSingleton<IDistributedCache>(cache));

        Assert.Equal(1, Handled(await site.GetJsonAsync("/count")));
        using var response = await site.Client.GetAsync("/session-get?id=late");
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);

        // The cache only answers once the engine that asked is gone.
        cache.Gate.SetResult();
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        Assert.Equal(1, Handled(await site.GetJsonAsync("/count")));
        Assert.DoesNotContain(site.Logs.Entries, entry => entry.Level >= LogLevel.Error);
    }

    private sealed record CacheCall(string Operation, string Key, DistributedCacheEntryOptions? Options);

    private class RecordingCache : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner = new(Options.Create(new MemoryDistributedCacheOptions()));

        public ConcurrentQueue<CacheCall> Calls { get; } = new();

        public virtual Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            Calls.Enqueue(new CacheCall("get", key, null));
            return _inner.GetAsync(key, token);
        }

        public virtual Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Calls.Enqueue(new CacheCall("set", key, options));
            return _inner.SetAsync(key, value, options, token);
        }

        public virtual Task RemoveAsync(string key, CancellationToken token = default)
        {
            Calls.Enqueue(new CacheCall("remove", key, null));
            return _inner.RemoveAsync(key, token);
        }

        public byte[]? Get(string key) => _inner.Get(key);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _inner.Set(key, value, options);

        public void Remove(string key) => _inner.Remove(key);

        public void Refresh(string key) => _inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);
    }

    private sealed class FailingCache : RecordingCache
    {
        public override Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("cache is down");
    }

    /// <summary>A cache whose reads wait until <see cref="Gate"/> opens, or until the token fires when it is honoured.</summary>
    private sealed class GatedCache : RecordingCache
    {
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes when a read was ended because its token fired.</summary>
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool HonorCancellation { get; init; }

        public override async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            try
            {
                await (HonorCancellation ? Gate.Task.WaitAsync(token) : Gate.Task).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Cancelled.TrySetResult();
                throw;
            }

            return await base.GetAsync(key, token).ConfigureAwait(false);
        }
    }
}
