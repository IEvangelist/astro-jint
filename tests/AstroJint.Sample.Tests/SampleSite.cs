using System.Collections.Concurrent;
using System.Net;
using AstroJint.Sample;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(SampleSite))]

namespace AstroJint.Sample;

/// <summary>
/// Runs the sample host in memory, once for the whole test run, with the site that <c>pnpm build</c> produced. Hosts that
/// differ in their configuration are started on demand and shared as well.
/// </summary>
public sealed class SampleSite : IAsyncDisposable
{
    private static readonly string Dist = FindDistPath();

    private readonly WebApplicationFactory<Program> _default;
    private readonly ConcurrentDictionary<string, WebApplicationFactory<Program>> _variants = new(StringComparer.Ordinal);

    public SampleSite() => _default = Create(new Dictionary<string, string?>());

    /// <summary>The output of <c>astro build</c> that the hosts serve.</summary>
    public string DistPath => Dist;

    /// <summary>Talks to the default host. Redirects are not followed, so that tests see them.</summary>
    /// <param name="cookies">Keeps the cookies of the responses and sends them back, as a browser does.</param>
    /// <param name="https">
    /// Uses <c>https://localhost</c>. Astro marks the cookie of a session as secure in a production build, and a cookie
    /// container sends a secure cookie over HTTPS only.
    /// </param>
    public HttpClient CreateClient(bool cookies = false, bool https = false) => Wrap(_default, cookies, https);

    /// <summary>Talks to a host with additional configuration, for example <c>Astro:Env:API_TOKEN</c>.</summary>
    public HttpClient CreateClient(IReadOnlyDictionary<string, string?> settings, bool cookies = false, bool https = false)
    {
        var key = string.Join(';', settings.OrderBy(setting => setting.Key, StringComparer.Ordinal).Select(setting => $"{setting.Key}={setting.Value}"));
        return Wrap(_variants.GetOrAdd(key, _ => Create(settings)), cookies, https);
    }

    public async ValueTask DisposeAsync()
    {
        await _default.DisposeAsync();
        foreach (var factory in _variants.Values)
        {
            await factory.DisposeAsync();
        }
    }

    private static HttpClient Wrap(WebApplicationFactory<Program> factory, bool cookies, bool https) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = cookies,
            BaseAddress = new Uri(https ? "https://localhost" : "http://localhost"),
        });

    private static WebApplicationFactory<Program> Create(IReadOnlyDictionary<string, string?> settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Astro:DistPath", Dist);
            builder.UseSetting("Astro:PoolSize", "2");
            foreach (var (name, value) in settings)
            {
                builder.UseSetting(name, value);
            }

            // The in-memory server has no connection. Kestrel would report the address of the peer, which is the proxy
            // in front of the host in production and the test itself here.
            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, LoopbackPeerFilter>());
        });

    private static string FindDistPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AstroJint.slnx")))
            {
                var dist = Path.Combine(directory.FullName, "samples", "hybrid", "dist");
                return File.Exists(Path.Combine(dist, "astro-jint.json"))
                    ? dist
                    : throw new InvalidOperationException($"The sample site is not built: '{dist}' has no astro-jint.json. Run 'pnpm install' and 'pnpm build' at the root of the repository.");
            }
        }

        throw new InvalidOperationException("The root of the repository (the folder with AstroJint.slnx) is not above the test binaries.");
    }

    private sealed class LoopbackPeerFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
