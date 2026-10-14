using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace AstroJint.Conformance;

/// <summary>
/// Builds the Astro projects under <c>tests/conformance/fixtures</c> with astro-adapter-jint, and rebuilds one when its
/// sources, or the adapter, changed since its last build. Only <c>pnpm install</c> has to be run beforehand.
/// </summary>
internal static class FixtureBuilder
{
    private const string ManifestFile = "astro-jint.json";

    private static readonly string[] SkippedFolders = ["node_modules", "dist", ".astro"];
    private static readonly SemaphoreSlim BuildSlots = new(Math.Max(2, Environment.ProcessorCount / 4));
    private static readonly ConcurrentDictionary<string, Lazy<Task<string>>> Builds = new(StringComparer.Ordinal);
    private static readonly Lazy<Task> Adapter = new(BuildAdapterAsync);

    /// <returns>The <c>dist</c> folder that holds the build of the fixture.</returns>
    public static Task<string> EnsureBuiltAsync(string fixture) =>
        Builds.GetOrAdd(fixture, name => new Lazy<Task<string>>(() => BuildAsync(name))).Value;

    private static async Task<string> BuildAsync(string fixture)
    {
        var root = Path.Combine(Repository.FixturesRoot, fixture);
        if (!File.Exists(Path.Combine(root, "package.json")))
        {
            throw new DirectoryNotFoundException($"There is no conformance fixture '{fixture}' in '{Repository.FixturesRoot}'.");
        }

        await Adapter.Value;

        var dist = Path.Combine(root, "dist");
        var manifest = Path.Combine(dist, ManifestFile);
        var sources = Newest(root);
        var adapter = Newest(Path.Combine(Repository.AdapterRoot, "dist"));
        if (File.Exists(manifest) && File.GetLastWriteTimeUtc(manifest) > (sources > adapter ? sources : adapter))
        {
            return dist;
        }

        var astro = Path.Combine(root, "node_modules", "astro", "bin", "astro.mjs");
        if (!File.Exists(astro))
        {
            throw new InvalidOperationException($"The dependencies of the fixture '{fixture}' are not installed. Run 'pnpm install' in '{Repository.Root}'.");
        }

        await BuildSlots.WaitAsync();
        try
        {
            var (exitCode, output) = await RunAsync("node", [astro, "build"], root);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"'astro build' failed for the fixture '{fixture}' with exit code {exitCode}:{Environment.NewLine}{output}");
            }
        }
        finally
        {
            BuildSlots.Release();
        }

        if (!File.Exists(manifest))
        {
            throw new InvalidOperationException($"'astro build' of the fixture '{fixture}' did not write '{manifest}'.");
        }

        return dist;
    }

    private static async Task BuildAdapterAsync()
    {
        var entry = Path.Combine(Repository.AdapterRoot, "dist", "server.js");
        if (File.Exists(entry) && File.GetLastWriteTimeUtc(entry) > Newest(Path.Combine(Repository.AdapterRoot, "src")))
        {
            return;
        }

        var (exitCode, output) = await RunAsync(OperatingSystem.IsWindows() ? "pnpm.cmd" : "pnpm", ["--filter", "astro-adapter-jint", "build"], Repository.Root);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Building astro-adapter-jint failed with exit code {exitCode}:{Environment.NewLine}{output}");
        }
    }

    private static DateTime Newest(string folder)
    {
        var newest = DateTime.MinValue;
        if (!Directory.Exists(folder))
        {
            return newest;
        }

        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.TryPop(out var current))
        {
            foreach (var file in Directory.EnumerateFiles(current))
            {
                var modified = File.GetLastWriteTimeUtc(file);
                if (modified > newest)
                {
                    newest = modified;
                }
            }

            foreach (var child in Directory.EnumerateDirectories(current))
            {
                if (Array.IndexOf(SkippedFolders, Path.GetFileName(child)) < 0)
                {
                    pending.Push(child);
                }
            }
        }

        return newest;
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string fileName, string[] arguments, string workingDirectory)
    {
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["ASTRO_TELEMETRY_DISABLED"] = "1";
        start.Environment["CI"] = "1";

        var output = new StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        return (process.ExitCode, output.ToString());

        void Append(string? line)
        {
            if (line is not null)
            {
                lock (output)
                {
                    output.AppendLine(line);
                }
            }
        }
    }
}
