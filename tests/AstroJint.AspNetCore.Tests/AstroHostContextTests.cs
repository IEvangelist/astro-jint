using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace AstroJint.AspNetCore.Tests;

/// <summary>What the host tells the bundle: <c>clientAddress</c>, <c>locals</c>, <c>env</c>, <c>readClientFile</c>, <c>readClientBytes</c>, <c>readClientHeaders</c> and <c>sha256</c>.</summary>
public class AstroHostContextTests
{
    private static readonly string[] AdminAndDev = ["admin", "dev"];

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
                Roles = AdminAndDev,
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
    public async Task ReadClientBytes_ReturnsTheBytesOfAnyFileOfTheBuild()
    {
        await using var site = await TestSite.StartAsync();
        var expected = Enumerable.Range(0, 1000).Select(i => (byte)i).ToArray();

        var host = await site.GetJsonAsync("/host-bytes?file=%2Fblob.bin");

        Assert.True(host.GetProperty("found").GetBoolean());
        Assert.True(host.GetProperty("isArrayBuffer").GetBoolean());
        Assert.Equal(1000, host.GetProperty("length").GetInt32());
        Assert.Equal([0, 1, 2, 3, 4, 5], host.GetProperty("head").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(expected)), host.GetProperty("sha256").GetString());
    }

    [Fact]
    public async Task ReadClientHeaders_ReturnsTheHeadersRecordedForAFile()
    {
        using var dist = DistFolder.CreateDefault()
            .WithStaticHeaders("404.html", ("Content-Security-Policy", "script-src 'self' 'sha256-missing'"), ("X-Page", "404"));
        await using var site = await TestSite.StartAsync(dist);

        var host = await site.GetJsonAsync("/host-headers?file=%2F404.html");

        Assert.True(host.GetProperty("found").GetBoolean());
        var headers = host.GetProperty("headers");
        Assert.Equal("script-src 'self' 'sha256-missing'", headers.GetProperty("Content-Security-Policy").GetString());
        Assert.Equal("404", headers.GetProperty("X-Page").GetString());
    }

    [Theory]
    [InlineData("/about/index.html")]
    [InlineData("/missing.html")]
    [InlineData("/../astro-jint.json")]
    [InlineData("/private/.env")]
    [InlineData("")]
    public async Task ReadClientHeaders_ReturnsNullForAnythingWithoutRecordedHeaders(string path)
    {
        using var dist = DistFolder.CreateDefault()
            .WithStaticHeaders("404.html", ("X-Page", "404"))
            .WithStaticHeaders("private/.env", ("X-Page", "private"));
        await using var site = await TestSite.StartAsync(dist);

        var host = await site.GetJsonAsync($"/host-headers?file={Uri.EscapeDataString(path)}");

        Assert.False(host.GetProperty("found").GetBoolean());
    }

    [Fact]
    public async Task ReadClientHeaders_DecodesPercentEncodedPaths_AndStripsTheBase()
    {
        using var dist = DistFolder.Create(baseFolder: "/docs")
            .WithClientFile("a file.html", "<p>spaced</p>")
            .WithClientFile("404.html", "<p>missing</p>")
            .WithStaticHeaders("a file.html", ("X-Page", "spaced"))
            .WithStaticHeaders("404.html", ("X-Page", "404"));
        await using var site = await TestSite.StartAsync(dist);

        var spaced = await site.GetJsonAsync("/host-headers?file=%2Fdocs%2Fa%2520file.html");
        var withBase = await site.GetJsonAsync("/host-headers?file=%2Fdocs%2F404.html");

        Assert.Equal("spaced", spaced.GetProperty("headers").GetProperty("X-Page").GetString());
        Assert.Equal("404", withBase.GetProperty("headers").GetProperty("X-Page").GetString());
    }

    [Theory]
    [InlineData("/missing.bin")]
    [InlineData("/about/")]
    [InlineData("/../astro-jint.json")]
    [InlineData("/private/.env")]
    [InlineData("")]
    public async Task ReadClientBytes_ReturnsNullForAnythingElse(string path)
    {
        await using var site = await TestSite.StartAsync();

        var host = await site.GetJsonAsync($"/host-bytes?file={Uri.EscapeDataString(path)}");

        Assert.False(host.GetProperty("found").GetBoolean());
    }

    [Fact]
    public async Task ReadClientBytes_DecodesPercentEncodedPaths_AndStripsTheBase()
    {
        using var dist = DistFolder.Create(baseFolder: "/docs")
            .WithClientFile("_astro/a penguin.png", [137, 80, 78, 71]);
        await using var site = await TestSite.StartAsync(dist);

        var host = await site.GetJsonAsync("/host-bytes?file=%2Fdocs%2F_astro%2Fa%2520penguin.png");

        Assert.True(host.GetProperty("found").GetBoolean());
        Assert.Equal(4, host.GetProperty("length").GetInt32());
        Assert.Equal([137, 80, 78, 71], host.GetProperty("head").EnumerateArray().Select(value => value.GetInt32()));
    }

    [Theory]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("97,98,99", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public async Task Sha256_HashesAnArrayBuffer(string bytes, string expected)
    {
        await using var site = await TestSite.StartAsync();

        var result = await site.GetJsonAsync($"/sha256?bytes={bytes}");

        Assert.Equal(expected, result.GetProperty("ofBuffer").GetString());
    }

    [Fact]
    public async Task Sha256_ReturnsNullForAnythingButAnArrayBuffer()
    {
        await using var site = await TestSite.StartAsync();

        var result = await site.GetJsonAsync("/sha256?bytes=97,98,99");

        Assert.Equal(JsonValueKind.Null, result.GetProperty("ofView").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("ofText").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("ofNothing").ValueKind);
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
