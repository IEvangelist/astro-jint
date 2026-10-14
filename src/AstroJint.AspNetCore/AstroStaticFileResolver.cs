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
internal sealed class AstroStaticFileResolver
{
    private readonly HashSet<string> _files;
    private readonly string _base;
    private readonly string _assetsPrefix;
    private readonly string _trailingSlash;
    private readonly string _buildFormat;

    /// <param name="files">Paths relative to the client folder, using <c>/</c> as the separator.</param>
    /// <param name="manifest">The Astro build manifest, which carries <c>base</c>, <c>trailingSlash</c>, <c>build.format</c> and the assets folder.</param>
    public AstroStaticFileResolver(IEnumerable<string> files, AstroManifest manifest)
    {
        _files = new HashSet<string>(files.Where(IsServable), StringComparer.Ordinal);
        _base = manifest.Base is "/" or "" ? "" : "/" + manifest.Base.Trim('/');
        _assetsPrefix = manifest.AssetsDir.Trim('/') + "/";
        _trailingSlash = manifest.TrailingSlash;
        _buildFormat = manifest.BuildFormat;
    }

    public int Count => _files.Count;

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

    /// <summary>Finds a file by its exact request path, without page conventions. Used for prerendered error pages.</summary>
    public bool TryGetFile(string requestPath, [NotNullWhen(true)] out string? relativePath)
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

    // Strips the base and the surrounding slashes. Anything that could escape the client folder or that
    // addresses a hidden file (except /.well-known) is rejected up front. Doubled slashes are rejected too:
    // Trim() would hide them, and a redirect to "//host" is a protocol-relative URL that leaves the site.
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
