using System.Diagnostics;
using AstroJint.LoadTest;

// Drives a running host with keep-alive connections and reports throughput and latency.
//   dotnet run -c Release --project benchmarks/AstroJint.LoadTest -- --scenario all --pid <server process id>
LoadOptions options;
try
{
    options = LoadOptions.Parse(args);
}
catch (ArgumentException ex)
{
    await Console.Error.WriteLineAsync(ex.Message + Environment.NewLine + Environment.NewLine + LoadOptions.Usage);
    return 2;
}

try
{
    using var host = options.LaunchPath is { } launchPath
        ? await LaunchedHost.StartAsync(launchPath, options.Url, options.HostEnvironment)
        : null;
    using var attached = options.ServerProcessId is { } pid ? Process.GetProcessById(pid) : null;
    var server = host?.Process ?? attached;
    var scenarios = options.Scenario == "all" ? Scenarios.Names : [options.Scenario];

    Console.WriteLine(Report.Header(withUsage: server is not null, withGc: options.GcStatsPath is not null));
    foreach (var scenario in scenarios)
    {
        var requests = await Scenarios.BuildAsync(scenario, options.Url);
        Console.WriteLine(Report.Row(await LoadRunner.RunAsync(options, scenario, requests, server)));
    }
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
{
    await Console.Error.WriteLineAsync(ex.Message);
    return 1;
}

return 0;
