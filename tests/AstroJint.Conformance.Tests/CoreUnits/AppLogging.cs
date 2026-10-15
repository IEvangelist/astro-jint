using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// What the loggers of Astro write inside the host: the <c>console</c> of the script has no process streams, it feeds the log of
/// the host under its own category, so the log of the site is where a destination of the logger can be observed. The page
/// <c>/logger/&lt;message&gt;</c> of the <c>app-render</c> fixture writes its last segment through <c>Astro.logger.info()</c>, which
/// lets every test look for a line of its own.
/// </summary>
internal static class AppLogging
{
    private const string ConsoleCategory = "AstroJint.Console";

    /// <summary>The URL of the page that writes <paramref name="message"/> through <c>Astro.logger</c>.</summary>
    public static string LoggerPage(string message) => $"/logger/{Uri.EscapeDataString(message)}";

    /// <summary>The log entries that the <c>console</c> of the script wrote.</summary>
    public static IEnumerable<LogEntry> ConsoleEntries(this ConformanceSite site) =>
        site.Logs.Where(entry => entry.Category == ConsoleCategory);

    /// <summary>The one entry that the console destination of the logger wrote for <paramref name="message"/>: a time, then the text.</summary>
    public static LogEntry ConsoleLine(this ConformanceSite site, string message) =>
        Assert.Single(site.ConsoleEntries(), entry => entry.Message.EndsWith(message, StringComparison.Ordinal));

    /// <summary>
    /// The one entry that the JSON destination of the logger (<c>astro/logger/json</c>) wrote for <paramref name="message"/>, with
    /// the object it carries: <c>{ "message", "label", "level" }</c>.
    /// </summary>
    public static (LogEntry Entry, JsonElement Payload) JsonLine(this ConformanceSite site, string message)
    {
        var lines = site.ConsoleEntries()
            .Where(entry => entry.Message.StartsWith('{'))
            .Select(entry => (Entry: entry, Payload: JsonDocument.Parse(entry.Message).RootElement))
            .ToList();
        return Assert.Single(lines, line => line.Payload.TryGetProperty("message", out var text) && text.GetString() == message);
    }
}
