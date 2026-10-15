namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// What the tests of the incremental build share. Astro keeps what a build rendered in the cache of the fixture
/// (<c>node_modules/.astro</c>), and a later build of the same config takes the pages that did not change from there, so a
/// test builds a fixture twice and the second build depends on the first.
/// </summary>
internal static class IncrementalBuild
{
    /// <summary>
    /// Starts from nothing, as the upstream tests do with <c>dist</c> and the cache: the output of the earlier builds of the
    /// fixture and its cache are deleted, so that both builds of the test happen now, the second one into the cache of the first.
    /// </summary>
    public static void Reset(string fixture)
    {
        var root = Path.Combine(Repository.FixturesRoot, fixture);
        foreach (var entry in Directory.EnumerateFileSystemEntries(root, "dist*"))
        {
            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }

        if (Directory.Exists(CacheDirectory(fixture)))
        {
            Directory.Delete(CacheDirectory(fixture), recursive: true);
        }
    }

    public static string CacheDirectory(string fixture) => Path.Combine(Repository.FixturesRoot, fixture, "node_modules", ".astro");

    public static string CacheFile(string fixture) => Path.Combine(CacheDirectory(fixture), "incremental-build.json");

    /// <summary>The copy that the cache keeps of an output file, named relative to the output folder of the build.</summary>
    public static string CachedCopy(string fixture, string file) => Path.Combine(CacheDirectory(fixture), "dist", file);

    /// <summary>
    /// The config of the build with this number. The harness builds every config into a folder of its own, and the folder is
    /// not part of what Astro keys the cache by, so builds with different numbers write into one cache and not into one folder.
    /// The <c>cb</c> flags are not Astro config (see <c>support/cb/variant.mjs</c>).
    /// </summary>
    public static object Build(int number) => new { cb = new { build = number } };
}
