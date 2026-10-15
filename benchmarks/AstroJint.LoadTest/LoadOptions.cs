using System.Globalization;

namespace AstroJint.LoadTest;

internal sealed record LoadOptions(Uri Url, int Connections, TimeSpan Warmup, TimeSpan Duration, string Scenario, int? ServerProcessId, string? GcStatsPath)
{
    public const string Usage = """
        Usage: AstroJint.LoadTest [--url <base address>] [--scenario <name>|all] [--connections <n>] [--duration <seconds>] [--warmup <seconds>] [--pid <server process id>] [--gc-stats <path>]

        Defaults: --url http://127.0.0.1:5199 --scenario static-page --connections 64 --duration 10 --warmup 3
        The server is started separately, so that its runtime settings are the ones that are measured.
        With --pid the report adds the processor time and the memory that the server used while it was measured.
        With --gc-stats the report adds the garbage collections of the server while it was measured. The path (the sample host
        has /api/host) answers JSON with a "gc" object of gen0, gen1, gen2, pauseMs and allocatedBytes.
        """;

    public static LoadOptions Parse(string[] args)
    {
        var url = new Uri("http://127.0.0.1:5199");
        var connections = 64;
        var warmup = 3d;
        var duration = 10d;
        var scenario = "static-page";
        int? processId = null;
        string? gcStatsPath = null;

        for (var i = 0; i < args.Length; i += 2)
        {
            var name = args[i];
            var value = i + 1 < args.Length ? args[i + 1] : throw new ArgumentException($"{name} needs a value.");
            switch (name)
            {
                case "--url":
                    url = Uri.TryCreate(value, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttp
                        ? parsed
                        : throw new ArgumentException($"--url must be an absolute http address, not '{value}'.");
                    break;
                case "--scenario":
                    scenario = value;
                    break;
                case "--connections":
                    connections = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count > 0
                        ? count
                        : throw new ArgumentException($"--connections must be a positive number, not '{value}'.");
                    break;
                case "--duration":
                    duration = ParseSeconds(name, value, minimum: 1);
                    break;
                case "--warmup":
                    warmup = ParseSeconds(name, value, minimum: 0);
                    break;
                case "--pid":
                    processId = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && pid > 0
                        ? pid
                        : throw new ArgumentException($"--pid must be a process id, not '{value}'.");
                    break;
                case "--gc-stats":
                    gcStatsPath = value.StartsWith('/')
                        ? value
                        : throw new ArgumentException($"--gc-stats must be a path that starts with '/', not '{value}'.");
                    break;
                default:
                    throw new ArgumentException($"Unknown option {name}.");
            }
        }

        return new LoadOptions(url, connections, TimeSpan.FromSeconds(warmup), TimeSpan.FromSeconds(duration), scenario, processId, gcStatsPath);
    }

    private static double ParseSeconds(string name, string value, double minimum) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= minimum
            ? seconds
            : throw new ArgumentException($"{name} must be a number of seconds, at least {minimum}, not '{value}'.");
}
