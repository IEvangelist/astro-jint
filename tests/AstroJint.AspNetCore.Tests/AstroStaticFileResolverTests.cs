namespace AstroJint.AspNetCore.Tests;

public class AstroStaticFileResolverTests
{
    private static readonly string[] DirectoryFormatFiles =
    [
        "index.html", "about/index.html", "blog/index.html", "blog/first/index.html", "404.html",
        "robots.txt", "_astro/app.abc123.js", ".well-known/security.txt", ".env", ".hidden/secret.txt",
    ];

    private static readonly string[] FileFormatFiles =
    [
        "index.html", "about.html", "blog.html", "blog/first.html", "404.html", "_astro/app.abc123.js",
    ];

    private static AstroStaticFileResolver Create(
        IEnumerable<string> files,
        string @base = "/",
        string trailingSlash = "ignore",
        string buildFormat = "directory") =>
        new(files, new AstroManifest { Base = @base, TrailingSlash = trailingSlash, BuildFormat = buildFormat });

    [Theory]
    [InlineData("/", "index.html")]
    [InlineData("/about", "about/index.html")]
    [InlineData("/about/", "about/index.html")]
    [InlineData("/about/index.html", "about/index.html")]
    [InlineData("/blog", "blog/index.html")]
    [InlineData("/blog/first", "blog/first/index.html")]
    [InlineData("/404.html", "404.html")]
    [InlineData("/robots.txt", "robots.txt")]
    [InlineData("/.well-known/security.txt", ".well-known/security.txt")]
    public void Resolve_DirectoryFormat_FindsPagesAndFiles(string requestPath, string expectedFile)
    {
        var match = Create(DirectoryFormatFiles).Resolve(requestPath);

        Assert.Equal(StaticFileMatchKind.File, match.Kind);
        Assert.Equal(expectedFile, match.Path);
        Assert.False(match.Immutable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("about")]
    [InlineData("/missing")]
    [InlineData("/about/missing")]
    [InlineData("/404")]
    [InlineData("/ABOUT")]
    [InlineData("/_astro")]
    [InlineData("/_astro/app.abc123.js/")]
    public void Resolve_DirectoryFormat_IgnoresWhatIsNotPrerendered(string requestPath)
    {
        Assert.Equal(StaticFileMatchKind.None, Create(DirectoryFormatFiles).Resolve(requestPath).Kind);
    }

    [Theory]
    [InlineData("/.env")]
    [InlineData("/.hidden/secret.txt")]
    [InlineData("/.well-known/")]
    [InlineData("/about/../about")]
    [InlineData("/./about")]
    [InlineData("/about/.")]
    [InlineData("/about/..%2f")]
    [InlineData("/about\\index.html")]
    [InlineData("/about\0")]
    [InlineData("//")]
    [InlineData("//about")]
    [InlineData("//about/")]
    [InlineData("/about//")]
    [InlineData("/blog//first")]
    [InlineData("/%2e%2e/about")]
    public void Resolve_NeverServesHiddenOrSuspiciousPaths(string requestPath)
    {
        Assert.Equal(StaticFileMatchKind.None, Create(DirectoryFormatFiles).Resolve(requestPath).Kind);
    }

    [Fact]
    public void Constructor_DropsHiddenFilesFromTheFileSet()
    {
        var resolver = Create(DirectoryFormatFiles);

        Assert.Equal(DirectoryFormatFiles.Length - 2, resolver.Count);
    }

    [Fact]
    public void Resolve_MarksTheAssetsFolderImmutable()
    {
        var match = Create(DirectoryFormatFiles).Resolve("/_astro/app.abc123.js");

        Assert.Equal(StaticFileMatchKind.File, match.Kind);
        Assert.Equal("_astro/app.abc123.js", match.Path);
        Assert.True(match.Immutable);
    }

    [Fact]
    public void Resolve_HonoursACustomAssetsFolder()
    {
        var manifest = new AstroManifest { AssetsDir = "assets" };
        var resolver = new AstroStaticFileResolver(["assets/app.js", "_astro/other.js"], manifest);

        Assert.True(resolver.Resolve("/assets/app.js").Immutable);
        Assert.False(resolver.Resolve("/_astro/other.js").Immutable);
    }

    [Theory]
    [InlineData("/", "index.html")]
    [InlineData("/about", "about.html")]
    [InlineData("/about/", "about.html")]
    [InlineData("/about.html", "about.html")]
    [InlineData("/blog", "blog.html")]
    [InlineData("/blog/first", "blog/first.html")]
    [InlineData("/blog/first/", "blog/first.html")]
    public void Resolve_FileFormat_FindsPages(string requestPath, string expectedFile)
    {
        var match = Create(FileFormatFiles, buildFormat: "file").Resolve(requestPath);

        Assert.Equal(StaticFileMatchKind.File, match.Kind);
        Assert.Equal(expectedFile, match.Path);
    }

    [Fact]
    public void Resolve_FileFormat_FallsBackToDirectoryIndexes()
    {
        var resolver = Create(["blog/index.html"], buildFormat: "preserve");

        Assert.Equal("blog/index.html", resolver.Resolve("/blog").Path);
    }

    [Theory]
    [InlineData("/about", "Redirect", "/about/")]
    [InlineData("/blog/first", "Redirect", "/blog/first/")]
    [InlineData("/about/", "File", "about/index.html")]
    [InlineData("/", "File", "index.html")]
    [InlineData("/robots.txt", "File", "robots.txt")]
    [InlineData("/missing", "None", null)]
    public void Resolve_TrailingSlashAlways(string requestPath, string kind, string? path)
    {
        var match = Create(DirectoryFormatFiles, trailingSlash: "always").Resolve(requestPath);

        Assert.Equal(kind, match.Kind.ToString());
        Assert.Equal(path, match.Path);
    }

    [Theory]
    [InlineData("/about/", "Redirect", "/about")]
    [InlineData("/blog/first/", "Redirect", "/blog/first")]
    [InlineData("/about", "File", "about/index.html")]
    [InlineData("/", "File", "index.html")]
    [InlineData("/missing/", "None", null)]
    public void Resolve_TrailingSlashNever(string requestPath, string kind, string? path)
    {
        var match = Create(DirectoryFormatFiles, trailingSlash: "never").Resolve(requestPath);

        Assert.Equal(kind, match.Kind.ToString());
        Assert.Equal(path, match.Path);
    }

    [Theory]
    [InlineData("/docs", "index.html")]
    [InlineData("/docs/", "index.html")]
    [InlineData("/docs/about", "about/index.html")]
    [InlineData("/docs/about/", "about/index.html")]
    [InlineData("/docs/robots.txt", "robots.txt")]
    [InlineData("/docs/_astro/app.abc123.js", "_astro/app.abc123.js")]
    public void Resolve_WithABase_StripsIt(string requestPath, string expectedFile)
    {
        var match = Create(DirectoryFormatFiles, "/docs").Resolve(requestPath);

        Assert.Equal(StaticFileMatchKind.File, match.Kind);
        Assert.Equal(expectedFile, match.Path);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/about")]
    [InlineData("/documents")]
    [InlineData("/documents/about")]
    [InlineData("/doc")]
    [InlineData("/robots.txt")]
    public void Resolve_WithABase_IgnoresPathsOutsideOfIt(string requestPath)
    {
        Assert.Equal(StaticFileMatchKind.None, Create(DirectoryFormatFiles, "/docs").Resolve(requestPath).Kind);
    }

    [Theory]
    [InlineData("/docs/")]
    [InlineData("docs/")]
    [InlineData("/docs")]
    public void Constructor_NormalizesTheBase(string @base)
    {
        Assert.Equal("about/index.html", Create(DirectoryFormatFiles, @base).Resolve("/docs/about").Path);
    }

    [Theory]
    [InlineData("always", "/docs", "/docs/")]
    [InlineData("always", "/docs/about", "/docs/about/")]
    [InlineData("never", "/docs/", "/docs")]
    [InlineData("never", "/docs/about/", "/docs/about")]
    public void Resolve_WithABase_RedirectsThePublicPath(string trailingSlash, string requestPath, string expected)
    {
        var match = Create(DirectoryFormatFiles, "/docs", trailingSlash).Resolve(requestPath);

        Assert.Equal(StaticFileMatchKind.Redirect, match.Kind);
        Assert.Equal(expected, match.Path);
    }

    [Theory]
    [InlineData("/404.html", "404.html")]
    [InlineData("/robots.txt", "robots.txt")]
    public void TryGetFile_FindsFilesByTheirExactPath(string requestPath, string expectedFile)
    {
        Assert.True(Create(DirectoryFormatFiles).TryGetFile(requestPath, out var relativePath));
        Assert.Equal(expectedFile, relativePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/about")]
    [InlineData("/about/")]
    [InlineData("/missing.html")]
    [InlineData("/.env")]
    [InlineData("/.hidden/secret.txt")]
    [InlineData("/../404.html")]
    [InlineData("//404.html")]
    public void TryGetFile_RejectsEverythingElse(string requestPath)
    {
        Assert.False(Create(DirectoryFormatFiles).TryGetFile(requestPath, out var relativePath));
        Assert.Null(relativePath);
    }

    [Fact]
    public void TryGetFile_WithABase_RequiresIt()
    {
        var resolver = Create(DirectoryFormatFiles, "/docs");

        Assert.True(resolver.TryGetFile("/docs/404.html", out var relativePath));
        Assert.Equal("404.html", relativePath);
        Assert.False(resolver.TryGetFile("/404.html", out _));
    }

    [Fact]
    public void GetHeaders_ReturnsWhatTheManifestRecordedForAFile()
    {
        var manifest = new AstroManifest
        {
            StaticHeaders =
            [
                new AstroStaticHeaders { File = "index.html", Headers = new Dictionary<string, string> { ["Content-Security-Policy"] = "script-src 'self'" } },
                new AstroStaticHeaders { File = "about/index.html", Headers = new Dictionary<string, string> { ["Content-Security-Policy"] = "default-src 'none'" } },
            ],
        };
        var resolver = new AstroStaticFileResolver(DirectoryFormatFiles, manifest);

        Assert.Equal("script-src 'self'", resolver.GetHeaders("index.html")?["Content-Security-Policy"]);
        Assert.Equal("default-src 'none'", resolver.GetHeaders("about/index.html")?["Content-Security-Policy"]);
    }

    [Fact]
    public void GetHeaders_IsEmptyForFilesWithoutHeaders()
    {
        var resolver = Create(DirectoryFormatFiles);

        Assert.Null(resolver.GetHeaders("index.html"));
        Assert.Null(resolver.GetHeaders("missing.html"));
    }

    [Fact]
    public void GetHeaders_KeepsTheLastEntryOfAFileThatIsListedTwice()
    {
        var manifest = new AstroManifest
        {
            StaticHeaders =
            [
                new AstroStaticHeaders { File = "index.html", Headers = new Dictionary<string, string> { ["X-Test"] = "first" } },
                new AstroStaticHeaders { File = "index.html", Headers = new Dictionary<string, string> { ["X-Test"] = "second" } },
            ],
        };

        var resolver = new AstroStaticFileResolver(DirectoryFormatFiles, manifest);

        Assert.Equal("second", resolver.GetHeaders("index.html")?["X-Test"]);
    }

    [Fact]
    public void FromDirectory_ReadsTheClientFolder()
    {
        using var dist = DistFolder.CreateDefault();

        var resolver = AstroStaticFileResolver.FromDirectory(dist.ClientPath, new AstroManifest());

        Assert.Equal("about/index.html", resolver.Resolve("/about").Path);
        Assert.Equal("_astro/app.abc123.js", resolver.Resolve("/_astro/app.abc123.js").Path);
        Assert.Equal(StaticFileMatchKind.File, resolver.Resolve("/.well-known/security.txt").Kind);
        Assert.Equal(StaticFileMatchKind.None, resolver.Resolve("/private/.env").Kind);
    }

    [Fact]
    public void FromDirectory_ThrowsWhenTheFolderIsMissing()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            AstroStaticFileResolver.FromDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), new AstroManifest()));
    }
}
