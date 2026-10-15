using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace AstroJint.LoadTest;

internal sealed record ServerUsage(double ServerCores, double GeneratorCores, long ServerWorkingSetBytes);

internal sealed record LoadResult(
    string Scenario,
    int Connections,
    double Seconds,
    long Requests,
    long Errors,
    IReadOnlyDictionary<int, long> Failures,
    double AverageBytes,
    LatencyHistogram Latency,
    ServerUsage? Usage);

/// <summary>
/// Opens the connections, sends the requests of a scenario for as long as the measurement lasts, and sums up what the
/// connections saw. A request counts for the measurement when it starts after the warm-up.
/// </summary>
internal static class LoadRunner
{
    public static async Task<LoadResult> RunAsync(LoadOptions options, string scenario, LoadRequest[] requests, Process? server)
    {
        var endPoint = await ResolveAsync(options.Url).ConfigureAwait(false);
        await ProbeAsync(endPoint, requests).ConfigureAwait(false);

        var now = Stopwatch.GetTimestamp();
        var measureStart = now + (long)(options.Warmup.TotalSeconds * Stopwatch.Frequency);
        var end = measureStart + (long)(options.Duration.TotalSeconds * Stopwatch.Frequency);

        var workers = new Worker[options.Connections];
        var tasks = new Task[workers.Length];
        for (var i = 0; i < workers.Length; i++)
        {
            var worker = workers[i] = new Worker();
            var offset = i;
            tasks[i] = Task.Run(() => worker.RunAsync(endPoint, requests, offset, measureStart, end));
        }

        var usage = MeasureUsageAsync(server, measureStart, end);
        await Task.WhenAll(tasks).ConfigureAwait(false);

        var latency = new LatencyHistogram();
        var failures = new Dictionary<int, long>();
        long errors = 0;
        long bytes = 0;
        foreach (var worker in workers)
        {
            latency.Add(worker.Latency);
            errors += worker.Errors;
            bytes += worker.Bytes;
            foreach (var (status, count) in worker.Failures)
            {
                failures[status] = failures.GetValueOrDefault(status) + count;
            }
        }

        var seconds = options.Duration.TotalSeconds;
        return new LoadResult(
            scenario,
            options.Connections,
            seconds,
            latency.Count,
            errors,
            failures,
            latency.Count == 0 ? 0 : (double)bytes / latency.Count,
            latency,
            await usage.ConfigureAwait(false));
    }

    private static async Task<IPEndPoint> ResolveAsync(Uri url)
    {
        var addresses = await Dns.GetHostAddressesAsync(url.Host).ConfigureAwait(false);
        var address = Array.Find(addresses, static a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
        return new IPEndPoint(address, url.Port);
    }

    /// <summary>Sends every request once, so that a scenario that the server answers differently is reported before it is measured.</summary>
    private static async Task ProbeAsync(IPEndPoint endPoint, LoadRequest[] requests)
    {
        using var connection = new HttpConnection(endPoint);
        foreach (var request in requests)
        {
            var (status, _) = await connection.SendAsync(request.Bytes).ConfigureAwait(false);
            if (status != request.ExpectedStatus)
            {
                throw new InvalidOperationException($"{request.Description} answered {(status == 0 ? "nothing usable" : status)}; expected {request.ExpectedStatus}. Is the server running and the site built?");
            }
        }
    }

    private static async Task<ServerUsage?> MeasureUsageAsync(Process? server, long measureStart, long end)
    {
        if (server is null)
        {
            return null;
        }

        using var generator = Process.GetCurrentProcess();

        await DelayUntilAsync(measureStart).ConfigureAwait(false);
        server.Refresh();
        var serverStart = server.TotalProcessorTime;
        var generatorStart = generator.TotalProcessorTime;
        var wallStart = Stopwatch.GetTimestamp();

        await DelayUntilAsync(end).ConfigureAwait(false);
        server.Refresh();
        generator.Refresh();
        var wall = Stopwatch.GetElapsedTime(wallStart).TotalSeconds;
        return new ServerUsage(
            (server.TotalProcessorTime - serverStart).TotalSeconds / wall,
            (generator.TotalProcessorTime - generatorStart).TotalSeconds / wall,
            server.WorkingSet64);
    }

    private static async Task DelayUntilAsync(long timestamp)
    {
        var remaining = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), timestamp);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining).ConfigureAwait(false);
        }
    }

    private sealed class Worker
    {
        public LatencyHistogram Latency { get; } = new();

        public long Errors { get; private set; }

        public long Bytes { get; private set; }

        public Dictionary<int, long> Failures { get; } = [];

        public async Task RunAsync(IPEndPoint endPoint, LoadRequest[] requests, int offset, long measureStart, long end)
        {
            using var connection = new HttpConnection(endPoint);
            for (var next = offset; ; next++)
            {
                var request = requests[next % requests.Length];
                var started = Stopwatch.GetTimestamp();
                if (started >= end)
                {
                    return;
                }

                var (status, bytes) = await connection.SendAsync(request.Bytes).ConfigureAwait(false);
                if (started < measureStart)
                {
                    continue;
                }

                if (status == request.ExpectedStatus)
                {
                    Latency.Record((long)(Stopwatch.GetElapsedTime(started).TotalMicroseconds));
                    Bytes += bytes;
                }
                else
                {
                    Errors++;
                    Failures[status] = Failures.GetValueOrDefault(status) + 1;
                }
            }
        }
    }
}
