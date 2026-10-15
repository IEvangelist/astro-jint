using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace AstroJint.AspNetCore;

internal enum StaticFileMatchKind
{
    /// <summary>Not a static file; the request belongs to Astro's on-demand runtime.</summary>
    None,

    /// <summary>Serve <see cref="StaticFileMatch.Path"/>, a file path relative to the client folder.</summary>
    File,

    /// <summary>Send a 301 to <see cref="StaticFileMatch.Path"/>, a request path.</summary>
    Redirect,
}

internal readonly record struct StaticFileMatch(StaticFileMatchKind Kind, string? Path = null, bool Immutable = false);

/// <summary>
/// Maps request paths to the files Astro prerendered into <c>dist/client</c>. It works from an in-memory list of
/// the files that exist, so a request can never reach anything that was not part of the build output, and a
/// request for an on-demand page costs a few hash lookups instead of file system calls.
/// </summary>
/// <remarks>
/// Every way a request path can name a file is worked out once, when the resolver is created: the files by their
/// path, and the pages (<c>/about</c>, <c>/about/</c>) by the file that answers for them. A request is then looked up
/// as a slice of its own text, so that resolving a path to a file allocates nothing. The tables only hold names of
/// servable files, so a path that tries to leave the client folder or to reach a hidden file is simply not in them.
/// </remarks>
internal sealed class AstroStaticFileResolver
{
    private readonly FrozenDictionary<string, StaticFileMatch> _files;
    private readonly FrozenDictionary<string, StaticFileMatch>.AlternateLookup<ReadOnlySpan<char>> _fileLookup;
    private readonly FrozenDictionary<string, StaticFileMatch>.AlternateLookup<ReadOnlySpan<char>> _pageLookup;
    private readonly StaticFileMatch _home;
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _headers = new(StringComparer.Ordinal);
    private readonly string _base;
    private readonly TrailingSlashMode _trailingSlash;

    private enum TrailingSlashMode
    {
        Ignore,
        Never,
        Always,
    }

    /// <param name="files">Paths relative to the client folder, using <c>/</c> as the separator.</param>
    /// <param name="manifest">The Astro build manifest, which carries <c>base</c>, <c>trailingSlash</c>, <c>build.format</c>, the assets folder and the headers of prerendered pages.</param>
    public AstroStaticFileResolver(IEnumerable<string> files, AstroManifest manifest)
    {
        _base = manifest.Base is "/" or "" ? "" : "/" + manifest.Base.Trim('/');
        _trailingSlash = manifest.TrailingSlash switch
        {
            "never" => TrailingSlashMode.Never,
            "always" => TrailingSlashMode.Always,
            _ => TrailingSlashMode.Ignore,
        };

        var assetsPrefix = manifest.AssetsDir.Trim('/') + "/";
        var exact = new Dictionary<string, StaticFileMatch>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (IsServable(file))
            {
                exact[file] = new StaticFileMatch(StaticFileMatchKind.File, file, file.StartsWith(assetsPrefix, StringComparison.Ordinal));
            }
        }

        // The file that answers for a page. In the directory format that is the index file of the folder. Otherwise
        // a file named after the page takes precedence over the index file of a folder of that name.
        var pages = new Dictionary<string, StaticFileMatch>(StringComparer.Ordinal);
        if (manifest.BuildFormat != "directory")
        {
            AddPages(pages, exact.Keys, ".html");
        }

        AddPages(pages, exact.Keys, "/index.html");

        _home = exact.TryGetValue("index.html", out var home) ? new StaticFileMatch(StaticFileMatchKind.File, home.Path) : default;
        _files = exact.ToFrozenDictionary(StringComparer.Ordinal);
        _fileLookup = _files.GetAlternateLookup<ReadOnlySpan<char>>();
        _pageLookup = pages.ToFrozenDictionary(StringComparer.Ordinal).GetAlternateLookup<ReadOnlySpan<char>>();

