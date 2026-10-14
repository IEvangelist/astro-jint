using System.Net;
using System.Text.Json;

namespace AstroJint.AspNetCore.Tests;

/// <summary>What the host tells the bundle: <c>clientAddress</c>, <c>locals</c>, <c>env</c> and <c>readClientFile</c>.</summary>
public class AstroHostContextTests
{
    [Fact]
    public async Task ClientAddress_IsNullWhenTheConnectionHasNoRemoteAddress()
    {
        await using var site = await TestSite.StartAsync();
        var host = await site.GetJsonAsync("/host");

        Assert.Equal(JsonValueKind.Null, host.GetProperty("clientAddress").ValueKind);
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    public async Task ClientAddress_IsTheRemoteAddress(string remote, string expected)
    {
        await using var site = await TestSite.StartAsync(configureApp: TestSite.WithRemoteAddress(remote));
        var host = await site.GetJsonAsync("/host");

        Assert.Equal(expected, host.GetProperty("clientAddress").GetString());
    }

    [Fact]
    public async Task Locals_AreNullWithoutAFactory()
    {
        await using var site = await TestSite.StartAsync();
        var host = await site.GetJsonAsync("/host");

        Assert.Equal(JsonValueKind.Null, host.GetProperty("locals").ValueKind);
    }

    [Fact]
    public async Task Locals_AreSerializedAsCamelCaseJson()
    {
        await using var site = await TestSite.StartAsync(configure: options =>
            options.LocalsFactory = context => new
            {
                UserName = "ada",
                Roles = new[] { "admin", "dev" },
                Path = context.Request.Path.Value,
            });

        var host = await site.GetJsonAsync("/host");
        var locals = JsonDocument.Parse(host.GetProperty("locals").GetString()!).RootElement;

        Assert.Equal("ada", locals.GetProperty("userName").GetString());
        Assert.Equal(["admin", "dev"], locals.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        Assert.Equal("/host", locals.GetProperty("path").GetString());
    }

    [Fact]
    public async Task Locals_DoNotLeakIntoTheNextRequestOnTheSameEngine()
    {
        await using var site = await TestSite.StartAsync(configure: options =>
        {
            options.PoolSize = 1;
            options.LocalsFactory = context => context.Request.Query.TryGetValue("user", out var user) ? new { Name = user.ToString() } : null;
        });

        var first = await site.GetJsonAsync("/host?user=ada");
        var second = await site.GetJsonAsync("/host");

        Assert.Contains("ada", first.GetProperty("locals").GetString());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("locals").ValueKind);
    }

    [Fact]
    public async Task Env_ComesFromTheOptionsOnly()
    {
        Environment.SetEnvironmentVariable("ASTRO_JINT_TEST_SECRET", "from-process");
        try
        {
            await using var site = await TestSite.StartAsync(configure: options => options.Env["GREETING"] = "hello");

            var known = await site.GetJsonAsync("/host?env=GREETING");
            var unknown = await site.GetJsonAsync("/host?env=ASTRO_JINT_TEST_SECRET");

            Assert.Equal("hello", known.GetProperty("env").GetString());
            Assert.Equal(JsonValueKind.Null, unknown.GetProperty("env").ValueKind);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASTRO_JINT_TEST_SECRET", null);
        }
    }

    [Theory]
    [InlineData("/404.html", "<h1>Prerendered 404</h1>")]
    [InlineData("/about/index.html", "<h1>About</h1>")]
    [InlineData("/data.json", "{\"a\":1}")]
    public async Task ReadClientFile_ReturnsPrerenderedFiles(string path, string expected)
    {
        await using var site = await TestSite.StartAsync();
        var host = await site.GetJsonAsync($"/host?file={Uri.EscapeDataString(path)}");

        Assert.Equal(expected, host.GetProperty("file").GetString());
    }

    [Theory]
    [InlineData("/missing.html")]
    [InlineData("/about/")]
    [InlineData("/../astro-jint.json")]
    [InlineData("/private/.env")]
    [InlineData("")]
    public async Task ReadClientFile_ReturnsNullForAnythingElse(string path)
    {
        await using var site = await TestSite.StartAsync();
        var host = await site.GetJsonAsync($"/host?file={Uri.EscapeDataString(path)}");

        Assert.Equal(JsonValueKind.Null, host.GetProperty("file").ValueKind);
    }

    [Fact]
    public async Task ReadClientFile_DecodesPercentEncodedPaths_AndStripsTheBase()
    {
        using var dist = DistFolder.Create(baseFolder: "/docs")
            .WithClientFile("a file.html", "<p>spaced</p>")
            .WithClientFile("404.html", "<p>missing</p>");
        await using var site = await TestSite.StartAsync(dist);

        var spaced = await site.GetJsonAsync("/host?file=%2Fdocs%2Fa%2520file.html");
        var withBase = await site.GetJsonAsync("/host?file=%2Fdocs%2F404.html");

        Assert.Equal("<p>spaced</p>", spaced.GetProperty("file").GetString());
        Assert.Equal("<p>missing</p>", withBase.GetProperty("file").GetString());
    }

    [Fact]
    public async Task HostObject_IsAvailableToEveryEngineInThePool()
    {
        await using var site = await TestSite.StartAsync(
            configure: options =>
            {
                options.PoolSize = 3;
                options.Env["GREETING"] = "hello";
            },
            configureApp: TestSite.WithRemoteAddress("198.51.100.4"));

        var hosts = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => site.GetJsonAsync("/host?env=GREETING")));

        Assert.All(hosts, host =>
        {
            Assert.Equal("198.51.100.4", host.GetProperty("clientAddress").GetString());
            Assert.Equal("hello", host.GetProperty("env").GetString());
        });
    }
}
