namespace AstroJint.AspNetCore.Tests;

/// <summary>
/// The resolver answers from tables that are built once. These tests hold it to the behavior of the plain
/// implementation that it replaced, which looked at the list of files for every request, over many kinds of builds
/// and of paths, including the ones that try to leave the client folder.
/// </summary>
public class AstroStaticFileResolverDifferentialTests
{
    private static readonly string[][] FileSets =
    [
        [
            "index.html", "about/index.html", "blog/index.html", "blog/hello/index.html", "404.html", "500.html",
            "_astro/app.abc.js", "_astro/Base.css", "favicon.svg", "robots.txt",
            ".well-known/security.txt", ".well-known/nested/deep.txt", "private/.env", ".hidden/index.html", "docs/.git/config",
        ],
        [
            "index.html", "about.html", "blog.html", "blog/index.html", "blog/hello.html", "blog/hello/index.html",
            "a.html", "a/index.html", "index/index.html", "x/y.html", "404.html", "_astro/a.js", "favicon.svg",
        ],
        [
            "a.html", "a/index.html", "a/b.html", "a/b/index.html", "index.html", "index/index.html", "index.html.html",
            "page.html/index.html", "dot.name.html", "dot.name/index.html", "UPPER.html", "upper/index.html", "sp ace.html",
            "pct%20x.html", "uni/\u00e9.html", ".html", "a/.html", "/index.html", "a//b.html", "a\\b.html", "nul\0.html",
            ".well-known.html", ".well-known/index.html", ".well-known", "_astro/x/y.js", "_astro", "_astroish/x.js",
            "docs/index.html", "docs.html", "docs/about/index.html",
        ],
        ["about/index.html", "_astro/a.js"],
        ["_astro/a.js", "favicon.svg"],
        [],
    ];

    private static readonly string[] Requests =
    [
        "/", "", "//", "///", "/index", "/index/", "/index.html", "/index.html/", "/about", "/about/", "/about.html",
        "/about/index.html", "/about/index", "/blog", "/blog/", "/blog/hello", "/blog/hello/", "/blog/hello.html",
        "/a", "/a/", "/a/b", "/a/b/", "/a.html", "/a/index.html", "/x/y", "/x/y/", "/x/y.html", "/x",
        "/404", "/404.html", "/404.html/", "/500.html", "/_astro/app.abc.js", "/_astro/Base.css", "/_astro/", "/_astro",
        "/_astro/x/y.js", "/_astroish/x.js", "/_astro/a.js", "/_astro/a.js/", "/favicon.svg", "/favicon.svg/", "/robots.txt",
        "/.well-known/security.txt", "/.well-known/security.txt/", "/.well-known/", "/.well-known",
        "/.well-known/nested/deep.txt", "/.well-known/index.html", "/.well-known.html", "/private/.env", "/.env",
        "/.hidden", "/.hidden/index.html", "/docs/.git/config", "/a/.html", "/.html", "/./about", "/about/.", "/about/./",
        "/../about", "/about/..", "/about/../index.html", "/a/../a", "/%2e%2e/about", "/%2E/about", "//about", "/about//",
        "/about//index.html", "//evil.example", "/\\about", "/about\\", "/a\\b", "/about\0", "/\0", "/nul\0.html",
        "/UPPER", "/upper", "/Upper", "/ABOUT", "/About/", "/sp ace", "/sp%20ace", "/pct%20x", "/uni/\u00e9", "/uni/\u00c9",
        "/docs", "/docs/", "/docs/about", "/docs/about/", "/docs/index", "/docsx", "/docsx/about", "/docs//about",
        "/docs/_astro/app.abc.js", "/DOCS", "/doc", "/docs/.well-known/security.txt", "/Docs", "/docs/sub", "/docs/sub/",
        "/docs/sub/about", "/docs.html", "/docs/index.html", "about", "x", "*", "docs",
    ];

    private static readonly string[] Segments =
        ["", ".", "..", "a", "b", "about", "index", "index.html", "x.html", ".well-known", ".git", "_astro", "docs", "UPPER", "sp ace", "%2e", "\\", "\0", "hello", "blog", "404.html"];

