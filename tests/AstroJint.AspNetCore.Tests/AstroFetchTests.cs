using System.Text.Json;

namespace AstroJint.AspNetCore.Tests;

/// <summary>What the bundle can reach with <c>fetch</c>: nothing by default, whatever <see cref="AstroOptions.FetchUrlFilter"/> allows otherwise.</summary>
public class AstroFetchTests
{
    private static Task<JsonElement> FetchAsync(TestSite site, Uri url, string? options = null) =>
        site.GetJsonAsync($"/fetch?url={Uri.EscapeDataString(url.ToString())}{options}");

    [Fact]
    public async Task Fetch_IsRejectedByDefault()
    {
        await using var origin = await LoopbackOrigin.StartAsync();
        await using var site = await TestSite.StartAsync();

        var result = await FetchAsync(site, origin.Url("/hello"));

        Assert.False(result.GetProperty("ok").GetBoolean());
        Assert.Empty(origin.Requests);
    }

    [Fact]
    public async Task Fetch_ReachesWhatTheFilterAllows()
    {
        await using var origin = await LoopbackOrigin.StartAsync();
        await using var site = await TestSite.StartAsync(configure: options => options.FetchUrlFilter = url => url.IsLoopback);

        var result = await FetchAsync(site, origin.Url("/hello"));

        Assert.True(result.GetProperty("ok").GetBoolean(), result.ToString());
        Assert.Equal(200, result.GetProperty("status").GetInt32());
        Assert.Equal("hello from the origin", result.GetProperty("body").GetString());
        Assert.Equal(origin.Url("/hello").ToString(), result.GetProperty("url").GetString());
        Assert.Equal(["/hello"], origin.Requests);
    }

    [Fact]
    public async Task Fetch_ReadsBinaryBodies()
    {
        await using var origin = await LoopbackOrigin.StartAsync();
        await using var site = await TestSite.StartAsync(configure: options => options.FetchUrlFilter = url => url.IsLoopback);

        var result = await FetchAsync(site, origin.Url("/bytes"), "&binary");

        Assert.True(result.GetProperty("ok").GetBoolean(), result.ToString());
        Assert.Equal(LoopbackOrigin.Bytes.Select(value => (int)value), result.GetProperty("body").EnumerateArray().Select(value => value.GetInt32()));
    }

    [Fact]
    public async Task Fetch_AsksTheFilterAboutEveryUrl()
    {
        await using var origin = await LoopbackOrigin.StartAsync();
        var asked = new List<Uri>();
        await using var site = await TestSite.StartAsync(configure: options => options.FetchUrlFilter = url =>
        {
            lock (asked)
            {
                asked.Add(url);
            }

            return !url.AbsolutePath.StartsWith("/hello", StringComparison.Ordinal);
        });

        var allowed = await FetchAsync(site, origin.Url("/missing"));
        var rejected = await FetchAsync(site, origin.Url("/hello"));

        Assert.True(allowed.GetProperty("ok").GetBoolean(), allowed.ToString());
        Assert.Equal(404, allowed.GetProperty("status").GetInt32());
        Assert.False(rejected.GetProperty("ok").GetBoolean());
        Assert.Equal(["/missing"], origin.Requests);
        Assert.Equal([origin.Url("/missing"), origin.Url("/hello")], asked);
    }

    [Fact]
    public async Task Fetch_FollowsRedirects()
    {
        await using var origin = await LoopbackOrigin.StartAsync();
        await using var site = await TestSite.StartAsync(configure: options => options.FetchUrlFilter = url => url.IsLoopback);

        var result = await FetchAsync(site, origin.Url("/redirect-twice"));

        Assert.True(result.GetProperty("ok").GetBoolean(), result.ToString());
        Assert.Equal(200, result.GetProperty("status").GetInt32());
        Assert.True(result.GetProperty("redirected").GetBoolean());
        Assert.Equal(origin.Url("/hello").ToString(), result.GetProperty("url").GetString());
        Assert.Equal("hello from the origin", result.GetProperty("body").GetString());
    }

    // Astro's image endpoint follows redirects itself, to check every hop against the allowed domains.
    [Fact]
    public async Task Fetch_CanLeaveRedirectsToTheCaller()
    {
        await using var origin = await LoopbackOrigin.StartAsync();
        await using var site = await TestSite.StartAsync(configure: options => options.FetchUrlFilter = url => url.IsLoopback);

        var result = await FetchAsync(site, origin.Url("/redirect"), "&redirect=manual");

        Assert.True(result.GetProperty("ok").GetBoolean(), result.ToString());
        Assert.Equal(302, result.GetProperty("status").GetInt32());
        Assert.Equal(["/redirect"], origin.Requests);
    }
}
