using Microsoft.Extensions.Options;

namespace AstroJint.AspNetCore.Tests;

public sealed class AstroManifestTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "astro-jint-tests", Guid.NewGuid().ToString("N"));

    public AstroManifestTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private void Write(string json) => File.WriteAllText(Path.Combine(_folder, AstroManifest.FileName), json);

    [Fact]
    public void Load_ReadsWhatTheAdapterWrites()
    {
        Write("""
            {
              "version": 1,
              "astroVersion": "7.3.6",
              "base": "/docs",
              "trailingSlash": "always",
              "buildFormat": "file",
              "assetsDir": "assets",
              "client": "public-site",
              "server": "ssr",
              "entry": "ssr/main.mjs",
              "routes": [
                { "route": "/", "type": "page", "prerender": true, "origin": "project" },
                { "route": "/time", "type": "page", "prerender": false, "origin": "project", "extra": 1 }
              ],
              "adapter": { "name": "astro-adapter-jint", "version": "0.1.0" }
            }
            """);

        var manifest = AstroManifest.Load(_folder);

        Assert.Equal("7.3.6", manifest.AstroVersion);
        Assert.Equal("/docs", manifest.Base);
        Assert.Equal("always", manifest.TrailingSlash);
        Assert.Equal("file", manifest.BuildFormat);
        Assert.Equal("assets", manifest.AssetsDir);
        Assert.Equal("public-site", manifest.Client);
        Assert.Equal("ssr", manifest.Server);
        Assert.Equal("ssr/main.mjs", manifest.Entry);
        Assert.Equal(2, manifest.Routes.Count);
        Assert.True(manifest.Routes[0].Prerender);
        Assert.False(manifest.Routes[1].Prerender);
        Assert.Equal("/time", manifest.Routes[1].Route);
    }

    [Fact]
    public void Load_AppliesDefaultsForMissingFields()
    {
        Write("""{ "version": 1 }""");

        var manifest = AstroManifest.Load(_folder);

        Assert.Equal("/", manifest.Base);
        Assert.Equal("ignore", manifest.TrailingSlash);
        Assert.Equal("directory", manifest.BuildFormat);
        Assert.Equal("_astro", manifest.AssetsDir);
        Assert.Equal("client", manifest.Client);
        Assert.Equal("server", manifest.Server);
        Assert.Equal("server/entry.mjs", manifest.Entry);
        Assert.Empty(manifest.Routes);
    }

    [Fact]
    public void Load_ExplainsHowToFixAMissingManifest()
    {
        var exception = Assert.Throws<FileNotFoundException>(() => AstroManifest.Load(_folder));

        Assert.Contains(AstroManifest.FileName, exception.Message);
        Assert.Contains("AstroOptions.DistPath", exception.Message);
    }

    [Fact]
    public void Load_RejectsAnUnsupportedVersion()
    {
        Write("""{ "version": 2 }""");

        var exception = Assert.Throws<InvalidDataException>(() => AstroManifest.Load(_folder));

        Assert.Contains("version 2", exception.Message);
    }

    [Fact]
    public void Load_RejectsAnEmptyDocument()
    {
        Write("null");

        Assert.Throws<InvalidDataException>(() => AstroManifest.Load(_folder));
    }

    [Fact]
    public void Load_RejectsMalformedJson()
    {
        Write("{ not json");

        Assert.ThrowsAny<System.Text.Json.JsonException>(() => AstroManifest.Load(_folder));
    }
}

public class AstroOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(Action<AstroOptions>? configure = null)
    {
        var options = new AstroOptions { DistPath = "dist" };
        configure?.Invoke(options);
        return new AstroOptionsValidator().Validate(null, options);
    }

    [Fact]
    public void Defaults_AreValid()
    {
        Assert.True(Validate().Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DistPath_MustNotBeBlank(string value)
    {
        var result = Validate(o => o.DistPath = value);

        Assert.True(result.Failed);
        Assert.Contains(nameof(AstroOptions.DistPath), result.FailureMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PoolSize_MustBePositive(int value)
    {
        var result = Validate(o => o.PoolSize = value);

        Assert.True(result.Failed);
        Assert.Contains(nameof(AstroOptions.PoolSize), result.FailureMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void RequestTimeout_MustBePositive(int seconds)
    {
        var result = Validate(o => o.RequestTimeout = TimeSpan.FromSeconds(seconds));

        Assert.True(result.Failed);
        Assert.Contains(nameof(AstroOptions.RequestTimeout), result.FailureMessage);
    }

    [Fact]
    public void RequestTimeout_CanBeInfinite()
    {
        Assert.True(Validate(o => o.RequestTimeout = Timeout.InfiniteTimeSpan).Succeeded);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void MemoryLimit_MustBePositive(long value)
    {
        var result = Validate(o => o.MemoryLimitBytes = value);

        Assert.True(result.Failed);
        Assert.Contains(nameof(AstroOptions.MemoryLimitBytes), result.FailureMessage);
    }

    [Fact]
    public void MemoryLimit_CanBeDisabled()
    {
        Assert.True(Validate(o => o.MemoryLimitBytes = null).Succeeded);
    }

    [Fact]
    public void MaxRequestBodyBytes_MayBeZeroButNotNegative()
    {
        Assert.True(Validate(o => o.MaxRequestBodyBytes = 0).Succeeded);
        Assert.True(Validate(o => o.MaxRequestBodyBytes = -1).Failed);
    }

    [Fact]
    public void AllFailuresAreReportedTogether()
    {
        var result = Validate(o =>
        {
            o.DistPath = "";
            o.PoolSize = 0;
            o.MemoryLimitBytes = 0;
        });

        Assert.Equal(3, result.Failures!.Count());
    }
}
