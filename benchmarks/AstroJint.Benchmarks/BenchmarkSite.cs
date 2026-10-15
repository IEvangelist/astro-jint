using AstroJint.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AstroJint.Benchmarks;

/// <summary>The sample site (<c>samples/hybrid</c>) hosted in this process, the way an application would host it.</summary>
internal sealed class BenchmarkSite : IAsyncDisposable
{
    private readonly IHost _host;

    private BenchmarkSite(IHost host, AstroApp app)
    {
        _host = host;
        App = app;
    }

    public AstroApp App { get; }

    public IServiceProvider Services => _host.Services;

    /// <summary>The build output of the sample site: <c>ASTRO_JINT_DIST</c>, else <c>samples/hybrid/dist</c> of this repository.</summary>
    public static string FindDistPath()
    {
        if (Environment.GetEnvironmentVariable("ASTRO_JINT_DIST") is { Length: > 0 } configured)
        {
            return Path.GetFullPath(configured);
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AstroJint.slnx")))
            {
                var dist = Path.Combine(directory.FullName, "samples", "hybrid", "dist");
                if (File.Exists(Path.Combine(dist, AstroManifest.FileName)))
                {
                    return dist;
                }

                break;
            }
        }

        throw new DirectoryNotFoundException("The sample site is not built. Run 'pnpm --filter hybrid... build', or set ASTRO_JINT_DIST to a build output.");
    }

    /// <param name="poolSize">Number of engines.</param>
    /// <param name="warmUp">Create the engines before the first request.</param>
    /// <param name="withLocals">Fill <c>Astro.locals</c> like the sample host does.</param>
    /// <param name="staticFileCacheBytes">Memory for prerendered files; <see langword="null"/> keeps the default, 0 serves them from disk.</param>
    /// <param name="configure">Applied last, to change any other option.</param>
    public static async Task<BenchmarkSite> StartAsync(int poolSize = 1, bool warmUp = true, bool withLocals = true, long? staticFileCacheBytes = null, Action<AstroOptions>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            ContentRootPath = Environment.CurrentDirectory,
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services.AddAstro(options =>
        {
            options.DistPath = FindDistPath();
            options.PoolSize = poolSize;
            options.WarmUp = false;
            if (staticFileCacheBytes is { } cacheBytes)
            {
                options.StaticFileCacheBytes = cacheBytes;
            }

            if (withLocals)
            {
                options.LocalsFactory = context => new { traceId = context.TraceIdentifier, user = context.User.Identity?.Name };
            }

            configure?.Invoke(options);
        });

        var host = builder.Build();
        await host.StartAsync().ConfigureAwait(false);

        var app = host.Services.GetRequiredService<AstroApp>();
        if (warmUp)
        {
            await app.WarmUpAsync().ConfigureAwait(false);
        }

        return new BenchmarkSite(host, app);
    }

    /// <summary>A file of the build output that is served with the immutable cache header, e.g. the stylesheet.</summary>
    public string FindAsset(string extension) => FindAsset(App.DistPath, extension);

    public static string FindAsset(string distPath, string extension)
    {
        var manifest = AstroManifest.Load(distPath);
        var folder = Path.Combine(Path.GetFullPath(manifest.Client, distPath), manifest.AssetsDir);
        var file = Directory.EnumerateFiles(folder, "*" + extension).First();
        return "/" + manifest.AssetsDir + "/" + Path.GetFileName(file);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync().ConfigureAwait(false);
        _host.Dispose();
    }
}
