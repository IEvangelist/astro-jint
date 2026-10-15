using Microsoft.AspNetCore.StaticFiles;

namespace AstroJint.AspNetCore;

/// <summary>What the response for a prerendered file says about the file, shared by the paths that serve it.</summary>
internal static class StaticFileConventions
{
    public const string ImmutableCacheControl = "public, max-age=31536000, immutable";
    public const string RevalidateCacheControl = "public, max-age=0, must-revalidate";

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static string GetContentType(string fileName)
    {
        if (!ContentTypes.TryGetContentType(fileName, out var contentType))
        {
            return "application/octet-stream";
        }

        var isText = contentType.StartsWith("text/", StringComparison.Ordinal)
            || contentType is "application/json" or "application/javascript" or "application/xml"
            || contentType.EndsWith("+json", StringComparison.Ordinal)
            || contentType.EndsWith("+xml", StringComparison.Ordinal);

        return isText ? contentType + "; charset=utf-8" : contentType;
    }

    /// <summary>HTTP dates have a resolution of one second, so that is all the validators of a file can tell apart.</summary>
    public static DateTimeOffset GetLastModified(DateTime lastWriteTimeUtc) =>
        new(lastWriteTimeUtc.Ticks - (lastWriteTimeUtc.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

    /// <summary>The entity tag, including its quotes, of a file with this modification time and length.</summary>
    public static string GetETag(DateTimeOffset lastModified, long length) => $"\"{lastModified.ToFileTime() ^ length:x}\"";
}
