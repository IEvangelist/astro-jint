using System.Globalization;
using System.Text;

namespace AstroJint.LoadTest;

/// <summary>The results as a Markdown table, so that a run can be pasted into a document as it is.</summary>
internal static class Report
{
    public static string Header(bool withUsage)
    {
        var header = new StringBuilder("| Scenario | Connections | Requests/s | p50 | p90 | p99 | max | Errors | Bytes/response |");
        var rule = new StringBuilder("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        if (withUsage)
        {
            header.Append(" Server cores | Generator cores | Server memory |");
            rule.Append("---:|---:|---:|");
        }

        return header.AppendLine().Append(rule).ToString();
    }

    public static string Row(LoadResult result)
    {
        var culture = CultureInfo.InvariantCulture;
        var latency = result.Latency;
        var row = new StringBuilder()
            .Append("| ").Append(result.Scenario)
            .Append(" | ").Append(result.Connections.ToString(culture))
            .Append(" | ").Append((result.Requests / result.Seconds).ToString("N0", culture))
            .Append(" | ").Append(Duration(latency.Percentile(50)))
            .Append(" | ").Append(Duration(latency.Percentile(90)))
            .Append(" | ").Append(Duration(latency.Percentile(99)))
            .Append(" | ").Append(Duration(latency.MaxMicroseconds))
            .Append(" | ").Append(Errors(result))
            .Append(" | ").Append(result.AverageBytes.ToString("N0", culture))
            .Append(" |");
        if (result.Usage is { } usage)
        {
            row.Append(' ').Append(usage.ServerCores.ToString("F1", culture))
                .Append(" | ").Append(usage.GeneratorCores.ToString("F1", culture))
                .Append(" | ").Append((usage.ServerWorkingSetBytes / (1024d * 1024)).ToString("F0", culture)).Append(" MB |");
        }

        return row.ToString();
    }

    private static string Duration(long microseconds) =>
        microseconds < 1000
            ? microseconds.ToString(CultureInfo.InvariantCulture) + " us"
            : (microseconds / 1000d).ToString(microseconds < 100_000 ? "F2" : "F0", CultureInfo.InvariantCulture) + " ms";

    private static string Errors(LoadResult result) =>
        result.Errors == 0
            ? "0"
            : result.Errors.ToString(CultureInfo.InvariantCulture) + " (" + string.Join(", ", result.Failures.OrderBy(static f => f.Key).Select(static f => (f.Key == 0 ? "connection" : f.Key.ToString(CultureInfo.InvariantCulture)) + " x" + f.Value.ToString(CultureInfo.InvariantCulture))) + ")";
}
