using System.Text;

namespace AstroJint.AspNetCore.Tests;

public sealed class AstroStaticFileCacheTests
{
    private const long DefaultBudget = 32L * 1024 * 1024;

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(-1, 0, 0)]
    [InlineData(9, 0, 0)]
    [InlineData(10, 1, 10)]
    [InlineData(109, 1, 10)]
    [InlineData(110, 2, 110)]
    [InlineData(1109, 2, 110)]
    [InlineData(1110, 3, 1110)]
    [InlineData(long.MaxValue, 3, 1110)]
    public void Load_KeepsTheSmallestFilesThatFitTheBudget(long budget, int expectedCount, long expectedBytes)
    {
        using var dist = DistFolder.Create()
            .WithClientFile("thousand.txt", new string('c', 1000))
            .WithClientFile("ten.txt", new string('a', 10))
            .WithClientFile("hundred.txt", new string('b', 100));

        var cache = Load(dist, budget);

        Assert.Equal(expectedCount, cache.Count);
        Assert.Equal(expectedBytes, cache.Bytes);
        Assert.Equal(expectedCount >= 1, cache.TryGet("ten.txt", out _));
        Assert.Equal(expectedCount >= 2, cache.TryGet("hundred.txt", out _));
        Assert.Equal(expectedCount >= 3, cache.TryGet("thousand.txt", out _));
    }

    [Fact]
    public void Load_WithoutABudget_HoldsNothingAndDoesNotReadTheFiles()
    {
        using var dist = DistFolder.Create().WithClientFile("index.html", "<h1>Home</h1>");

        Assert.Same(AstroStaticFileCache.Empty, Load(dist, 0));
        Assert.Equal(0, AstroStaticFileCache.Empty.Count);
        Assert.Equal(0, AstroStaticFileCache.Empty.Bytes);
        Assert.False(AstroStaticFileCache.Empty.TryGet("index.html", out _));
    }

    [Fact]
    public void Load_BreaksTiesBetweenFilesOfTheSameSizeByPath()
    {
        using var dist = DistFolder.Create()
            .WithClientFile("b.txt", "0123456789")
            .WithClientFile("a.txt", "0123456789");

        var cache = Load(dist, 10);

        Assert.True(cache.TryGet("a.txt", out _));
        Assert.False(cache.TryGet("b.txt", out _));
    }

    [Fact]
    public void Load_LeavesFilesAboveTheLimitToTheFileSystem()
    {
        var limit = (int)AstroStaticFileCache.MaxFileBytes;
        using var dist = DistFolder.Create()
            .WithClientFile("limit.bin", new byte[limit])
            .WithClientFile("over.bin", new byte[limit + 1]);

        var cache = Load(dist);

        Assert.True(cache.TryGet("limit.bin", out var file));
        Assert.Equal(limit, file.Content.Length);
        Assert.False(cache.TryGet("over.bin", out _));
        Assert.Equal(limit, cache.Bytes);
    }

    [Fact]
    public void Load_HoldsOnlyTheFilesThatAreServed()
    {
        using var dist = DistFolder.CreateDefault();

        var cache = Load(dist);

        Assert.Equal(9, cache.Count);
        Assert.True(cache.TryGet(".well-known/security.txt", out _));
        Assert.True(cache.TryGet("_astro/app.abc123.js", out _));
        Assert.False(cache.TryGet("private/.env", out _));
        Assert.False(cache.TryGet("nothing.html", out _));
    }

    [Fact]
    public void Load_HoldsFilesWithoutContent()
    {
        using var dist = DistFolder.Create().WithClientFile("empty.txt", "");

        Assert.True(Load(dist).TryGet("empty.txt", out var file));
        Assert.Empty(file.Content);
        Assert.Equal("text/plain; charset=utf-8", file.ContentType);
    }

    [Fact]
    public void Load_DescribesAFileTheWayAFileResultDoes()
    {
        using var dist = DistFolder.Create().WithClientFile("assets/app.css", "body { margin: 0 }");
        File.SetLastWriteTimeUtc(Path.Combine(dist.ClientPath, "assets", "app.css"), new DateTime(2026, 3, 4, 5, 6, 7, 890, DateTimeKind.Utc));

        Assert.True(Load(dist).TryGet("assets/app.css", out var file));

        Assert.Equal("body { margin: 0 }", Encoding.UTF8.GetString(file.Content));
        Assert.Equal("text/css; charset=utf-8", file.ContentType);
        Assert.Equal("Wed, 04 Mar 2026 05:06:07 GMT", file.LastModified);
        Assert.Equal(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero), file.LastModifiedValue);
        Assert.Matches("^\"[0-9a-f]+\"$", file.ETag);
        Assert.Equal(file.ETag, file.EntityTag.Tag.Value);
        Assert.Empty(file.Headers);
    }

    [Fact]
    public void Load_KeepsTheHeadersThatAstroRenderedForAFile()
    {
        using var dist = DistFolder.Create()
            .WithClientFile("index.html", "<h1>Home</h1>")
            .WithClientFile("about/index.html", "<h1>About</h1>")
            .WithStaticHeaders("index.html", ("Content-Security-Policy", "default-src 'self'"));

        var cache = Load(dist);

        Assert.True(cache.TryGet("index.html", out var home));
        var header = Assert.Single(home.Headers);
        Assert.Equal("Content-Security-Policy", header.Key);
        Assert.Equal("default-src 'self'", header.Value);

        Assert.True(cache.TryGet("about/index.html", out var about));
        Assert.Empty(about.Headers);
    }

    [Fact]
    public void Load_IsASnapshotOfTheFilesAtStartup()
    {
        using var dist = DistFolder.Create().WithClientFile("a.txt", "before");
        var cache = Load(dist);

        File.WriteAllText(Path.Combine(dist.ClientPath, "a.txt"), "changed, and longer");

        Assert.True(cache.TryGet("a.txt", out var file));
        Assert.Equal("before", Encoding.UTF8.GetString(file.Content));
    }

    private static AstroStaticFileCache Load(DistFolder dist, long budget = DefaultBudget)
    {
        var resolver = AstroStaticFileResolver.FromDirectory(dist.ClientPath, AstroManifest.Load(dist.DistPath));
        return AstroStaticFileCache.Load(resolver, dist.ClientPath, budget);
    }
}
