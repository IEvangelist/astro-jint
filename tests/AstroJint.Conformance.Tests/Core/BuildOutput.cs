using System.Text.RegularExpressions;

namespace AstroJint.Conformance.Core;

/// <summary>Reads the bundle that a fixture build produced, for the tests that check what ended up in it.</summary>
internal static partial class BuildOutput
{
    /// <summary>The text of every script (<c>.mjs</c>, <c>.js</c>, <c>.cjs</c>) under the output directory, client and server.</summary>
    public static IEnumerable<string> ReadScripts(ConformanceSite site) =>
        Directory.EnumerateFiles(site.DistPath, "*", SearchOption.AllDirectories)
            .Where(file => Path.GetExtension(file) is ".mjs" or ".js" or ".cjs")
            .Select(File.ReadAllText);

    /// <summary>
    /// Whether the build bundled unstorage, whose top-level export is <c>createStorage</c>, and the session runtime, which is
    /// the <c>AstroSession</c> class.
    /// </summary>
    public static (bool Unstorage, bool SessionRuntime) FindSessionRuntime(ConformanceSite site)
    {
        var unstorage = false;
        var sessionRuntime = false;
        foreach (var script in ReadScripts(site))
        {
            unstorage |= CreateStorage().IsMatch(script);
            sessionRuntime |= AstroSessionClass().IsMatch(script);
        }

        return (unstorage, sessionRuntime);
    }

    [GeneratedRegex(@"\bcreateStorage\b")]
    private static partial Regex CreateStorage();

    [GeneratedRegex(@"class AstroSession\b")]
    private static partial Regex AstroSessionClass();
}
