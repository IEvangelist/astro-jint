using System.Diagnostics;

namespace AstroJint.LoadTest;

/// <summary>
/// A host that the load test starts itself (--launch), so that every run measures a fresh process with the settings that it
/// was given, and nothing is left running afterwards.
/// </summary>
internal sealed class LaunchedHost : IDisposable
{
    // The sample host answers this once the application has started.
    private const string ReadyPath = "/healthz";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);

    private LaunchedHost(Process process) => Process = process;

    public Process Process { get; }

    public static async Task<LaunchedHost> StartAsync(string hostPath, Uri url, IEnumerable<KeyValuePair<string, string>> environment)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add(hostPath);
        info.Environment["ASPNETCORE_URLS"] = url.GetLeftPart(UriPartial.Authority);
        foreach (var (name, value) in environment)
        {
            info.Environment[name] = value;
        }

        var host = new LaunchedHost(Process.Start(info) ?? throw new InvalidOperationException($"'dotnet {hostPath}' did not start."));
        try
        {
            // Nobody reads what the host writes, and a full pipe would stall it.
            host.Process.OutputDataReceived += static (_, _) => { };
            host.Process.ErrorDataReceived += static (_, _) => { };
            host.Process.BeginOutputReadLine();
            host.Process.BeginErrorReadLine();

            await host.WaitUntilReadyAsync(url).ConfigureAwait(false);
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
            }

            Process.WaitForExit();
        }
        catch (InvalidOperationException)
        {
            // It has exited on its own in the meantime.
        }
        finally
        {
            Process.Dispose();
        }
    }

    private async Task WaitUntilReadyAsync(Uri url)
    {
        using var client = new HttpClient { BaseAddress = url, Timeout = TimeSpan.FromSeconds(2) };
        var deadline = Stopwatch.GetTimestamp() + (long)(StartTimeout.TotalSeconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < deadline)
        {
            if (Process.HasExited)
            {
                throw new InvalidOperationException($"The host exited with code {Process.ExitCode} before it was ready.");
            }

            try
            {
                using var response = await client.GetAsync(ReadyPath).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // It does not listen yet.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"The host did not answer {ReadyPath} within {StartTimeout.TotalSeconds:F0} seconds.");
    }
}
