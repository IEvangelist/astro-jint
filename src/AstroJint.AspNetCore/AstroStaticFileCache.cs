using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace AstroJint.AspNetCore;

/// <summary>A prerendered file that is kept in memory, with everything that its response says formatted in advance.</summary>
internal sealed class CachedStaticFile
{
    public CachedStaticFile(byte[] content, string contentType, DateTimeOffset lastModified, KeyValuePair<string, StringValues>[] headers)
    {
        Content = content;
        ContentType = contentType;
        LastModifiedValue = lastModified;
        LastModified = HeaderUtilities.FormatDate(lastModified);
        ETag = StaticFileConventions.GetETag(lastModified, content.Length);
        EntityTag = new EntityTagHeaderValue(ETag);
        Headers = headers;
    }

    public byte[] Content { get; }

    public string ContentType { get; }

    /// <summary>The entity tag with its quotes, as it is sent.</summary>
    public string ETag { get; }

    public EntityTagHeaderValue EntityTag { get; }

    /// <summary>The <c>Last-Modified</c> date as it is sent.</summary>
    public string LastModified { get; }

    public DateTimeOffset LastModifiedValue { get; }

    /// <summary>The headers that Astro rendered for the file at build time, such as its <c>Content-Security-Policy</c>.</summary>
    public KeyValuePair<string, StringValues>[] Headers { get; }
}

/// <summary>
/// The prerendered files of a build that fit in a memory budget, read once when the host starts. A request for one of
/// them is answered without a single file system call. The smallest files are kept first, so that as many pages as
/// possible are covered; a file that did not make it is still served, from disk.
/// </summary>
internal sealed class AstroStaticFileCache
{
    /// <summary>A file above this size is left to the file system, which sends it without copying it through memory.</summary>
    public const long MaxFileBytes = 1024 * 1024;

    private readonly FrozenDictionary<string, CachedStaticFile> _files;

    private AstroStaticFileCache(FrozenDictionary<string, CachedStaticFile> files, long bytes)
    {
        _files = files;
        Bytes = bytes;
    }

    public static AstroStaticFileCache Empty { get; } = new(FrozenDictionary<string, CachedStaticFile>.Empty, 0);

    public int Count => _files.Count;

    /// <summary>Total size of the cached content.</summary>
    public long Bytes { get; }

    /// <param name="relativePath">A path relative to the client folder, as in <see cref="StaticFileMatch.Path"/>.</param>
    /// <param name="file">The file, when it is held in memory.</param>
    public bool TryGet(string relativePath, [NotNullWhen(true)] out CachedStaticFile? file) => _files.TryGetValue(relativePath, out file);

    /// <param name="resolver">Decides which files are served at all.</param>
    /// <param name="clientRoot">The folder that the files of <paramref name="resolver"/> are relative to.</param>
    /// <param name="budgetBytes">Upper bound for the total size of the cached content. Zero or less keeps nothing.</param>
    public static AstroStaticFileCache Load(AstroStaticFileResolver resolver, string clientRoot, long budgetBytes)
    {
        if (budgetBytes <= 0)
        {
            return Empty;
        }

        var candidates = new List<Candidate>(resolver.Paths.Length);
        foreach (var relativePath in resolver.Paths)
        {
            try
            {
                var info = new FileInfo(Path.Combine(clientRoot, relativePath));
                if (info.Exists && info.Length <= MaxFileBytes)
                {
                    candidates.Add(new Candidate(relativePath, info.FullName, info.Length, info.LastWriteTimeUtc));
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        // The tie-break keeps the choice, and so the behavior of the host, the same from one start to the next.
        candidates.Sort(static (a, b) => a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a.RelativePath, b.RelativePath));

        var files = new Dictionary<string, CachedStaticFile>(candidates.Count, StringComparer.Ordinal);
        var used = 0L;
        foreach (var candidate in candidates)
        {
            // Sorted by size: when this file does not fit, none of the remaining ones does.
            if (used + candidate.Length > budgetBytes)
            {
                break;
            }

            byte[] content;
            try
            {
                content = File.ReadAllBytes(candidate.FullName);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            // The file changed while the host was starting; its length and date no longer describe what was read.
            if (content.Length != candidate.Length)
            {
                continue;
            }

            used += content.Length;
            files.Add(candidate.RelativePath, new CachedStaticFile(
                content,
                StaticFileConventions.GetContentType(candidate.RelativePath),
                StaticFileConventions.GetLastModified(candidate.LastWriteTimeUtc),
                ToHeaders(resolver.GetHeaders(candidate.RelativePath))));
        }

        return new AstroStaticFileCache(files.ToFrozenDictionary(StringComparer.Ordinal), used);
    }

    private static KeyValuePair<string, StringValues>[] ToHeaders(IReadOnlyDictionary<string, string>? headers) =>
        headers is null ? [] : [.. headers.Select(static header => KeyValuePair.Create(header.Key, new StringValues(header.Value)))];

    private readonly record struct Candidate(string RelativePath, string FullName, long Length, DateTime LastWriteTimeUtc);
}
