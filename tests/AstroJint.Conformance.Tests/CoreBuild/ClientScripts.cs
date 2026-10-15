using System.Text.RegularExpressions;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>The JavaScript files that a build emits for the browser (<c>dist/client</c>) and the imports between them.</summary>
internal static partial class ClientScripts
{
    /// <summary>Static imports of another chunk: <c>from "./chunk.js"</c>, with or without a query string.</summary>
    [GeneratedRegex("""from\s*["'`](\.\.?/[^"'`]+\.(?:js|mjs)(?:\?[^"'`]*)?)["'`]""")]
    public static partial Regex StaticImport { get; }

    /// <summary>Dynamic imports of another chunk: <c>import("./chunk.js")</c> (Rolldown writes template literals).</summary>
    [GeneratedRegex("""import\s*\(\s*["'`](\.\.?/[^"'`]+\.(?:js|mjs)(?:\?[^"'`]*)?)["'`]""")]
    public static partial Regex DynamicImport { get; }

    /// <summary>The scripts as path relative to <c>dist/client</c> (with forward slashes) and code.</summary>
    public static IEnumerable<(string Path, string Code)> Read(ConformanceSite site)
    {
        var client = Path.Combine(site.DistPath, "client");

        return Directory.EnumerateFiles(client, "*.js", SearchOption.AllDirectories)
            .Select(file => (Path.GetRelativePath(client, file).Replace('\\', '/'), File.ReadAllText(file)));
    }

    /// <summary>
    /// Requests the file that an import of the script at <paramref name="scriptPath"/> names, the way a browser resolves it
    /// (against the URL of the importing script, query string included), and checks that the host serves it as JavaScript.
    /// </summary>
    public static async Task AssertImportResolvesAsync(ConformanceSite site, string scriptPath, string import)
    {
        var target = new Uri(new Uri($"http://localhost/{scriptPath}"), import).PathAndQuery;
        using var response = await site.GetAsync(target);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, $"{import} imported by /{scriptPath} (requested as {target}) answered {(int)response.StatusCode}.");
        Assert.Contains("javascript", response.Content.Headers.ContentType?.ToString());
    }
}