    public static TheoryData<string, string, string> Configurations()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var trailingSlash in new[] { "ignore", "never", "always" })
        {
            foreach (var buildFormat in new[] { "directory", "file", "preserve" })
            {
                foreach (var baseFolder in new[] { "/", "", "/docs", "/docs/", "docs", "/docs/sub" })
                {
                    data.Add(trailingSlash, buildFormat, baseFolder);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Resolve_AnswersLikeThePlainImplementation(string trailingSlash, string buildFormat, string baseFolder)
    {
        var mismatches = new List<string>();
        var random = new Random(20260101);

        foreach (var assetsDir in new[] { "_astro", "/assets/", "" })
        {
            var manifest = new AstroManifest { Base = baseFolder, TrailingSlash = trailingSlash, BuildFormat = buildFormat, AssetsDir = assetsDir };

            foreach (var files in FileSets)
            {
                var plain = new PlainResolver(files, manifest);
                var tables = new AstroStaticFileResolver(files, manifest);

                if (plain.Count != tables.Count)
                {
                    mismatches.Add($"Count: {plain.Count} != {tables.Count} ({string.Join(", ", files)})");
                }

                foreach (var path in PathsFor(files, baseFolder, random))
                {
                    var expected = plain.Resolve(path);
                    var actual = tables.Resolve(path);
                    if (expected != actual)
                    {
                        mismatches.Add($"Resolve({Escape(path)}) assets='{assetsDir}': {expected} != {actual} ({string.Join(", ", files)})");
                    }

                    var expectedFound = plain.TryGetFile(path, out var expectedFile);
                    var actualFound = tables.TryGetFile(path, out var actualFile);
                    if (expectedFound != actualFound || expectedFile != actualFile)
                    {
                        mismatches.Add($"TryGetFile({Escape(path)}): {expectedFound} '{expectedFile}' != {actualFound} '{actualFile}' ({string.Join(", ", files)})");
                    }

                    if (mismatches.Count > 20)
                    {
                        Assert.Fail(string.Join(Environment.NewLine, mismatches));
                    }
                }
            }
        }

        Assert.Empty(mismatches);
    }

    [Fact]
    public void Paths_AreTheServableFiles()
    {
        var manifest = new AstroManifest();
        var resolver = new AstroStaticFileResolver(
            ["index.html", "about/index.html", ".hidden/x.html", "a//b.html", "a\\b.html", ".well-known/x.txt", "index.html"],
            manifest);

        Assert.Equal([".well-known/x.txt", "about/index.html", "index.html"], resolver.Paths.Order(StringComparer.Ordinal));
        Assert.Equal(3, resolver.Count);
    }

    private static IEnumerable<string> PathsFor(string[] files, string baseFolder, Random random)
    {
        var prefix = baseFolder.Trim('/') is { Length: > 0 } trimmed ? "/" + trimmed : "";

        foreach (var request in Requests)
        {
            yield return request;
            yield return prefix + request;
        }

        // Paths that name the files of the build, which is where the matches are.
        foreach (var file in files)
        {
            var withoutExtension = file.EndsWith(".html", StringComparison.Ordinal) ? file[..^5] : file;
            var withoutIndex = file.EndsWith("/index.html", StringComparison.Ordinal) ? file[..^11] : file;

            foreach (var name in new[] { file, withoutExtension, withoutIndex })
            {
                yield return "/" + name;
                yield return "/" + name + "/";
                yield return prefix + "/" + name;
                yield return prefix + "/" + name + "/";
            }
        }

        for (var i = 0; i < 2000; i++)
        {
            var segments = Enumerable.Range(0, random.Next(1, 6)).Select(_ => Segments[random.Next(Segments.Length)]);
            var path = (random.Next(10) == 0 ? "" : "/") + string.Join('/', segments) + (random.Next(3) == 0 ? "/" : "");
            yield return random.Next(2) == 0 ? path : prefix + path;
        }
    }

    private static string Escape(string path) => path.Replace("\0", "\\0", StringComparison.Ordinal);

    /// <summary>The implementation before the tables: every request is checked against the set of files, as it comes.</summary>
    private sealed class PlainResolver
    {
        private readonly HashSet<string> _files;
        private readonly string _base;
        private readonly string _assetsPrefix;
        private readonly string _trailingSlash;
        private readonly string _buildFormat;

        public PlainResolver(IEnumerable<string> files, AstroManifest manifest)
        {
            _files = new HashSet<string>(files.Where(IsServable), StringComparer.Ordinal);
            _base = manifest.Base is "/" or "" ? "" : "/" + manifest.Base.Trim('/');
            _assetsPrefix = manifest.AssetsDir.Trim('/') + "/";
            _trailingSlash = manifest.TrailingSlash;
            _buildFormat = manifest.BuildFormat;
        }

        public int Count => _files.Count;

        public StaticFileMatch Resolve(string requestPath)
        {
            if (!TryGetRelativePath(requestPath, out var relative))
            {
                return default;
            }

            var hasTrailingSlash = requestPath.Length > 1 && requestPath[^1] == '/';

            if (!hasTrailingSlash && relative.Length > 0 && _files.Contains(relative))
            {
                return new StaticFileMatch(StaticFileMatchKind.File, relative, relative.StartsWith(_assetsPrefix, StringComparison.Ordinal));
            }

            var page = FindPage(relative);
            if (page is null)
            {
                return default;
            }

            var isHome = relative.Length == 0 && _base.Length == 0;
            if (!isHome)
            {
                if (_trailingSlash == "never" && hasTrailingSlash)
                {
                    return new StaticFileMatch(StaticFileMatchKind.Redirect, requestPath.TrimEnd('/'));
                }

                if (_trailingSlash == "always" && !hasTrailingSlash)
                {
                    return new StaticFileMatch(StaticFileMatchKind.Redirect, requestPath + "/");
                }
            }

            return new StaticFileMatch(StaticFileMatchKind.File, page);
        }

        public bool TryGetFile(string requestPath, out string? relativePath)
        {
            relativePath = null;
            if (!TryGetRelativePath(requestPath, out var relative) || relative.Length == 0 || requestPath[^1] == '/')
            {
                return false;
            }

            if (!_files.Contains(relative))
            {
                return false;
            }

            relativePath = relative;
            return true;
        }

        private string? FindPage(string relative)
        {
            if (relative.Length == 0)
            {
                return _files.Contains("index.html") ? "index.html" : null;
            }

            var directoryIndex = relative + "/index.html";
            if (_buildFormat == "directory")
            {
                return _files.Contains(directoryIndex) ? directoryIndex : null;
            }

            var file = relative + ".html";
            return _files.Contains(file) ? file
                : _files.Contains(directoryIndex) ? directoryIndex
                : null;
        }

        private bool TryGetRelativePath(string requestPath, out string relative)
        {
            relative = "";
            if (requestPath.Length == 0 || requestPath[0] != '/' || requestPath.Contains("//", StringComparison.Ordinal))
            {
                return false;
            }

            var rest = requestPath;
            if (_base.Length > 0)
            {
                if (!requestPath.StartsWith(_base, StringComparison.Ordinal))
                {
                    return false;
                }

                rest = requestPath[_base.Length..];
                if (rest.Length > 0 && rest[0] != '/')
                {
                    return false;
                }
            }

            var trimmed = rest.Trim('/');
            if (trimmed.Length > 0 && !IsServable(trimmed))
            {
                return false;
            }

            relative = trimmed;
            return true;
        }

        private static bool IsServable(string relativePath)
        {
            if (relativePath.Contains('\\') || relativePath.Contains('\0'))
            {
                return false;
            }

            var first = true;
            foreach (var segment in relativePath.Split('/'))
            {
                if (segment.Length == 0 || (segment[0] == '.' && !(first && segment == ".well-known")))
                {
                    return false;
                }

                first = false;
            }

            return true;
        }
    }
}