        foreach (var entry in manifest.StaticHeaders)
        {
            _headers[entry.File] = entry.Headers;
        }
    }

    public int Count => _files.Count;

    /// <summary>The paths of the servable files, relative to the client folder.</summary>
    public ImmutableArray<string> Paths => _files.Keys;

    /// <summary>The response headers that Astro rendered for a prerendered file, such as its <c>Content-Security-Policy</c>.</summary>
    /// <param name="relativePath">A path relative to the client folder, as in <see cref="StaticFileMatch.Path"/>.</param>
    public IReadOnlyDictionary<string, string>? GetHeaders(string relativePath) => _headers.GetValueOrDefault(relativePath);

    public static AstroStaticFileResolver FromDirectory(string clientRoot, AstroManifest manifest)
    {
        if (!Directory.Exists(clientRoot))
        {
            throw new DirectoryNotFoundException($"The Astro client folder '{clientRoot}' does not exist.");
        }

        var files = Directory
            .EnumerateFiles(clientRoot, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(clientRoot, file).Replace('\\', '/'));

        return new AstroStaticFileResolver(files, manifest);
    }

    /// <param name="requestPath">The decoded path of the request, including any path base and the Astro <c>base</c>.</param>
    public StaticFileMatch Resolve(string requestPath)
    {
        if (!TryGetRelativePath(requestPath, out var relative))
        {
            return default;
        }

        var hasTrailingSlash = requestPath.Length > 1 && requestPath[^1] == '/';

        if (!hasTrailingSlash && relative.Length > 0 && _fileLookup.TryGetValue(relative, out var file))
        {
            return file;
        }

        StaticFileMatch page;
        if (relative.Length == 0)
        {
            page = _home;
        }
        else if (!_pageLookup.TryGetValue(relative, out page))
        {
            return default;
        }

        if (page.Kind == StaticFileMatchKind.None)
        {
            return default;
        }

        var isHome = relative.Length == 0 && _base.Length == 0;
        if (!isHome)
        {
            if (_trailingSlash == TrailingSlashMode.Never && hasTrailingSlash)
            {
                return new StaticFileMatch(StaticFileMatchKind.Redirect, requestPath.TrimEnd('/'));
            }

            if (_trailingSlash == TrailingSlashMode.Always && !hasTrailingSlash)
            {
                return new StaticFileMatch(StaticFileMatchKind.Redirect, requestPath + "/");
            }
        }

        return page;
    }

    /// <summary>Finds a file by its exact request path, without page conventions. Used for prerendered error pages.</summary>
    public bool TryGetFile(string requestPath, [NotNullWhen(true)] out string? relativePath)
    {
        relativePath = null;
        if (!TryGetRelativePath(requestPath, out var relative) || relative.Length == 0 || requestPath[^1] == '/')
        {
            return false;
        }

        if (!_fileLookup.TryGetValue(relative, out var match) || match.Path is not { } path)
        {
            return false;
        }

        relativePath = path;
        return true;
    }

    // A page is found by its path without the extension of its file, and the first file to claim a path keeps it.
    private static void AddPages(Dictionary<string, StaticFileMatch> pages, IEnumerable<string> files, string suffix)
    {
        foreach (var file in files)
        {
            if (file.Length > suffix.Length && file.EndsWith(suffix, StringComparison.Ordinal))
            {
                pages.TryAdd(file[..^suffix.Length], new StaticFileMatch(StaticFileMatchKind.File, file));
            }
        }
    }

    // Strips the base and the surrounding slashes. A path that tries to leave the client folder, or that addresses
    // a hidden file (except /.well-known), is not in the tables, because they are built from servable files only.
    // Doubled slashes are rejected up front: Trim() would hide them, and a redirect to "//host" is a
    // protocol-relative URL that leaves the site.
    private bool TryGetRelativePath(string requestPath, out ReadOnlySpan<char> relative)
    {
        relative = default;
        if (requestPath.Length == 0 || requestPath[0] != '/' || requestPath.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        var rest = requestPath.AsSpan();
        if (_base.Length > 0)
        {
            if (!rest.StartsWith(_base, StringComparison.Ordinal))
            {
                return false;
            }

            rest = rest[_base.Length..];
            if (rest.Length > 0 && rest[0] != '/')
            {
                return false;
            }
        }

        relative = rest.Trim('/');
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
