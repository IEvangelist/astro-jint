namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// The shared fixture <c>cf-content</c>, which has the pages of several small upstream fixtures in one build: content
/// collections, nested <c>.astro</c> imports, <c>Code</c> and <c>Prism</c>, an action and a prerendered 404 page.
/// </summary>
internal static class CfContent
{
    public const string Fixture = "cf-content";

    /// <summary>The folder of the fixture, where the harness builds in a <c>dist</c> folder of its own.</summary>
    public static string RootOf(ConformanceSite site) =>
        Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(site.DistPath))!;

    /// <summary>The lines of the log of a build that have all of the given texts.</summary>
    public static IEnumerable<string> BuildLogLines(ConformanceSite site, params string[] texts) =>
        site.BuildLog.Split('\n').Where(line => texts.All(text => line.Contains(text, StringComparison.Ordinal)));

    /// <summary>The status, the body and the warnings and errors of the host's log, for the message of a failed assertion.</summary>
    public static string Describe(ConformanceSite site, HttpResponseMessage response, string body)
    {
        var problems = site.Logs
            .Where(entry => entry.Level >= Microsoft.Extensions.Logging.LogLevel.Warning)
            .Select(entry => $"[{entry.Level}] {entry.Category}: {entry.Message}{(entry.Exception is null ? "" : Environment.NewLine + entry.Exception)}");
        return $"{(int)response.StatusCode}: {body}{Environment.NewLine}{string.Join(Environment.NewLine, problems)}";
    }
}
